using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using NeverfadePos.Api.Entities;

namespace NeverfadePos.Api.Data.Configurations;

public sealed class RestaurantOrderItemConfiguration
    : IEntityTypeConfiguration<RestaurantOrderItem>
{
    public void Configure(EntityTypeBuilder<RestaurantOrderItem> builder)
    {
        builder.ToTable(
            "restaurant_order_items",
            table =>
            {
                table.HasCheckConstraint(
                    "CK_restaurant_order_items_Qty",
                    "\"Qty\" > 0");
                table.HasCheckConstraint(
                    "CK_restaurant_order_items_HargaJual",
                    "\"HargaJual\" >= 0");
                table.HasCheckConstraint(
                    "CK_restaurant_order_items_KitchenStatus",
                    "\"KitchenStatus\" IN ('draft', 'queued', 'preparing', 'ready', 'served', 'cancelled')");
            });

        builder.HasKey(x => x.Id);
        builder.HasIndex(x => new { x.TenantId, x.RestaurantOrderId });
        builder.HasIndex(x => new { x.TenantId, x.KitchenStatus, x.QueuedAt });

        builder.Property(x => x.Nama).HasMaxLength(200).IsRequired();
        builder.Property(x => x.HargaJual).HasPrecision(18, 2);
        builder.Property(x => x.Note).HasMaxLength(500);
        builder.Property(x => x.KitchenStatus).HasMaxLength(20).IsRequired();

        builder.HasOne(x => x.Tenant)
            .WithMany(x => x.RestaurantOrderItems)
            .HasForeignKey(x => x.TenantId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(x => x.RestaurantOrder)
            .WithMany(x => x.Items)
            .HasForeignKey(x => x.RestaurantOrderId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(x => x.Product)
            .WithMany()
            .HasForeignKey(x => x.ProductId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
