using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using NeverfadePos.Api.Entities;

namespace NeverfadePos.Api.Data.Configurations;

public sealed class AttendancePolicyConfiguration : IEntityTypeConfiguration<AttendancePolicy>
{
    public void Configure(EntityTypeBuilder<AttendancePolicy> builder)
    {
        builder.ToTable("attendance_policies", table =>
        {
            table.HasCheckConstraint("CK_attendance_policies_GraceMinutes", "\"GraceMinutes\" BETWEEN 0 AND 180");
            table.HasCheckConstraint("CK_attendance_policies_AbsenceThresholdMinutes", "\"AbsenceThresholdMinutes\" BETWEEN 1 AND 720");
        });

        builder.HasKey(x => x.Id);
        builder.HasIndex(x => x.TenantId).IsUnique();

        builder.HasOne(x => x.Tenant)
            .WithMany(x => x.AttendancePolicies)
            .HasForeignKey(x => x.TenantId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
