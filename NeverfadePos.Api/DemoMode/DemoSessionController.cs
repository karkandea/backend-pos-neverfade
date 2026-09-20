using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using NeverfadePos.Api.Data;
using NeverfadePos.Api.DTOs.Auth;
using NeverfadePos.Api.Services.Auth;

namespace NeverfadePos.Api.DemoMode;

[ApiController]
[Route("api/demo")]
public sealed class DemoSessionController(
    AppDbContext db,
    IJwtService jwtService,
    IConfiguration configuration)
    : ControllerBase
{
    [AllowAnonymous]
    [HttpPost("session")]
    public async Task<ActionResult<LoginResponseDto>> CreateSession(
        [FromQuery] string? businessType,
        CancellationToken cancellationToken)
    {
        if (!configuration.GetValue<bool>("DemoMode:Enabled"))
        {
            return NotFound();
        }

        if (!DemoModeDefaults.TryResolve(businessType, out var profile))
        {
            return BadRequest(new
            {
                code = "DEMO_BUSINESS_TYPE_INVALID",
                message = "Jenis bisnis demo tidak tersedia."
            });
        }

        var tenantIds = await db.Tenants
            .AsNoTracking()
            .Select(x => x.Id)
            .ToListAsync(cancellationToken);

        if (!DemoModeDefaults.IsExactDemoTenantSet(tenantIds))
        {
            throw new InvalidOperationException(
                "Public demo session requires an isolated NeverFade multi-business demo database.");
        }

        var user = await db.Users
            .IgnoreQueryFilters()
            .AsNoTracking()
            .SingleOrDefaultAsync(
                x => x.TenantId == profile.TenantId &&
                     x.Username == profile.Username &&
                     x.Active,
                cancellationToken)
            ?? throw new InvalidOperationException(
                $"Demo user for '{profile.Key}' is not available.");

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
}
