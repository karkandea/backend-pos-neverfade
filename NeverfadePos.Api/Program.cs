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
using NeverfadePos.Api.Services.Settings;
using NeverfadePos.Api.Services.StockHistory;
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

builder.Services.AddScoped<CurrentUser>();

builder.Services.AddScoped<TenantExecutionContext>();

builder.Services.AddScoped<ITenantExecutionContext>(
    services =>
        services.GetRequiredService<
            TenantExecutionContext>());

builder.Services.AddScoped<ITrustedTenantExecutionScope>(
    services =>
        services.GetRequiredService<
            TenantExecutionContext>());

builder.Services.AddScoped<
    IJwtService,
    JwtService>();

builder.Services.AddScoped<
    IAuthService,
    AuthService>();

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
    NeverfadePos.Api.Services.Users.IUserService,
    NeverfadePos.Api.Services.Users.UserService>();

builder.Services.AddDbContext<AppDbContext>(
    options =>
        options.UseNpgsql(connectionString));

builder.Services
    .AddAuthentication(
        JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
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
    });

builder.Services.AddAuthorization();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.MapScalarApiReference();
}

app.UseHttpsRedirection();

app.UseMiddleware<ExceptionMiddleware>();

app.UseCors("Default");

app.UseAuthentication();

app.UseAuthorization();

await NeverfadePos.Api.Data.Seed.SeedData
    .InitializeAsync(
        app.Services,
        app.Configuration,
        app.Environment);

app.MapControllers();

app.Run();
