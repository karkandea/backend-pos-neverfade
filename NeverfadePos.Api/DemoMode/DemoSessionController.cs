using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using NeverfadePos.Api.Auth;
using NeverfadePos.Api.Data;
using NeverfadePos.Api.Data.Seed;
using NeverfadePos.Api.DTOs.Auth;
using NeverfadePos.Api.Entities;
using NeverfadePos.Api.Services.Auth;

namespace NeverfadePos.Api.DemoMode;

[ApiController]
[Route("api/demo")]
public sealed class DemoSessionController(
    AppDbContext db,
    IServiceProvider services,
    IJwtService jwtService,
    IConfiguration configuration)
    : ControllerBase
{
    [AllowAnonymous]
    [HttpPost("session")]
    [EnableRateLimiting("demo-session")]
    public async Task<ActionResult<LoginResponseDto>> CreateSession(
        [FromQuery] string? businessType,
        CancellationToken cancellationToken)
    {
        if (!configuration.GetValue<bool>("DemoMode:Enabled"))
            return NotFound();

        if (!DemoModeDefaults.TryResolve(businessType, out var profile))
            return BadRequest(new
            {
                code = "DEMO_BUSINESS_TYPE_INVALID",
                message = "Jenis bisnis demo tidak tersedia."
            });

        await DemoSessionLifecycle.CreationLock.WaitAsync(cancellationToken);
        try
        {
            if (!await DemoTenantIntegrity.IsIsolatedAsync(db, cancellationToken))
                throw new InvalidOperationException(
                    "Public demo session requires an isolated demo database.");

            var cookie = Request.Cookies[DemoSessionLifecycle.CookieName];
            if (cookie?.Length != 64 || !cookie.All(Uri.IsHexDigit))
                cookie = Convert.ToHexString(RandomNumberGenerator.GetBytes(32));

            var cookieHash = Convert.ToHexString(SHA256.HashData(Encoding.ASCII.GetBytes(cookie)));
            var now = DateTime.UtcNow;
            var session = await db.DemoVisitorSessions
                .SingleOrDefaultAsync(x => x.CookieHash == cookieHash &&
                    x.BusinessType == profile.Key, cancellationToken);

            if (session is { ExpiresAt: var expiry } && expiry <= now)
                session = null;

            if (session is null)
            {
                await DemoSessionLifecycle.CleanupExpiredAsync(
                    db,
                    services.GetRequiredService<ITrustedTenantExecutionScope>(),
                    cancellationToken);

                if (await db.DemoVisitorSessions.CountAsync(cancellationToken) >=
                    DemoSessionLifecycle.MaxActiveBusinessSessions)
                {
                    return StatusCode(StatusCodes.Status503ServiceUnavailable, new
                    {
                        code = "DEMO_CAPACITY_REACHED",
                        message = "Demo sedang ramai. Coba beberapa saat lagi."
                    });
                }

                var tenantId = Guid.NewGuid();
                var suffix = tenantId.ToString("N");
                var visitorProfile = profile with
                {
                    TenantId = tenantId,
                    TenantSlug = $"demo-{profile.Key.Replace('_', '-')}-{suffix}",
                    Username = $"demo-{suffix}"
                };

                await using var transaction = db.Database.IsRelational()
                    ? await db.Database.BeginTransactionAsync(cancellationToken)
                    : null;

                await DemoSeedData.SeedTenantAsync(
                    db,
                    services.GetRequiredService<ITrustedTenantExecutionScope>(),
                    visitorProfile);

                session = new DemoVisitorSession
                {
                    CookieHash = cookieHash,
                    BusinessType = profile.Key,
                    TenantId = tenantId,
                    CreatedAt = now,
                    LastSeenAt = now,
                    ExpiresAt = now.Add(DemoSessionLifecycle.Lifetime)
                };
                db.DemoVisitorSessions.Add(session);
                await db.SaveChangesAsync(cancellationToken);
                if (transaction is not null)
                    await transaction.CommitAsync(cancellationToken);
            }
            else
            {
                session.LastSeenAt = now;
                session.ExpiresAt = now.Add(DemoSessionLifecycle.Lifetime);
                await db.SaveChangesAsync(cancellationToken);
            }

            Response.Cookies.Append(DemoSessionLifecycle.CookieName, cookie, new CookieOptions
            {
                HttpOnly = true,
                Secure = true,
                SameSite = SameSiteMode.Strict,
                Path = "/api/demo/session",
                MaxAge = DemoSessionLifecycle.Lifetime,
                IsEssential = true
            });

            var user = await db.Users.IgnoreQueryFilters().AsNoTracking()
                .SingleAsync(x => x.TenantId == session.TenantId && x.Active,
                    cancellationToken);

            return Ok(new LoginResponseDto
            {
                Token = jwtService.GenerateToken(user),
                User = new LoginUserDto
                {
                    Id = user.Id,
                    Nama = user.Nama,
                    Username = user.Username,
                    Role = user.Role
                }
            });
        }
        finally
        {
            DemoSessionLifecycle.CreationLock.Release();
        }
    }
}
