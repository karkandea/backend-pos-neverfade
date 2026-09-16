using Microsoft.EntityFrameworkCore;
using NeverfadePos.Api.Auth;
using NeverfadePos.Api.Data;
using NeverfadePos.Api.Entities;
using NeverfadePos.Api.Services.Outlet;
using Xunit;

namespace NeverfadePos.Api.Tests;

public sealed class OutletArchitectureTests
{
    [Fact]
    public async Task AddedTransaction_GetsOutletSnapshotFromExecutionScope()
    {
        var tenantId = Guid.NewGuid();
        var outletId = Guid.NewGuid();
        var tenantContext = new TestTenantExecutionContext(tenantId);
        var outletContext = new OutletExecutionContext();
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        await using var db = new AppDbContext(
            options,
            tenantContext,
            outletContext);

        using (outletContext.Begin(outletId))
        {
            db.Transactions.Add(new Transaction
            {
                TenantId = tenantId,
                NoTrx = "TRX-OUTLET-001",
                Kasir = "Kasir",
                KasirId = Guid.NewGuid(),
                MetodePembayaran = "tunai",
                Status = TransactionStatuses.Paid
            });

            await db.SaveChangesAsync();
        }

        var saved = await db.Transactions.SingleAsync();
        Assert.Equal(outletId, saved.OutletId);
    }

    [Fact]
    public async Task ExistingOutletSnapshot_IsNeverOverwritten()
    {
        var tenantId = Guid.NewGuid();
        var originalOutletId = Guid.NewGuid();
        var otherOutletId = Guid.NewGuid();
        var tenantContext = new TestTenantExecutionContext(tenantId);
        var outletContext = new OutletExecutionContext();
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        await using var db = new AppDbContext(
            options,
            tenantContext,
            outletContext);

        using (outletContext.Begin(otherOutletId))
        {
            db.Transactions.Add(new Transaction
            {
                TenantId = tenantId,
                OutletId = originalOutletId,
                NoTrx = "TRX-OUTLET-002",
                Kasir = "Kasir",
                KasirId = Guid.NewGuid(),
                MetodePembayaran = "tunai",
                Status = TransactionStatuses.Paid
            });

            await db.SaveChangesAsync();
        }

        var saved = await db.Transactions.SingleAsync();
        Assert.Equal(originalOutletId, saved.OutletId);
    }

    [Fact]
    public void OutletExecutionScope_RejectsNestedOutletSwitch()
    {
        var context = new OutletExecutionContext();

        using var scope = context.Begin(Guid.NewGuid());

        Assert.Throws<InvalidOperationException>(
            () => context.Begin(Guid.NewGuid()));
    }

    private sealed class TestTenantExecutionContext(Guid tenantId)
        : ITenantExecutionContext
    {
        public TenantExecutionMode Mode =>
            TenantExecutionMode.AuthenticatedTenant;

        public Guid? TargetTenantId => tenantId;

        public string? OperationName => null;

        public bool HasTargetTenant => true;
    }
}
