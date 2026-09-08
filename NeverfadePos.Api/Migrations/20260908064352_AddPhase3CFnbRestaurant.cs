using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NeverfadePos.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddPhase3CFnbRestaurant : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "restaurant_tables",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Code = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    Name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Capacity = table.Column<int>(type: "integer", nullable: false),
                    Active = table.Column<bool>(type: "boolean", nullable: false),
                    SortOrder = table.Column<int>(type: "integer", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_restaurant_tables", x => x.Id);
                    table.CheckConstraint("CK_restaurant_tables_Capacity", "\"Capacity\" > 0 AND \"Capacity\" <= 100");
                    table.ForeignKey(
                        name: "FK_restaurant_tables_tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "restaurant_orders",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TableId = table.Column<Guid>(type: "uuid", nullable: false),
                    OpenedByUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    TransactionId = table.Column<Guid>(type: "uuid", nullable: true),
                    OrderNumber = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    Status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    CancellationReason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ClosedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CancelledAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_restaurant_orders", x => x.Id);
                    table.CheckConstraint("CK_restaurant_orders_Status", "\"Status\" IN ('open', 'closed', 'cancelled')");
                    table.ForeignKey(
                        name: "FK_restaurant_orders_restaurant_tables_TableId",
                        column: x => x.TableId,
                        principalTable: "restaurant_tables",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_restaurant_orders_tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_restaurant_orders_transactions_TransactionId",
                        column: x => x.TransactionId,
                        principalTable: "transactions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_restaurant_orders_users_OpenedByUserId",
                        column: x => x.OpenedByUserId,
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "restaurant_order_items",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    RestaurantOrderId = table.Column<Guid>(type: "uuid", nullable: false),
                    ProductId = table.Column<Guid>(type: "uuid", nullable: false),
                    Nama = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    HargaJual = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    Qty = table.Column<int>(type: "integer", nullable: false),
                    Note = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    KitchenStatus = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    QueuedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    PreparingAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ReadyAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ServedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_restaurant_order_items", x => x.Id);
                    table.CheckConstraint("CK_restaurant_order_items_HargaJual", "\"HargaJual\" >= 0");
                    table.CheckConstraint("CK_restaurant_order_items_KitchenStatus", "\"KitchenStatus\" IN ('draft', 'queued', 'preparing', 'ready', 'served', 'cancelled')");
                    table.CheckConstraint("CK_restaurant_order_items_Qty", "\"Qty\" > 0");
                    table.ForeignKey(
                        name: "FK_restaurant_order_items_products_ProductId",
                        column: x => x.ProductId,
                        principalTable: "products",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_restaurant_order_items_restaurant_orders_RestaurantOrderId",
                        column: x => x.RestaurantOrderId,
                        principalTable: "restaurant_orders",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_restaurant_order_items_tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_restaurant_order_items_ProductId",
                table: "restaurant_order_items",
                column: "ProductId");

            migrationBuilder.CreateIndex(
                name: "IX_restaurant_order_items_RestaurantOrderId",
                table: "restaurant_order_items",
                column: "RestaurantOrderId");

            migrationBuilder.CreateIndex(
                name: "IX_restaurant_order_items_TenantId_KitchenStatus_QueuedAt",
                table: "restaurant_order_items",
                columns: new[] { "TenantId", "KitchenStatus", "QueuedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_restaurant_order_items_TenantId_RestaurantOrderId",
                table: "restaurant_order_items",
                columns: new[] { "TenantId", "RestaurantOrderId" });

            migrationBuilder.CreateIndex(
                name: "IX_restaurant_orders_OpenedByUserId",
                table: "restaurant_orders",
                column: "OpenedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_restaurant_orders_TableId",
                table: "restaurant_orders",
                column: "TableId",
                unique: true,
                filter: "\"Status\" = 'open'");

            migrationBuilder.CreateIndex(
                name: "IX_restaurant_orders_TenantId_OrderNumber",
                table: "restaurant_orders",
                columns: new[] { "TenantId", "OrderNumber" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_restaurant_orders_TenantId_Status_CreatedAt",
                table: "restaurant_orders",
                columns: new[] { "TenantId", "Status", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_restaurant_orders_TransactionId",
                table: "restaurant_orders",
                column: "TransactionId",
                unique: true,
                filter: "\"TransactionId\" IS NOT NULL");

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

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "restaurant_order_items");

            migrationBuilder.DropTable(
                name: "restaurant_orders");

            migrationBuilder.DropTable(
                name: "restaurant_tables");
        }
    }
}
