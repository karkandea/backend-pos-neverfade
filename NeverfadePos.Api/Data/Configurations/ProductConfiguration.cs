using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using NeverfadePos.Api.Entities;

namespace NeverfadePos.Api.Data.Configurations;

public class ProductConfiguration : IEntityTypeConfiguration<Product>
{
    public void Configure(EntityTypeBuilder<Product> builder)
    {
        builder.ToTable("products");

        builder.HasKey(x => x.Id);

        builder.HasIndex(x => x.TenantId);
        builder.HasIndex(x => new { x.TenantId, x.Kode }).IsUnique();

        builder.Property(x => x.Kode).HasMaxLength(50).IsRequired();
        builder.Property(x => x.Barcode).HasMaxLength(100);
        builder.Property(x => x.Nama).HasMaxLength(200).IsRequired();
        builder.Property(x => x.Kategori).HasMaxLength(100);
        builder.Property(x => x.Supplier).HasMaxLength(200);
        builder.Property(x => x.Satuan).HasMaxLength(50);
        builder.Property(x => x.Deskripsi).HasMaxLength(1000);

        builder.Property(x => x.HargaModal).HasPrecision(18, 2);
        builder.Property(x => x.HargaJual).HasPrecision(18, 2);

        builder.HasOne(x => x.Tenant)
            .WithMany(x => x.Products)
            .HasForeignKey(x => x.TenantId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
