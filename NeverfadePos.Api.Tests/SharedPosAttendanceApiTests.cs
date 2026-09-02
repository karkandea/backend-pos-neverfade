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
using NeverfadePos.Api.Auth;
using NeverfadePos.Api.Data;
using NeverfadePos.Api.DTOs.Auth;
using NeverfadePos.Api.DTOs.Karyawan;
using NeverfadePos.Api.DTOs.SharedPos;
using NeverfadePos.Api.Entities;
using NeverfadePos.Api.Services.SharedPos;
using Xunit;

namespace NeverfadePos.Api.Tests;

public sealed class SharedPosAttendanceApiTests
{
    private const string TenantKey =
        "shared-pos-test-key-123456789012345678901234567";
    private const string PlatformKey =
        "shared-pos-platform-test-key-12345678901234567890";
    private const string TenantIssuer = "NeverfadePos.SharedPos.Test";
    private const string TenantAudience = "NeverfadePos.SharedPos.Client";
    private const string PlatformIssuer = "NeverfadePos.Platform.SharedPos.Test";
    private const string PlatformAudience = "NeverfadePos.Platform.SharedPos.Client";
    private const string TestConnectionString =
        "Host=localhost;Database=test;Username=test;Password=test";

    [Fact]
    public async Task SharedAttendance_FullPunchFlowAutoLocksAfterEachPunch()
    {
        await using var factory = new SharedPosFactory();
        using var ownerClient = factory.CreateClient();
        await LoginOwnerAsync(ownerClient);

        var employees = await ownerClient.GetFromJsonAsync<List<KaryawanDto>>("/api/karyawan");
        var employee = Assert.Single(employees!.Where(x => x.Nama == "Dewi Safitri"));

        var accessResponse = await ownerClient.PutAsJsonAsync(
            $"/api/karyawan/{employee.Id}/shared-access",
            new { pin = "4321", clearPin = false, clearUserLink = false });
        Assert.Equal(HttpStatusCode.OK, accessResponse.StatusCode);

        var deviceResponse = await ownerClient.PostAsJsonAsync(
            "/api/shared-pos/devices",
            new { name = "Front Counter" });
        Assert.Equal(HttpStatusCode.OK, deviceResponse.StatusCode);
        var registered = await deviceResponse.Content
            .ReadFromJsonAsync<RegisteredSharedPosDeviceDto>();
        Assert.NotNull(registered);
        Assert.False(string.IsNullOrWhiteSpace(registered.DeviceToken));

        using var sharedClient = factory.CreateClient();
        var firstUnlock = await UnlockAsync(sharedClient, registered.DeviceToken, "4321");
        Assert.NotNull(firstUnlock);
        Assert.Equal(employee.Id, firstUnlock.Employee.Id);
        Assert.Null(firstUnlock.PosToken);

        sharedClient.DefaultRequestHeaders.Remove("X-NF-Session-Token");
        sharedClient.DefaultRequestHeaders.Add("X-NF-Session-Token", firstUnlock.SessionToken);
        var checkIn = await sharedClient.PostAsync("/api/shared-pos/attendance/checkin", null);
        Assert.Equal(HttpStatusCode.OK, checkIn.StatusCode);
        var checkInBody = await checkIn.Content.ReadFromJsonAsync<SharedAttendanceResultDto>();
        Assert.NotNull(checkInBody);
        Assert.True(checkInBody.Ok);
        Assert.NotNull(checkInBody.Attendance.CheckIn);

        var staleSession = await sharedClient.GetAsync("/api/shared-pos/session");
        Assert.Equal(HttpStatusCode.Unauthorized, staleSession.StatusCode);

        var secondUnlock = await UnlockAsync(sharedClient, registered.DeviceToken, "4321");
        Assert.Equal("checkout", secondUnlock!.Attendance.NextAction);

        sharedClient.DefaultRequestHeaders.Remove("X-NF-Session-Token");
        sharedClient.DefaultRequestHeaders.Add("X-NF-Session-Token", secondUnlock.SessionToken);
        var checkOut = await sharedClient.PostAsync("/api/shared-pos/attendance/checkout", null);
        Assert.Equal(HttpStatusCode.OK, checkOut.StatusCode);
        var checkOutBody = await checkOut.Content.ReadFromJsonAsync<SharedAttendanceResultDto>();
        Assert.NotNull(checkOutBody);
        Assert.True(checkOutBody.Ok);
        Assert.NotNull(checkOutBody.Attendance.CheckOut);
        Assert.Null(checkOutBody.Attendance.NextAction);

        var staleAfterCheckout = await sharedClient.GetAsync("/api/shared-pos/session");
        Assert.Equal(HttpStatusCode.Unauthorized, staleAfterCheckout.StatusCode);
    }

    [Fact]
    public async Task SharedUnlock_LocksDeviceAfterFiveInvalidPins()
    {
        await using var factory = new SharedPosFactory();
        using var ownerClient = factory.CreateClient();
        await LoginOwnerAsync(ownerClient);

        var employees = await ownerClient.GetFromJsonAsync<List<KaryawanDto>>("/api/karyawan");
        var employee = Assert.Single(employees!.Where(x => x.Nama == "Dewi Safitri"));
        var accessResponse = await ownerClient.PutAsJsonAsync(
            $"/api/karyawan/{employee.Id}/shared-access",
            new { pin = "4321", clearPin = false, clearUserLink = false });
        Assert.Equal(HttpStatusCode.OK, accessResponse.StatusCode);

        var deviceResponse = await ownerClient.PostAsJsonAsync(
            "/api/shared-pos/devices",
            new { name = "Lockout Counter" });
        var registered = await deviceResponse.Content
            .ReadFromJsonAsync<RegisteredSharedPosDeviceDto>();
        Assert.NotNull(registered);

        using var sharedClient = factory.CreateClient();
        sharedClient.DefaultRequestHeaders.Add("X-NF-Device-Token", registered.DeviceToken);

        for (var attempt = 0; attempt < 5; attempt++)
        {
            var invalid = await sharedClient.PostAsJsonAsync(
                "/api/shared-pos/unlock",
                new { pin = "9999" });
            Assert.Equal(HttpStatusCode.Unauthorized, invalid.StatusCode);
            var body = await invalid.Content.ReadFromJsonAsync<ApiError>();
            Assert.Equal("SHARED_POS_AUTH_FAILED", body?.Code);
        }

        var locked = await sharedClient.PostAsJsonAsync(
            "/api/shared-pos/unlock",
            new { pin = "4321" });
        Assert.Equal(HttpStatusCode.TooManyRequests, locked.StatusCode);
        var lockedBody = await locked.Content.ReadFromJsonAsync<ApiError>();
        Assert.Equal("SHARED_DEVICE_TEMPORARILY_LOCKED", lockedBody?.Code);
    }

    [Fact]
    public async Task SharedUnlock_DoesNotResolvePinAcrossTenantBoundary()
    {
        await using var factory = new SharedPosFactory();
        using var ownerClient = factory.CreateClient();
        await LoginOwnerAsync(ownerClient);

        var employees = await ownerClient.GetFromJsonAsync<List<KaryawanDto>>("/api/karyawan");
        var employee = Assert.Single(employees!.Where(x => x.Nama == "Dewi Safitri"));
        var accessResponse = await ownerClient.PutAsJsonAsync(
            $"/api/karyawan/{employee.Id}/shared-access",
            new { pin = "4321", clearPin = false, clearUserLink = false });
        Assert.Equal(HttpStatusCode.OK, accessResponse.StatusCode);

        var otherDeviceToken = await SeedOtherTenantDeviceAsync(factory, "5678");

        using var sharedClient = factory.CreateClient();
        sharedClient.DefaultRequestHeaders.Add("X-NF-Device-Token", otherDeviceToken);
        var response = await sharedClient.PostAsJsonAsync(
            "/api/shared-pos/unlock",
            new { pin = "4321" });

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<ApiError>();
        Assert.Equal("SHARED_POS_AUTH_FAILED", body?.Code);
    }

    private static async Task LoginOwnerAsync(HttpClient client)
    {
        var login = await client.PostAsJsonAsync(
            "/api/auth/login",
            new { username = "owner", password = "owner123" });
        Assert.Equal(HttpStatusCode.OK, login.StatusCode);
        var body = await login.Content.ReadFromJsonAsync<LoginResponseDto>();
        Assert.NotNull(body);
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", body.Token);
    }

    private static async Task<SharedPosUnlockResponseDto?> UnlockAsync(
        HttpClient client,
        string deviceToken,
        string pin)
    {
        client.DefaultRequestHeaders.Remove("X-NF-Device-Token");
        client.DefaultRequestHeaders.Add("X-NF-Device-Token", deviceToken);
        var response = await client.PostAsJsonAsync(
            "/api/shared-pos/unlock",
            new { pin });
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return await response.Content.ReadFromJsonAsync<SharedPosUnlockResponseDto>();
    }

    private static async Task<string> SeedOtherTenantDeviceAsync(
        SharedPosFactory factory,
        string pin)
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var trusted = scope.ServiceProvider.GetRequiredService<ITrustedTenantExecutionScope>();
        var security = scope.ServiceProvider.GetRequiredService<SharedPosSecurity>();

        var tenant = new Tenant
        {
            NamaToko = "OTHER TENANT",
            Slug = $"other-{Guid.NewGuid():N}",
            BusinessType = "general_retail"
        };
        db.Tenants.Add(tenant);
        await db.SaveChangesAsync();

        using var tenantScope = trusted.Begin(tenant.Id, "shared-pos-cross-tenant-test");
        var owner = new User
        {
            TenantId = tenant.Id,
            Nama = "Other Owner",
            Username = $"other-owner-{Guid.NewGuid():N}",
            PasswordHash = BCrypt.Net.BCrypt.HashPassword("other-owner-password"),
            Role = "owner",
            Active = true
        };
        var employee = new Karyawan
        {
            TenantId = tenant.Id,
            Nama = "Other Employee",
            Jabatan = "Kasir",
            Status = "aktif",
            TanggalMasuk = new DateOnly(2026, 1, 1),
            PinHash = SharedPosSecurity.HashPin(pin),
            PinFingerprint = security.FingerprintPin(tenant.Id, pin),
            PinUpdatedAt = DateTime.UtcNow
        };
        var token = SharedPosSecurity.GenerateOpaqueToken();
        var device = new SharedPosDevice
        {
            TenantId = tenant.Id,
            Name = "Other Counter",
            TokenHash = SharedPosSecurity.HashToken(token),
            Active = true,
            CreatedByUserId = owner.Id
        };

        db.Users.Add(owner);
        db.Karyawans.Add(employee);
        db.SharedPosDevices.Add(device);
        await db.SaveChangesAsync();
        return token;
    }

    private sealed class ApiError
    {
        public string Code { get; set; } = string.Empty;
        public string Message { get; set; } = string.Empty;
    }

    private sealed class SharedPosFactory : WebApplicationFactory<Program>
    {
        private readonly string _databaseName = $"shared-pos-{Guid.NewGuid():N}";

        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseEnvironment("Development");
            builder.UseSetting("ConnectionStrings:DefaultConnection", TestConnectionString);
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
                    ["ConnectionStrings:DefaultConnection"] = TestConnectionString,
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
                services.RemoveAll<IDbContextOptionsConfiguration<AppDbContext>>();
                services.AddDbContext<AppDbContext>(options =>
                    options.UseInMemoryDatabase(_databaseName));
            });
        }
    }
}