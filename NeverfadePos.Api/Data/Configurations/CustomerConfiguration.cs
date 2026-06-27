using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using NeverfadePos.Api.Entities;

namespace NeverfadePos.Api.Data.Configurations;

public class CustomerConfiguration : IEntityTypeConfiguration<Customer>
{
    public void Configure(EntityTypeBuilder<Customer> builder)
    {
        builder.ToTable("customers");

        builder.HasKey(x => x.Id);

        builder.HasIndex(x => x.TenantId);

        builder.Property(x => x.Nama).HasMaxLength(200).IsRequired();
        builder.Property(x => x.Hp).HasMaxLength(50).IsRequired();
        builder.Property(x => x.Email).HasMaxLength(200);
        builder.Property(x => x.Alamat).HasMaxLength(500);

        builder.HasOne(x => x.Tenant)
            .WithMany(x => x.Customers)
            .HasForeignKey(x => x.TenantId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
