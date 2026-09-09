using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using NeverfadePos.Api.Entities;

namespace NeverfadePos.Api.Data.Configurations;

public class TransactionItemConfiguration : IEntityTypeConfiguration<TransactionItem>
{
    public void Configure(EntityTypeBuilder<TransactionItem> builder)
    {
        builder.ToTable(
            "transaction_items",
            table =>
            {
                table.HasCheckConstraint(
                    "CK_transaction_items_ProductType",
                    "\"ProductType\" IN ('goods', 'service')");
                table.HasCheckConstraint(
                    "CK_transaction_items_Quantity",
                    "\"Quantity\" > 0");
                table.HasCheckConstraint(
                    "CK_transaction_items_QuantityPrecision",
                    "\"QuantityPrecision\" BETWEEN 0 AND 3");
            });

        builder.HasKey(x => x.Id);

        builder.HasIndex(x => x.TenantId);

        builder.Property(x => x.Nama)
            .HasMaxLength(200)
            .IsRequired();

        builder.Property(x => x.HargaJual)
            .HasPrecision(18,2);

        builder.Property(x => x.Quantity)
            .HasPrecision(18,3);

        builder.Property(x => x.ProductType)
            .HasMaxLength(20)
            .IsRequired();

        builder.Property(x => x.Unit)
            .HasMaxLength(50);

        builder.Property(x => x.Subtotal)
            .HasPrecision(18,2);

        builder.HasOne(x => x.Tenant)
            .WithMany(x => x.TransactionItems)
            .HasForeignKey(x => x.TenantId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(x => x.Transaction)
            .WithMany(x => x.Items)
            .HasForeignKey(x => x.TransactionId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(x => x.Product)
            .WithMany(x => x.TransactionItems)
            .HasForeignKey(x => x.ProductId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
