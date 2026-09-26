using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using NeverfadePos.Api.Entities;

namespace NeverfadePos.Api.Data.Configurations;

public sealed class SaleQuoteConfiguration : IEntityTypeConfiguration<SaleQuote>
{
    public void Configure(EntityTypeBuilder<SaleQuote> builder)
    {
        builder.ToTable("sale_quotes", t =>
        {
            t.HasCheckConstraint("CK_sale_quotes_Total", "\"Total\" >= 0");
            t.HasCheckConstraint("CK_sale_quotes_Status", "\"Status\" IN ('quoted', 'consumed')");
        });
        builder.HasKey(x => x.Id);
        builder.HasIndex(x => new { x.TenantId, x.OutletId, x.ExpiresAt });
        builder.HasIndex(x => new { x.TenantId, x.QuoteVersion }).IsUnique();
        builder.Property(x => x.SnapshotJson).HasColumnType("text").IsRequired();
        builder.Property(x => x.Status).HasMaxLength(20).IsRequired();
        builder.Property(x => x.IdempotencyKey).HasMaxLength(128);
        builder.Property(x => x.IdempotencyRequestHash).HasMaxLength(64);
        builder.HasIndex(x => new { x.TenantId, x.OutletId, x.IdempotencyKey }).IsUnique();
        builder.Property(x => x.Total).HasPrecision(18, 2);
    }
}
