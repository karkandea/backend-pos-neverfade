using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using NeverfadePos.Api.Entities;

namespace NeverfadePos.Api.Data.Configurations;

public sealed class EmployeeScheduleExceptionConfiguration : IEntityTypeConfiguration<EmployeeScheduleException>
{
    public void Configure(EntityTypeBuilder<EmployeeScheduleException> builder)
    {
        builder.ToTable("employee_schedule_exceptions", table =>
            table.HasCheckConstraint(
                "CK_employee_schedule_exceptions_Type",
                "\"Type\" IN ('leave', 'holiday', 'changed_shift', 'off')"));

        builder.HasKey(x => x.Id);
        builder.HasIndex(x => new { x.TenantId, x.KaryawanId, x.Date }).IsUnique();
        builder.Property(x => x.Type).HasMaxLength(30).IsRequired();
        builder.Property(x => x.Note).HasMaxLength(500);

        builder.HasOne(x => x.Tenant)
            .WithMany(x => x.EmployeeScheduleExceptions)
            .HasForeignKey(x => x.TenantId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(x => x.Karyawan)
            .WithMany(x => x.ScheduleExceptions)
            .HasForeignKey(x => x.KaryawanId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
