using Scalar.AspNetCore;
using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using NeverfadePos.Api.Auth;
using NeverfadePos.Api.Data;
using NeverfadePos.Api.Middleware;
using NeverfadePos.Api.Services.Absensi;
using NeverfadePos.Api.Services.Auth;
using NeverfadePos.Api.Services.Customer;
using NeverfadePos.Api.Services.Karyawan;
using NeverfadePos.Api.Services.Laporan;
using NeverfadePos.Api.Services.Product;
using NeverfadePos.Api.Services.PlatformAuth;
using NeverfadePos.Api.Services.PlatformBootstrap;
using NeverfadePos.Api.Services.PlatformTenant;
using NeverfadePos.Api.Services.Payment;
using NeverfadePos.Api.Services.Finance;
using NeverfadePos.Api.Payments.Xendit;
using NeverfadePos.Api.Payments;
using NeverfadePos.Api.Services.Settings;
using NeverfadePos.Api.Services.StockHistory;
using NeverfadePos.Api.Services.Tenant;
using NeverfadePos.Api.Services.Transaction;

var builder = WebApplication.CreateBuilder(args);

var connectionString =
    builder.Configuration.GetConnectionString(
        "DefaultConnection");

if (string.IsNullOrWhiteSpace(connectionString))
{
    throw new InvalidOperationException(
        "ConnectionStrings:DefaultConnection missing.");
}

var jwtKey =
    builder.Configuration["Jwt:Key"];

if (string.IsNullOrWhiteSpace(jwtKey) ||
    jwtKey.Length < 32)
{
    throw new InvalidOperationException(
        "Jwt:Key missing or shorter than 32 characters.");
}

var jwtIssuer =
    builder.Configuration["Jwt:Issuer"];

if (string.IsNullOrWhiteSpace(jwtIssuer))
{
    throw new InvalidOperationException(
        "Jwt:Issuer missing.");
}

var jwtAudience =
    builder.Configuration["Jwt:Audience"];

if (string.IsNullOrWhiteSpace(jwtAudience))
{
    throw new InvalidOperationException(
        "Jwt:Audience missing.");
}

var platformJwtKey =
    builder.Configuration["PlatformJwt:Key"];

if (string.IsNullOrWhiteSpace(platformJwtKey) ||
    platformJwtKey.Length < 32)
{
    throw new InvalidOperationException(
        "PlatformJwt:Key missing or shorter than 32 characters.");
}

var platformJwtIssuer =
    builder.Configuration["PlatformJwt:Issuer"];

if (string.IsNullOrWhiteSpace(platformJwtIssuer))
{
    throw new InvalidOperationException(
        "PlatformJwt:Issuer missing.");
}

var platformJwtAudience =
    builder.Configuration["PlatformJwt:Audience"];

if (string.IsNullOrWhiteSpace(platformJwtAudience))
{
    throw new InvalidOperationException(
        "PlatformJwt:Audience missing.");
}

if (platformJwtKey == jwtKey ||
    platformJwtIssuer == jwtIssuer ||
    platformJwtAudience == jwtAudience)
{
    throw new InvalidOperationException(
        "Platform JWT key, issuer, and audience must be separate from tenant JWT configuration.");
}

var allowedOrigins =
    (builder.Configuration["Cors:AllowedOrigins"] ?? "")
    .Split(
        ',',
        StringSplitOptions.RemoveEmptyEntries |
        StringSplitOptions.TrimEntries);

if (!builder.Environment.IsDevelopment() &&
    allowedOrigins.Length == 0)
{
    throw new InvalidOperationException(
        "Cors:AllowedOrigins missing in production.");
}

builder.Services.AddControllers();

builder.Services.AddOpenApi(options =>
{
    options.AddDocumentTransformer<
        NeverfadePos.Api.Common
            .BearerSecuritySchemeTransformer>();
});

builder.Services.AddEndpointsApiExplorer();

builder.Services.AddCors(options =>
{
    options.AddPolicy(
        "Default",
        policy =>
        {
            if (builder.Environment.IsDevelopment())
            {
                policy
                    .AllowAnyOrigin()
                    .AllowAnyHeader()
                    .AllowAnyMethod();

                return;
            }

            policy
                .WithOrigins(allowedOrigins)
                .AllowAnyHeader()
                .AllowAnyMethod();
        });
});

builder.Services.AddHttpContextAccessor();

builder.Services.Configure<XenditOptions>(
    builder.Configuration.GetSection("Xendit"));

builder.Services.Configure<PaymentModeOptions>(
    builder.Configuration.GetSection("Payments"));

builder.Services.AddSingleton<
    IPaymentModeGate,
    PaymentModeGate>();

builder.Services.AddHttpClient<
    IXenditPaymentProvider,
    XenditPaymentProvider>(client =>
    {
        client.BaseAddress = new Uri("https://api.xendit.co/");
        client.Timeout = TimeSpan.FromSeconds(30);
    });

builder.Services.AddHttpClient<
    IXenditSandboxSimulator,
    XenditSandboxSimulator>(client =>
    {
        client.BaseAddress = new Uri("https://api.xendit.co/");
        client.Timeout = TimeSpan.FromSeconds(30);
    });

builder.Services.AddScoped<CurrentUser>();
builder.Services.AddScoped<PlatformCurrentUser>();

builder.Services.AddScoped<TenantExecutionContext>();

builder.Services.AddScoped<ITenantExecutionContext>(
    services =>
        services.GetRequiredService<
            TenantExecutionContext>());

builder.Services.AddScoped<ITrustedTenantExecutionScope>(
    services =>
        services.GetRequiredService<
            TenantExecutionContext>());

builder.Services.AddScoped<TenantContextService>();
builder.Services.AddScoped<ITenantContextService>(
    services => services.GetRequiredService<TenantContextService>());
builder.Services.AddScoped<ITenantCapabilityService>(
    services => services.GetRequiredService<TenantContextService>());

builder.Services.AddScoped<
    IJwtService,
    JwtService>();

builder.Services.AddScoped<
    IAuthService,
    AuthService>();

builder.Services.AddScoped<
    IPlatformJwtService,
    PlatformJwtService>();

builder.Services.AddScoped<
    IPlatformAuthService,
    PlatformAuthService>();

builder.Services.AddScoped<
    PlatformUserBootstrapService>();

builder.Services.AddScoped<TenantProvisioningService>();

builder.Services.AddScoped<
    IPlatformTenantService,
    PlatformTenantService>();

builder.Services.AddScoped<
    IProductService,
    ProductService>();

builder.Services.AddScoped<
    ISettingsService,
    SettingsService>();

builder.Services.AddScoped<
    ICustomerService,
    CustomerService>();

builder.Services.AddScoped<
    IKaryawanService,
    KaryawanService>();

builder.Services.AddScoped<
    IStockHistoryService,
    StockHistoryService>();

builder.Services.AddScoped<
    IAbsensiService,
    AbsensiService>();

builder.Services.AddScoped<
    ILaporanService,
    LaporanService>();

builder.Services.AddScoped<
    ITransactionService,
    TransactionService>();

builder.Services.AddScoped<
    IPaymentService,
    PaymentService>();

builder.Services.AddScoped<
    ISandboxQrisQaService,
    SandboxQrisQaService>();

builder.Services.AddScoped<
    ITenantFinanceService,
    TenantFinanceService>();

builder.Services.AddScoped<
    IPlatformWithdrawalService,
    PlatformWithdrawalService>();

builder.Services.AddScoped<
    NeverfadePos.Api.Services.Users.IUserService,
    NeverfadePos.Api.Services.Users.UserService>();

builder.Services.AddDbContext<AppDbContext>(
    options =>
        options.UseNpgsql(connectionString));

builder.Services
    .AddAuthentication(
        JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(
        JwtBearerDefaults.AuthenticationScheme,
        options =>
    {
        options.TokenValidationParameters =
            new TokenValidationParameters
            {
                ValidateIssuer = true,
                ValidateAudience = true,
                ValidateLifetime = true,
                ValidateIssuerSigningKey = true,

                ValidIssuer = jwtIssuer,
                ValidAudience = jwtAudience,

                IssuerSigningKey =
                    new SymmetricSecurityKey(
                        Encoding.UTF8.GetBytes(
                            jwtKey))
            };

        options.Events = new JwtBearerEvents
        {
            OnTokenValidated = context =>
            {
                var principal = context.Principal;
                var tenantId = principal?
                    .FindFirst("tenant_id")?.Value;
                var role = principal?
                    .FindFirst(System.Security.Claims.ClaimTypes.Role)?.Value ??
                    principal?.FindFirst("role")?.Value;

                if (principal?.HasClaim("scope", "tenant") != true ||
                    !Guid.TryParse(tenantId, out var parsedTenantId) ||
                    parsedTenantId == Guid.Empty ||
                    role is not ("owner" or "admin" or "kasir"))
                {
                    context.Fail("Invalid tenant identity.");
                }

                return Task.CompletedTask;
            }
        };
    })
    .AddJwtBearer(
        PlatformAuthConstants.AuthenticationScheme,
        options =>
        {
            options.TokenValidationParameters =
                new TokenValidationParameters
                {
                    ValidateIssuer = true,
                    ValidateAudience = true,
                    ValidateLifetime = true,
                    ValidateIssuerSigningKey = true,

                    ValidIssuer = platformJwtIssuer,
                    ValidAudience = platformJwtAudience,

                    IssuerSigningKey =
                        new SymmetricSecurityKey(
                            Encoding.UTF8.GetBytes(
                                platformJwtKey))
                };

            options.Events = new JwtBearerEvents
            {
                OnTokenValidated = context =>
                {
                    if (context.Principal?.HasClaim(
                        claim =>
                            claim.Type == "tenant_id") == true)
                    {
                        context.Fail(
                            "Platform token must not contain tenant_id.");
                    }

                    return Task.CompletedTask;
                },
                OnChallenge = async context =>
                {
                    context.HandleResponse();
                    context.Response.StatusCode =
                        StatusCodes.Status401Unauthorized;
                    context.Response.ContentType =
                        "application/json";

                    await context.Response.WriteAsJsonAsync(
                        new
                        {
                            code =
                                "PLATFORM_AUTHENTICATION_REQUIRED",
                            message =
                                "Autentikasi platform diperlukan."
                        });
                }
            };
        });

builder.Services.AddAuthorization(options =>
{
    options.AddPolicy(
        PlatformAuthConstants.AuthorizationPolicy,
        policy =>
        {
            policy.AddAuthenticationSchemes(
                PlatformAuthConstants.AuthenticationScheme);
            policy.RequireAuthenticatedUser();
            policy.RequireClaim(
                PlatformAuthConstants.ScopeClaim,
                PlatformAuthConstants.PlatformScope);
            policy.RequireRole(
                PlatformAuthConstants.SuperAdminRole);
            policy.RequireAssertion(context =>
                !context.User.HasClaim(
                    claim => claim.Type == "tenant_id"));
        });
});

var app = builder.Build();

_ = app.Services.GetRequiredService<IPaymentModeGate>();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.MapScalarApiReference();
}

app.UseHttpsRedirection();

app.UseMiddleware<ExceptionMiddleware>();

app.UseCors("Default");

app.UseAuthentication();

app.UseMiddleware<TenantStatusMiddleware>();

app.UseAuthorization();

await NeverfadePos.Api.Data.Seed.SeedData
    .InitializeAsync(
        app.Services,
        app.Configuration,
        app.Environment);

await using (var bootstrapScope =
    app.Services.CreateAsyncScope())
{
    await bootstrapScope.ServiceProvider
        .GetRequiredService<
            PlatformUserBootstrapService>()
        .InitializeAsync();
}

app.MapControllers();

app.Run();

public partial class Program;