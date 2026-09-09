using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using NeverfadePos.Api.Entities;

namespace NeverfadePos.Api.Data.Configurations;

public sealed class LaundryWorkOrderConfiguration
    : IEntityTypeConfiguration<LaundryWorkOrder>
{
    public void Configure(EntityTypeBuilder<LaundryWorkOrder> builder)
    {
        builder.ToTable(
            "laundry_work_orders",
            table =>
            {
                table.HasCheckConstraint(
                    "CK_laundry_work_orders_Status",
                    "\"Status\" IN ('received', 'in_progress', 'ready', 'completed', 'cancelled')");
                table.HasCheckConstraint(
                    "CK_laundry_work_orders_PaymentStatus",
                    "\"PaymentStatus\" IN ('unpaid', 'paid')");
            });

        builder.HasKey(x => x.Id);
        builder.HasIndex(x => new { x.TenantId, x.OrderNumber }).IsUnique();
        builder.HasIndex(x => new { x.TenantId, x.Status, x.CreatedAt });
        builder.HasIndex(x => x.CustomerId);
        builder.HasIndex(x => x.TransactionId)
            .IsUnique()
            .HasFilter("\"TransactionId\" IS NOT NULL");

        builder.Property(x => x.OrderNumber).HasMaxLength(50).IsRequired();
        builder.Property(x => x.Status).HasMaxLength(20).IsRequired();
        builder.Property(x => x.PaymentStatus).HasMaxLength(20).IsRequired();
        builder.Property(x => x.Notes).HasMaxLength(1000);
        builder.Property(x => x.CancellationReason).HasMaxLength(500);

        builder.HasOne(x => x.Tenant)
            .WithMany()
            .HasForeignKey(x => x.TenantId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(x => x.Customer)
            .WithMany()
            .HasForeignKey(x => x.CustomerId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.CreatedByUser)
            .WithMany()
            .HasForeignKey(x => x.CreatedByUserId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.Transaction)
            .WithMany()
            .HasForeignKey(x => x.TransactionId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
