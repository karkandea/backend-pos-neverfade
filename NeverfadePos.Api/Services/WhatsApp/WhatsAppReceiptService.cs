using System.Globalization;
using System.Text;
using Microsoft.EntityFrameworkCore;
using NeverfadePos.Api.Auth;
using NeverfadePos.Api.Data;

namespace NeverfadePos.Api.Services.WhatsApp;

public sealed record WhatsAppConnectionStatus(
    bool Configured,
    string Status,
    string? PhoneNumber,
    string? PushName);

public sealed record WhatsAppReceiptResult(
    string PhoneMasked);

public interface IWhatsAppReceiptService
{
    Task<WhatsAppConnectionStatus> GetStatusAsync(
        CancellationToken cancellationToken = default);

    Task<WhatsAppConnectionStatus> ConnectAsync(
        CancellationToken cancellationToken = default);

    Task<WahaQrCode> GetQrAsync(
        CancellationToken cancellationToken = default);

    Task LogoutAsync(
        CancellationToken cancellationToken = default);

    Task<WhatsAppReceiptResult> SendReceiptAsync(
        Guid transactionId,
        string phoneNumber,
        CancellationToken cancellationToken = default);
}

public sealed class WhatsAppReceiptService(
    AppDbContext db,
    ITenantExecutionContext tenantContext,
    IWahaClient wahaClient)
    : IWhatsAppReceiptService
{
    public async Task<WhatsAppConnectionStatus> GetStatusAsync(
        CancellationToken cancellationToken = default)
    {
        var session = await wahaClient.GetSessionAsync(
            GetSessionName(),
            cancellationToken);

        return session is null
            ? new WhatsAppConnectionStatus(
                false,
                "NOT_CONFIGURED",
                null,
                null)
            : ToStatus(session);
    }

    public async Task<WhatsAppConnectionStatus> ConnectAsync(
        CancellationToken cancellationToken = default)
    {
        var session = await wahaClient.EnsureSessionAsync(
            GetSessionName(),
            cancellationToken);

        return ToStatus(session);
    }

    public async Task<WahaQrCode> GetQrAsync(
        CancellationToken cancellationToken = default)
    {
        await wahaClient.EnsureSessionAsync(
            GetSessionName(),
            cancellationToken);

        return await wahaClient.GetQrAsync(
            GetSessionName(),
            cancellationToken);
    }

    public Task LogoutAsync(
        CancellationToken cancellationToken = default)
    {
        return wahaClient.LogoutAsync(
            GetSessionName(),
            cancellationToken);
    }

    public async Task<WhatsAppReceiptResult> SendReceiptAsync(
        Guid transactionId,
        string phoneNumber,
        CancellationToken cancellationToken = default)
    {
        var phone = WhatsAppPhone.NormalizeIndonesia(phoneNumber);
        var session = await wahaClient.GetSessionAsync(
            GetSessionName(),
            cancellationToken);

        if (session is null ||
            !string.Equals(
                session.Status,
                "WORKING",
                StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(
                "WhatsApp toko belum terhubung.");
        }

        var transaction = await db.Transactions
            .AsNoTracking()
            .Include(x => x.Items)
            .FirstOrDefaultAsync(
                x => x.Id == transactionId,
                cancellationToken)
            ?? throw new KeyNotFoundException(
                "Transaksi tidak ditemukan.");

        var settings = await db.Settings
            .AsNoTracking()
            .FirstOrDefaultAsync(cancellationToken)
            ?? throw new KeyNotFoundException(
                "Settings tidak ditemukan.");

        var message = BuildReceiptText(
            settings.NamaToko,
            settings.HeaderStruk,
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
            session.Name,
            phone,
            message,
            cancellationToken);

        return new WhatsAppReceiptResult(
            WhatsAppPhone.Mask(phone));
    }

    private string GetSessionName()
    {
        var tenantId = tenantContext.TargetTenantId
            ?? throw new InvalidOperationException(
                "Tenant context tidak tersedia.");

        return $"nf-{tenantId:N}";
    }

    private static WhatsAppConnectionStatus ToStatus(
        WahaSessionInfo session)
    {
        return new WhatsAppConnectionStatus(
            true,
            session.Status,
            session.PhoneNumber,
            session.PushName);
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

        text.AppendLine($"*{storeName.Trim()}*");
        if (!string.IsNullOrWhiteSpace(header))
        {
            text.AppendLine(header.Trim());
        }

        text.AppendLine($"No. Transaksi: {transactionNumber}");
        text.AppendLine(
            transactionDate.ToLocalTime().ToString(
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
