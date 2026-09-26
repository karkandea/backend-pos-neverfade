using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using NeverfadePos.Api.Entities;

namespace NeverfadePos.Api.Data.Configurations;

public sealed class UserOutletAssignmentConfiguration : IEntityTypeConfiguration<UserOutletAssignment>
{
    public void Configure(EntityTypeBuilder<UserOutletAssignment> builder)
    {
        builder.ToTable("user_outlet_assignments");
        builder.HasKey(x => x.Id);
        builder.HasIndex(x => new { x.TenantId, x.UserId, x.OutletId }).IsUnique();
        builder.HasIndex(x => new { x.TenantId, x.OutletId });
        builder.HasOne(x => x.User).WithMany(x => x.OutletAssignments)
            .HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Cascade);
        builder.HasOne(x => x.Outlet).WithMany(x => x.UserAssignments)
            .HasForeignKey(x => x.OutletId).OnDelete(DeleteBehavior.Cascade);
    }
}
