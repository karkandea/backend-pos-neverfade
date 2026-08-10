using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using NeverfadePos.Api.Entities;

namespace NeverfadePos.Api.Data.Configurations;

public class TenantConfiguration : IEntityTypeConfiguration<Tenant>
{
    public void Configure(EntityTypeBuilder<Tenant> builder)
    {
        builder.ToTable(
            "tenants",
            table =>
                table.HasCheckConstraint(
                    "CK_tenants_Status",
                    "\"Status\" IN ('active', 'suspended')"));

        builder.HasKey(x => x.Id);

        builder.Property(x => x.NamaToko)
            .HasMaxLength(200)
            .IsRequired();

        builder.Property(x => x.Slug)
            .HasMaxLength(100)
            .IsRequired();

        builder.HasIndex(x => x.Slug)
            .IsUnique();

        builder.Property(x => x.Status)
            .HasMaxLength(20)
            .HasDefaultValue("active")
            .IsRequired();

        builder.HasIndex(x => x.Status);
    }
}
