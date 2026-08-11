using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using NeverfadePos.Api.Entities;

namespace NeverfadePos.Api.Data.Configurations;

public sealed class PaymentLedgerEntryConfiguration
    : IEntityTypeConfiguration<PaymentLedgerEntry>
{
    public void Configure(EntityTypeBuilder<PaymentLedgerEntry> builder)
    {
        builder.ToTable(
            "payment_ledger_entries",
            table => table.HasCheckConstraint(
                "CK_payment_ledger_entries_EntryType",
                "\"EntryType\" IN ('payment_credit')"));

        builder.HasKey(x => x.Id);
        builder.HasIndex(x => x.TenantId);
        builder.HasIndex(x => new { x.PaymentId, x.EntryType }).IsUnique();
        builder.HasIndex(x => new { x.TenantId, x.CreatedAt });

        builder.Property(x => x.EntryType).HasMaxLength(50).IsRequired();
        builder.Property(x => x.Amount).HasPrecision(18, 2);
        builder.Property(x => x.Currency).HasMaxLength(3).IsRequired();
        builder.Property(x => x.ProviderReference).HasMaxLength(100).IsRequired();

        builder.HasOne(x => x.Tenant)
            .WithMany(x => x.PaymentLedgerEntries)
            .HasForeignKey(x => x.TenantId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.Payment)
            .WithMany(x => x.LedgerEntries)
            .HasForeignKey(x => x.PaymentId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.Transaction)
            .WithMany()
            .HasForeignKey(x => x.TransactionId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
