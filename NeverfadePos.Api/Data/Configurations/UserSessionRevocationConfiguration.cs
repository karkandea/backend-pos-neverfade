using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using NeverfadePos.Api.Entities;

namespace NeverfadePos.Api.Data.Configurations;

public sealed class UserSessionRevocationConfiguration
    : IEntityTypeConfiguration<UserSessionRevocation>
{
    public void Configure(EntityTypeBuilder<UserSessionRevocation> builder)
    {
        builder.ToTable("user_session_revocations");
        builder.HasKey(x => x.Id);
        builder.HasIndex(x => new { x.TenantId, x.IdempotencyKey }).IsUnique();
        builder.HasIndex(x => new { x.TenantId, x.UserId, x.CreatedAt });
        builder.Property(x => x.IdempotencyKey).HasMaxLength(128).IsRequired();
        builder.HasOne<Tenant>().WithMany()
            .HasForeignKey(x => x.TenantId).OnDelete(DeleteBehavior.Cascade);
        // Preserve audit ledger even if its target user is later deleted.
    }
}
