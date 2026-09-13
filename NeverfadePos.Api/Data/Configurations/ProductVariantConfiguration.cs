using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using NeverfadePos.Api.Entities;

namespace NeverfadePos.Api.Data.Configurations;

public sealed class ProductVariantConfiguration : IEntityTypeConfiguration<ProductVariant>
{
    public void Configure(EntityTypeBuilder<ProductVariant> builder)
    {
        builder.ToTable(
            "product_variants",
            table => table.HasCheckConstraint(
                "CK_product_variants_Stock",
                "\"Stok\" >= 0"));

        builder.HasKey(x => x.Id);
        builder.HasIndex(x => x.TenantId);
        builder.HasIndex(x => new { x.TenantId, x.ProductId });
        builder.HasIndex(x => new { x.TenantId, x.Sku }).IsUnique();
        builder.HasIndex(x => new { x.TenantId, x.Barcode })
            .IsUnique()
            .HasFilter("\"Barcode\" <> ''");

        builder.Property(x => x.Sku).HasMaxLength(100).IsRequired();
        builder.Property(x => x.Barcode).HasMaxLength(100);
        builder.Property(x => x.Label).HasMaxLength(200).IsRequired();
        builder.Property(x => x.Option1Name).HasMaxLength(50);
        builder.Property(x => x.Option1Value).HasMaxLength(100);
        builder.Property(x => x.Option2Name).HasMaxLength(50);
        builder.Property(x => x.Option2Value).HasMaxLength(100);
        builder.Property(x => x.Option3Name).HasMaxLength(50);
        builder.Property(x => x.Option3Value).HasMaxLength(100);
        builder.Property(x => x.HargaModal).HasPrecision(18, 2);
        builder.Property(x => x.HargaJual).HasPrecision(18, 2);

        builder.HasOne(x => x.Tenant)
            .WithMany(x => x.ProductVariants)
            .HasForeignKey(x => x.TenantId)
            .OnDelete(DeleteBehavior.Cascade);
        builder.HasOne(x => x.Product)
            .WithMany(x => x.Variants)
            .HasForeignKey(x => x.ProductId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
