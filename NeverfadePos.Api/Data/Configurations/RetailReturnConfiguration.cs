using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using NeverfadePos.Api.Entities;

namespace NeverfadePos.Api.Data.Configurations;

public sealed class RetailReturnConfiguration : IEntityTypeConfiguration<RetailReturn>
{
    public void Configure(EntityTypeBuilder<RetailReturn> builder)
    {
        builder.ToTable(
            "retail_returns",
            table => table.HasCheckConstraint(
                "CK_retail_returns_RefundAmount",
                "\"RefundAmount\" >= 0"));

        builder.HasKey(x => x.Id);
        builder.HasIndex(x => x.TenantId);
        builder.HasIndex(x => new { x.TenantId, x.ReturnNumber }).IsUnique();
        builder.HasIndex(x => new { x.TenantId, x.IdempotencyKey }).IsUnique();
        builder.HasIndex(x => new { x.TenantId, x.TransactionId });

        builder.Property(x => x.ReturnNumber).HasMaxLength(50).IsRequired();
        builder.Property(x => x.IdempotencyKey).HasMaxLength(100).IsRequired();
        builder.Property(x => x.TransactionNumber).HasMaxLength(100).IsRequired();
        builder.Property(x => x.Type).HasMaxLength(20).IsRequired();
        builder.Property(x => x.Reason).HasMaxLength(500).IsRequired();
        builder.Property(x => x.Notes).HasMaxLength(1000);
        builder.Property(x => x.RefundAmount).HasPrecision(18, 2);
        builder.Property(x => x.CreatedByName).HasMaxLength(200).IsRequired();

        builder.HasOne(x => x.Tenant)
            .WithMany()
            .HasForeignKey(x => x.TenantId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(x => x.Transaction)
            .WithMany()
            .HasForeignKey(x => x.TransactionId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
