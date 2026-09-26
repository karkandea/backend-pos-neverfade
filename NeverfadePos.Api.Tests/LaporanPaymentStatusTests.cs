using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using NeverfadePos.Api.Auth;
using NeverfadePos.Api.Data;
using NeverfadePos.Api.Entities;
using NeverfadePos.Api.Services.Laporan;
using Xunit;

namespace NeverfadePos.Api.Tests;

public sealed class LaporanPaymentStatusTests
{
    [Fact]
    public async Task Reports_IncludeOnlyPaidTransactions()
    {
        var tenantId = Guid.NewGuid();
        var context = CreateContext(tenantId);
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase($"laporan-paid-{Guid.NewGuid():N}")
            .Options;

        await using var db = new AppDbContext(options, context);
        db.Transactions.AddRange(
            NewTransaction(tenantId, TransactionStatuses.Paid, "PAID", 100m),
            NewTransaction(tenantId, TransactionStatuses.PendingPayment, "PENDING", 900m),
            NewTransaction(tenantId, TransactionStatuses.Failed, "FAILED", 800m));
        await db.SaveChangesAsync();

        var service = CreateReportService(db, tenantId);
        var summary = await service.GetSummaryAsync("harian");
        var chart = await service.GetChartAsync();
        var products = await service.GetTopProductsAsync("harian");

        Assert.Equal(100m, summary.Omzet);
        Assert.Equal(1, summary.Transaksi);
        Assert.Equal(100m, summary.Avg);
        Assert.Equal(100m, chart.Sum(x => x.Total));
        var product = Assert.Single(products);
        Assert.Equal("PAID", product.Nama);
        Assert.Equal(1, product.Qty);
        Assert.Equal(100m, product.Revenue);
    }

    [Fact]
    public async Task Chart_UsesSelectedPeriodAndOnlyPaidTransactions()
    {
        var tenantId = Guid.NewGuid();
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase($"laporan-period-{Guid.NewGuid():N}").Options;
        await using var db = new AppDbContext(options, CreateContext(tenantId));
        db.Transactions.AddRange(
            NewTransaction(tenantId, TransactionStatuses.Paid, "PAID", 125m),
            NewTransaction(tenantId, TransactionStatuses.PendingPayment, "PENDING", 900m));
        await db.SaveChangesAsync();

        var service = CreateReportService(db, tenantId);
        var wib = TimeZoneInfo.FindSystemTimeZoneById(
            OperatingSystem.IsWindows() ? "SE Asia Standard Time" : "Asia/Jakarta");
        var today = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, wib);
        var daily = await service.GetChartAsync("harian");
        var weekly = await service.GetChartAsync("mingguan");
        var monthly = await service.GetChartAsync("bulanan");
        var yearly = await service.GetChartAsync("tahunan");

        Assert.Equal(24, daily.Count);
        Assert.Equal(7, weekly.Count);
        Assert.Equal(today.Day, monthly.Count);
        Assert.Equal(today.Month, yearly.Count);
        Assert.Equal($"{today:yyyy-MM-dd}T00:00", daily[0].Date);
        Assert.Equal(125m, daily.Sum(item => item.Total));
        Assert.Equal(125m, weekly.Sum(item => item.Total));
        Assert.Equal(125m, monthly.Sum(item => item.Total));
        Assert.Equal(125m, yearly.Sum(item => item.Total));
        Assert.Equal(7, (await service.GetChartAsync()).Count);
    }

    [Fact]
    public async Task CustomDateRange_AppliesToSummaryChartAndTopProducts()
    {
        var tenantId = Guid.NewGuid();
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase($"laporan-custom-{Guid.NewGuid():N}").Options;
        await using var db = new AppDbContext(options, CreateContext(tenantId));
        var zone = TimeZoneInfo.FindSystemTimeZoneById(OperatingSystem.IsWindows() ? "SE Asia Standard Time" : "Asia/Jakarta");
        var localToday = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, zone).Date;
        var yesterday = DateOnly.FromDateTime(localToday.AddDays(-1));
        var today = DateOnly.FromDateTime(localToday);
        var included = NewTransaction(tenantId, TransactionStatuses.Paid, "IN-RANGE", 125m);
        included.CreatedAt = TimeZoneInfo.ConvertTimeToUtc(localToday.AddDays(-1).AddHours(12), zone);
        var excluded = NewTransaction(tenantId, TransactionStatuses.Paid, "OUT-RANGE", 900m);
        excluded.CreatedAt = TimeZoneInfo.ConvertTimeToUtc(localToday.AddDays(-3).AddHours(12), zone);
        db.Transactions.AddRange(included, excluded);
        await db.SaveChangesAsync();

        var service = CreateReportService(db, tenantId);
        var summary = await service.GetSummaryAsync("harian", default, yesterday, today);
        var chart = await service.GetChartAsync("harian", default, yesterday, today);
        var products = await service.GetTopProductsAsync("harian", default, yesterday, today);
        Assert.Equal(125m, summary.Omzet);
        Assert.Equal(2, chart.Count);
        Assert.Equal(125m, chart.Sum(item => item.Total));
        Assert.Equal("IN-RANGE", Assert.Single(products).Nama);
        var monthly = await service.GetChartAsync("harian", default, today.AddDays(-40), today);
        Assert.InRange(monthly.Count, 2, 3);
        Assert.All(monthly, item => Assert.Equal(7, item.Date.Length));
    }

    private static LaporanService CreateReportService(AppDbContext db, Guid tenantId)
    {
        var accessor = new HttpContextAccessor
        {
            HttpContext = new DefaultHttpContext
            {
                User = new ClaimsPrincipal(new ClaimsIdentity(new[]
                {
                    new Claim("tenant_id", tenantId.ToString()),
                    new Claim(ClaimTypes.NameIdentifier, Guid.NewGuid().ToString()),
                    new Claim(ClaimTypes.Role, "owner")
                }, "Test"))
            }
        };
        return new LaporanService(db, new CurrentUser(accessor), accessor);
    }

    private static Transaction NewTransaction(
        Guid tenantId,
        string status,
        string productName,
        decimal total)
    {
        var transaction = new Transaction
        {
            TenantId = tenantId,
            NoTrx = $"TRX-{productName}",
            Kasir = "QA",
            Status = status,
            Subtotal = total,
            Total = total,
            Dibayar = status == TransactionStatuses.Paid ? total : 0m,
            CreatedAt = DateTime.UtcNow,
            Tanggal = DateTime.UtcNow
        };
        transaction.Items.Add(new TransactionItem
        {
            TenantId = tenantId,
            TransactionId = transaction.Id,
            ProductId = Guid.NewGuid(),
            Nama = productName,
            HargaJual = total,
            Qty = 1,
            Subtotal = total
        });
        return transaction;
    }

    private static TenantExecutionContext CreateContext(Guid tenantId)
    {
        var accessor = new HttpContextAccessor
        {
            HttpContext = new DefaultHttpContext
            {
                User = new ClaimsPrincipal(new ClaimsIdentity(
                    new[] { new Claim("tenant_id", tenantId.ToString()) },
                    "Test"))
            }
        };
        return new TenantExecutionContext(new CurrentUser(accessor));
    }
}
