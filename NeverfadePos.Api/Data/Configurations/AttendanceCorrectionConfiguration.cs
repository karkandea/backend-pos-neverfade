using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using NeverfadePos.Api.Entities;

namespace NeverfadePos.Api.Data.Configurations;

public sealed class AttendanceCorrectionConfiguration : IEntityTypeConfiguration<AttendanceCorrection>
{
    public void Configure(EntityTypeBuilder<AttendanceCorrection> builder)
    {
        builder.ToTable("attendance_corrections");
        builder.HasKey(x => x.Id);
        builder.HasIndex(x => new { x.TenantId, x.AbsensiId, x.CreatedAt });
        builder.Property(x => x.CorrectedByUsername).HasMaxLength(100).IsRequired();
        builder.Property(x => x.Reason).HasMaxLength(500).IsRequired();
        builder.Property(x => x.BeforeData).HasMaxLength(2000).IsRequired();
        builder.Property(x => x.AfterData).HasMaxLength(2000).IsRequired();

        builder.HasOne(x => x.Tenant)
            .WithMany(x => x.AttendanceCorrections)
            .HasForeignKey(x => x.TenantId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(x => x.Absensi)
            .WithMany(x => x.Corrections)
            .HasForeignKey(x => x.AbsensiId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(x => x.CorrectedByUser)
            .WithMany()
            .HasForeignKey(x => x.CorrectedByUserId)
            .OnDelete(DeleteBehavior.SetNull);
    }
}
