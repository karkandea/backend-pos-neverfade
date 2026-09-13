using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using NeverfadePos.Api.Entities;

namespace NeverfadePos.Api.Data.Configurations;

public sealed class ProductPriceConfiguration : IEntityTypeConfiguration<ProductPrice>
{
    public void Configure(EntityTypeBuilder<ProductPrice> builder)
    {
        builder.ToTable(
            "product_prices",
            table =>
            {
                table.HasCheckConstraint("CK_product_prices_MinQuantity", "\"MinQuantity\" > 0");
                table.HasCheckConstraint("CK_product_prices_UnitPrice", "\"UnitPrice\" >= 0");
            });
        builder.HasKey(x => x.Id);
        builder.HasIndex(x => x.TenantId);
        builder.HasIndex(x => new { x.TenantId, x.ProductId });
        builder.HasIndex(x => new { x.TenantId, x.ProductId, x.PriceLevelId })
            .IsUnique()
            .HasFilter("\"ProductVariantId\" IS NULL");
        builder.HasIndex(x => new { x.TenantId, x.ProductVariantId, x.PriceLevelId })
            .IsUnique()
            .HasFilter("\"ProductVariantId\" IS NOT NULL");
        builder.Property(x => x.MinQuantity).HasPrecision(18, 3);
        builder.Property(x => x.UnitPrice).HasPrecision(18, 2);

        builder.HasOne(x => x.Tenant)
            .WithMany(x => x.ProductPrices)
            .HasForeignKey(x => x.TenantId)
            .OnDelete(DeleteBehavior.Cascade);
        builder.HasOne(x => x.Product)
            .WithMany(x => x.Prices)
            .HasForeignKey(x => x.ProductId)
            .OnDelete(DeleteBehavior.Cascade);
        builder.HasOne(x => x.ProductVariant)
            .WithMany(x => x.Prices)
            .HasForeignKey(x => x.ProductVariantId)
            .OnDelete(DeleteBehavior.Cascade);
        builder.HasOne(x => x.PriceLevel)
            .WithMany(x => x.Prices)
            .HasForeignKey(x => x.PriceLevelId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
