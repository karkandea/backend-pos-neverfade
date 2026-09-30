using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using NeverfadePos.Api.Entities;

namespace NeverfadePos.Api.Data.Configurations;

public sealed class StockBalanceConfiguration : IEntityTypeConfiguration<StockBalance>
{
    public void Configure(EntityTypeBuilder<StockBalance> builder)
    {
        builder.ToTable("stock_balances", table =>
        {
            table.HasCheckConstraint("CK_stock_balances_OnHand", "\"OnHand\" >= 0");
            table.HasCheckConstraint("CK_stock_balances_Reserved", "\"Reserved\" >= 0");
            table.HasCheckConstraint("CK_stock_balances_Quarantine", "\"Quarantine\" >= 0");
            table.HasCheckConstraint("CK_stock_balances_Available", "\"Available\" >= 0");
            table.HasCheckConstraint(
                "CK_stock_balances_AvailableFormula",
                "\"Available\" = \"OnHand\" - \"Reserved\" - \"Quarantine\"");
            table.HasCheckConstraint("CK_stock_balances_Version", "\"Version\" > 0");
        });

        builder.HasKey(x => x.Id);
        builder.HasIndex(x => new { x.TenantId, x.OutletId, x.ProductId });

        builder.HasIndex(x => new { x.TenantId, x.OutletId, x.ProductId })
            .IsUnique()
            .HasFilter("\"ProductVariantId\" IS NULL AND \"LotId\" IS NULL");
        builder.HasIndex(x => new { x.TenantId, x.OutletId, x.ProductId, x.ProductVariantId })
            .IsUnique()
            .HasFilter("\"ProductVariantId\" IS NOT NULL AND \"LotId\" IS NULL");
        builder.HasIndex(x => new
        {
            x.TenantId, x.OutletId, x.ProductId, x.ProductVariantId, x.LotId
        })
            .IsUnique()
            .HasFilter("\"LotId\" IS NOT NULL");

        builder.Property(x => x.OnHand).HasPrecision(20, 6);
        builder.Property(x => x.Reserved).HasPrecision(20, 6);
        builder.Property(x => x.Quarantine).HasPrecision(20, 6);
        builder.Property(x => x.Available).HasPrecision(20, 6);
        builder.Property(x => x.AverageCost).HasPrecision(20, 6);

        builder.HasOne(x => x.Outlet)
            .WithMany()
            .HasForeignKey(x => x.OutletId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.Product)
            .WithMany()
            .HasForeignKey(x => x.ProductId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.ProductVariant)
            .WithMany()
            .HasForeignKey(x => x.ProductVariantId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
