using Microsoft.EntityFrameworkCore;
using NeverfadePos.Api.Auth;
using NeverfadePos.Api.Data;
using NeverfadePos.Api.Entities;
using Npgsql;
using Xunit;

namespace NeverfadePos.PostgresIntegrationTests;

/// <summary>
/// Uses separate, disposable PostgreSQL databases to verify constraints that
/// EF's in-memory provider cannot enforce. No application DB is reused.
/// </summary>
public sealed class PostgresPersistenceTests
{
    [Fact]
    public async Task Transactions_AreTenantIsolatedAndCrossTenantWritesAreRejected()
    {
        await using var fixture = await PostgresDatabase.CreateAsync();
        var tenantA = Guid.NewGuid();
        var tenantB = Guid.NewGuid();
        await fixture.SeedTenantsAsync(tenantA, tenantB);

        await using (var first = fixture.OpenTenant(tenantA))
        {
            first.Transactions.Add(NewTransaction("POS-ISOLATION"));
            await first.SaveChangesAsync();
        }

        await using (var otherTenant = fixture.OpenTenant(tenantB))
        {
            Assert.Empty(await otherTenant.Transactions.ToListAsync());

            // Same receipt number is valid for another tenant (composite index).
            otherTenant.Transactions.Add(NewTransaction("POS-ISOLATION"));
            await otherTenant.SaveChangesAsync();
        }

        await using (var second = fixture.OpenTenant(tenantB))
        {
            second.Transactions.Add(NewTransaction("ILLEGAL-WRITE", tenantA));
            await Assert.ThrowsAsync<InvalidOperationException>(() =>
                second.SaveChangesAsync());
        }

        await using var verifyA = fixture.OpenTenant(tenantA);
        await using var verifyB = fixture.OpenTenant(tenantB);
        Assert.Equal(1, await verifyA.Transactions.CountAsync());
        Assert.Equal(1, await verifyB.Transactions.CountAsync());
    }

    [Fact]
    public async Task ConcurrentTransactions_SameTenantAndReceiptOnlyOnePersists()
    {
        await using var fixture = await PostgresDatabase.CreateAsync();
        var tenant = Guid.NewGuid();
        await fixture.SeedTenantsAsync(tenant);
        var gate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        async Task<bool> InsertAsync()
        {
            await using var db = fixture.OpenTenant(tenant);
            db.Transactions.Add(NewTransaction("CONCURRENT-RECEIPT"));
            await gate.Task;
            try
            {
                await db.SaveChangesAsync();
                return true;
            }
            catch (DbUpdateException ex) when (IsUniqueViolation(ex))
            {
                return false;
            }
        }

        var first = Task.Run(InsertAsync);
        var second = Task.Run(InsertAsync);
        gate.SetResult();
        var outcomes = await Task.WhenAll(first, second).WaitAsync(TimeSpan.FromSeconds(20));

        Assert.Single(outcomes, saved => saved);
        Assert.Single(outcomes, saved => !saved);
        await using var verify = fixture.OpenTenant(tenant);
        Assert.Equal(1, await verify.Transactions.CountAsync());
    }

    [Fact]
    public async Task ConcurrentPayments_SameProviderReferenceOnlyOnePersists()
    {
        await using var fixture = await PostgresDatabase.CreateAsync();
        var tenant = Guid.NewGuid();
        await fixture.SeedTenantsAsync(tenant);
        Guid firstTransactionId;
        Guid secondTransactionId;

        await using (var seed = fixture.OpenTenant(tenant))
        {
            var first = NewTransaction("PAYMENT-1");
            var second = NewTransaction("PAYMENT-2");
            seed.Transactions.AddRange(first, second);
            await seed.SaveChangesAsync();
            firstTransactionId = first.Id;
            secondTransactionId = second.Id;
        }

        var gate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        async Task<bool> InsertAsync(Guid transactionId)
        {
            await using var db = fixture.OpenTenant(tenant);
            db.Payments.Add(new Payment
            {
                TransactionId = transactionId,
                ProviderReferenceId = "duplicate-provider-ref",
                Amount = 125_000m,
                Status = "pending",
            });
            await gate.Task;
            try
            {
                await db.SaveChangesAsync();
                return true;
            }
            catch (DbUpdateException ex) when (IsUniqueViolation(ex))
            {
                return false;
            }
        }

        var firstPayment = Task.Run(() => InsertAsync(firstTransactionId));
        var secondPayment = Task.Run(() => InsertAsync(secondTransactionId));
        gate.SetResult();
        var outcomes = await Task.WhenAll(firstPayment, secondPayment)
            .WaitAsync(TimeSpan.FromSeconds(20));

        Assert.Single(outcomes, saved => saved);
        Assert.Single(outcomes, saved => !saved);
        await using var verify = fixture.OpenTenant(tenant);
        Assert.Equal(1, await verify.Payments.CountAsync());
    }

    private static Transaction NewTransaction(string receipt, Guid? tenantId = null) => new()
    {
        TenantId = tenantId ?? Guid.Empty,
        NoTrx = receipt,
        Kasir = "Integration Test",
        Total = 125_000m,
        MetodePembayaran = "cash",
        Status = "paid",
    };

    private static bool IsUniqueViolation(DbUpdateException exception) =>
        exception.InnerException is PostgresException { SqlState: "23505" };

    private sealed class FixedTenant(Guid id) : ITenantExecutionContext
    {
        public TenantExecutionMode Mode => TenantExecutionMode.AuthenticatedTenant;
        public Guid? TargetTenantId => id;
        public string? OperationName => null;
        public bool HasTargetTenant => true;
    }

    private sealed class PostgresDatabase : IAsyncDisposable
    {
        private readonly string _connectionString;

        private PostgresDatabase(string connectionString) =>
            _connectionString = connectionString;

        public static async Task<PostgresDatabase> CreateAsync()
        {
            var raw = Environment.GetEnvironmentVariable("POS_TEST_POSTGRES");
            if (string.IsNullOrWhiteSpace(raw))
            {
                throw new InvalidOperationException(
                    "POS_TEST_POSTGRES must point to a disposable loopback PostgreSQL server.");
            }

            var connection = new NpgsqlConnectionStringBuilder(raw);
            if (connection.Host is not ("localhost" or "127.0.0.1" or "::1") ||
                !string.Equals(connection.Database, "postgres", StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException(
                    "Integration tests require loopback host and Database=postgres.");
            }

            connection.Database = "neverfade_it_" + Guid.NewGuid().ToString("N");
            connection.Pooling = false;
            var fixture = new PostgresDatabase(connection.ConnectionString);
            await using var context = fixture.OpenUnscoped();
            try
            {
                await context.Database.EnsureCreatedAsync();
            }
            catch
            {
                await context.Database.EnsureDeletedAsync();
                throw;
            }

            return fixture;
        }

        public AppDbContext OpenTenant(Guid tenant) => new(
            NewOptions(), new FixedTenant(tenant));

        private AppDbContext OpenUnscoped() => new(NewOptions());

        private DbContextOptions<AppDbContext> NewOptions() =>
            new DbContextOptionsBuilder<AppDbContext>()
                .UseNpgsql(_connectionString)
                .Options;

        public async Task SeedTenantsAsync(params Guid[] ids)
        {
            await using var db = OpenUnscoped();
            foreach (var id in ids)
            {
                db.Tenants.Add(new Tenant
                {
                    Id = id,
                    NamaToko = "Integration Test",
                    Slug = "test-" + id.ToString("N"),
                });
            }
            await db.SaveChangesAsync();
        }

        public async ValueTask DisposeAsync()
        {
            var database = new NpgsqlConnectionStringBuilder(_connectionString).Database;
            if (database is null || !database.StartsWith("neverfade_it_", StringComparison.Ordinal))
            {
                throw new InvalidOperationException("Refusing to drop a non-test database.");
            }

            await using var context = OpenUnscoped();
            await context.Database.EnsureDeletedAsync();
        }
    }
}
