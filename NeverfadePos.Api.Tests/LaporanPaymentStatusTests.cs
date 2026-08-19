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

        var service = new LaporanService(db);
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
