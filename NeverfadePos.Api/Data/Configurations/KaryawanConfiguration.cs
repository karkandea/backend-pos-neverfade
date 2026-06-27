using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using NeverfadePos.Api.Entities;

namespace NeverfadePos.Api.Data.Configurations;

public class KaryawanConfiguration : IEntityTypeConfiguration<Karyawan>
{
    public void Configure(EntityTypeBuilder<Karyawan> builder)
    {
        builder.ToTable("karyawans");

        builder.HasKey(x => x.Id);

        builder.HasIndex(x => x.TenantId);

        builder.Property(x => x.Nama).HasMaxLength(200).IsRequired();
        builder.Property(x => x.Jabatan).HasMaxLength(100).IsRequired();
        builder.Property(x => x.Telepon).HasMaxLength(50);
        builder.Property(x => x.Email).HasMaxLength(200);
        builder.Property(x => x.Status).HasMaxLength(30);
        builder.Property(x => x.Catatan).HasMaxLength(1000);

        builder.Property(x => x.Gaji).HasPrecision(18, 2);

        builder.HasOne(x => x.Tenant)
            .WithMany(x => x.Karyawans)
            .HasForeignKey(x => x.TenantId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
