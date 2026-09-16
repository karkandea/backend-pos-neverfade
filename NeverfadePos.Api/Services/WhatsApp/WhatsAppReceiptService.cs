using System.Globalization;
using System.Text;
using Microsoft.EntityFrameworkCore;
using NeverfadePos.Api.Data;
using NeverfadePos.Api.Entities;
using NeverfadePos.Api.Services.Outlet;

namespace NeverfadePos.Api.Services.WhatsApp;

public sealed record WhatsAppConnectionStatus(
    bool Configured,
    string Status,
    string? PhoneNumber,
    string? PushName,
    Guid? OutletId = null,
    Guid? SenderId = null);

public sealed record WhatsAppReceiptResult(
    string PhoneMasked);

public interface IWhatsAppReceiptService
{
    Task<WhatsAppConnectionStatus> GetStatusAsync(
        Guid? outletId,
        CancellationToken cancellationToken = default);

    Task<WhatsAppConnectionStatus> ConnectAsync(
        Guid? outletId,
        CancellationToken cancellationToken = default);

    Task<WahaQrCode> GetQrAsync(
        Guid? outletId,
        CancellationToken cancellationToken = default);

    Task LogoutAsync(
        Guid? outletId,
        CancellationToken cancellationToken = default);

    Task<WhatsAppReceiptResult> SendReceiptAsync(
        Guid transactionId,
        string phoneNumber,
        CancellationToken cancellationToken = default);
}

public sealed class WhatsAppReceiptService(
    AppDbContext db,
    IOutletService outletService,
    IWhatsAppSenderResolver senderResolver,
    IWahaClient wahaClient)
    : IWhatsAppReceiptService
{
    public async Task<WhatsAppConnectionStatus> GetStatusAsync(
        Guid? outletId,
        CancellationToken cancellationToken = default)
    {
        var outlet = await outletService.ResolveAsync(
            outletId,
            cancellationToken);
        var sender = await senderResolver.FindDefaultAsync(
            outlet.Id,
            cancellationToken);

        if (sender is null)
        {
            return new WhatsAppConnectionStatus(
                false,
                "NOT_CONFIGURED",
                null,
                null,
                outlet.Id,
                null);
        }

        var session = await wahaClient.GetSessionAsync(
            sender.SessionName,
            cancellationToken);

        if (session is null)
        {
            return new WhatsAppConnectionStatus(
                false,
                "NOT_CONFIGURED",
                sender.PhoneNumber,
                sender.DisplayName,
                outlet.Id,
                sender.Id);
        }

        await senderResolver.SyncStatusAsync(
            sender,
            session,
            cancellationToken);

        return ToStatus(outlet.Id, sender, session);
    }

    public async Task<WhatsAppConnectionStatus> ConnectAsync(
        Guid? outletId,
        CancellationToken cancellationToken = default)
    {
        var outlet = await outletService.ResolveAsync(
            outletId,
            cancellationToken);
        var sender = await senderResolver.GetOrCreateDefaultAsync(
            outlet.Id,
            cancellationToken);
        var session = await wahaClient.EnsureSessionAsync(
            sender.SessionName,
            cancellationToken);

        await senderResolver.SyncStatusAsync(
            sender,
            session,
            cancellationToken);

        return ToStatus(outlet.Id, sender, session);
    }

    public async Task<WahaQrCode> GetQrAsync(
        Guid? outletId,
        CancellationToken cancellationToken = default)
    {
        var outlet = await outletService.ResolveAsync(
            outletId,
            cancellationToken);
        var sender = await senderResolver.GetOrCreateDefaultAsync(
            outlet.Id,
            cancellationToken);

        await wahaClient.EnsureSessionAsync(
            sender.SessionName,
            cancellationToken);

        return await wahaClient.GetQrAsync(
            sender.SessionName,
            cancellationToken);
    }

    public async Task LogoutAsync(
        Guid? outletId,
        CancellationToken cancellationToken = default)
    {
        var outlet = await outletService.ResolveAsync(
            outletId,
            cancellationToken);
        var sender = await senderResolver.FindDefaultAsync(
            outlet.Id,
            cancellationToken);

        if (sender is null)
        {
            return;
        }

        await wahaClient.LogoutAsync(
            sender.SessionName,
            cancellationToken);

        var session = await wahaClient.GetSessionAsync(
            sender.SessionName,
            cancellationToken);

        if (session is not null)
        {
            await senderResolver.SyncStatusAsync(
                sender,
                session,
                cancellationToken);
        }
    }

    public async Task<WhatsAppReceiptResult> SendReceiptAsync(
        Guid transactionId,
        string phoneNumber,
        CancellationToken cancellationToken = default)
    {
        var phone = WhatsAppPhone.NormalizeIndonesia(phoneNumber);

        var transaction = await db.Transactions
            .AsNoTracking()
            .Include(x => x.Items)
            .Include(x => x.Outlet)
            .FirstOrDefaultAsync(
                x => x.Id == transactionId,
                cancellationToken)
            ?? throw new KeyNotFoundException(
                "Transaksi tidak ditemukan.");

        if (!string.Equals(
                transaction.Status,
                TransactionStatuses.Paid,
                StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(
                "Struk WhatsApp hanya dapat dikirim untuk transaksi yang sudah berhasil.");
        }

        var sender = await senderResolver.FindDefaultAsync(
            transaction.OutletId,
            cancellationToken)
            ?? throw new InvalidOperationException(
                "WhatsApp outlet belum dikonfigurasi.");

        var session = await wahaClient.GetSessionAsync(
            sender.SessionName,
            cancellationToken);

        if (session is null ||
            !string.Equals(
                session.Status,
                "WORKING",
                StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(
                "WhatsApp outlet belum terhubung.");
        }

        await senderResolver.SyncStatusAsync(
            sender,
            session,
            cancellationToken);

        var settings = await db.Settings
            .AsNoTracking()
            .FirstOrDefaultAsync(cancellationToken)
            ?? throw new KeyNotFoundException(
                "Settings tidak ditemukan.");

        var header = BuildOutletReceiptHeader(
            settings.HeaderStruk,
            settings.NamaToko,
            transaction.Outlet);

        var message = BuildReceiptText(
            settings.NamaToko,
            header,
            settings.FooterStruk,
            transaction.NoTrx,
            transaction.Tanggal,
            transaction.Items
                .Select(x => new ReceiptLine(
                    BuildItemName(x.Nama, x.VariantLabel),
                    x.Quantity > 0 ? x.Quantity : x.Qty,
                    x.HargaJual,
                    x.Subtotal,
                    x.Unit))
                .ToList(),
            transaction.Subtotal,
            transaction.DiscAmt,
            transaction.TaxAmt,
            transaction.Total,
            transaction.MetodePembayaran,
            transaction.Dibayar,
            transaction.Kembalian);

        await wahaClient.SendTextAsync(
            sender.SessionName,
            phone,
            message,
            cancellationToken);

        return new WhatsAppReceiptResult(
            WhatsAppPhone.Mask(phone));
    }

    private static WhatsAppConnectionStatus ToStatus(
        Guid outletId,
        WhatsAppSender sender,
        WahaSessionInfo session)
    {
        return new WhatsAppConnectionStatus(
            true,
            session.Status,
            session.PhoneNumber,
            session.PushName,
            outletId,
            sender.Id);
    }

    private static string BuildOutletReceiptHeader(
        string configuredHeader,
        string storeName,
        NeverfadePos.Api.Entities.Outlet? outlet)
    {
        var parts = new List<string>();

        if (!string.IsNullOrWhiteSpace(configuredHeader))
        {
            parts.Add(configuredHeader.Trim());
        }

        if (outlet is not null &&
            !string.Equals(
                outlet.Name.Trim(),
                storeName.Trim(),
                StringComparison.OrdinalIgnoreCase))
        {
            parts.Add($"Outlet: {outlet.Name.Trim()}");
        }

        if (outlet is not null &&
            !string.IsNullOrWhiteSpace(outlet.Address))
        {
            parts.Add(outlet.Address.Trim());
        }

        return string.Join(Environment.NewLine, parts);
    }

    private static string BuildItemName(
        string name,
        string variantLabel)
    {
        return string.IsNullOrWhiteSpace(variantLabel)
            ? name
            : $"{name} ({variantLabel})";
    }

    internal static string BuildReceiptText(
        string storeName,
        string header,
        string footer,
        string transactionNumber,
        DateTime transactionDate,
        IReadOnlyCollection<ReceiptLine> items,
        decimal subtotal,
        decimal discount,
        decimal tax,
        decimal total,
        string paymentMethod,
        decimal paid,
        decimal change)
    {
        var culture = CultureInfo.GetCultureInfo("id-ID");
        var text = new StringBuilder();
        var localTransactionDate = ToJakartaTime(transactionDate);

        text.AppendLine($"*{storeName.Trim()}*");
        if (!string.IsNullOrWhiteSpace(header))
        {
            text.AppendLine(header.Trim());
        }

        text.AppendLine($"No. Transaksi: {transactionNumber}");
        text.AppendLine(
            localTransactionDate.ToString(
                "dd MMM yyyy, HH:mm",
                culture));
        text.AppendLine("------------------------------");

        foreach (var item in items)
        {
            var qty = item.Quantity.ToString(
                item.Quantity % 1 == 0 ? "0" : "0.###",
                culture);
            var unit = string.IsNullOrWhiteSpace(item.Unit)
                ? string.Empty
                : $" {item.Unit}";

            text.AppendLine(item.Name);
            text.AppendLine(
                $"{qty}{unit} × {Money(item.UnitPrice, culture)} = {Money(item.Subtotal, culture)}");
        }

        text.AppendLine("------------------------------");
        text.AppendLine($"Subtotal: {Money(subtotal, culture)}");

        if (discount > 0)
        {
            text.AppendLine($"Diskon: -{Money(discount, culture)}");
        }

        if (tax > 0)
        {
            text.AppendLine($"Pajak: {Money(tax, culture)}");
        }

        text.AppendLine($"*Total: {Money(total, culture)}*");
        text.AppendLine($"Pembayaran: {paymentMethod}");

        if (paid > 0)
        {
            text.AppendLine($"Dibayar: {Money(paid, culture)}");
        }

        if (change > 0)
        {
            text.AppendLine($"Kembalian: {Money(change, culture)}");
        }

        if (!string.IsNullOrWhiteSpace(footer))
        {
            text.AppendLine();
            text.AppendLine(footer.Trim());
        }

        return text.ToString().Trim();
    }

    private static DateTime ToJakartaTime(DateTime value)
    {
        var jakarta = TimeZoneInfo.FindSystemTimeZoneById("Asia/Jakarta");

        return value.Kind switch
        {
            DateTimeKind.Utc => TimeZoneInfo.ConvertTimeFromUtc(value, jakarta),
            DateTimeKind.Local => TimeZoneInfo.ConvertTime(value, jakarta),
            _ => TimeZoneInfo.ConvertTimeFromUtc(
                DateTime.SpecifyKind(value, DateTimeKind.Utc),
                jakarta)
        };
    }

    private static string Money(
        decimal value,
        CultureInfo culture)
    {
        return $"Rp{value.ToString("N0", culture)}";
    }

    internal sealed record ReceiptLine(
        string Name,
        decimal Quantity,
        decimal UnitPrice,
        decimal Subtotal,
        string Unit);
}
