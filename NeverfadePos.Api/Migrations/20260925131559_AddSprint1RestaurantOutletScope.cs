using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NeverfadePos.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddSprint1RestaurantOutletScope : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_restaurant_tables_TenantId_Active_SortOrder",
                table: "restaurant_tables");

            migrationBuilder.DropIndex(
                name: "IX_restaurant_tables_TenantId_Code",
                table: "restaurant_tables");

            migrationBuilder.AddColumn<Guid>(
                name: "OutletId",
                table: "restaurant_tables",
                type: "uuid",
                nullable: true);

            // Existing restaurant tables belong to their tenant's original/default outlet.
            // Never assign Guid.Empty: it breaks the outlet FK on populated databases.
            migrationBuilder.Sql(
                """
                UPDATE restaurant_tables t
                SET "OutletId" = (
                    SELECT o."Id" FROM outlets o
                    WHERE o."TenantId" = t."TenantId"
                    ORDER BY o."IsDefault" DESC, o."CreatedAt", o."Id"
                    LIMIT 1
                )
                WHERE t."OutletId" IS NULL;
                """);

            migrationBuilder.Sql(
                """
                DO $$ BEGIN
                    IF EXISTS (SELECT 1 FROM restaurant_tables WHERE "OutletId" IS NULL) THEN
                        RAISE EXCEPTION 'Restaurant outlet migration: existing table has no tenant outlet';
                    END IF;
                END $$;
                """);

            migrationBuilder.AlterColumn<Guid>(
                name: "OutletId",
                table: "restaurant_tables",
                type: "uuid",
                nullable: false,
                oldClrType: typeof(Guid),
                oldType: "uuid",
                oldNullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_restaurant_tables_OutletId",
                table: "restaurant_tables",
                column: "OutletId");

            migrationBuilder.CreateIndex(
                name: "IX_restaurant_tables_TenantId_OutletId_Active_SortOrder",
                table: "restaurant_tables",
                columns: new[] { "TenantId", "OutletId", "Active", "SortOrder" });

            migrationBuilder.CreateIndex(
                name: "IX_restaurant_tables_TenantId_OutletId_Code",
                table: "restaurant_tables",
                columns: new[] { "TenantId", "OutletId", "Code" },
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_restaurant_tables_outlets_OutletId",
                table: "restaurant_tables",
                column: "OutletId",
                principalTable: "outlets",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_restaurant_tables_outlets_OutletId",
                table: "restaurant_tables");

            migrationBuilder.DropIndex(
                name: "IX_restaurant_tables_OutletId",
                table: "restaurant_tables");

            migrationBuilder.DropIndex(
                name: "IX_restaurant_tables_TenantId_OutletId_Active_SortOrder",
                table: "restaurant_tables");

            migrationBuilder.DropIndex(
                name: "IX_restaurant_tables_TenantId_OutletId_Code",
                table: "restaurant_tables");

            migrationBuilder.DropColumn(
                name: "OutletId",
                table: "restaurant_tables");

            migrationBuilder.CreateIndex(
                name: "IX_restaurant_tables_TenantId_Active_SortOrder",
                table: "restaurant_tables",
                columns: new[] { "TenantId", "Active", "SortOrder" });

            migrationBuilder.CreateIndex(
                name: "IX_restaurant_tables_TenantId_Code",
                table: "restaurant_tables",
                columns: new[] { "TenantId", "Code" },
                unique: true);
        }
    }
}
