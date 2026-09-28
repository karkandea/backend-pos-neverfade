using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NeverfadePos.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddSprint2QuoteCashCommit : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "ConsumedTransactionId",
                table: "sale_quotes",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "IdempotencyKey",
                table: "sale_quotes",
                type: "character varying(128)",
                maxLength: 128,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "IdempotencyRequestHash",
                table: "sale_quotes",
                type: "character varying(64)",
                maxLength: 64,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_sale_quotes_TenantId_OutletId_IdempotencyKey",
                table: "sale_quotes",
                columns: new[] { "TenantId", "OutletId", "IdempotencyKey" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_sale_quotes_TenantId_OutletId_IdempotencyKey",
                table: "sale_quotes");

            migrationBuilder.DropColumn(
                name: "ConsumedTransactionId",
                table: "sale_quotes");

            migrationBuilder.DropColumn(
                name: "IdempotencyKey",
                table: "sale_quotes");

            migrationBuilder.DropColumn(
                name: "IdempotencyRequestHash",
                table: "sale_quotes");
        }
    }
}
