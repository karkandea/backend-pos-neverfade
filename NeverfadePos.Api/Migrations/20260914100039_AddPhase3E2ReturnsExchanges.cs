using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NeverfadePos.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddPhase3E2ReturnsExchanges : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "retail_returns",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ReturnNumber = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    IdempotencyKey = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    TransactionId = table.Column<Guid>(type: "uuid", nullable: false),
                    TransactionNumber = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Type = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Reason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    Notes = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    RefundAmount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    CreatedByUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedByName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_retail_returns", x => x.Id);
                    table.CheckConstraint("CK_retail_returns_RefundAmount", "\"RefundAmount\" >= 0");
                    table.ForeignKey(
                        name: "FK_retail_returns_tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_retail_returns_transactions_TransactionId",
                        column: x => x.TransactionId,
                        principalTable: "transactions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "retail_return_items",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    RetailReturnId = table.Column<Guid>(type: "uuid", nullable: false),
                    TransactionItemId = table.Column<Guid>(type: "uuid", nullable: false),
                    ProductId = table.Column<Guid>(type: "uuid", nullable: false),
                    ProductName = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    OriginalVariantId = table.Column<Guid>(type: "uuid", nullable: true),
                    OriginalVariantSku = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    OriginalVariantLabel = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Quantity = table.Column<int>(type: "integer", nullable: false),
                    OriginalUnitPrice = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    RefundAmount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    Restock = table.Column<bool>(type: "boolean", nullable: false),
                    ReplacementVariantId = table.Column<Guid>(type: "uuid", nullable: true),
                    ReplacementVariantSku = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    ReplacementVariantLabel = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_retail_return_items", x => x.Id);
                    table.CheckConstraint("CK_retail_return_items_Quantity", "\"Quantity\" > 0");
                    table.ForeignKey(
                        name: "FK_retail_return_items_products_ProductId",
                        column: x => x.ProductId,
                        principalTable: "products",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_retail_return_items_retail_returns_RetailReturnId",
                        column: x => x.RetailReturnId,
                        principalTable: "retail_returns",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_retail_return_items_transaction_items_TransactionItemId",
                        column: x => x.TransactionItemId,
                        principalTable: "transaction_items",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_retail_return_items_ProductId",
                table: "retail_return_items",
                column: "ProductId");

            migrationBuilder.CreateIndex(
                name: "IX_retail_return_items_RetailReturnId",
                table: "retail_return_items",
                column: "RetailReturnId");

            migrationBuilder.CreateIndex(
                name: "IX_retail_return_items_TenantId",
                table: "retail_return_items",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_retail_return_items_TenantId_RetailReturnId",
                table: "retail_return_items",
                columns: new[] { "TenantId", "RetailReturnId" });

            migrationBuilder.CreateIndex(
                name: "IX_retail_return_items_TenantId_TransactionItemId",
                table: "retail_return_items",
                columns: new[] { "TenantId", "TransactionItemId" });

            migrationBuilder.CreateIndex(
                name: "IX_retail_return_items_TransactionItemId",
                table: "retail_return_items",
                column: "TransactionItemId");

            migrationBuilder.CreateIndex(
                name: "IX_retail_returns_TenantId",
                table: "retail_returns",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_retail_returns_TenantId_IdempotencyKey",
                table: "retail_returns",
                columns: new[] { "TenantId", "IdempotencyKey" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_retail_returns_TenantId_ReturnNumber",
                table: "retail_returns",
                columns: new[] { "TenantId", "ReturnNumber" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_retail_returns_TenantId_TransactionId",
                table: "retail_returns",
                columns: new[] { "TenantId", "TransactionId" });

            migrationBuilder.CreateIndex(
                name: "IX_retail_returns_TransactionId",
                table: "retail_returns",
                column: "TransactionId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "retail_return_items");

            migrationBuilder.DropTable(
                name: "retail_returns");
        }
    }
}
