using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NeverfadePos.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddXenditSandboxPaymentFoundation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "FinalizedAt",
                table: "transactions",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Status",
                table: "transactions",
                type: "character varying(30)",
                maxLength: 30,
                nullable: false,
                defaultValue: "paid");

            migrationBuilder.CreateTable(
                name: "payments",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TransactionId = table.Column<Guid>(type: "uuid", nullable: false),
                    Provider = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    ProviderReferenceId = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    ProviderPaymentRequestId = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    ProviderPaymentId = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    Method = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    Currency = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false),
                    Amount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    Status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    FailureCode = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    PaidAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_payments", x => x.Id);
                    table.CheckConstraint("CK_payments_Status", "\"Status\" IN ('creating', 'pending', 'paid', 'failed')");
                    table.ForeignKey(
                        name: "FK_payments_tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_payments_transactions_TransactionId",
                        column: x => x.TransactionId,
                        principalTable: "transactions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "payment_ledger_entries",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    PaymentId = table.Column<Guid>(type: "uuid", nullable: false),
                    TransactionId = table.Column<Guid>(type: "uuid", nullable: false),
                    EntryType = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    Amount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    Currency = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false),
                    ProviderReference = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_payment_ledger_entries", x => x.Id);
                    table.CheckConstraint("CK_payment_ledger_entries_EntryType", "\"EntryType\" IN ('payment_credit')");
                    table.ForeignKey(
                        name: "FK_payment_ledger_entries_payments_PaymentId",
                        column: x => x.PaymentId,
                        principalTable: "payments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_payment_ledger_entries_tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_payment_ledger_entries_transactions_TransactionId",
                        column: x => x.TransactionId,
                        principalTable: "transactions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "payment_routes",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    PaymentId = table.Column<Guid>(type: "uuid", nullable: false),
                    Provider = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    ProviderPaymentRequestId = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_payment_routes", x => x.Id);
                    table.ForeignKey(
                        name: "FK_payment_routes_payments_PaymentId",
                        column: x => x.PaymentId,
                        principalTable: "payments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_payment_routes_tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "payment_webhook_events",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    PaymentId = table.Column<Guid>(type: "uuid", nullable: false),
                    ProviderEventKey = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    EventType = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    ProviderPaymentId = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    ProcessingStatus = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_payment_webhook_events", x => x.Id);
                    table.ForeignKey(
                        name: "FK_payment_webhook_events_payments_PaymentId",
                        column: x => x.PaymentId,
                        principalTable: "payments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_payment_webhook_events_tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.AddCheckConstraint(
                name: "CK_transactions_Status",
                table: "transactions",
                sql: "\"Status\" IN ('pending_payment', 'paid', 'failed')");

            migrationBuilder.CreateIndex(
                name: "IX_payment_ledger_entries_PaymentId_EntryType",
                table: "payment_ledger_entries",
                columns: new[] { "PaymentId", "EntryType" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_payment_ledger_entries_TenantId",
                table: "payment_ledger_entries",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_payment_ledger_entries_TenantId_CreatedAt",
                table: "payment_ledger_entries",
                columns: new[] { "TenantId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_payment_ledger_entries_TransactionId",
                table: "payment_ledger_entries",
                column: "TransactionId");

            migrationBuilder.CreateIndex(
                name: "IX_payment_routes_PaymentId",
                table: "payment_routes",
                column: "PaymentId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_payment_routes_Provider_ProviderPaymentRequestId",
                table: "payment_routes",
                columns: new[] { "Provider", "ProviderPaymentRequestId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_payment_routes_TenantId",
                table: "payment_routes",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_payment_webhook_events_PaymentId",
                table: "payment_webhook_events",
                column: "PaymentId");

            migrationBuilder.CreateIndex(
                name: "IX_payment_webhook_events_ProviderEventKey",
                table: "payment_webhook_events",
                column: "ProviderEventKey",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_payment_webhook_events_TenantId",
                table: "payment_webhook_events",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_payment_webhook_events_TenantId_CreatedAt",
                table: "payment_webhook_events",
                columns: new[] { "TenantId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_payments_ProviderPaymentId",
                table: "payments",
                column: "ProviderPaymentId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_payments_ProviderPaymentRequestId",
                table: "payments",
                column: "ProviderPaymentRequestId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_payments_ProviderReferenceId",
                table: "payments",
                column: "ProviderReferenceId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_payments_TenantId",
                table: "payments",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_payments_TenantId_Status_CreatedAt",
                table: "payments",
                columns: new[] { "TenantId", "Status", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_payments_TransactionId",
                table: "payments",
                column: "TransactionId",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "payment_ledger_entries");

            migrationBuilder.DropTable(
                name: "payment_routes");

            migrationBuilder.DropTable(
                name: "payment_webhook_events");

            migrationBuilder.DropTable(
                name: "payments");

            migrationBuilder.DropCheckConstraint(
                name: "CK_transactions_Status",
                table: "transactions");

            migrationBuilder.DropColumn(
                name: "FinalizedAt",
                table: "transactions");

            migrationBuilder.DropColumn(
                name: "Status",
                table: "transactions");
        }
    }
}
