using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NeverfadePos.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddEndgameV2OutletStockBalances : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_stock_histories_TenantId_ProdukId",
                table: "stock_histories");

            migrationBuilder.AddColumn<Guid>(
                name: "OutletId",
                table: "stock_histories",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "stock_balances",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    OutletId = table.Column<Guid>(type: "uuid", nullable: false),
                    ProductId = table.Column<Guid>(type: "uuid", nullable: false),
                    ProductVariantId = table.Column<Guid>(type: "uuid", nullable: true),
                    LotId = table.Column<Guid>(type: "uuid", nullable: true),
                    OnHand = table.Column<decimal>(type: "numeric(20,6)", precision: 20, scale: 6, nullable: false),
                    Reserved = table.Column<decimal>(type: "numeric(20,6)", precision: 20, scale: 6, nullable: false),
                    Quarantine = table.Column<decimal>(type: "numeric(20,6)", precision: 20, scale: 6, nullable: false),
                    Available = table.Column<decimal>(type: "numeric(20,6)", precision: 20, scale: 6, nullable: false),
                    AverageCost = table.Column<decimal>(type: "numeric(20,6)", precision: 20, scale: 6, nullable: false),
                    AsOf = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Version = table.Column<long>(type: "bigint", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_stock_balances", x => x.Id);
                    table.CheckConstraint("CK_stock_balances_Available", "\"Available\" >= 0");
                    table.CheckConstraint("CK_stock_balances_AvailableFormula", "\"Available\" = \"OnHand\" - \"Reserved\" - \"Quarantine\"");
                    table.CheckConstraint("CK_stock_balances_OnHand", "\"OnHand\" >= 0");
                    table.CheckConstraint("CK_stock_balances_Quarantine", "\"Quarantine\" >= 0");
                    table.CheckConstraint("CK_stock_balances_Reserved", "\"Reserved\" >= 0");
                    table.CheckConstraint("CK_stock_balances_Version", "\"Version\" > 0");
                    table.ForeignKey(
                        name: "FK_stock_balances_outlets_OutletId",
                        column: x => x.OutletId,
                        principalTable: "outlets",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_stock_balances_product_variants_ProductVariantId",
                        column: x => x.ProductVariantId,
                        principalTable: "product_variants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_stock_balances_products_ProductId",
                        column: x => x.ProductId,
                        principalTable: "products",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.Sql("""
                WITH default_outlets AS (
                    SELECT DISTINCT ON ("TenantId")
                        "TenantId", "Id" AS "OutletId"
                    FROM outlets
                    WHERE "Active" = TRUE
                    ORDER BY "TenantId", "IsDefault" DESC, "CreatedAt", "Id"
                )
                INSERT INTO stock_balances (
                    "Id", "TenantId", "OutletId", "ProductId", "ProductVariantId", "LotId",
                    "OnHand", "Reserved", "Quarantine", "Available", "AverageCost",
                    "AsOf", "Version", "CreatedAt")
                SELECT
                    md5(p."TenantId"::text || ':' || d."OutletId"::text || ':' || p."Id"::text || ':base')::uuid,
                    p."TenantId", d."OutletId", p."Id", NULL::uuid, NULL::uuid,
                    p."Stok", 0, 0, p."Stok", p."HargaModal",
                    NOW(), 1, NOW()
                FROM products p
                JOIN default_outlets d ON d."TenantId" = p."TenantId"
                WHERE p."TracksStock" = TRUE
                  AND NOT EXISTS (
                      SELECT 1 FROM product_variants v
                      WHERE v."TenantId" = p."TenantId" AND v."ProductId" = p."Id")
                UNION ALL
                SELECT
                    md5(v."TenantId"::text || ':' || d."OutletId"::text || ':' || v."ProductId"::text || ':' || v."Id"::text)::uuid,
                    v."TenantId", d."OutletId", v."ProductId", v."Id", NULL::uuid,
                    v."Stok", 0, 0, v."Stok", COALESCE(v."HargaModal", p."HargaModal"),
                    NOW(), 1, NOW()
                FROM product_variants v
                JOIN products p ON p."Id" = v."ProductId" AND p."TenantId" = v."TenantId"
                JOIN default_outlets d ON d."TenantId" = v."TenantId";
                """);

            migrationBuilder.CreateIndex(
                name: "IX_stock_histories_TenantId_OutletId_ProdukId",
                table: "stock_histories",
                columns: new[] { "TenantId", "OutletId", "ProdukId" });

            migrationBuilder.CreateIndex(
                name: "IX_stock_balances_OutletId",
                table: "stock_balances",
                column: "OutletId");

            migrationBuilder.CreateIndex(
                name: "IX_stock_balances_ProductId",
                table: "stock_balances",
                column: "ProductId");

            migrationBuilder.CreateIndex(
                name: "IX_stock_balances_ProductVariantId",
                table: "stock_balances",
                column: "ProductVariantId");

            migrationBuilder.CreateIndex(
                name: "IX_stock_balances_TenantId_OutletId_ProductId",
                table: "stock_balances",
                columns: new[] { "TenantId", "OutletId", "ProductId" },
                unique: true,
                filter: "\"ProductVariantId\" IS NULL AND \"LotId\" IS NULL");

            migrationBuilder.CreateIndex(
                name: "IX_stock_balances_TenantId_OutletId_ProductId_ProductVariantId",
                table: "stock_balances",
                columns: new[] { "TenantId", "OutletId", "ProductId", "ProductVariantId" },
                unique: true,
                filter: "\"ProductVariantId\" IS NOT NULL AND \"LotId\" IS NULL");

            migrationBuilder.CreateIndex(
                name: "IX_stock_balances_TenantId_OutletId_ProductId_ProductVariantId~",
                table: "stock_balances",
                columns: new[] { "TenantId", "OutletId", "ProductId", "ProductVariantId", "LotId" },
                unique: true,
                filter: "\"LotId\" IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "stock_balances");

            migrationBuilder.DropIndex(
                name: "IX_stock_histories_TenantId_OutletId_ProdukId",
                table: "stock_histories");

            migrationBuilder.DropColumn(
                name: "OutletId",
                table: "stock_histories");

            migrationBuilder.CreateIndex(
                name: "IX_stock_histories_TenantId_ProdukId",
                table: "stock_histories",
                columns: new[] { "TenantId", "ProdukId" });
        }
    }
}
