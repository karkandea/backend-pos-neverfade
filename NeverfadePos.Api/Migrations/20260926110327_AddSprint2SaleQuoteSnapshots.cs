using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NeverfadePos.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddSprint2SaleQuoteSnapshots : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "sale_quotes",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    OutletId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedByUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    QuoteVersion = table.Column<Guid>(type: "uuid", nullable: false),
                    ExpiresAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    SnapshotJson = table.Column<string>(type: "text", nullable: false),
                    Total = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    Status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_sale_quotes", x => x.Id);
                    table.CheckConstraint("CK_sale_quotes_Status", "\"Status\" IN ('quoted', 'consumed')");
                    table.CheckConstraint("CK_sale_quotes_Total", "\"Total\" >= 0");
                });

            migrationBuilder.CreateIndex(
                name: "IX_sale_quotes_TenantId_OutletId_ExpiresAt",
                table: "sale_quotes",
                columns: new[] { "TenantId", "OutletId", "ExpiresAt" });

            migrationBuilder.CreateIndex(
                name: "IX_sale_quotes_TenantId_QuoteVersion",
                table: "sale_quotes",
                columns: new[] { "TenantId", "QuoteVersion" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "sale_quotes");
        }
    }
}
