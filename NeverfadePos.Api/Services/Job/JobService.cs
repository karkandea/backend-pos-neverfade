using Microsoft.EntityFrameworkCore;
using NeverfadePos.Api.Auth;
using NeverfadePos.Api.Common;
using NeverfadePos.Api.Data;
using NeverfadePos.Api.DTOs.Job;
using NeverfadePos.Api.Services.Outlet;

namespace NeverfadePos.Api.Services.Job;

public interface IJobService
{
    Task<JobDto> GetAsync(
        Guid id,
        Guid? outletId,
        CancellationToken cancellationToken = default);
}

public sealed class JobService(
    AppDbContext db,
    CurrentUser currentUser,
    IOutletService outletService)
    : IJobService
{
    public async Task<JobDto> GetAsync(
        Guid id,
        Guid? outletId,
        CancellationToken cancellationToken = default)
    {
        if (!currentUser.UserId.HasValue ||
            currentUser.UserId.Value == Guid.Empty ||
            !currentUser.TenantId.HasValue ||
            currentUser.TenantId.Value == Guid.Empty)
        {
            throw new TenantApiException(
                StatusCodes.Status401Unauthorized,
                "AUTHENTICATION_REQUIRED",
                "Autentikasi diperlukan.");
        }

        var query = db.Jobs
            .AsNoTracking()
            .Where(x => x.Id == id);

        if (outletId.HasValue)
        {
            if (outletId.Value == Guid.Empty)
            {
                throw new TenantApiException(
                    StatusCodes.Status400BadRequest,
                    "INVALID_OUTLET_ID",
                    "Outlet yang dipilih tidak valid.");
            }

            var outlet = await outletService.ResolveAsync(
                outletId,
                cancellationToken);
            query = query.Where(x => x.OutletId == outlet.Id);
        }

        if (!string.Equals(
            currentUser.Role,
            "owner",
            StringComparison.Ordinal))
        {
            var actorId = currentUser.UserId.Value;
            query = query.Where(x => x.ActorUserId == actorId);
        }

        var job = await query.SingleOrDefaultAsync(cancellationToken);
        if (job is null)
        {
            throw new TenantApiException(
                StatusCodes.Status404NotFound,
                "JOB_NOT_FOUND",
                "Job tidak ditemukan.");
        }

        return new JobDto
        {
            Id = job.Id,
            Kind = job.Kind,
            State = job.State,
            ResultReference = job.ResultReference,
            CorrelationId = job.CorrelationId
        };
    }
}
