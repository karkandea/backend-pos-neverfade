using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using NeverfadePos.Api.Entities;

namespace NeverfadePos.Api.Data.Configurations;

public sealed class PlatformProvisioningRequestConfiguration
    : IEntityTypeConfiguration<PlatformProvisioningRequest>
{
    public void Configure(EntityTypeBuilder<PlatformProvisioningRequest> builder)
    {
        builder.ToTable("platform_provisioning_requests");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Key).HasMaxLength(128).IsRequired();
        builder.Property(x => x.RequestHash).HasMaxLength(64).IsRequired();
        builder.HasIndex(x => new { x.ActorPlatformUserId, x.Key }).IsUnique();
        builder.HasOne<PlatformUser>().WithMany()
            .HasForeignKey(x => x.ActorPlatformUserId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Tenant>().WithMany()
            .HasForeignKey(x => x.TenantId).OnDelete(DeleteBehavior.Restrict);
    }
}
