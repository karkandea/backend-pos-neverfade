using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using NeverfadePos.Api.Entities;

namespace NeverfadePos.Api.Data.Configurations;

public sealed class TenantAuditEventConfiguration : IEntityTypeConfiguration<TenantAuditEvent>
{
    public void Configure(EntityTypeBuilder<TenantAuditEvent> builder)
    {
        builder.ToTable("tenant_audit_events");
        builder.HasKey(x => x.Id);
        builder.HasIndex(x => new { x.TenantId, x.EventType, x.CreatedAt });
        builder.Property(x => x.EventType).HasMaxLength(100).IsRequired();
        builder.Property(x => x.Metadata).HasMaxLength(2000);
        builder.HasOne(x => x.Tenant)
            .WithMany(x => x.TenantAuditEvents)
            .HasForeignKey(x => x.TenantId)
            .OnDelete(DeleteBehavior.Cascade);
        builder.HasOne(x => x.ActorUser)
            .WithMany()
            .HasForeignKey(x => x.ActorUserId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.ActorKaryawan)
            .WithMany()
            .HasForeignKey(x => x.ActorKaryawanId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
