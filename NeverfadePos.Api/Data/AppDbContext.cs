using Microsoft.EntityFrameworkCore;
using NeverfadePos.Api.Auth;
using NeverfadePos.Api.Common;
using NeverfadePos.Api.Entities;

namespace NeverfadePos.Api.Data;

public class AppDbContext : DbContext
{
    private readonly ITenantExecutionContext? _tenantExecutionContext;

    public AppDbContext(
        DbContextOptions<AppDbContext> options,
        ITenantExecutionContext? tenantExecutionContext = null)
        : base(options)
    {
        _tenantExecutionContext = tenantExecutionContext;
    }

    public DbSet<Tenant> Tenants => Set<Tenant>();
    public DbSet<PlatformUser> PlatformUsers => Set<PlatformUser>();
    public DbSet<PlatformAuditEvent> PlatformAuditEvents => Set<PlatformAuditEvent>();
    public DbSet<User> Users => Set<User>();
    public DbSet<Settings> Settings => Set<Settings>();
    public DbSet<Product> Products => Set<Product>();
    public DbSet<Customer> Customers => Set<Customer>();
    public DbSet<Karyawan> Karyawans => Set<Karyawan>();
    public DbSet<Absensi> Absensis => Set<Absensi>();
    public DbSet<StockHistory> StockHistories => Set<StockHistory>();
    public DbSet<Transaction> Transactions => Set<Transaction>();
    public DbSet<TransactionItem> TransactionItems => Set<TransactionItem>();
    public DbSet<Payment> Payments => Set<Payment>();
    public DbSet<PaymentLedgerEntry> PaymentLedgerEntries => Set<PaymentLedgerEntry>();
    public DbSet<PaymentWebhookEvent> PaymentWebhookEvents => Set<PaymentWebhookEvent>();
    public DbSet<PaymentRoute> PaymentRoutes => Set<PaymentRoute>();
    public DbSet<WithdrawalRequest> WithdrawalRequests => Set<WithdrawalRequest>();
    public DbSet<WithdrawalRoute> WithdrawalRoutes => Set<WithdrawalRoute>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly);

        ApplyTenantFilters(modelBuilder);
    }

    private void ApplyTenantFilters(ModelBuilder modelBuilder)
    {
        foreach (var entityType in modelBuilder.Model.GetEntityTypes())
        {
            if (!typeof(BaseEntity).IsAssignableFrom(entityType.ClrType))
                continue;

            var method = typeof(AppDbContext)
                .GetMethod(nameof(SetTenantFilter), System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!
                .MakeGenericMethod(entityType.ClrType);

            method.Invoke(this, new object[] { modelBuilder });
        }
    }

    private void SetTenantFilter<TEntity>(ModelBuilder builder)
        where TEntity : BaseEntity
    {
        builder.Entity<TEntity>()
            .HasQueryFilter(x =>
                HasTargetTenant &&
                x.TenantId == TargetTenantId);
    }

    public override int SaveChanges()
    {
        ValidateTenantWrites();

        return base.SaveChanges();
    }

    public override int SaveChanges(
        bool acceptAllChangesOnSuccess)
    {
        ValidateTenantWrites();

        return base.SaveChanges(
            acceptAllChangesOnSuccess);
    }

    public override Task<int> SaveChangesAsync(
        CancellationToken cancellationToken = default)
    {
        ValidateTenantWrites();

        return base.SaveChangesAsync(
            cancellationToken);
    }

    public override Task<int> SaveChangesAsync(
        bool acceptAllChangesOnSuccess,
        CancellationToken cancellationToken = default)
    {
        ValidateTenantWrites();

        return base.SaveChangesAsync(
            acceptAllChangesOnSuccess,
            cancellationToken);
    }

    private bool HasTargetTenant =>
        _tenantExecutionContext?.HasTargetTenant == true;

    private Guid TargetTenantId =>
        _tenantExecutionContext?.TargetTenantId ??
        Guid.Empty;

    private void ValidateTenantWrites()
    {
        var entries = ChangeTracker
            .Entries<BaseEntity>()
            .Where(x =>
                x.State is
                    EntityState.Added or
                    EntityState.Modified or
                    EntityState.Deleted)
            .ToList();

        if (entries.Count == 0)
        {
            return;
        }

        if (!HasTargetTenant)
        {
            throw new InvalidOperationException(
                "Tenant-scoped writes require an explicit tenant execution context.");
        }

        var targetTenantId = TargetTenantId;

        foreach (var entry in entries)
        {
            var tenantProperty = entry.Property(
                nameof(BaseEntity.TenantId));

            if (entry.State == EntityState.Added &&
                entry.Entity.TenantId == Guid.Empty)
            {
                entry.Entity.TenantId = targetTenantId;
            }

            if (entry.State != EntityState.Added &&
                tenantProperty.IsModified)
            {
                throw new InvalidOperationException(
                    "TenantId cannot be changed.");
            }

            if (entry.Entity.TenantId != targetTenantId)
            {
                throw new InvalidOperationException(
                    "Tenant-scoped entity does not match the active tenant execution context.");
            }

            if (entry.State != EntityState.Added &&
                entry.OriginalValues.GetValue<Guid>(
                    nameof(BaseEntity.TenantId)) !=
                targetTenantId)
            {
                throw new InvalidOperationException(
                    "Tenant-scoped entity does not belong to the active tenant execution context.");
            }
        }
    }
}
