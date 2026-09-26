using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NeverfadePos.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddSprint2CashPreparedAttempt : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_sale_quotes_Status",
                table: "sale_quotes");

            migrationBuilder.AddColumn<decimal>(
                name: "PreparedAmountReceived",
                table: "sale_quotes",
                type: "numeric(18,2)",
                precision: 18,
                scale: 2,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "PreparedAt",
                table: "sale_quotes",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_sale_quotes_TenantId_OutletId_CreatedByUserId_PreparedAt",
                table: "sale_quotes",
                columns: new[] { "TenantId", "OutletId", "CreatedByUserId", "PreparedAt" });

            migrationBuilder.AddCheckConstraint(
                name: "CK_sale_quotes_Status",
                table: "sale_quotes",
                sql: "\"Status\" IN ('quoted', 'consumed', 'abandoned')");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_sale_quotes_TenantId_OutletId_CreatedByUserId_PreparedAt",
                table: "sale_quotes");

            migrationBuilder.DropCheckConstraint(
                name: "CK_sale_quotes_Status",
                table: "sale_quotes");

            migrationBuilder.DropColumn(
                name: "PreparedAmountReceived",
                table: "sale_quotes");

            migrationBuilder.DropColumn(
                name: "PreparedAt",
                table: "sale_quotes");

            migrationBuilder.AddCheckConstraint(
                name: "CK_sale_quotes_Status",
                table: "sale_quotes",
                sql: "\"Status\" IN ('quoted', 'consumed')");
        }
    }
}
