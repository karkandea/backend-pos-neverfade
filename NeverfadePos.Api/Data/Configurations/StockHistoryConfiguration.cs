using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using NeverfadePos.Api.Entities;

namespace NeverfadePos.Api.Data.Configurations;

public class StockHistoryConfiguration : IEntityTypeConfiguration<StockHistory>
{
    public void Configure(EntityTypeBuilder<StockHistory> builder)
    {
        builder.ToTable("stock_histories");

        builder.HasKey(x => x.Id);

        builder.HasIndex(x => x.TenantId);
        builder.HasIndex(x => new { x.TenantId, x.ProdukId });

        builder.Property(x => x.ProdukNama)
            .HasMaxLength(200)
            .IsRequired();

        builder.Property(x => x.Tipe)
            .HasMaxLength(50)
            .IsRequired();

        builder.Property(x => x.Keterangan)
            .HasMaxLength(500);

        builder.Property(x => x.User)
            .HasMaxLength(100);

        builder.HasOne(x => x.Tenant)
            .WithMany(x => x.StockHistories)
            .HasForeignKey(x => x.TenantId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(x => x.Product)
            .WithMany(x => x.StockHistories)
            .HasForeignKey(x => x.ProdukId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
