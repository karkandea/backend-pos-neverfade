using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using NeverfadePos.Api.Entities;

namespace NeverfadePos.Api.Data.Configurations;

public sealed class SharedPosDeviceConfiguration : IEntityTypeConfiguration<SharedPosDevice>
{
    public void Configure(EntityTypeBuilder<SharedPosDevice> builder)
    {
        builder.ToTable("shared_pos_devices", table =>
            table.HasCheckConstraint(
                "CK_shared_pos_devices_FailedUnlockCount",
                "\"FailedUnlockCount\" >= 0"));

        builder.HasKey(x => x.Id);
        builder.HasIndex(x => new { x.TenantId, x.TokenHash }).IsUnique();
        builder.Property(x => x.Name).HasMaxLength(100).IsRequired();
        builder.Property(x => x.TokenHash).HasMaxLength(64).IsRequired();

        builder.HasOne(x => x.Tenant)
            .WithMany(x => x.SharedPosDevices)
            .HasForeignKey(x => x.TenantId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(x => x.CreatedByUser)
            .WithMany()
            .HasForeignKey(x => x.CreatedByUserId)
            .OnDelete(DeleteBehavior.SetNull);
    }
}
