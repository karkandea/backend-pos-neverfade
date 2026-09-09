using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using NeverfadePos.Api.Entities;

namespace NeverfadePos.Api.Data.Configurations;

public sealed class LaundryWorkOrderStatusHistoryConfiguration
    : IEntityTypeConfiguration<LaundryWorkOrderStatusHistory>
{
    public void Configure(EntityTypeBuilder<LaundryWorkOrderStatusHistory> builder)
    {
        builder.ToTable("laundry_work_order_status_history");

        builder.HasKey(x => x.Id);
        builder.HasIndex(x => x.TenantId);
        builder.HasIndex(x => new { x.LaundryWorkOrderId, x.CreatedAt });

        builder.Property(x => x.FromStatus).HasMaxLength(20);
        builder.Property(x => x.ToStatus).HasMaxLength(20).IsRequired();
        builder.Property(x => x.ActorName).HasMaxLength(200).IsRequired();
        builder.Property(x => x.Reason).HasMaxLength(500);

        builder.HasOne(x => x.Tenant)
            .WithMany()
            .HasForeignKey(x => x.TenantId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(x => x.LaundryWorkOrder)
            .WithMany(x => x.StatusHistory)
            .HasForeignKey(x => x.LaundryWorkOrderId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(x => x.ActorUser)
            .WithMany()
            .HasForeignKey(x => x.ActorUserId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
