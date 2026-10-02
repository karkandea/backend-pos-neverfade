using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using NeverfadePos.Api.Entities;

namespace NeverfadePos.Api.Data.Configurations;

public sealed class OpsAlertConfiguration : IEntityTypeConfiguration<OpsAlert>
{
    public void Configure(EntityTypeBuilder<OpsAlert> builder)
    {
        builder.ToTable("ops_alerts");
        builder.HasKey(x => x.Id);
        builder.HasIndex(x => new { x.TenantId, x.SourceKind, x.SourceId })
            .IsUnique();
        builder.HasIndex(x => new { x.TenantId, x.CreatedAt });
        builder.Property(x => x.SourceKind).HasMaxLength(48).IsRequired();
        builder.Property(x => x.State).HasMaxLength(24).IsRequired();
        builder.Property(x => x.CorrelationId).HasMaxLength(128).IsRequired();
        builder.HasOne<Tenant>().WithMany()
            .HasForeignKey(x => x.TenantId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
