using Microsoft.EntityFrameworkCore;
using NeverfadePos.Api.Auth;
using NeverfadePos.Api.Data;
using NeverfadePos.Api.Entities;
using NeverfadePos.Api.Services.Outlet;
using NeverfadePos.Api.Services.WhatsApp;
using Xunit;

namespace NeverfadePos.Api.Tests;

public sealed class WhatsAppReceiptAvailabilityTests
{
    [Theory]
    [InlineData(null, false, false, "NOT_CONFIGURED")]
    [InlineData("SCAN_QR_CODE", true, false, "SCAN_QR_CODE")]
    [InlineData("WORKING", true, true, "WORKING")]
    public async Task Availability_DoesNotClaimDeliveryWithoutConnectedSender(
        string? status, bool configured, bool connected, string expectedStatus)
    {
        var tenantId = Guid.NewGuid();
        var outletId = Guid.NewGuid();
        var transactionId = Guid.NewGuid();
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options;
        await using var db = new AppDbContext(options,
            new TestTenant(tenantId), new OutletExecutionContext());
        db.Transactions.Add(new Transaction
        {
            Id = transactionId, TenantId = tenantId, OutletId = outletId,
            NoTrx = "TRX-TEST", Kasir = "QA", KasirId = Guid.NewGuid(),
            MetodePembayaran = "tunai", Status = TransactionStatuses.Paid
        });
        await db.SaveChangesAsync();

        var sender = status is null ? null : new WhatsAppSender
        {
            TenantId = tenantId, OutletId = outletId,
            SessionName = "test-sender"
        };
        var client = new StubWahaClient(status);
        var service = new WhatsAppReceiptService(db, null!,
            new StubResolver(sender), client);
        var result = await service.GetReceiptAvailabilityAsync(transactionId);
        Assert.Equal(configured, result.Configured);
        Assert.Equal(connected, result.Connected);
        Assert.Equal(expectedStatus, result.Status);
        Assert.Equal(status is null ? 0 : 1, client.ReadCount);
    }

    [Fact]
    public async Task Availability_CannotReadAnotherTenantsTransaction()
    {
        var tenantId = Guid.NewGuid();
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options;
        var transactionId = Guid.NewGuid();
        await using (var db = new AppDbContext(options,
            new TestTenant(tenantId), new OutletExecutionContext()))
        {
            db.Transactions.Add(new Transaction
            {
                Id = transactionId, TenantId = tenantId, OutletId = Guid.NewGuid(),
                NoTrx = "TRX-PRIVATE", Kasir = "QA", KasirId = Guid.NewGuid(),
                MetodePembayaran = "tunai", Status = TransactionStatuses.Paid
            });
            await db.SaveChangesAsync();
        }
        await using var foreignDb = new AppDbContext(options,
            new TestTenant(Guid.NewGuid()), new OutletExecutionContext());
        var service = new WhatsAppReceiptService(foreignDb, null!,
            new StubResolver(null), new StubWahaClient(null));
        await Assert.ThrowsAsync<KeyNotFoundException>(() =>
            service.GetReceiptAvailabilityAsync(transactionId));
    }

    private sealed class TestTenant(Guid tenantId) : ITenantExecutionContext
    {
        public TenantExecutionMode Mode => TenantExecutionMode.AuthenticatedTenant;
        public Guid? TargetTenantId => tenantId;
        public string? OperationName => null;
        public bool HasTargetTenant => true;
    }

    private sealed class StubResolver(WhatsAppSender? sender) : IWhatsAppSenderResolver
    {
        public Task<WhatsAppSender?> FindDefaultAsync(Guid? outletId,
            CancellationToken cancellationToken = default) => Task.FromResult(sender);
        public Task<WhatsAppSender> GetOrCreateDefaultAsync(Guid outletId,
            CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task SyncStatusAsync(WhatsAppSender value, WahaSessionInfo session,
            CancellationToken cancellationToken = default) => throw new NotSupportedException();
    }

    private sealed class StubWahaClient(string? status) : IWahaClient
    {
        public int ReadCount { get; private set; }
        public Task<WahaSessionInfo?> GetSessionAsync(string name,
            CancellationToken cancellationToken = default)
        {
            ReadCount++;
            return Task.FromResult<WahaSessionInfo?>(status is null ? null :
                new WahaSessionInfo(name, status, null, null));
        }
        public Task<WahaSessionInfo> EnsureSessionAsync(string name,
            CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<WahaQrCode> GetQrAsync(string name,
            CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task SendTextAsync(string name, string phoneNumber, string text,
            CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task LogoutAsync(string name,
            CancellationToken cancellationToken = default) => throw new NotSupportedException();
    }
}
