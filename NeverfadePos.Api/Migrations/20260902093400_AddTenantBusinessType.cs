using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using NeverfadePos.Api.Data;

#nullable disable

namespace NeverfadePos.Api.Migrations;

[DbContext(typeof(AppDbContext))]
[Migration("20260902093400_AddTenantBusinessType")]
public partial class AddTenantBusinessType : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<string>(
            name: "BusinessType",
            table: "tenants",
            type: "character varying(40)",
            maxLength: 40,
            nullable: false,
            defaultValue: "general_retail");

        migrationBuilder.AddCheckConstraint(
            name: "CK_tenants_BusinessType",
            table: "tenants",
            sql: "\"BusinessType\" IN ('general_retail', 'food_beverage', 'laundry', 'salon_barbershop')");

        migrationBuilder.CreateIndex(
            name: "IX_tenants_BusinessType",
            table: "tenants",
            column: "BusinessType");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropIndex(
            name: "IX_tenants_BusinessType",
            table: "tenants");

        migrationBuilder.DropCheckConstraint(
            name: "CK_tenants_BusinessType",
            table: "tenants");

        migrationBuilder.DropColumn(
            name: "BusinessType",
            table: "tenants");
    }
}
