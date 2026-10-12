using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using NeverfadePos.Api.Entities;

namespace NeverfadePos.Api.Data.Configurations;

public sealed class MfaCredentialConfiguration : IEntityTypeConfiguration<MfaCredential>
{
    public void Configure(EntityTypeBuilder<MfaCredential> builder)
    {
        builder.ToTable("mfa_credentials");
        builder.HasKey(x => x.Id);
        builder.HasIndex(x => new { x.TenantId, x.UserId }).IsUnique();
        builder.Property(x => x.ProtectedSecret).HasMaxLength(512).IsRequired();
        builder.Property(x => x.RecoveryCodeHashesJson).HasColumnType("text").IsConcurrencyToken();
        builder.Property(x => x.LastUsedStep).IsConcurrencyToken();
        builder.Property(x => x.FailedAttempts).IsConcurrencyToken();
        builder.Property(x => x.EnabledAtUtc).IsConcurrencyToken();
        builder.HasOne<User>().WithMany()
            .HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Cascade);
    }
}
