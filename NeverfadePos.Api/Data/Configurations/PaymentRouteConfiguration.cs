using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using NeverfadePos.Api.Entities;

namespace NeverfadePos.Api.Data.Configurations;

public sealed class PaymentRouteConfiguration
    : IEntityTypeConfiguration<PaymentRoute>
{
    public void Configure(EntityTypeBuilder<PaymentRoute> builder)
    {
        builder.ToTable("payment_routes");

        builder.HasKey(x => x.Id);
        builder.HasIndex(x => x.PaymentId).IsUnique();
        builder.HasIndex(x => new { x.Provider, x.ProviderPaymentRequestId })
            .IsUnique();
        builder.HasIndex(x => x.TenantId);

        builder.Property(x => x.Provider).HasMaxLength(30).IsRequired();
        builder.Property(x => x.ProviderPaymentRequestId).HasMaxLength(100).IsRequired();

        builder.HasOne(x => x.Tenant)
            .WithMany(x => x.PaymentRoutes)
            .HasForeignKey(x => x.TenantId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.Payment)
            .WithOne()
            .HasForeignKey<PaymentRoute>(x => x.PaymentId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
