using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using NeverfadePos.Api.Data;
using NeverfadePos.Api.DTOs.Auth;
using NeverfadePos.Api.DTOs.Tenant;
using Xunit;

namespace NeverfadePos.Api.Tests;

public sealed class TenantContextApiTests
{
    private const string TenantKey =
        "tenant-context-test-key-123456789012345678901234";
    private const string PlatformKey =
        "platform-context-test-key-1234567890123456789012";
    private const string TenantIssuer =
        "NeverfadePos.TenantContext.Test";
    private const string TenantAudience =
        "NeverfadePos.TenantContext.Client";
    private const string PlatformIssuer =
        "NeverfadePos.Platform.TenantContext.Test";
    private const string PlatformAudience =
        "NeverfadePos.Platform.TenantContext.Client";
    private const string TestConnectionString =
        "Host=localhost;Database=test;Username=test;Password=test";

    [Theory]
    [InlineData("owner", "owner123", "owner")]
    [InlineData("admin", "admin123", "admin")]
    [InlineData("kasir", "kasir123", "kasir")]
    public async Task TenantContext_ReturnsServerResolvedBusinessModeForAuthenticatedRole(
        string username,
        string password,
        string expectedRole)
    {
        await using var factory = new TenantContextFactory();
        using var client = factory.CreateClient();

        var login = await client.PostAsJsonAsync(
            "/api/auth/login",
            new { username, password });
        Assert.Equal(HttpStatusCode.OK, login.StatusCode);

        var loginBody = await login.Content
            .ReadFromJsonAsync<LoginResponseDto>();
        Assert.NotNull(loginBody);
        Assert.False(string.IsNullOrWhiteSpace(loginBody.Token));

        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", loginBody.Token);

        var response = await client.GetAsync("/api/tenant/context");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var context = await response.Content
            .ReadFromJsonAsync<TenantContextDto>();
        Assert.NotNull(context);
        Assert.NotEqual(Guid.Empty, context.TenantId);
        Assert.Equal("WARUNG LUMPIA BEEF", context.NamaToko);
        Assert.Equal("general_retail", context.BusinessType);
        Assert.Equal(expectedRole, context.Role);
        Assert.Equal(
            new[]
            {
                "core_pos",
                "inventory",
                "customers",
                "reports",
                "attendance",
                "finance_withdrawal"
            },
            context.Capabilities);
        Assert.DoesNotContain("table_orders", context.Capabilities);
        Assert.DoesNotContain("work_orders", context.Capabilities);
        Assert.DoesNotContain("appointments", context.Capabilities);
    }

    [Fact]
    public async Task TenantContext_RejectsAnonymousRequest()
    {
        await using var factory = new TenantContextFactory();
        using var client = factory.CreateClient();

        var response = await client.GetAsync("/api/tenant/context");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    private sealed class TenantContextFactory : WebApplicationFactory<Program>
    {
        private readonly string _databaseName =
            $"tenant-context-{Guid.NewGuid():N}";

        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseEnvironment("Development");
            builder.UseSetting(
                "ConnectionStrings:DefaultConnection",
                TestConnectionString);
            builder.UseSetting("Jwt:Key", TenantKey);
            builder.UseSetting("Jwt:Issuer", TenantIssuer);
            builder.UseSetting("Jwt:Audience", TenantAudience);
            builder.UseSetting("PlatformJwt:Key", PlatformKey);
            builder.UseSetting("PlatformJwt:Issuer", PlatformIssuer);
            builder.UseSetting("PlatformJwt:Audience", PlatformAudience);
            builder.UseSetting("Payments:Mode", "Disabled");
            builder.UseSetting("PlatformBootstrap:Enabled", "false");

            builder.ConfigureAppConfiguration((_, config) =>
                config.AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["ConnectionStrings:DefaultConnection"] =
                        TestConnectionString,
                    ["Jwt:Key"] = TenantKey,
                    ["Jwt:Issuer"] = TenantIssuer,
                    ["Jwt:Audience"] = TenantAudience,
                    ["PlatformJwt:Key"] = PlatformKey,
                    ["PlatformJwt:Issuer"] = PlatformIssuer,
                    ["PlatformJwt:Audience"] = PlatformAudience,
                    ["Payments:Mode"] = "Disabled",
                    ["PlatformBootstrap:Enabled"] = "false"
                }));

            builder.ConfigureServices(services =>
            {
                services.RemoveAll<AppDbContext>();
                services.RemoveAll<DbContextOptions<AppDbContext>>();
                services.RemoveAll<
                    IDbContextOptionsConfiguration<AppDbContext>>();
                services.AddDbContext<AppDbContext>(options =>
                    options.UseInMemoryDatabase(_databaseName));
            });
        }
    }
}
