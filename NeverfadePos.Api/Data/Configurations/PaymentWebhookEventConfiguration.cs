using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using NeverfadePos.Api.Entities;

namespace NeverfadePos.Api.Data.Configurations;

public sealed class PaymentWebhookEventConfiguration
    : IEntityTypeConfiguration<PaymentWebhookEvent>
{
    public void Configure(EntityTypeBuilder<PaymentWebhookEvent> builder)
    {
        builder.ToTable("payment_webhook_events");

        builder.HasKey(x => x.Id);
        builder.HasIndex(x => x.TenantId);
        builder.HasIndex(x => x.ProviderEventKey).IsUnique();
        builder.HasIndex(x => new { x.TenantId, x.CreatedAt });

        builder.Property(x => x.ProviderEventKey).HasMaxLength(255).IsRequired();
        builder.Property(x => x.EventType).HasMaxLength(100).IsRequired();
        builder.Property(x => x.ProviderPaymentId).HasMaxLength(100).IsRequired();
        builder.Property(x => x.ProcessingStatus).HasMaxLength(30).IsRequired();

        builder.HasOne(x => x.Tenant)
            .WithMany(x => x.PaymentWebhookEvents)
            .HasForeignKey(x => x.TenantId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.Payment)
            .WithMany(x => x.WebhookEvents)
            .HasForeignKey(x => x.PaymentId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
