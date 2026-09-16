using Microsoft.EntityFrameworkCore;
using NeverfadePos.Api.Auth;
using NeverfadePos.Api.Data;
using NeverfadePos.Api.Entities;

namespace NeverfadePos.Api.Services.WhatsApp;

public interface IWhatsAppSenderResolver
{
    Task<WhatsAppSender?> FindDefaultAsync(
        Guid? outletId,
        CancellationToken cancellationToken = default);

    Task<WhatsAppSender> GetOrCreateDefaultAsync(
        Guid outletId,
        CancellationToken cancellationToken = default);

    Task SyncStatusAsync(
        WhatsAppSender sender,
        WahaSessionInfo session,
        CancellationToken cancellationToken = default);
}

public sealed class WhatsAppSenderResolver(
    AppDbContext db,
    ITenantExecutionContext tenantContext)
    : IWhatsAppSenderResolver
{
    public Task<WhatsAppSender?> FindDefaultAsync(
        Guid? outletId,
        CancellationToken cancellationToken = default)
    {
        var query = db.WhatsAppSenders
            .Where(x =>
                x.Provider == "waha" &&
                x.Active);

        query = outletId.HasValue && outletId.Value != Guid.Empty
            ? query.Where(x => x.OutletId == outletId.Value)
            : query.Where(x =>
                x.Outlet != null &&
                x.Outlet.Active &&
                x.Outlet.IsDefault);

        return query
            .OrderByDescending(x => x.IsDefault)
            .ThenBy(x => x.CreatedAt)
            .FirstOrDefaultAsync(cancellationToken);
    }

    public async Task<WhatsAppSender> GetOrCreateDefaultAsync(
        Guid outletId,
        CancellationToken cancellationToken = default)
    {
        var tenantId = tenantContext.TargetTenantId
            ?? throw new UnauthorizedAccessException();

        var outletExists = await db.Outlets
            .AnyAsync(
                x => x.Id == outletId && x.Active,
                cancellationToken);

        if (!outletExists)
        {
            throw new KeyNotFoundException(
                "Outlet aktif tidak ditemukan.");
        }

        var sender = await FindDefaultAsync(
            outletId,
            cancellationToken);

        if (sender is not null)
        {
            if (!sender.IsDefault)
            {
                sender.IsDefault = true;
                await db.SaveChangesAsync(cancellationToken);
            }

            return sender;
        }

        var senderId = Guid.NewGuid();
        sender = new WhatsAppSender
        {
            Id = senderId,
            TenantId = tenantId,
            OutletId = outletId,
            Provider = "waha",
            SessionName = BuildSessionName(
                tenantId,
                outletId,
                senderId),
            DisplayName = "Struk Utama",
            LastStatus = "NOT_CONFIGURED",
            IsDefault = true,
            Active = true
        };

        db.WhatsAppSenders.Add(sender);
        await db.SaveChangesAsync(cancellationToken);

        return sender;
    }

    public async Task SyncStatusAsync(
        WhatsAppSender sender,
        WahaSessionInfo session,
        CancellationToken cancellationToken = default)
    {
        var changed = false;

        if (!string.Equals(
                sender.LastStatus,
                session.Status,
                StringComparison.Ordinal))
        {
            sender.LastStatus = session.Status;
            changed = true;
        }

        var phoneNumber = session.PhoneNumber ?? string.Empty;
        if (!string.Equals(
                sender.PhoneNumber,
                phoneNumber,
                StringComparison.Ordinal))
        {
            sender.PhoneNumber = phoneNumber;
            changed = true;
        }

        if (!string.IsNullOrWhiteSpace(session.PushName) &&
            !string.Equals(
                sender.DisplayName,
                session.PushName,
                StringComparison.Ordinal))
        {
            sender.DisplayName = session.PushName;
            changed = true;
        }

        if (string.Equals(
                session.Status,
                "WORKING",
                StringComparison.OrdinalIgnoreCase) &&
            !sender.LastConnectedAt.HasValue)
        {
            sender.LastConnectedAt = DateTime.UtcNow;
            changed = true;
        }

        if (changed)
        {
            await db.SaveChangesAsync(cancellationToken);
        }
    }

    internal static string BuildSessionName(
        Guid tenantId,
        Guid outletId,
        Guid senderId)
    {
        return $"nf-{tenantId:N}-{outletId:N}-{senderId:N}";
    }
}
