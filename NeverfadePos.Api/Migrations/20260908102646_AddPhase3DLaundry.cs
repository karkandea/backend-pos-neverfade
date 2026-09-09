using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NeverfadePos.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddPhase3DLaundry : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ProductType",
                table: "transaction_items",
                type: "character varying(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "goods");

            migrationBuilder.AddColumn<decimal>(
                name: "Quantity",
                table: "transaction_items",
                type: "numeric(18,3)",
                precision: 18,
                scale: 3,
                nullable: false,
                defaultValue: 1m);

            migrationBuilder.AddColumn<int>(
                name: "QuantityPrecision",
                table: "transaction_items",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<bool>(
                name: "TracksStock",
                table: "transaction_items",
                type: "boolean",
                nullable: false,
                defaultValue: true);

            migrationBuilder.AddColumn<string>(
                name: "Unit",
                table: "transaction_items",
                type: "character varying(50)",
                maxLength: 50,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<int>(
                name: "QuantityPrecision",
                table: "products",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<bool>(
                name: "TracksStock",
                table: "products",
                type: "boolean",
                nullable: false,
                defaultValue: true);

            migrationBuilder.AddColumn<string>(
                name: "Type",
                table: "products",
                type: "character varying(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "goods");

            migrationBuilder.Sql(
                """
                UPDATE "products"
                SET
                    "Type" = 'goods',
                    "TracksStock" = TRUE,
                    "QuantityPrecision" = 0;

                UPDATE "transaction_items" AS ti
                SET
                    "ProductType" = 'goods',
                    "TracksStock" = TRUE,
                    "QuantityPrecision" = 0,
                    "Quantity" = ti."Qty",
                    "Unit" = COALESCE(p."Satuan", '')
                FROM "products" AS p
                WHERE p."Id" = ti."ProductId";
                """);

            migrationBuilder.CreateTable(
                name: "laundry_work_orders",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CustomerId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedByUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    TransactionId = table.Column<Guid>(type: "uuid", nullable: true),
                    OrderNumber = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    Status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    PaymentStatus = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Notes = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    CancellationReason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    PromisedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    PaidAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CompletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CancelledAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_laundry_work_orders", x => x.Id);
                    table.CheckConstraint("CK_laundry_work_orders_PaymentStatus", "\"PaymentStatus\" IN ('unpaid', 'paid')");
                    table.CheckConstraint("CK_laundry_work_orders_Status", "\"Status\" IN ('received', 'in_progress', 'ready', 'completed', 'cancelled')");
                    table.ForeignKey(
                        name: "FK_laundry_work_orders_customers_CustomerId",
                        column: x => x.CustomerId,
                        principalTable: "customers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_laundry_work_orders_tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_laundry_work_orders_transactions_TransactionId",
                        column: x => x.TransactionId,
                        principalTable: "transactions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_laundry_work_orders_users_CreatedByUserId",
                        column: x => x.CreatedByUserId,
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "laundry_work_order_items",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    LaundryWorkOrderId = table.Column<Guid>(type: "uuid", nullable: false),
                    ProductId = table.Column<Guid>(type: "uuid", nullable: false),
                    Nama = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    ProductType = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Unit = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    Quantity = table.Column<decimal>(type: "numeric(18,3)", precision: 18, scale: 3, nullable: false),
                    QuantityPrecision = table.Column<int>(type: "integer", nullable: false),
                    UnitPrice = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    Subtotal = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_laundry_work_order_items", x => x.Id);
                    table.CheckConstraint("CK_laundry_work_order_items_ProductType", "\"ProductType\" IN ('goods', 'service')");
                    table.CheckConstraint("CK_laundry_work_order_items_Quantity", "\"Quantity\" > 0");
                    table.CheckConstraint("CK_laundry_work_order_items_QuantityPrecision", "\"QuantityPrecision\" BETWEEN 0 AND 3");
                    table.ForeignKey(
                        name: "FK_laundry_work_order_items_laundry_work_orders_LaundryWorkOrd~",
                        column: x => x.LaundryWorkOrderId,
                        principalTable: "laundry_work_orders",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_laundry_work_order_items_products_ProductId",
                        column: x => x.ProductId,
                        principalTable: "products",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_laundry_work_order_items_tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "laundry_work_order_status_history",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    LaundryWorkOrderId = table.Column<Guid>(type: "uuid", nullable: false),
                    ActorUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    FromStatus = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    ToStatus = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    ActorName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Reason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_laundry_work_order_status_history", x => x.Id);
                    table.ForeignKey(
                        name: "FK_laundry_work_order_status_history_laundry_work_orders_Laund~",
                        column: x => x.LaundryWorkOrderId,
                        principalTable: "laundry_work_orders",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_laundry_work_order_status_history_tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_laundry_work_order_status_history_users_ActorUserId",
                        column: x => x.ActorUserId,
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.AddCheckConstraint(
                name: "CK_transaction_items_ProductType",
                table: "transaction_items",
                sql: "\"ProductType\" IN ('goods', 'service')");

            migrationBuilder.AddCheckConstraint(
                name: "CK_transaction_items_Quantity",
                table: "transaction_items",
                sql: "\"Quantity\" > 0");

            migrationBuilder.AddCheckConstraint(
                name: "CK_transaction_items_QuantityPrecision",
                table: "transaction_items",
                sql: "\"QuantityPrecision\" BETWEEN 0 AND 3");

            migrationBuilder.AddCheckConstraint(
                name: "CK_products_GoodsPrecision",
                table: "products",
                sql: "\"Type\" <> 'goods' OR \"QuantityPrecision\" = 0");

            migrationBuilder.AddCheckConstraint(
                name: "CK_products_QuantityPrecision",
                table: "products",
                sql: "\"QuantityPrecision\" BETWEEN 0 AND 3");

            migrationBuilder.AddCheckConstraint(
                name: "CK_products_ServiceStock",
                table: "products",
                sql: "\"Type\" <> 'service' OR \"TracksStock\" = FALSE");

            migrationBuilder.AddCheckConstraint(
                name: "CK_products_Type",
                table: "products",
                sql: "\"Type\" IN ('goods', 'service')");

            migrationBuilder.CreateIndex(
                name: "IX_laundry_work_order_items_LaundryWorkOrderId",
                table: "laundry_work_order_items",
                column: "LaundryWorkOrderId");

            migrationBuilder.CreateIndex(
                name: "IX_laundry_work_order_items_ProductId",
                table: "laundry_work_order_items",
                column: "ProductId");

            migrationBuilder.CreateIndex(
                name: "IX_laundry_work_order_items_TenantId",
                table: "laundry_work_order_items",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_laundry_work_order_status_history_ActorUserId",
                table: "laundry_work_order_status_history",
                column: "ActorUserId");

            migrationBuilder.CreateIndex(
                name: "IX_laundry_work_order_status_history_LaundryWorkOrderId_Create~",
                table: "laundry_work_order_status_history",
                columns: new[] { "LaundryWorkOrderId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_laundry_work_order_status_history_TenantId",
                table: "laundry_work_order_status_history",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_laundry_work_orders_CreatedByUserId",
                table: "laundry_work_orders",
                column: "CreatedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_laundry_work_orders_CustomerId",
                table: "laundry_work_orders",
                column: "CustomerId");

            migrationBuilder.CreateIndex(
                name: "IX_laundry_work_orders_TenantId_OrderNumber",
                table: "laundry_work_orders",
                columns: new[] { "TenantId", "OrderNumber" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_laundry_work_orders_TenantId_Status_CreatedAt",
                table: "laundry_work_orders",
                columns: new[] { "TenantId", "Status", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_laundry_work_orders_TransactionId",
                table: "laundry_work_orders",
                column: "TransactionId",
                unique: true,
                filter: "\"TransactionId\" IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "laundry_work_order_items");

            migrationBuilder.DropTable(
                name: "laundry_work_order_status_history");

            migrationBuilder.DropTable(
                name: "laundry_work_orders");

            migrationBuilder.DropCheckConstraint(
                name: "CK_transaction_items_ProductType",
                table: "transaction_items");

            migrationBuilder.DropCheckConstraint(
                name: "CK_transaction_items_Quantity",
                table: "transaction_items");

            migrationBuilder.DropCheckConstraint(
                name: "CK_transaction_items_QuantityPrecision",
                table: "transaction_items");

            migrationBuilder.DropCheckConstraint(
                name: "CK_products_GoodsPrecision",
                table: "products");

            migrationBuilder.DropCheckConstraint(
                name: "CK_products_QuantityPrecision",
                table: "products");

            migrationBuilder.DropCheckConstraint(
                name: "CK_products_ServiceStock",
                table: "products");

            migrationBuilder.DropCheckConstraint(
                name: "CK_products_Type",
                table: "products");

            migrationBuilder.DropColumn(
                name: "ProductType",
                table: "transaction_items");

            migrationBuilder.DropColumn(
                name: "Quantity",
                table: "transaction_items");

            migrationBuilder.DropColumn(
                name: "QuantityPrecision",
                table: "transaction_items");

            migrationBuilder.DropColumn(
                name: "TracksStock",
                table: "transaction_items");

            migrationBuilder.DropColumn(
                name: "Unit",
                table: "transaction_items");

            migrationBuilder.DropColumn(
                name: "QuantityPrecision",
                table: "products");

            migrationBuilder.DropColumn(
                name: "TracksStock",
                table: "products");

            migrationBuilder.DropColumn(
                name: "Type",
                table: "products");
        }
    }
}
