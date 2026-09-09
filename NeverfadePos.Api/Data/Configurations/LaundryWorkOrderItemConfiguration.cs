using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using NeverfadePos.Api.Entities;

namespace NeverfadePos.Api.Data.Configurations;

public sealed class LaundryWorkOrderItemConfiguration
    : IEntityTypeConfiguration<LaundryWorkOrderItem>
{
    public void Configure(EntityTypeBuilder<LaundryWorkOrderItem> builder)
    {
        builder.ToTable(
            "laundry_work_order_items",
            table =>
            {
                table.HasCheckConstraint(
                    "CK_laundry_work_order_items_ProductType",
                    "\"ProductType\" IN ('goods', 'service')");
                table.HasCheckConstraint(
                    "CK_laundry_work_order_items_Quantity",
                    "\"Quantity\" > 0");
                table.HasCheckConstraint(
                    "CK_laundry_work_order_items_QuantityPrecision",
                    "\"QuantityPrecision\" BETWEEN 0 AND 3");
            });

        builder.HasKey(x => x.Id);
        builder.HasIndex(x => x.TenantId);
        builder.HasIndex(x => x.LaundryWorkOrderId);
        builder.HasIndex(x => x.ProductId);

        builder.Property(x => x.Nama).HasMaxLength(200).IsRequired();
        builder.Property(x => x.ProductType).HasMaxLength(20).IsRequired();
        builder.Property(x => x.Unit).HasMaxLength(50);
        builder.Property(x => x.Quantity).HasPrecision(18, 3);
        builder.Property(x => x.UnitPrice).HasPrecision(18, 2);
        builder.Property(x => x.Subtotal).HasPrecision(18, 2);

        builder.HasOne(x => x.Tenant)
            .WithMany()
            .HasForeignKey(x => x.TenantId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(x => x.LaundryWorkOrder)
            .WithMany(x => x.Items)
            .HasForeignKey(x => x.LaundryWorkOrderId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(x => x.Product)
            .WithMany()
            .HasForeignKey(x => x.ProductId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
