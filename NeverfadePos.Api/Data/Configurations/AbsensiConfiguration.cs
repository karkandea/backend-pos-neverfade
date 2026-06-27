using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using NeverfadePos.Api.Entities;

namespace NeverfadePos.Api.Data.Configurations;

public class AbsensiConfiguration : IEntityTypeConfiguration<Absensi>
{
    public void Configure(EntityTypeBuilder<Absensi> builder)
    {
        builder.ToTable("absensis");

        builder.HasKey(x => x.Id);

        builder.HasIndex(x => x.TenantId);
        builder.HasIndex(x => new { x.TenantId, x.KaryawanId, x.Tanggal });

        builder.HasOne(x => x.Tenant)
            .WithMany(x => x.Absensis)
            .HasForeignKey(x => x.TenantId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(x => x.Karyawan)
            .WithMany(x => x.Absensis)
            .HasForeignKey(x => x.KaryawanId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
