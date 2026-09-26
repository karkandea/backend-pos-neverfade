using Microsoft.EntityFrameworkCore;
using NeverfadePos.Api.Auth;
using NeverfadePos.Api.Common;
using Microsoft.AspNetCore.Http;
using NeverfadePos.Api.Data;
using NeverfadePos.Api.DTOs.Laporan;
using NeverfadePos.Api.Entities;

namespace NeverfadePos.Api.Services.Laporan;

public sealed class LaporanService(
    AppDbContext db, CurrentUser currentUser, IHttpContextAccessor httpContextAccessor)
    : ILaporanService
{
    // No header: owner sees tenant aggregate; admin sees only explicitly assigned outlets.
    // Header: both roles see only the validated selected outlet. Historical records in
    // disabled outlets remain readable; absent/foreign assignment never grants access.
    private async Task<Guid[]?> VisibleOutletIdsAsync(CancellationToken cancellationToken)
    {
        if (!currentUser.UserId.HasValue || !currentUser.TenantId.HasValue ||
            currentUser.Role is not ("owner" or "admin"))
            throw new TenantApiException(StatusCodes.Status403Forbidden,
                "REPORT_ACCESS_DENIED", "Akses laporan tidak diizinkan.");

        var raw = httpContextAccessor.HttpContext?.Request.Headers["X-Outlet-Id"].ToString();
        var isOwner = currentUser.Role == "owner";
        if (string.IsNullOrWhiteSpace(raw))
        {
            if (isOwner) return null;
            return await db.UserOutletAssignments.AsNoTracking()
                .Where(x => x.UserId == currentUser.UserId.Value)
                .Select(x => x.OutletId).Distinct().ToArrayAsync(cancellationToken);
        }
        if (!Guid.TryParse(raw, out var selected) || selected == Guid.Empty)
            throw new TenantApiException(StatusCodes.Status400BadRequest,
                "INVALID_OUTLET_ID", "Outlet yang dipilih tidak valid.");
        var exists = await db.Outlets.AsNoTracking()
            .AnyAsync(x => x.Id == selected, cancellationToken);
        if (!exists)
            throw new TenantApiException(StatusCodes.Status404NotFound,
                "OUTLET_NOT_FOUND", "Outlet tidak ditemukan.");
        if (!isOwner && !await db.UserOutletAssignments.AsNoTracking().AnyAsync(
                x => x.UserId == currentUser.UserId.Value && x.OutletId == selected,
                cancellationToken))
            throw new TenantApiException(StatusCodes.Status403Forbidden,
                "OUTLET_NOT_ASSIGNED", "Anda tidak memiliki akses ke outlet ini.");
        return [selected];
    }

    private static readonly TimeZoneInfo Wib =
        TimeZoneInfo.FindSystemTimeZoneById(
            OperatingSystem.IsWindows()
                ? "SE Asia Standard Time"
                : "Asia/Jakarta");

    private static readonly string[] Hari =
    {
        "Min",
        "Sen",
        "Sel",
        "Rab",
        "Kam",
        "Jum",
        "Sab"
    };

    public async Task<LaporanSummaryDto> GetSummaryAsync(
        string period,
        CancellationToken cancellationToken = default,
        DateOnly? startDate = null,
        DateOnly? endDate = null)
    {
        var startUtc = GetStartUtc(period, startDate);
        var endUtc = GetEndUtc(endDate);
        var visibleOutletIds = await VisibleOutletIdsAsync(cancellationToken);

        var query = db.Transactions.AsNoTracking();
        if (visibleOutletIds is not null)
            query = query.Where(x => x.OutletId.HasValue && visibleOutletIds.Contains(x.OutletId.Value));
        query = query.Where(
                x =>
                    x.Status == TransactionStatuses.Paid &&
                    x.CreatedAt >= startUtc &&
                    x.CreatedAt < endUtc);

        var omzet =
            await query
                .SumAsync(
                    x => (decimal?)x.Total,
                    cancellationToken)
            ?? 0m;

        var transaksi =
            await query
                .CountAsync(
                    cancellationToken);

        var pelanggan =
            await query
                .Where(
                    x => x.CustomerId != null)
                .Select(
                    x => x.CustomerId)
                .Distinct()
                .CountAsync(
                    cancellationToken);

        return new LaporanSummaryDto
        {
            Omzet = omzet,

            Transaksi = transaksi,

            Avg =
                transaksi == 0
                    ? 0
                    : omzet / transaksi,

            Pelanggan = pelanggan
        };
    }

    public async Task<List<LaporanChartDto>> GetChartAsync(
        string period = "mingguan",
        CancellationToken cancellationToken = default,
        DateOnly? startDate = null,
        DateOnly? endDate = null)
    {
        // Existing clients without a period keep the seven-day chart.
        var selected = period?.Trim().ToLowerInvariant() switch
        {
            "harian" => "harian",
            "bulanan" => "bulanan",
            "tahunan" => "tahunan",
            _ => "mingguan"
        };
        var todayWib = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, Wib).Date;
        var custom = startDate.HasValue && endDate.HasValue;
        var customDays = custom ? endDate!.Value.DayNumber - startDate!.Value.DayNumber + 1 : 0;
        var customMonthly = custom && customDays > 31;
        var startWib = custom ? startDate!.Value.ToDateTime(TimeOnly.MinValue) : selected switch
        {
            "mingguan" => todayWib.AddDays(-6),
            "bulanan" => new DateTime(todayWib.Year, todayWib.Month, 1),
            "tahunan" => new DateTime(todayWib.Year, 1, 1),
            _ => todayWib
        };
        var bucketStartWib = customMonthly ? new DateTime(startWib.Year, startWib.Month, 1) : startWib;
        var bucketCount = custom ? customMonthly
            ? (endDate!.Value.Year - startWib.Year) * 12 + endDate.Value.Month - startWib.Month + 1
            : customDays
            : selected switch
            {
                "harian" => 24,
                "mingguan" => 7,
                "bulanan" => todayWib.Day,
                _ => todayWib.Month
            };
        var endWib = custom ? endDate!.Value.AddDays(1).ToDateTime(TimeOnly.MinValue) : todayWib.AddDays(1);

        var startUtc =
            TimeZoneInfo.ConvertTimeToUtc(
                startWib,
                Wib);

        var endUtc =
            TimeZoneInfo.ConvertTimeToUtc(
                endWib,
                Wib);

        var visibleOutletIds = await VisibleOutletIdsAsync(cancellationToken);
        var transactions = db.Transactions.AsNoTracking();
        if (visibleOutletIds is not null)
            transactions = transactions.Where(x => x.OutletId.HasValue && visibleOutletIds.Contains(x.OutletId.Value));
        var raw =
            await transactions
                .Where(
                    x =>
                        x.Status == TransactionStatuses.Paid &&
                        x.CreatedAt >= startUtc &&
                        x.CreatedAt < endUtc)
                .Select(
                    x => new
                    {
                        x.CreatedAt,
                        x.Total
                    })
                .ToListAsync(
                    cancellationToken);

        var totals = raw
            .GroupBy(x =>
            {
                var local = ToWibDateTime(x.CreatedAt);
                if (customMonthly)
                    return (local.Year - startWib.Year) * 12 + local.Month - startWib.Month;
                if (custom)
                    return (local.Date - startWib).Days;
                return selected switch
                {
                    "harian" => local.Hour,
                    "bulanan" => local.Day - 1,
                    "tahunan" => local.Month - 1,
                    _ => (local.Date - startWib).Days
                };
            })
            .ToDictionary(group => group.Key, group => group.Sum(x => x.Total));

        var result = new List<LaporanChartDto>(bucketCount);

        for (var index = 0; index < bucketCount; index++)
        {
            var bucket = customMonthly ? bucketStartWib.AddMonths(index) : custom ? startWib.AddDays(index) : selected switch
            {
                "harian" => startWib.AddHours(index),
                "tahunan" => startWib.AddMonths(index),
                _ => startWib.AddDays(index)
            };
            totals.TryGetValue(index, out var total);

            result.Add(new LaporanChartDto
            {
                Date = customMonthly ? bucket.ToString("yyyy-MM") : custom ? bucket.ToString("yyyy-MM-dd") : selected switch
                {
                    "harian" => bucket.ToString("yyyy-MM-dd'T'HH':'mm", System.Globalization.CultureInfo.InvariantCulture),
                    "tahunan" => bucket.ToString("yyyy-MM"),
                    _ => bucket.ToString("yyyy-MM-dd")
                },
                Label = customMonthly ? bucket.ToString("MMM yy", System.Globalization.CultureInfo.GetCultureInfo("id-ID"))
                    : custom ? bucket.ToString("dd MMM", System.Globalization.CultureInfo.GetCultureInfo("id-ID")) : selected switch
                {
                    "harian" => bucket.ToString("HH.00"),
                    "mingguan" => Hari[(int)bucket.DayOfWeek],
                    "bulanan" => bucket.ToString("dd"),
                    _ => bucket.ToString("MMM", System.Globalization.CultureInfo.GetCultureInfo("id-ID"))
                },
                Total = total
            });
        }

        return result;
    }

    public async Task<List<TopProductDto>>
        GetTopProductsAsync(
            string period,
            CancellationToken cancellationToken = default,
            DateOnly? startDate = null,
            DateOnly? endDate = null)
    {
        var startUtc = GetStartUtc(period, startDate);
        var endUtc = GetEndUtc(endDate);
        var visibleOutletIds = await VisibleOutletIdsAsync(cancellationToken);

        var items = db.TransactionItems.AsNoTracking();
        if (visibleOutletIds is not null)
            items = items.Where(x => x.Transaction!.OutletId.HasValue &&
                visibleOutletIds.Contains(x.Transaction.OutletId.Value));
        return await items
            .Where(
                x =>
                    x.Transaction!.Status == TransactionStatuses.Paid &&
                    x.Transaction!.CreatedAt >= startUtc &&
                    x.Transaction!.CreatedAt < endUtc)
            .GroupBy(
                x => x.Nama)
            .Select(
                x => new TopProductDto
                {
                    Nama =
                        x.Key,

                    Qty =
                        x.Sum(
                            y => y.Qty),

                    Revenue =
                        x.Sum(
                            y => y.Subtotal)
                })
            .OrderByDescending(
                x => x.Qty)
            .Take(10)
            .ToListAsync(
                cancellationToken);
    }

    private static DateTime ToWibDateTime(
        DateTime utc)
    {
        var normalizedUtc =
            utc.Kind == DateTimeKind.Utc
                ? utc
                : DateTime.SpecifyKind(
                    utc,
                    DateTimeKind.Utc);

        var wib =
            TimeZoneInfo.ConvertTimeFromUtc(
                normalizedUtc,
                Wib);

        return wib;
    }

    private static DateTime GetEndUtc(DateOnly? customEndDate)
    {
        var exclusiveWib = customEndDate.HasValue
            ? customEndDate.Value.AddDays(1).ToDateTime(TimeOnly.MinValue)
            : TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, Wib).Date.AddDays(1);
        return TimeZoneInfo.ConvertTimeToUtc(exclusiveWib, Wib);
    }

    private static DateTime GetStartUtc(
        string period,
        DateOnly? customStartDate = null)
    {
        if (customStartDate.HasValue)
            return TimeZoneInfo.ConvertTimeToUtc(customStartDate.Value.ToDateTime(TimeOnly.MinValue), Wib);

        var now =
            TimeZoneInfo.ConvertTimeFromUtc(
                DateTime.UtcNow,
                Wib);

        DateTime start =
            period.ToLowerInvariant() switch
            {
                "harian" =>
                    now.Date,

                "mingguan" =>
                    now.Date.AddDays(-6),

                "bulanan" =>
                    new DateTime(
                        now.Year,
                        now.Month,
                        1),

                "tahunan" =>
                    new DateTime(
                        now.Year,
                        1,
                        1),

                _ =>
                    now.Date
            };

        return TimeZoneInfo.ConvertTimeToUtc(
            start,
            Wib);
    }
}
