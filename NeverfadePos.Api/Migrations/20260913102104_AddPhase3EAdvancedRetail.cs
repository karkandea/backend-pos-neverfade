using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NeverfadePos.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddPhase3EAdvancedRetail : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_tenants_BusinessType",
                table: "tenants");

            migrationBuilder.AddColumn<decimal>(
                name: "BasePrice",
                table: "transaction_items",
                type: "numeric(18,2)",
                precision: 18,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<Guid>(
                name: "PriceLevelId",
                table: "transaction_items",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PriceLevelName",
                table: "transaction_items",
                type: "character varying(100)",
                maxLength: 100,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<Guid>(
                name: "ProductVariantId",
                table: "transaction_items",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "VariantLabel",
                table: "transaction_items",
                type: "character varying(200)",
                maxLength: 200,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "VariantSku",
                table: "transaction_items",
                type: "character varying(100)",
                maxLength: 100,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<Guid>(
                name: "ProductVariantId",
                table: "stock_histories",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "VariantLabel",
                table: "stock_histories",
                type: "character varying(200)",
                maxLength: 200,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "VariantSku",
                table: "stock_histories",
                type: "character varying(100)",
                maxLength: 100,
                nullable: false,
                defaultValue: "");

            migrationBuilder.CreateTable(
                name: "price_levels",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Code = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    Name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    SortOrder = table.Column<int>(type: "integer", nullable: false),
                    Active = table.Column<bool>(type: "boolean", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_price_levels", x => x.Id);
                    table.ForeignKey(
                        name: "FK_price_levels_tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "product_variants",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ProductId = table.Column<Guid>(type: "uuid", nullable: false),
                    Sku = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Barcode = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Label = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Option1Name = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    Option1Value = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Option2Name = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    Option2Value = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Option3Name = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    Option3Value = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    HargaModal = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: true),
                    HargaJual = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: true),
                    Stok = table.Column<int>(type: "integer", nullable: false),
                    Active = table.Column<bool>(type: "boolean", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_product_variants", x => x.Id);
                    table.CheckConstraint("CK_product_variants_Stock", "\"Stok\" >= 0");
                    table.ForeignKey(
                        name: "FK_product_variants_products_ProductId",
                        column: x => x.ProductId,
                        principalTable: "products",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_product_variants_tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "product_prices",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ProductId = table.Column<Guid>(type: "uuid", nullable: false),
                    ProductVariantId = table.Column<Guid>(type: "uuid", nullable: true),
                    PriceLevelId = table.Column<Guid>(type: "uuid", nullable: false),
                    MinQuantity = table.Column<decimal>(type: "numeric(18,3)", precision: 18, scale: 3, nullable: false),
                    UnitPrice = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_product_prices", x => x.Id);
                    table.CheckConstraint("CK_product_prices_MinQuantity", "\"MinQuantity\" > 0");
                    table.CheckConstraint("CK_product_prices_UnitPrice", "\"UnitPrice\" >= 0");
                    table.ForeignKey(
                        name: "FK_product_prices_price_levels_PriceLevelId",
                        column: x => x.PriceLevelId,
                        principalTable: "price_levels",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_product_prices_product_variants_ProductVariantId",
                        column: x => x.ProductVariantId,
                        principalTable: "product_variants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_product_prices_products_ProductId",
                        column: x => x.ProductId,
                        principalTable: "products",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_product_prices_tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.AddCheckConstraint(
                name: "CK_tenants_BusinessType",
                table: "tenants",
                sql: "\"BusinessType\" IN ('general_retail', 'fashion_retail', 'food_beverage', 'laundry', 'salon_barbershop')");

            migrationBuilder.CreateIndex(
                name: "IX_price_levels_TenantId",
                table: "price_levels",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_price_levels_TenantId_Code",
                table: "price_levels",
                columns: new[] { "TenantId", "Code" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_product_prices_PriceLevelId",
                table: "product_prices",
                column: "PriceLevelId");

            migrationBuilder.CreateIndex(
                name: "IX_product_prices_ProductId",
                table: "product_prices",
                column: "ProductId");

            migrationBuilder.CreateIndex(
                name: "IX_product_prices_ProductVariantId",
                table: "product_prices",
                column: "ProductVariantId");

            migrationBuilder.CreateIndex(
                name: "IX_product_prices_TenantId",
                table: "product_prices",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_product_prices_TenantId_ProductId",
                table: "product_prices",
                columns: new[] { "TenantId", "ProductId" });

            migrationBuilder.CreateIndex(
                name: "IX_product_prices_TenantId_ProductId_PriceLevelId",
                table: "product_prices",
                columns: new[] { "TenantId", "ProductId", "PriceLevelId" },
                unique: true,
                filter: "\"ProductVariantId\" IS NULL");

            migrationBuilder.CreateIndex(
                name: "IX_product_prices_TenantId_ProductVariantId_PriceLevelId",
                table: "product_prices",
                columns: new[] { "TenantId", "ProductVariantId", "PriceLevelId" },
                unique: true,
                filter: "\"ProductVariantId\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_product_variants_ProductId",
                table: "product_variants",
                column: "ProductId");

            migrationBuilder.CreateIndex(
                name: "IX_product_variants_TenantId",
                table: "product_variants",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_product_variants_TenantId_Barcode",
                table: "product_variants",
                columns: new[] { "TenantId", "Barcode" },
                unique: true,
                filter: "\"Barcode\" <> ''");

            migrationBuilder.CreateIndex(
                name: "IX_product_variants_TenantId_ProductId",
                table: "product_variants",
                columns: new[] { "TenantId", "ProductId" });

            migrationBuilder.CreateIndex(
                name: "IX_product_variants_TenantId_Sku",
                table: "product_variants",
                columns: new[] { "TenantId", "Sku" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "product_prices");

            migrationBuilder.DropTable(
                name: "price_levels");

            migrationBuilder.DropTable(
                name: "product_variants");

            migrationBuilder.DropCheckConstraint(
                name: "CK_tenants_BusinessType",
                table: "tenants");

            migrationBuilder.DropColumn(
                name: "BasePrice",
                table: "transaction_items");

            migrationBuilder.DropColumn(
                name: "PriceLevelId",
                table: "transaction_items");

            migrationBuilder.DropColumn(
                name: "PriceLevelName",
                table: "transaction_items");

            migrationBuilder.DropColumn(
                name: "ProductVariantId",
                table: "transaction_items");

            migrationBuilder.DropColumn(
                name: "VariantLabel",
                table: "transaction_items");

            migrationBuilder.DropColumn(
                name: "VariantSku",
                table: "transaction_items");

            migrationBuilder.DropColumn(
                name: "ProductVariantId",
                table: "stock_histories");

            migrationBuilder.DropColumn(
                name: "VariantLabel",
                table: "stock_histories");

            migrationBuilder.DropColumn(
                name: "VariantSku",
                table: "stock_histories");

            migrationBuilder.AddCheckConstraint(
                name: "CK_tenants_BusinessType",
                table: "tenants",
                sql: "\"BusinessType\" IN ('general_retail', 'food_beverage', 'laundry', 'salon_barbershop')");
        }
    }
}
