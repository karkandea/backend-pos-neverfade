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
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.IdentityModel.Tokens;
using NeverfadePos.Api.Auth;
using NeverfadePos.Api.Data;
using NeverfadePos.Api.DTOs.PlatformAuth;
using NeverfadePos.Api.Entities;
using NeverfadePos.Api.Services.PlatformBootstrap;
using Xunit;

namespace NeverfadePos.Api.Tests;

public sealed class PlatformAuthenticationTests
{
    private const string TenantKey =
        "tenant-test-signing-key-that-is-at-least-32-characters";

    private const string PlatformKey =
        "platform-test-signing-key-that-is-at-least-32-characters";

    private const string TenantIssuer = "NeverfadePos.Test";
    private const string TenantAudience = "NeverfadePos.Test.Client";
    private const string PlatformIssuer = "NeverfadePos.Platform.Test";
    private const string PlatformAudience = "NeverfadePos.Platform.Test.Client";

    [Fact]
    public async Task PlatformLoginAndMe_SucceedWithIsolatedClaims()
    {
        await using var factory = new PlatformApiFactory();

        var user = await AddPlatformUserAsync(
            factory,
            active: true);

        using var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync(
            "/api/platform/auth/login",
            new
            {
                username = user.Username,
                password = "PlatformPassword123!"
            });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var login = await response.Content
            .ReadFromJsonAsync<PlatformLoginResponseDto>();

        Assert.NotNull(login);
        Assert.Equal(user.Id, login.User.Id);
        Assert.Equal("superadmin", login.User.Role);

        var jwt = new JwtSecurityTokenHandler()
            .ReadJwtToken(login.Token);

        Assert.Equal(PlatformIssuer, jwt.Issuer);
        Assert.Contains(PlatformAudience, jwt.Audiences);
        Assert.Equal(
            "platform",
            jwt.Claims.Single(x => x.Type == "scope").Value);
        Assert.Equal(
            "superadmin",
            jwt.Claims.Single(
                x => x.Type == ClaimTypes.Role).Value);
        Assert.DoesNotContain(
            jwt.Claims,
            x => x.Type == "tenant_id");

        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue(
                "Bearer",
                login.Token);

        var meResponse = await client.GetAsync(
            "/api/platform/auth/me");

        Assert.Equal(HttpStatusCode.OK, meResponse.StatusCode);

        var me = await meResponse.Content
            .ReadFromJsonAsync<PlatformUserDto>();

        Assert.NotNull(me);
        Assert.Equal(user.Id, me.Id);
        Assert.Equal(user.Username, me.Username);
    }

    [Theory]
    [InlineData("missing-user", "PlatformPassword123!")]
    [InlineData("platform.admin", "wrong-password")]
    public async Task PlatformLogin_InvalidCredentials_AreRejected(
        string username,
        string password)
    {
        await using var factory = new PlatformApiFactory();
        await AddPlatformUserAsync(factory, active: true);

        using var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync(
            "/api/platform/auth/login",
            new { username, password });

        Assert.Equal(
            HttpStatusCode.Unauthorized,
            response.StatusCode);
        Assert.Contains(
            "PLATFORM_INVALID_CREDENTIALS",
            await response.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task InactivePlatformUser_IsRejected()
    {
        await using var factory = new PlatformApiFactory();
        var user = await AddPlatformUserAsync(
            factory,
            active: false);

        using var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync(
            "/api/platform/auth/login",
            new
            {
                username = user.Username,
                password = "PlatformPassword123!"
            });

        Assert.Equal(
            HttpStatusCode.Forbidden,
            response.StatusCode);
        Assert.Contains(
            "PLATFORM_USER_INACTIVE",
            await response.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task PlatformToken_IsRejectedByTenantEndpoint()
    {
        await using var factory = new PlatformApiFactory();
        var user = await AddPlatformUserAsync(factory, active: true);

        using var client = factory.CreateClient();
        var token = CreateToken(
            PlatformKey,
            PlatformIssuer,
            PlatformAudience,
            user.Id,
            new Claim("scope", "platform"),
            new Claim(ClaimTypes.Role, "superadmin"));

        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", token);

        var response = await client.GetAsync("/api/products");

        Assert.Equal(
            HttpStatusCode.Unauthorized,
            response.StatusCode);
    }

    [Fact]
    public async Task TenantToken_IsRejectedByPlatformEndpoint()
    {
        await using var factory = new PlatformApiFactory();
        using var client = factory.CreateClient();

        var token = CreateToken(
            TenantKey,
            TenantIssuer,
            TenantAudience,
            Guid.NewGuid(),
            new Claim("scope", "tenant"),
            new Claim("tenant_id", Guid.NewGuid().ToString()),
            new Claim(ClaimTypes.Role, "owner"));

        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", token);

        var response = await client.GetAsync(
            "/api/platform/auth/me");

        Assert.Equal(
            HttpStatusCode.Unauthorized,
            response.StatusCode);
    }

    [Fact]
    public async Task PlatformTokenContainingTenantId_IsRejected()
    {
        await using var factory = new PlatformApiFactory();
        using var client = factory.CreateClient();

        var token = CreateToken(
            PlatformKey,
            PlatformIssuer,
            PlatformAudience,
            Guid.NewGuid(),
            new Claim("scope", "platform"),
            new Claim(ClaimTypes.Role, "superadmin"),
            new Claim("tenant_id", Guid.NewGuid().ToString()));

        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", token);

        var response = await client.GetAsync(
            "/api/platform/auth/me");

        Assert.Equal(
            HttpStatusCode.Unauthorized,
            response.StatusCode);
    }

    [Fact]
    public async Task Bootstrap_DoesNotCreateDuplicatePlatformUser()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(
                $"platform-bootstrap-{Guid.NewGuid():N}")
            .Options;

        await using var db = new AppDbContext(options);

        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["PlatformBootstrap:Enabled"] = "true",
                ["PlatformBootstrap:Nama"] = "Platform Admin",
                ["PlatformBootstrap:Username"] = "platform.admin",
                ["PlatformBootstrap:Password"] =
                    "PlatformPassword123!"
            })
            .Build();

        var service = new PlatformUserBootstrapService(
            db,
            configuration,
            NullLogger<PlatformUserBootstrapService>.Instance);

        await service.InitializeAsync();
        await service.InitializeAsync();

        var users = await db.PlatformUsers.ToListAsync();

        Assert.Single(users);
        Assert.Equal("superadmin", users[0].Role);
        Assert.NotEqual(
            "PlatformPassword123!",
            users[0].PasswordHash);
        Assert.True(BCrypt.Net.BCrypt.Verify(
            "PlatformPassword123!",
            users[0].PasswordHash));
    }

    private static async Task<PlatformUser>
        AddPlatformUserAsync(
            PlatformApiFactory factory,
            bool active)
    {
        await using var scope =
            factory.Services.CreateAsyncScope();

        var db = scope.ServiceProvider
            .GetRequiredService<AppDbContext>();

        var user = new PlatformUser
        {
            Nama = "Platform Admin",
            Username = "platform.admin",
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(
                "PlatformPassword123!"),
            Role = "superadmin",
            Active = active
        };

        db.PlatformUsers.Add(user);
        await db.SaveChangesAsync();

        return user;
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

        var token = new JwtSecurityToken(
            issuer,
            audience,
            allClaims,
            expires: DateTime.UtcNow.AddMinutes(10),
            signingCredentials: new SigningCredentials(
                new SymmetricSecurityKey(
                    Encoding.UTF8.GetBytes(key)),
                SecurityAlgorithms.HmacSha256));

        return new JwtSecurityTokenHandler()
            .WriteToken(token);
    }

    private sealed class PlatformApiFactory
        : WebApplicationFactory<Program>
    {
        private readonly string _databaseName =
            $"platform-api-{Guid.NewGuid():N}";

        protected override void ConfigureWebHost(
            IWebHostBuilder builder)
        {
            builder.UseEnvironment("Development");
            builder.UseSetting(
                "ConnectionStrings:DefaultConnection",
                "Host=localhost;Database=test;Username=test;Password=test");
            builder.UseSetting("Jwt:Key", TenantKey);
            builder.UseSetting("Jwt:Issuer", TenantIssuer);
            builder.UseSetting("Jwt:Audience", TenantAudience);
            builder.UseSetting("PlatformJwt:Key", PlatformKey);
            builder.UseSetting(
                "PlatformJwt:Issuer",
                PlatformIssuer);
            builder.UseSetting(
                "PlatformJwt:Audience",
                PlatformAudience);
            builder.UseSetting(
                "PlatformBootstrap:Enabled",
                "false");

            builder.ConfigureAppConfiguration((_, config) =>
            {
                config.AddInMemoryCollection(
                    new Dictionary<string, string?>
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
                    });
            });

            builder.ConfigureServices(services =>
            {
                services.RemoveAll<AppDbContext>();
                services.RemoveAll<
                    DbContextOptions<AppDbContext>>();
                services.RemoveAll<
                    IDbContextOptionsConfiguration<
                        AppDbContext>>();

                services.AddDbContext<AppDbContext>(options =>
                    options.UseInMemoryDatabase(
                        _databaseName));
            });
        }
    }
}
