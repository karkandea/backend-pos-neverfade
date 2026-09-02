using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using NeverfadePos.Api.Entities;

namespace NeverfadePos.Api.Data.Configurations;

public sealed class EmployeeWeeklyScheduleConfiguration : IEntityTypeConfiguration<EmployeeWeeklySchedule>
{
    public void Configure(EntityTypeBuilder<EmployeeWeeklySchedule> builder)
    {
        builder.ToTable("employee_weekly_schedules", table =>
            table.HasCheckConstraint("CK_employee_weekly_schedules_DayOfWeek", "\"DayOfWeek\" BETWEEN 0 AND 6"));
        builder.HasKey(x => x.Id);
        builder.HasIndex(x => new { x.TenantId, x.KaryawanId, x.DayOfWeek }).IsUnique();
        builder.HasOne(x => x.Tenant)
            .WithMany(x => x.EmployeeWeeklySchedules)
            .HasForeignKey(x => x.TenantId)
            .OnDelete(DeleteBehavior.Cascade);
        builder.HasOne(x => x.Karyawan)
            .WithMany(x => x.WeeklySchedules)
            .HasForeignKey(x => x.KaryawanId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
