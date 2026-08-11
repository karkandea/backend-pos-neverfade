using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using NeverfadePos.Api.Entities;

namespace NeverfadePos.Api.Data.Configurations;

public sealed class PaymentConfiguration : IEntityTypeConfiguration<Payment>
{
    public void Configure(EntityTypeBuilder<Payment> builder)
    {
        builder.ToTable(
            "payments",
            table => table.HasCheckConstraint(
                "CK_payments_Status",
                "\"Status\" IN ('creating', 'pending', 'paid', 'failed')"));

        builder.HasKey(x => x.Id);
        builder.HasIndex(x => x.TenantId);
        builder.HasIndex(x => x.TransactionId).IsUnique();
        builder.HasIndex(x => x.ProviderReferenceId).IsUnique();
        builder.HasIndex(x => x.ProviderPaymentRequestId).IsUnique();
        builder.HasIndex(x => x.ProviderPaymentId).IsUnique();
        builder.HasIndex(x => new { x.TenantId, x.Status, x.CreatedAt });

        builder.Property(x => x.Provider).HasMaxLength(30).IsRequired();
        builder.Property(x => x.ProviderReferenceId).HasMaxLength(255).IsRequired();
        builder.Property(x => x.ProviderPaymentRequestId).HasMaxLength(100);
        builder.Property(x => x.ProviderPaymentId).HasMaxLength(100);
        builder.Property(x => x.Method).HasMaxLength(30).IsRequired();
        builder.Property(x => x.Currency).HasMaxLength(3).IsRequired();
        builder.Property(x => x.Amount).HasPrecision(18, 2);
        builder.Property(x => x.Status).HasMaxLength(20).IsRequired();
        builder.Property(x => x.FailureCode).HasMaxLength(100);

        builder.HasOne(x => x.Tenant)
            .WithMany(x => x.Payments)
            .HasForeignKey(x => x.TenantId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.Transaction)
            .WithOne(x => x.Payment)
            .HasForeignKey<Payment>(x => x.TransactionId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
