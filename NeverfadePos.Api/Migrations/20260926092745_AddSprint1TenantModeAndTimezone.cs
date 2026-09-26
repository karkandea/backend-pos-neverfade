using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NeverfadePos.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddSprint1TenantModeAndTimezone : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Mode",
                table: "tenants",
                type: "character varying(10)",
                maxLength: 10,
                nullable: false,
                defaultValue: "live");

            migrationBuilder.AddColumn<string>(
                name: "TimeZoneId",
                table: "tenants",
                type: "character varying(64)",
                maxLength: 64,
                nullable: false,
                defaultValue: "Asia/Jakarta");

            migrationBuilder.AddCheckConstraint(
                name: "CK_tenants_Mode",
                table: "tenants",
                sql: "\"Mode\" IN ('live', 'demo')");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_tenants_Mode",
                table: "tenants");

            migrationBuilder.DropColumn(
                name: "Mode",
                table: "tenants");

            migrationBuilder.DropColumn(
                name: "TimeZoneId",
                table: "tenants");
        }
    }
}
