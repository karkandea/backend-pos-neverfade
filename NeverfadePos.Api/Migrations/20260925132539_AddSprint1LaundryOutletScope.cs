using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NeverfadePos.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddSprint1LaundryOutletScope : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "OutletId",
                table: "laundry_work_orders",
                type: "uuid",
                nullable: true);

            // Preserve existing work orders by mapping them to the tenant's default outlet.
            migrationBuilder.Sql(
                """
                UPDATE laundry_work_orders w
                SET "OutletId" = (
                    SELECT o."Id" FROM outlets o
                    WHERE o."TenantId" = w."TenantId"
                    ORDER BY o."IsDefault" DESC, o."CreatedAt", o."Id"
                    LIMIT 1
                )
                WHERE w."OutletId" IS NULL;
                """);

            migrationBuilder.Sql(
                """
                DO $$ BEGIN
                    IF EXISTS (SELECT 1 FROM laundry_work_orders WHERE "OutletId" IS NULL) THEN
                        RAISE EXCEPTION 'Laundry outlet migration: existing work order has no tenant outlet';
                    END IF;
                END $$;
                """);

            migrationBuilder.AlterColumn<Guid>(
                name: "OutletId",
                table: "laundry_work_orders",
                type: "uuid",
                nullable: false,
                oldClrType: typeof(Guid),
                oldType: "uuid",
                oldNullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_laundry_work_orders_OutletId",
                table: "laundry_work_orders",
                column: "OutletId");

            migrationBuilder.CreateIndex(
                name: "IX_laundry_work_orders_TenantId_OutletId_Status_CreatedAt",
                table: "laundry_work_orders",
                columns: new[] { "TenantId", "OutletId", "Status", "CreatedAt" });

            migrationBuilder.AddForeignKey(
                name: "FK_laundry_work_orders_outlets_OutletId",
                table: "laundry_work_orders",
                column: "OutletId",
                principalTable: "outlets",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_laundry_work_orders_outlets_OutletId",
                table: "laundry_work_orders");

            migrationBuilder.DropIndex(
                name: "IX_laundry_work_orders_OutletId",
                table: "laundry_work_orders");

            migrationBuilder.DropIndex(
                name: "IX_laundry_work_orders_TenantId_OutletId_Status_CreatedAt",
                table: "laundry_work_orders");

            migrationBuilder.DropColumn(
                name: "OutletId",
                table: "laundry_work_orders");
        }
    }
}
