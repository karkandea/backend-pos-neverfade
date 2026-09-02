using System.IdentityModel.Tokens.Jwt;
using Microsoft.Extensions.Configuration;
using NeverfadePos.Api.Entities;
using NeverfadePos.Api.Services.SharedPos;
using Xunit;

namespace NeverfadePos.Api.Tests;

public sealed class SharedPosSecurityTests
{
    private const string JwtKey =
        "shared-pos-security-test-key-123456789012345678901234567";

    [Fact]
    public void PinFingerprint_IsStableWithinTenantAndDifferentAcrossTenants()
    {
        var security = new SharedPosSecurity(Configuration());
        var tenantA = Guid.NewGuid();
        var tenantB = Guid.NewGuid();

        var first = security.FingerprintPin(tenantA, "4321");
        var second = security.FingerprintPin(tenantA, "4321");
        var otherTenant = security.FingerprintPin(tenantB, "4321");

        Assert.Equal(first, second);
        Assert.NotEqual(first, otherTenant);
        Assert.Equal(64, first.Length);
    }

    [Fact]
    public void PinHash_VerifiesCorrectPinAndRejectsWrongPin()
    {
        var hash = SharedPosSecurity.HashPin("4321");

        Assert.True(SharedPosSecurity.VerifyPin("4321", hash));
        Assert.False(SharedPosSecurity.VerifyPin("9999", hash));
        Assert.DoesNotContain("4321", hash, StringComparison.Ordinal);
    }

    [Fact]
    public void OwnerSharedJwt_ContainsServerSessionAndRecentReauthClaims()
    {
        var service = new SharedPosJwtService(Configuration());
        var tenantId = Guid.NewGuid();
        var employeeId = Guid.NewGuid();
        var sessionId = Guid.NewGuid();
        var user = User(tenantId, "owner");

        var generated = service.Generate(user, employeeId, sessionId);
        var token = new JwtSecurityTokenHandler().ReadJwtToken(generated.Token);

        Assert.Equal("tenant", token.Claims.Single(x => x.Type == "scope").Value);
        Assert.Equal("shared_pos", token.Claims.Single(x => x.Type == "session_kind").Value);
        Assert.Equal(employeeId.ToString(), token.Claims.Single(x => x.Type == "employee_id").Value);
        Assert.Equal(sessionId.ToString(), token.Claims.Single(x => x.Type == "shared_session_id").Value);
        Assert.Equal(tenantId.ToString(), token.Claims.Single(x => x.Type == "tenant_id").Value);
        Assert.Contains(token.Claims, x => x.Type == "shared_reauth_until");
        Assert.True(generated.ExpiresAtUtc <= DateTime.UtcNow.AddMinutes(31));
        Assert.NotNull(generated.ReauthUntilUtc);
    }

    [Fact]
    public void CashierSharedJwt_DoesNotGetPrivilegedReauthWindow()
    {
        var service = new SharedPosJwtService(Configuration());
        var user = User(Guid.NewGuid(), "kasir");

        var generated = service.Generate(user, Guid.NewGuid(), Guid.NewGuid());
        var token = new JwtSecurityTokenHandler().ReadJwtToken(generated.Token);

        Assert.DoesNotContain(token.Claims, x => x.Type == "shared_reauth_until");
        Assert.Null(generated.ReauthUntilUtc);
    }

    private static IConfiguration Configuration() =>
        new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Jwt:Key"] = JwtKey,
                ["Jwt:Issuer"] = "NeverfadePos.Shared.Security.Test",
                ["Jwt:Audience"] = "NeverfadePos.Shared.Security.Client"
            })
            .Build();

    private static User User(Guid tenantId, string role) => new()
    {
        Id = Guid.NewGuid(),
        TenantId = tenantId,
        Nama = "Shared User",
        Username = $"shared-{role}",
        PasswordHash = "not-used",
        Role = role,
        Active = true
    };
}
