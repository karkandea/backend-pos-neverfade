using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using NeverfadePos.Api.Entities;

namespace NeverfadePos.Api.Data.Configurations;

public sealed class PlatformUserConfiguration
    : IEntityTypeConfiguration<PlatformUser>
{
    public void Configure(
        EntityTypeBuilder<PlatformUser> builder)
    {
        builder.ToTable(
            "platform_users",
            table =>
                table.HasCheckConstraint(
                    "CK_platform_users_Role",
                    "\"Role\" = 'superadmin'"));

        builder.HasKey(x => x.Id);

        builder.HasIndex(x => x.Username)
            .IsUnique();

        builder.Property(x => x.Nama)
            .HasMaxLength(200)
            .IsRequired();

        builder.Property(x => x.Username)
            .HasMaxLength(100)
            .IsRequired();

        builder.Property(x => x.PasswordHash)
            .IsRequired();

        builder.Property(x => x.Role)
            .HasMaxLength(20)
            .IsRequired();
    }
}
