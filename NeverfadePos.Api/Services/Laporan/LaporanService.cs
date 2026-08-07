using Microsoft.EntityFrameworkCore;
using NeverfadePos.Api.Data;
using NeverfadePos.Api.DTOs.Laporan;

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
                x => x.CreatedAt >= startUtc);

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
        CancellationToken cancellationToken = default)
    {
        var nowWib =
            TimeZoneInfo.ConvertTimeFromUtc(
                DateTime.UtcNow,
                Wib);

        var todayWib =
            nowWib.Date;

        var startWib =
            todayWib.AddDays(-6);

        var endWib =
            todayWib.AddDays(1);

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

        var totalsByWibDate =
            raw
                .GroupBy(
                    x =>
                        ToWibDate(
                            x.CreatedAt))
                .ToDictionary(
                    x => x.Key,
                    x => x.Sum(
                        y => y.Total));

        var result =
            new List<LaporanChartDto>(
                capacity: 7);

        for (var i = 0; i < 7; i++)
        {
            var day =
                startWib.AddDays(i);

            var date =
                DateOnly.FromDateTime(day);

            totalsByWibDate.TryGetValue(
                date,
                out var total);

            result.Add(
                new LaporanChartDto
                {
                    Date =
                        day.ToString(
                            "yyyy-MM-dd"),

                    Label =
                        Hari[
                            (int)day.DayOfWeek],

                    Total =
                        total
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

    private static DateOnly ToWibDate(
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

        return DateOnly.FromDateTime(
            wib);
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
