using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using NeverfadePos.Api.Entities;

namespace NeverfadePos.Api.Data.Configurations;

public class TransactionConfiguration : IEntityTypeConfiguration<Transaction>
{
    public void Configure(EntityTypeBuilder<Transaction> builder)
    {
        builder.ToTable(
            "transactions",
            table => table.HasCheckConstraint(
                "CK_transactions_Status",
                "\"Status\" IN ('pending_payment', 'paid', 'failed')"));

        builder.HasKey(x => x.Id);

        builder.HasIndex(x => x.TenantId);
        builder.HasIndex(x => new { x.TenantId, x.NoTrx }).IsUnique();

        builder.Property(x => x.NoTrx).HasMaxLength(50).IsRequired();
        builder.Property(x => x.Kasir).HasMaxLength(200).IsRequired();
        builder.Property(x => x.CustomerNama).HasMaxLength(200);
        builder.Property(x => x.MetodePembayaran).HasMaxLength(50);
        builder.Property(x => x.Status)
            .HasMaxLength(30)
            .HasDefaultValue(TransactionStatuses.Paid)
            .IsRequired();

        builder.Property(x => x.Subtotal).HasPrecision(18,2);
        builder.Property(x => x.Disc).HasPrecision(18,2);
        builder.Property(x => x.Tax).HasPrecision(18,2);
        builder.Property(x => x.DiscAmt).HasPrecision(18,2);
        builder.Property(x => x.TaxAmt).HasPrecision(18,2);
        builder.Property(x => x.Total).HasPrecision(18,2);
        builder.Property(x => x.Dibayar).HasPrecision(18,2);
        builder.Property(x => x.Kembalian).HasPrecision(18,2);

        builder.HasOne(x => x.Tenant)
            .WithMany(x => x.Transactions)
            .HasForeignKey(x => x.TenantId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(x => x.Customer)
            .WithMany(x => x.Transactions)
            .HasForeignKey(x => x.CustomerId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.HasMany(x => x.Items)
            .WithOne(x => x.Transaction)
            .HasForeignKey(x => x.TransactionId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
