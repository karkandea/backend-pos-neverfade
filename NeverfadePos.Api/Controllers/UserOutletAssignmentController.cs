using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using NeverfadePos.Api.Auth;
using NeverfadePos.Api.Common;
using NeverfadePos.Api.Data;
using NeverfadePos.Api.Entities;

namespace NeverfadePos.Api.Controllers;

public sealed class AssignUserOutletsRequest
{
    public List<Guid> OutletIds { get; set; } = [];
}

[ApiController]
[Authorize(Roles = "owner,admin")]
[RequireRecentSharedDeviceReauth]
[Route("api/users/{userId:guid}/outlets")]
public sealed class UserOutletAssignmentController(AppDbContext db, CurrentUser actor) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<Guid>>> Get(Guid userId, CancellationToken ct)
    {
        await RequireManageableUserAsync(userId, ct);
        var assigned = await db.UserOutletAssignments.AsNoTracking()
            .Where(x => x.UserId == userId).Select(x => x.OutletId).ToListAsync(ct);
        return Ok(assigned);
    }

    [HttpPut]
    public async Task<ActionResult<IReadOnlyList<Guid>>> Replace(Guid userId,
        AssignUserOutletsRequest request, CancellationToken ct)
    {
        await RequireManageableUserAsync(userId, ct);
        if (request.OutletIds is null || request.OutletIds.Any(x => x == Guid.Empty) ||
            request.OutletIds.Count > 100 || request.OutletIds.Count != request.OutletIds.Distinct().Count())
            return BadRequest(new { code = "INVALID_OUTLET_ASSIGNMENT", message = "Outlet harus unik dan valid." });

        var allowed = await db.Outlets.AsNoTracking().Where(x => x.Active &&
            request.OutletIds.Contains(x.Id)).Select(x => x.Id).ToListAsync(ct);
        if (allowed.Count != request.OutletIds.Count)
            throw new TenantApiException(403, "OUTLET_NOT_AVAILABLE", "Outlet tidak ditemukan atau tidak aktif.");

        if (actor.Role == "admin")
        {
            var manageable = await db.UserOutletAssignments.AsNoTracking()
                .Where(x => x.UserId == actor.UserId).Select(x => x.OutletId).ToListAsync(ct);
            if (request.OutletIds.Except(manageable).Any())
                throw new TenantApiException(403, "OUTLET_NOT_ASSIGNED", "Admin tidak dapat memberikan outlet di luar penugasannya.");
        }

        var current = await db.UserOutletAssignments.Where(x => x.UserId == userId).ToListAsync(ct);
        var requested = request.OutletIds.ToHashSet();
        db.UserOutletAssignments.RemoveRange(current.Where(x => !requested.Contains(x.OutletId)));
        var currentIds = current.Select(x => x.OutletId).ToHashSet();
        foreach (var outletId in requested.Except(currentIds))
            db.UserOutletAssignments.Add(new UserOutletAssignment
            {
                TenantId = actor.TenantId!.Value, UserId = userId, OutletId = outletId
            });

        db.TenantAuditEvents.Add(new TenantAuditEvent
        {
            TenantId = actor.TenantId!.Value,
            ActorUserId = actor.UserId,
            EventType = "USER_OUTLET_ASSIGNMENTS_CHANGED",
            Metadata = JsonSerializer.Serialize(new { userId, outletIds = request.OutletIds.OrderBy(x => x) })
        });
        await db.SaveChangesAsync(ct);
        return Ok(request.OutletIds);
    }

    private async Task RequireManageableUserAsync(Guid userId, CancellationToken ct)
    {
        var target = await db.Users.AsNoTracking().FirstOrDefaultAsync(x => x.Id == userId, ct)
            ?? throw new KeyNotFoundException("User tidak ditemukan.");
        if (target.Role == "owner")
            throw new TenantApiException(403, "OWNER_ACCOUNT_PROTECTED", "Owner memiliki akses semua outlet.");
        if (actor.Role == "admin" && target.Role == "admin" && target.Id == actor.UserId)
            throw new TenantApiException(403, "SELF_ASSIGNMENT_FORBIDDEN", "Admin tidak dapat mengubah assignment sendiri.");
    }
}
