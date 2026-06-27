using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using NeverfadePos.Api.Entities;

namespace NeverfadePos.Api.Data.Configurations;

public class SettingsConfiguration : IEntityTypeConfiguration<Settings>
{
    public void Configure(EntityTypeBuilder<Settings> builder)
    {
        builder.ToTable("settings");

        builder.HasKey(x => x.Id);

        builder.HasIndex(x => x.TenantId)
            .IsUnique();

        builder.Property(x => x.NamaToko)
            .HasMaxLength(200)
            .IsRequired();

        builder.Property(x => x.Alamat)
            .HasMaxLength(500);

        builder.Property(x => x.Telepon)
            .HasMaxLength(50);

        builder.Property(x => x.Email)
            .HasMaxLength(200);

        builder.Property(x => x.Website)
            .HasMaxLength(200);

        builder.Property(x => x.HeaderStruk)
            .HasMaxLength(500);

        builder.Property(x => x.FooterStruk)
            .HasMaxLength(500);

        builder.Property(x => x.DefaultTax)
            .HasPrecision(18,2);

        builder.HasOne(x => x.Tenant)
            .WithMany(x => x.Settings)
            .HasForeignKey(x => x.TenantId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
