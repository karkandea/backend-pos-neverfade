using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using NeverfadePos.Api.Entities;

namespace NeverfadePos.Api.Data.Configurations;

public sealed class JobConfiguration : IEntityTypeConfiguration<Job>
{
    public void Configure(EntityTypeBuilder<Job> builder)
    {
        builder.ToTable("jobs", table =>
        {
            table.HasCheckConstraint(
                "CK_jobs_State",
                "\"State\" IN ('queued', 'running', 'succeeded', 'failed')");
        });

        builder.HasKey(x => x.Id);
        builder.HasIndex(x => new { x.TenantId, x.CreatedAt, x.Id });
        builder.HasIndex(x => new { x.TenantId, x.OutletId, x.CreatedAt, x.Id });
        builder.HasIndex(x => new { x.TenantId, x.ActorUserId, x.CreatedAt, x.Id });

        builder.Property(x => x.Kind).HasMaxLength(100).IsRequired();
        builder.Property(x => x.State).HasMaxLength(20).IsRequired();
        builder.Property(x => x.CorrelationId).HasMaxLength(128).IsRequired();

        builder.HasOne<Tenant>()
            .WithMany()
            .HasForeignKey(x => x.TenantId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne<Outlet>()
            .WithMany()
            .HasForeignKey(x => x.OutletId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<User>()
            .WithMany()
            .HasForeignKey(x => x.ActorUserId)
            .OnDelete(DeleteBehavior.SetNull);
    }
}
