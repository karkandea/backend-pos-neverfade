using Microsoft.EntityFrameworkCore;
using NeverfadePos.Api.Data;
using NeverfadePos.Api.DTOs.Laporan;
using NeverfadePos.Api.Entities;

namespace NeverfadePos.Api.Services.Laporan;

public sealed class LaporanService(AppDbContext db)
    : ILaporanService
{
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
        CancellationToken cancellationToken = default)
    {
        var startUtc =
            GetStartUtc(period);

        var query = db.Transactions
            .AsNoTracking()
            .Where(
                x =>
                    x.Status == TransactionStatuses.Paid &&
                    x.CreatedAt >= startUtc);

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
        CancellationToken cancellationToken = default)
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
        var startWib = selected switch
        {
            "mingguan" => todayWib.AddDays(-6),
            "bulanan" => new DateTime(todayWib.Year, todayWib.Month, 1),
            "tahunan" => new DateTime(todayWib.Year, 1, 1),
            _ => todayWib
        };
        var bucketCount = selected switch
        {
            "harian" => 24,
            "mingguan" => 7,
            "bulanan" => todayWib.Day,
            _ => todayWib.Month
        };
        var endWib = todayWib.AddDays(1);

        var startUtc =
            TimeZoneInfo.ConvertTimeToUtc(
                startWib,
                Wib);

        var endUtc =
            TimeZoneInfo.ConvertTimeToUtc(
                endWib,
                Wib);

        var raw =
            await db.Transactions
                .AsNoTracking()
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
            var bucket = selected switch
            {
                "harian" => startWib.AddHours(index),
                "tahunan" => startWib.AddMonths(index),
                _ => startWib.AddDays(index)
            };
            totals.TryGetValue(index, out var total);

            result.Add(new LaporanChartDto
            {
                Date = selected switch
                {
                    "harian" => bucket.ToString("yyyy-MM-dd'T'HH':'mm", System.Globalization.CultureInfo.InvariantCulture),
                    "tahunan" => bucket.ToString("yyyy-MM"),
                    _ => bucket.ToString("yyyy-MM-dd")
                },
                Label = selected switch
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
            CancellationToken cancellationToken = default)
    {
        var startUtc =
            GetStartUtc(period);

        return await db.TransactionItems
            .AsNoTracking()
            .Where(
                x =>
                    x.Transaction!.Status == TransactionStatuses.Paid &&
                    x.Transaction!.CreatedAt >=
                    startUtc)
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

    private static DateTime GetStartUtc(
        string period)
    {
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
