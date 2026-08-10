using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NeverfadePos.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddTenantControlPlane : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Status",
                table: "tenants",
                type: "character varying(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "active");

            migrationBuilder.AddColumn<DateTime>(
                name: "UpdatedAt",
                table: "tenants",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.Sql(
                "UPDATE tenants SET \"UpdatedAt\" = \"CreatedAt\";");

            migrationBuilder.AlterColumn<DateTime>(
                name: "UpdatedAt",
                table: "tenants",
                type: "timestamp with time zone",
                nullable: false,
                oldClrType: typeof(DateTime),
                oldType: "timestamp with time zone",
                oldNullable: true);

            migrationBuilder.CreateTable(
                name: "platform_audit_events",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ActorPlatformUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    EventType = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Metadata = table.Column<string>(type: "jsonb", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_platform_audit_events", x => x.Id);
                    table.CheckConstraint("CK_platform_audit_events_EventType", "\"EventType\" IN ('TENANT_PROVISIONED', 'TENANT_ACTIVATED', 'TENANT_SUSPENDED')");
                    table.ForeignKey(
                        name: "FK_platform_audit_events_platform_users_ActorPlatformUserId",
                        column: x => x.ActorPlatformUserId,
                        principalTable: "platform_users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_platform_audit_events_tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_tenants_Status",
                table: "tenants",
                column: "Status");

            migrationBuilder.AddCheckConstraint(
                name: "CK_tenants_Status",
                table: "tenants",
                sql: "\"Status\" IN ('active', 'suspended')");

            migrationBuilder.CreateIndex(
                name: "IX_platform_audit_events_ActorPlatformUserId_CreatedAt",
                table: "platform_audit_events",
                columns: new[] { "ActorPlatformUserId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_platform_audit_events_EventType_CreatedAt",
                table: "platform_audit_events",
                columns: new[] { "EventType", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_platform_audit_events_TenantId_CreatedAt",
                table: "platform_audit_events",
                columns: new[] { "TenantId", "CreatedAt" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "platform_audit_events");

            migrationBuilder.DropIndex(
                name: "IX_tenants_Status",
                table: "tenants");

            migrationBuilder.DropCheckConstraint(
                name: "CK_tenants_Status",
                table: "tenants");

            migrationBuilder.DropColumn(
                name: "Status",
                table: "tenants");

            migrationBuilder.DropColumn(
                name: "UpdatedAt",
                table: "tenants");
        }
    }
}
