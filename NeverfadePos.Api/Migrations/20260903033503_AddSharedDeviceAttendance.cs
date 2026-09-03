using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NeverfadePos.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddSharedDeviceAttendance : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_absensis_TenantId_KaryawanId_Tanggal",
                table: "absensis");

            migrationBuilder.AddColumn<string>(
                name: "PinFingerprint",
                table: "karyawans",
                type: "character varying(64)",
                maxLength: 64,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PinHash",
                table: "karyawans",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "PinUpdatedAt",
                table: "karyawans",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "UserId",
                table: "karyawans",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "CheckInAtUtc",
                table: "absensis",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "CheckOutAtUtc",
                table: "absensis",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "OutsideSchedule",
                table: "absensis",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.CreateTable(
                name: "attendance_corrections",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    AbsensiId = table.Column<Guid>(type: "uuid", nullable: false),
                    CorrectedByUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    CorrectedByUsername = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Reason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    BeforeData = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    AfterData = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_attendance_corrections", x => x.Id);
                    table.ForeignKey(
                        name: "FK_attendance_corrections_absensis_AbsensiId",
                        column: x => x.AbsensiId,
                        principalTable: "absensis",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_attendance_corrections_tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_attendance_corrections_users_CorrectedByUserId",
                        column: x => x.CorrectedByUserId,
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "attendance_policies",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    GraceMinutes = table.Column<int>(type: "integer", nullable: false),
                    AbsenceThresholdMinutes = table.Column<int>(type: "integer", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_attendance_policies", x => x.Id);
                    table.CheckConstraint("CK_attendance_policies_AbsenceThresholdMinutes", "\"AbsenceThresholdMinutes\" BETWEEN 1 AND 720");
                    table.CheckConstraint("CK_attendance_policies_GraceMinutes", "\"GraceMinutes\" BETWEEN 0 AND 180");
                    table.ForeignKey(
                        name: "FK_attendance_policies_tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "employee_schedule_exceptions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    KaryawanId = table.Column<Guid>(type: "uuid", nullable: false),
                    Date = table.Column<DateOnly>(type: "date", nullable: false),
                    Type = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    StartTime = table.Column<TimeOnly>(type: "time without time zone", nullable: true),
                    EndTime = table.Column<TimeOnly>(type: "time without time zone", nullable: true),
                    Note = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_employee_schedule_exceptions", x => x.Id);
                    table.CheckConstraint("CK_employee_schedule_exceptions_Type", "\"Type\" IN ('leave', 'holiday', 'changed_shift', 'off')");
                    table.ForeignKey(
                        name: "FK_employee_schedule_exceptions_karyawans_KaryawanId",
                        column: x => x.KaryawanId,
                        principalTable: "karyawans",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_employee_schedule_exceptions_tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "employee_weekly_schedules",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    KaryawanId = table.Column<Guid>(type: "uuid", nullable: false),
                    DayOfWeek = table.Column<int>(type: "integer", nullable: false),
                    IsWorkingDay = table.Column<bool>(type: "boolean", nullable: false),
                    StartTime = table.Column<TimeOnly>(type: "time without time zone", nullable: true),
                    EndTime = table.Column<TimeOnly>(type: "time without time zone", nullable: true),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_employee_weekly_schedules", x => x.Id);
                    table.CheckConstraint("CK_employee_weekly_schedules_DayOfWeek", "\"DayOfWeek\" BETWEEN 0 AND 6");
                    table.ForeignKey(
                        name: "FK_employee_weekly_schedules_karyawans_KaryawanId",
                        column: x => x.KaryawanId,
                        principalTable: "karyawans",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_employee_weekly_schedules_tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "shared_pos_devices",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    TokenHash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    Active = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedByUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    LastUsedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    FailedUnlockCount = table.Column<int>(type: "integer", nullable: false),
                    LockedUntilUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_shared_pos_devices", x => x.Id);
                    table.CheckConstraint("CK_shared_pos_devices_FailedUnlockCount", "\"FailedUnlockCount\" >= 0");
                    table.ForeignKey(
                        name: "FK_shared_pos_devices_tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_shared_pos_devices_users_CreatedByUserId",
                        column: x => x.CreatedByUserId,
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "tenant_audit_events",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ActorUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    ActorKaryawanId = table.Column<Guid>(type: "uuid", nullable: true),
                    EventType = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    Metadata = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_tenant_audit_events", x => x.Id);
                    table.ForeignKey(
                        name: "FK_tenant_audit_events_karyawans_ActorKaryawanId",
                        column: x => x.ActorKaryawanId,
                        principalTable: "karyawans",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_tenant_audit_events_tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_tenant_audit_events_users_ActorUserId",
                        column: x => x.ActorUserId,
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "shared_pos_sessions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    DeviceId = table.Column<Guid>(type: "uuid", nullable: false),
                    KaryawanId = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: true),
                    TokenHash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    ExpiresAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    RevokedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_shared_pos_sessions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_shared_pos_sessions_karyawans_KaryawanId",
                        column: x => x.KaryawanId,
                        principalTable: "karyawans",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_shared_pos_sessions_shared_pos_devices_DeviceId",
                        column: x => x.DeviceId,
                        principalTable: "shared_pos_devices",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_shared_pos_sessions_tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_shared_pos_sessions_users_UserId",
                        column: x => x.UserId,
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateIndex(
                name: "IX_karyawans_TenantId_PinFingerprint",
                table: "karyawans",
                columns: new[] { "TenantId", "PinFingerprint" },
                unique: true,
                filter: "\"PinFingerprint\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_karyawans_TenantId_UserId",
                table: "karyawans",
                columns: new[] { "TenantId", "UserId" },
                unique: true,
                filter: "\"UserId\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_karyawans_UserId",
                table: "karyawans",
                column: "UserId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_absensis_TenantId_KaryawanId_Tanggal",
                table: "absensis",
                columns: new[] { "TenantId", "KaryawanId", "Tanggal" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_attendance_corrections_AbsensiId",
                table: "attendance_corrections",
                column: "AbsensiId");

            migrationBuilder.CreateIndex(
                name: "IX_attendance_corrections_CorrectedByUserId",
                table: "attendance_corrections",
                column: "CorrectedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_attendance_corrections_TenantId_AbsensiId_CreatedAt",
                table: "attendance_corrections",
                columns: new[] { "TenantId", "AbsensiId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_attendance_policies_TenantId",
                table: "attendance_policies",
                column: "TenantId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_employee_schedule_exceptions_KaryawanId",
                table: "employee_schedule_exceptions",
                column: "KaryawanId");

            migrationBuilder.CreateIndex(
                name: "IX_employee_schedule_exceptions_TenantId_KaryawanId_Date",
                table: "employee_schedule_exceptions",
                columns: new[] { "TenantId", "KaryawanId", "Date" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_employee_weekly_schedules_KaryawanId",
                table: "employee_weekly_schedules",
                column: "KaryawanId");

            migrationBuilder.CreateIndex(
                name: "IX_employee_weekly_schedules_TenantId_KaryawanId_DayOfWeek",
                table: "employee_weekly_schedules",
                columns: new[] { "TenantId", "KaryawanId", "DayOfWeek" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_shared_pos_devices_CreatedByUserId",
                table: "shared_pos_devices",
                column: "CreatedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_shared_pos_devices_TenantId_TokenHash",
                table: "shared_pos_devices",
                columns: new[] { "TenantId", "TokenHash" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_shared_pos_sessions_DeviceId",
                table: "shared_pos_sessions",
                column: "DeviceId");

            migrationBuilder.CreateIndex(
                name: "IX_shared_pos_sessions_KaryawanId",
                table: "shared_pos_sessions",
                column: "KaryawanId");

            migrationBuilder.CreateIndex(
                name: "IX_shared_pos_sessions_TenantId_DeviceId_ExpiresAtUtc",
                table: "shared_pos_sessions",
                columns: new[] { "TenantId", "DeviceId", "ExpiresAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_shared_pos_sessions_TokenHash",
                table: "shared_pos_sessions",
                column: "TokenHash",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_shared_pos_sessions_UserId",
                table: "shared_pos_sessions",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_tenant_audit_events_ActorKaryawanId",
                table: "tenant_audit_events",
                column: "ActorKaryawanId");

            migrationBuilder.CreateIndex(
                name: "IX_tenant_audit_events_ActorUserId",
                table: "tenant_audit_events",
                column: "ActorUserId");

            migrationBuilder.CreateIndex(
                name: "IX_tenant_audit_events_TenantId_CreatedAt",
                table: "tenant_audit_events",
                columns: new[] { "TenantId", "CreatedAt" });

            migrationBuilder.AddForeignKey(
                name: "FK_karyawans_users_UserId",
                table: "karyawans",
                column: "UserId",
                principalTable: "users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_karyawans_users_UserId",
                table: "karyawans");

            migrationBuilder.DropTable(
                name: "attendance_corrections");

            migrationBuilder.DropTable(
                name: "attendance_policies");

            migrationBuilder.DropTable(
                name: "employee_schedule_exceptions");

            migrationBuilder.DropTable(
                name: "employee_weekly_schedules");

            migrationBuilder.DropTable(
                name: "shared_pos_sessions");

            migrationBuilder.DropTable(
                name: "tenant_audit_events");

            migrationBuilder.DropTable(
                name: "shared_pos_devices");

            migrationBuilder.DropIndex(
                name: "IX_karyawans_TenantId_PinFingerprint",
                table: "karyawans");

            migrationBuilder.DropIndex(
                name: "IX_karyawans_TenantId_UserId",
                table: "karyawans");

            migrationBuilder.DropIndex(
                name: "IX_karyawans_UserId",
                table: "karyawans");

            migrationBuilder.DropIndex(
                name: "IX_absensis_TenantId_KaryawanId_Tanggal",
                table: "absensis");

            migrationBuilder.DropColumn(
                name: "PinFingerprint",
                table: "karyawans");

            migrationBuilder.DropColumn(
                name: "PinHash",
                table: "karyawans");

            migrationBuilder.DropColumn(
                name: "PinUpdatedAt",
                table: "karyawans");

            migrationBuilder.DropColumn(
                name: "UserId",
                table: "karyawans");

            migrationBuilder.DropColumn(
                name: "CheckInAtUtc",
                table: "absensis");

            migrationBuilder.DropColumn(
                name: "CheckOutAtUtc",
                table: "absensis");

            migrationBuilder.DropColumn(
                name: "OutsideSchedule",
                table: "absensis");

            migrationBuilder.CreateIndex(
                name: "IX_absensis_TenantId_KaryawanId_Tanggal",
                table: "absensis",
                columns: new[] { "TenantId", "KaryawanId", "Tanggal" });
        }
    }
}
