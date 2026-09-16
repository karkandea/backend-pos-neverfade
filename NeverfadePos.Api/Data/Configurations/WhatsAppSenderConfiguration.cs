using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using NeverfadePos.Api.Entities;

namespace NeverfadePos.Api.Data.Configurations;

public sealed class WhatsAppSenderConfiguration : IEntityTypeConfiguration<WhatsAppSender>
{
    public void Configure(EntityTypeBuilder<WhatsAppSender> builder)
    {
        builder.ToTable("whatsapp_senders");

        builder.HasKey(x => x.Id);

        builder.HasIndex(x => x.TenantId);
        builder.HasIndex(x => x.OutletId);
        builder.HasIndex(x => x.SessionName).IsUnique();
        builder.HasIndex(x => new { x.TenantId, x.OutletId, x.IsDefault })
            .IsUnique()
            .HasFilter("\"IsDefault\" = TRUE AND \"Active\" = TRUE");

        builder.Property(x => x.Provider).HasMaxLength(30).IsRequired();
        builder.Property(x => x.SessionName).HasMaxLength(220).IsRequired();
        builder.Property(x => x.DisplayName).HasMaxLength(120);
        builder.Property(x => x.PhoneNumber).HasMaxLength(50);
        builder.Property(x => x.LastStatus).HasMaxLength(40).IsRequired();
        builder.Property(x => x.Active).HasDefaultValue(true);

        builder.HasOne(x => x.Tenant)
            .WithMany(x => x.WhatsAppSenders)
            .HasForeignKey(x => x.TenantId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(x => x.Outlet)
            .WithMany(x => x.WhatsAppSenders)
            .HasForeignKey(x => x.OutletId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
