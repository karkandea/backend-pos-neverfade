using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.IdentityModel.Tokens;
using NeverfadePos.Api.Auth;
using NeverfadePos.Api.Data;
using NeverfadePos.Api.DTOs.Auth;
using NeverfadePos.Api.DTOs.PlatformTenant;
using NeverfadePos.Api.Entities;
using Xunit;

namespace NeverfadePos.Api.Tests;

public sealed class PlatformTenantControlPlaneTests
{
    private const string TenantKey =
        "tenant-control-plane-test-key-12345678901234567890";
    private const string PlatformKey =
        "platform-control-plane-test-key-123456789012345678";
    private const string TenantIssuer = "NeverfadePos.ControlPlane.Test";
    private const string TenantAudience = "NeverfadePos.ControlPlane.Client";
    private const string PlatformIssuer = "NeverfadePos.Platform.ControlPlane.Test";
    private const string PlatformAudience = "NeverfadePos.Platform.ControlPlane.Client";

    [Fact]
    public async Task Provisioning_CreatesActiveTenantOwnerSettingsAndAudit()
    {
        await using var factory = new ControlPlaneFactory();
        var (client, actor) = await CreatePlatformClientAsync(factory);

        var response = await client.PostAsJsonAsync(
            "/api/platform/tenants",
            CreateRequest("Kedai Énak", "owner.kedai"));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var created = await response.Content
            .ReadFromJsonAsync<PlatformTenantDto>();
        Assert.NotNull(created);
        Assert.Equal("active", created.Status);
        Assert.Equal("kedai-enak", created.Slug);
        Assert.Equal("general_retail", created.BusinessType);
        Assert.Contains("core_pos", created.Capabilities);
        Assert.Contains("finance_withdrawal", created.Capabilities);
        Assert.DoesNotContain("table_orders", created.Capabilities);
        Assert.Equal("owner", await GetOwnerRoleAsync(factory, created.Id));

        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var settingsScope = scope.ServiceProvider
            .GetRequiredService<ITrustedTenantExecutionScope>();
        using (settingsScope.Begin(created.Id, "TEST_VERIFY_PROVISIONING"))
        {
            var settings = await db.Settings.SingleAsync();
            Assert.Equal("Kedai Énak", settings.NamaToko);
            Assert.False(settings.ShowTax);
            Assert.False(settings.ShowPoint);
            Assert.Equal(0, settings.MinStok);

            var owner = await db.Users.SingleAsync();
            Assert.Equal(created.Id, owner.TenantId);
            Assert.Equal("owner", owner.Role);
            Assert.NotEqual("InitialPassword123!", owner.PasswordHash);
            Assert.True(BCrypt.Net.BCrypt.Verify(
                "InitialPassword123!",
                owner.PasswordHash));
        }

        var audit = await db.PlatformAuditEvents.SingleAsync(
            x => x.TenantId == created.Id);
        Assert.Equal("TENANT_PROVISIONED", audit.EventType);
        Assert.Equal(actor.Id, audit.ActorPlatformUserId);
        Assert.Null(audit.Metadata);
    }

    [Fact]
    public async Task Provisioning_RejectsMissingOrInvalidBusinessType()
    {
        await using var factory = new ControlPlaneFactory();
        var (client, _) = await CreatePlatformClientAsync(factory);

        var missing = await client.PostAsJsonAsync(
            "/api/platform/tenants",
            new
            {
                namaToko = "No Type Shop",
                owner = new
                {
                    nama = "Owner No Type",
                    username = "owner.no.type",
                    password = "InitialPassword123!"
                }
            });
        Assert.Equal(HttpStatusCode.BadRequest, missing.StatusCode);
        Assert.Contains("VALIDATION_ERROR", await missing.Content.ReadAsStringAsync());

        var invalid = await client.PostAsJsonAsync(
            "/api/platform/tenants",
            CreateRequest("Invalid Type Shop", "owner.invalid.type", "hotel"));
        Assert.Equal(HttpStatusCode.BadRequest, invalid.StatusCode);
        Assert.Contains("VALIDATION_ERROR", await invalid.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task BusinessProfileUpdate_ChangesCapabilitiesAndCreatesAudit()
    {
        await using var factory = new ControlPlaneFactory();
        var (client, actor) = await CreatePlatformClientAsync(factory);
        var tenant = await CreateTenantAsync(
            client,
            "Business Mode Shop",
            "owner.business.mode");

        var response = await client.PutAsJsonAsync(
            $"/api/platform/tenants/{tenant.Id}/business-profile",
            new { businessType = "food_beverage" });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var updated = await response.Content.ReadFromJsonAsync<PlatformTenantDto>();
        Assert.NotNull(updated);
        Assert.Equal("food_beverage", updated.BusinessType);
        Assert.Contains("table_orders", updated.Capabilities);
        Assert.Contains("kitchen_queue", updated.Capabilities);
        Assert.DoesNotContain("work_orders", updated.Capabilities);

        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var audit = await db.PlatformAuditEvents.SingleAsync(
            x => x.TenantId == tenant.Id &&
                 x.EventType == "TENANT_BUSINESS_PROFILE_CHANGED");
        Assert.Equal(actor.Id, audit.ActorPlatformUserId);
        Assert.Contains("general_retail", audit.Metadata);
        Assert.Contains("food_beverage", audit.Metadata);
    }

    [Fact]
    public async Task DuplicateUsername_FailsWithoutPartialProvisioning()
    {
        await using var factory = new ControlPlaneFactory();
        var (client, _) = await CreatePlatformClientAsync(factory);

        var first = await client.PostAsJsonAsync(
            "/api/platform/tenants",
            CreateRequest("Tenant First", "shared.owner"));
        Assert.Equal(HttpStatusCode.OK, first.StatusCode);

        var before = await GetCountsAsync(factory);
        var duplicate = await client.PostAsJsonAsync(
            "/api/platform/tenants",
            CreateRequest("Tenant Should Roll Back", "shared.owner"));

        Assert.Equal(HttpStatusCode.Conflict, duplicate.StatusCode);
        Assert.Contains(
            "OWNER_USERNAME_CONFLICT",
            await duplicate.Content.ReadAsStringAsync());
        Assert.Equal(before, await GetCountsAsync(factory));
    }

    [Fact]
    public async Task DuplicateBaseSlug_GetsFrozenGuidSuffix()
    {
        await using var factory = new ControlPlaneFactory();
        var (client, _) = await CreatePlatformClientAsync(factory);

        var first = await CreateTenantAsync(
            client,
            "Same Shop",
            "owner.same.one");
        var second = await CreateTenantAsync(
            client,
            "Same Shop",
            "owner.same.two");

        Assert.Equal("same-shop", first.Slug);
        Assert.StartsWith("same-shop-", second.Slug);
        Assert.EndsWith(second.Id.ToString("N"), second.Slug);
        Assert.NotEqual(first.Slug, second.Slug);
        Assert.True(second.Slug.Length <= 100);
    }

    [Fact]
    public async Task TenantToken_CannotUsePlatformTenantEndpoints()
    {
        await using var factory = new ControlPlaneFactory();
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue(
                "Bearer",
                CreateToken(
                    TenantKey,
                    TenantIssuer,
                    TenantAudience,
                    Guid.NewGuid(),
                    new Claim("scope", "tenant"),
                    new Claim("tenant_id", Guid.NewGuid().ToString()),
                    new Claim(ClaimTypes.Role, "owner")));

        var response = await client.GetAsync("/api/platform/tenants");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task PlatformTenantEndpoints_RequireSuperAdmin()
    {
        await using var factory = new ControlPlaneFactory();
        using var anonymousClient = factory.CreateClient();
        Assert.Equal(
            HttpStatusCode.Unauthorized,
            (await anonymousClient.GetAsync("/api/platform/tenants"))
                .StatusCode);

        using var wrongRoleClient = factory.CreateClient();
        wrongRoleClient.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue(
                "Bearer",
                CreateToken(
                    PlatformKey,
                    PlatformIssuer,
                    PlatformAudience,
                    Guid.NewGuid(),
                    new Claim("scope", "platform"),
                    new Claim(ClaimTypes.Role, "admin")));

        Assert.Equal(
            HttpStatusCode.Forbidden,
            (await wrongRoleClient.GetAsync("/api/platform/tenants"))
                .StatusCode);
    }

    [Fact]
    public async Task LifecycleTransitions_CreateExactlyOneAuditEach()
    {
        await using var factory = new ControlPlaneFactory();
        var (client, _) = await CreatePlatformClientAsync(factory);
        var tenant = await CreateTenantAsync(
            client,
            "Lifecycle Shop",
            "owner.lifecycle");

        var suspend = await client.PostAsJsonAsync(
            $"/api/platform/tenants/{tenant.Id}/suspend",
            new { reason = "  Operational review  " });
        Assert.Equal(HttpStatusCode.OK, suspend.StatusCode);
        Assert.Equal(
            "suspended",
            (await suspend.Content.ReadFromJsonAsync<PlatformTenantDto>())!.Status);

        var repeatedSuspend = await client.PostAsJsonAsync<object?>(
            $"/api/platform/tenants/{tenant.Id}/suspend",
            null);
        Assert.Equal(HttpStatusCode.Conflict, repeatedSuspend.StatusCode);

        var activate = await client.PostAsync(
            $"/api/platform/tenants/{tenant.Id}/activate",
            null);
        Assert.Equal(HttpStatusCode.OK, activate.StatusCode);

        var repeatedActivate = await client.PostAsync(
            $"/api/platform/tenants/{tenant.Id}/activate",
            null);
        Assert.Equal(HttpStatusCode.Conflict, repeatedActivate.StatusCode);

        var unsafeReason = await client.PostAsJsonAsync(
            $"/api/platform/tenants/{tenant.Id}/suspend",
            new { reason = "password=must-not-be-audit-metadata" });
        Assert.Equal(HttpStatusCode.BadRequest, unsafeReason.StatusCode);
        Assert.Contains(
            "VALIDATION_ERROR",
            await unsafeReason.Content.ReadAsStringAsync());

        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var events = await db.PlatformAuditEvents
            .Where(x => x.TenantId == tenant.Id)
            .ToListAsync();

        Assert.Equal(3, events.Count);
        Assert.Single(events, x => x.EventType == "TENANT_PROVISIONED");
        Assert.Single(events, x => x.EventType == "TENANT_SUSPENDED");
        Assert.Single(events, x => x.EventType == "TENANT_ACTIVATED");
        Assert.Contains("Operational review", events
            .Single(x => x.EventType == "TENANT_SUSPENDED").Metadata);
    }

    [Fact]
    public async Task SuspendedTenant_LoginAndExistingJwtAreRejected()
    {
        await using var factory = new ControlPlaneFactory();
        var (platformClient, _) = await CreatePlatformClientAsync(factory);
        using var tenantClient = factory.CreateClient();

        var login = await tenantClient.PostAsJsonAsync(
            "/api/auth/login",
            new { username = "owner", password = "owner123" });
        Assert.Equal(HttpStatusCode.OK, login.StatusCode);
        var tenantLogin = await login.Content
            .ReadFromJsonAsync<LoginResponseDto>();
        Assert.NotNull(tenantLogin);

        var demoTenantId = await GetDemoTenantIdAsync(factory);
        var suspend = await platformClient.PostAsJsonAsync(
            $"/api/platform/tenants/{demoTenantId}/suspend",
            new { reason = "QA suspension" });
        Assert.Equal(HttpStatusCode.OK, suspend.StatusCode);

        var rejectedLogin = await tenantClient.PostAsJsonAsync(
            "/api/auth/login",
            new { username = "owner", password = "owner123" });
        Assert.Equal(HttpStatusCode.Forbidden, rejectedLogin.StatusCode);
        Assert.Contains(
            "TENANT_SUSPENDED",
            await rejectedLogin.Content.ReadAsStringAsync());

        tenantClient.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", tenantLogin.Token);
        var rejectedExistingSession =
            await tenantClient.GetAsync("/api/products");
        Assert.Equal(
            HttpStatusCode.Forbidden,
            rejectedExistingSession.StatusCode);
        Assert.Contains(
            "TENANT_SUSPENDED",
            await rejectedExistingSession.Content.ReadAsStringAsync());
    }

    private static object CreateRequest(
        string shop,
        string username,
        string businessType = "general_retail") =>
        new
        {
            namaToko = shop,
            businessType,
            owner = new
            {
                nama = $"Owner {shop}",
                username,
                password = "InitialPassword123!"
            }
        };

    private static async Task<PlatformTenantDto> CreateTenantAsync(
        HttpClient client,
        string shop,
        string username)
    {
        var response = await client.PostAsJsonAsync(
            "/api/platform/tenants",
            CreateRequest(shop, username));
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return (await response.Content
            .ReadFromJsonAsync<PlatformTenantDto>())!;
    }

    private static async Task<(HttpClient Client, PlatformUser Actor)>
        CreatePlatformClientAsync(ControlPlaneFactory factory)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var actor = new PlatformUser
        {
            Nama = "Platform Admin",
            Username = $"platform.{Guid.NewGuid():N}",
            PasswordHash = BCrypt.Net.BCrypt.HashPassword("unused-password"),
            Role = "superadmin",
            Active = true
        };
        db.PlatformUsers.Add(actor);
        await db.SaveChangesAsync();

        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue(
                "Bearer",
                CreateToken(
                    PlatformKey,
                    PlatformIssuer,
                    PlatformAudience,
                    actor.Id,
                    new Claim("scope", "platform"),
                    new Claim(ClaimTypes.Role, "superadmin")));
        return (client, actor);
    }

    private static async Task<string> GetOwnerRoleAsync(
        ControlPlaneFactory factory,
        Guid tenantId)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var trusted = scope.ServiceProvider
            .GetRequiredService<ITrustedTenantExecutionScope>();
        using var tenantScope = trusted.Begin(tenantId, "TEST_OWNER_ROLE");
        return (await db.Users.SingleAsync()).Role;
    }

    private static async Task<(int Tenants, int Users, int Settings, int Audits)>
        GetCountsAsync(ControlPlaneFactory factory)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var tenantCount = await db.Tenants.CountAsync();
        var auditCount = await db.PlatformAuditEvents.CountAsync();
        var userCount = 0;
        var settingsCount = 0;
        var trusted = scope.ServiceProvider
            .GetRequiredService<ITrustedTenantExecutionScope>();
        foreach (var tenantId in await db.Tenants.Select(x => x.Id).ToListAsync())
        {
            using var tenantScope = trusted.Begin(tenantId, "TEST_COUNTS");
            userCount += await db.Users.CountAsync();
            settingsCount += await db.Settings.CountAsync();
        }

        return (tenantCount, userCount, settingsCount, auditCount);
    }

    private static async Task<Guid> GetDemoTenantIdAsync(
        ControlPlaneFactory factory)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        return await scope.ServiceProvider
            .GetRequiredService<AppDbContext>()
            .Tenants
            .Where(x => x.Slug == "warung-lumpia-beef")
            .Select(x => x.Id)
            .SingleAsync();
    }

    private static string CreateToken(
        string key,
        string issuer,
        string audience,
        Guid subject,
        params Claim[] claims)
    {
        var allClaims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, subject.ToString())
        };
        allClaims.AddRange(claims);
        return new JwtSecurityTokenHandler().WriteToken(
            new JwtSecurityToken(
                issuer,
                audience,
                allClaims,
                expires: DateTime.UtcNow.AddMinutes(10),
                signingCredentials: new SigningCredentials(
                    new SymmetricSecurityKey(Encoding.UTF8.GetBytes(key)),
                    SecurityAlgorithms.HmacSha256)));
    }

    private sealed class ControlPlaneFactory : WebApplicationFactory<Program>
    {
        private readonly string _databaseName =
            $"control-plane-{Guid.NewGuid():N}";

        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseEnvironment("Development");
            builder.UseSetting(
                "ConnectionStrings:DefaultConnection",
                "Host=localhost;Database=test;Username=test;Password=test");
            builder.UseSetting("Jwt:Key", TenantKey);
            builder.UseSetting("Jwt:Issuer", TenantIssuer);
            builder.UseSetting("Jwt:Audience", TenantAudience);
            builder.UseSetting("PlatformJwt:Key", PlatformKey);
            builder.UseSetting("PlatformJwt:Issuer", PlatformIssuer);
            builder.UseSetting("PlatformJwt:Audience", PlatformAudience);
            builder.UseSetting("PlatformBootstrap:Enabled", "false");

            builder.ConfigureAppConfiguration((_, config) =>
                config.AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["ConnectionStrings:DefaultConnection"] =
                        "Host=localhost;Database=test;Username=test;Password=test",
                    ["Jwt:Key"] = TenantKey,
                    ["Jwt:Issuer"] = TenantIssuer,
                    ["Jwt:Audience"] = TenantAudience,
                    ["PlatformJwt:Key"] = PlatformKey,
                    ["PlatformJwt:Issuer"] = PlatformIssuer,
                    ["PlatformJwt:Audience"] = PlatformAudience,
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
