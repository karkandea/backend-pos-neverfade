using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using NeverfadePos.Api.Entities;

namespace NeverfadePos.Api.Data.Configurations;

public sealed class WithdrawalRouteConfiguration
    : IEntityTypeConfiguration<WithdrawalRoute>
{
    public void Configure(EntityTypeBuilder<WithdrawalRoute> builder)
    {
        builder.ToTable("withdrawal_routes");

        builder.HasKey(x => x.Id);
        builder.HasIndex(x => x.TenantId);
        builder.HasIndex(x => x.WithdrawalRequestId).IsUnique();
        builder.HasIndex(x => new { x.TenantId, x.CreatedAt });

        builder.HasOne(x => x.Tenant)
            .WithMany(x => x.WithdrawalRoutes)
            .HasForeignKey(x => x.TenantId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.WithdrawalRequest)
            .WithOne()
            .HasForeignKey<WithdrawalRoute>(x => x.WithdrawalRequestId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
