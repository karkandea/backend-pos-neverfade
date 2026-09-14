using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using NeverfadePos.Api.Entities;

namespace NeverfadePos.Api.Data.Configurations;

public sealed class RetailReturnItemConfiguration : IEntityTypeConfiguration<RetailReturnItem>
{
    public void Configure(EntityTypeBuilder<RetailReturnItem> builder)
    {
        builder.ToTable(
            "retail_return_items",
            table => table.HasCheckConstraint(
                "CK_retail_return_items_Quantity",
                "\"Quantity\" > 0"));

        builder.HasKey(x => x.Id);
        builder.HasIndex(x => x.TenantId);
        builder.HasIndex(x => new { x.TenantId, x.RetailReturnId });
        builder.HasIndex(x => new { x.TenantId, x.TransactionItemId });

        builder.Property(x => x.ProductName).HasMaxLength(300).IsRequired();
        builder.Property(x => x.OriginalVariantSku).HasMaxLength(100);
        builder.Property(x => x.OriginalVariantLabel).HasMaxLength(200);
        builder.Property(x => x.OriginalUnitPrice).HasPrecision(18, 2);
        builder.Property(x => x.RefundAmount).HasPrecision(18, 2);
        builder.Property(x => x.ReplacementVariantSku).HasMaxLength(100);
        builder.Property(x => x.ReplacementVariantLabel).HasMaxLength(200);

        builder.HasOne(x => x.RetailReturn)
            .WithMany(x => x.Items)
            .HasForeignKey(x => x.RetailReturnId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(x => x.TransactionItem)
            .WithMany()
            .HasForeignKey(x => x.TransactionItemId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.Product)
            .WithMany()
            .HasForeignKey(x => x.ProductId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
