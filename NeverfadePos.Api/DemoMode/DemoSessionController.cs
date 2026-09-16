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
        CancellationToken cancellationToken)
    {
        if (!configuration.GetValue<bool>("DemoMode:Enabled"))
        {
            return NotFound();
        }

        var tenantIds = await db.Tenants
            .AsNoTracking()
            .Select(x => x.Id)
            .ToListAsync(cancellationToken);

        if (tenantIds.Count != 1 || tenantIds[0] != DemoModeDefaults.TenantId)
        {
            throw new InvalidOperationException(
                "Public demo session requires an isolated NeverFade demo database.");
        }

        var user = await db.Users
            .IgnoreQueryFilters()
            .AsNoTracking()
            .SingleOrDefaultAsync(
                x => x.TenantId == DemoModeDefaults.TenantId &&
                     x.Username == DemoModeDefaults.Username &&
                     x.Active,
                cancellationToken)
            ?? throw new InvalidOperationException("Demo user is not available.");

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
