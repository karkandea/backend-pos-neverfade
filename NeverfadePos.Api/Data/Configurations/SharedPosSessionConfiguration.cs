using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using NeverfadePos.Api.Entities;

namespace NeverfadePos.Api.Data.Configurations;

public sealed class SharedPosSessionConfiguration : IEntityTypeConfiguration<SharedPosSession>
{
    public void Configure(EntityTypeBuilder<SharedPosSession> builder)
    {
        builder.ToTable("shared_pos_sessions");
        builder.HasKey(x => x.Id);
        builder.HasIndex(x => x.TokenHash).IsUnique();
        builder.HasIndex(x => new { x.TenantId, x.DeviceId, x.ExpiresAtUtc });
        builder.Property(x => x.TokenHash).HasMaxLength(64).IsRequired();

        builder.HasOne(x => x.Tenant)
            .WithMany(x => x.SharedPosSessions)
            .HasForeignKey(x => x.TenantId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(x => x.Device)
            .WithMany(x => x.Sessions)
            .HasForeignKey(x => x.DeviceId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(x => x.Karyawan)
            .WithMany(x => x.SharedPosSessions)
            .HasForeignKey(x => x.KaryawanId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(x => x.User)
            .WithMany()
            .HasForeignKey(x => x.UserId)
            .OnDelete(DeleteBehavior.SetNull);
    }
}
