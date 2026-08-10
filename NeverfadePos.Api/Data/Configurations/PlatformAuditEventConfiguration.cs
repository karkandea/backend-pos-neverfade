using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using NeverfadePos.Api.Entities;

namespace NeverfadePos.Api.Data.Configurations;

public sealed class PlatformAuditEventConfiguration
    : IEntityTypeConfiguration<PlatformAuditEvent>
{
    public void Configure(
        EntityTypeBuilder<PlatformAuditEvent> builder)
    {
        builder.ToTable(
            "platform_audit_events",
            table => table.HasCheckConstraint(
                "CK_platform_audit_events_EventType",
                "\"EventType\" IN ('TENANT_PROVISIONED', 'TENANT_ACTIVATED', 'TENANT_SUSPENDED')"));

        builder.HasKey(x => x.Id);

        builder.Property(x => x.EventType)
            .HasMaxLength(50)
            .IsRequired();

        builder.Property(x => x.Metadata)
            .HasColumnType("jsonb");

        builder.HasIndex(x => new
        {
            x.TenantId,
            x.CreatedAt
        });

        builder.HasIndex(x => new
        {
            x.ActorPlatformUserId,
            x.CreatedAt
        });

        builder.HasIndex(x => new
        {
            x.EventType,
            x.CreatedAt
        });

        builder.HasOne(x => x.ActorPlatformUser)
            .WithMany(x => x.AuditEvents)
            .HasForeignKey(x => x.ActorPlatformUserId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.Tenant)
            .WithMany(x => x.PlatformAuditEvents)
            .HasForeignKey(x => x.TenantId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
