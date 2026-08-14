using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NeverfadePos.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddTenantFinanceManualWithdrawals : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_payment_ledger_entries_EntryType",
                table: "payment_ledger_entries");

            migrationBuilder.AlterColumn<Guid>(
                name: "TransactionId",
                table: "payment_ledger_entries",
                type: "uuid",
                nullable: true,
                oldClrType: typeof(Guid),
                oldType: "uuid");

            migrationBuilder.AlterColumn<string>(
                name: "ProviderReference",
                table: "payment_ledger_entries",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character varying(100)",
                oldMaxLength: 100);

            migrationBuilder.AlterColumn<Guid>(
                name: "PaymentId",
                table: "payment_ledger_entries",
                type: "uuid",
                nullable: true,
                oldClrType: typeof(Guid),
                oldType: "uuid");

            migrationBuilder.AddColumn<Guid>(
                name: "WithdrawalRequestId",
                table: "payment_ledger_entries",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "withdrawal_requests",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Amount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    Status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    RequestedByUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    ProcessedByPlatformUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ProcessedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_withdrawal_requests", x => x.Id);
                    table.CheckConstraint("CK_withdrawal_requests_Amount", "\"Amount\" > 0");
                    table.CheckConstraint("CK_withdrawal_requests_Status", "\"Status\" IN ('requested', 'paid', 'rejected')");
                    table.ForeignKey(
                        name: "FK_withdrawal_requests_platform_users_ProcessedByPlatformUserId",
                        column: x => x.ProcessedByPlatformUserId,
                        principalTable: "platform_users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_withdrawal_requests_tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_withdrawal_requests_users_RequestedByUserId",
                        column: x => x.RequestedByUserId,
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "withdrawal_routes",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    WithdrawalRequestId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_withdrawal_routes", x => x.Id);
                    table.ForeignKey(
                        name: "FK_withdrawal_routes_tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_withdrawal_routes_withdrawal_requests_WithdrawalRequestId",
                        column: x => x.WithdrawalRequestId,
                        principalTable: "withdrawal_requests",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_payment_ledger_entries_WithdrawalRequestId",
                table: "payment_ledger_entries",
                column: "WithdrawalRequestId",
                unique: true);

            migrationBuilder.AddCheckConstraint(
                name: "CK_payment_ledger_entries_EntryType",
                table: "payment_ledger_entries",
                sql: "\"EntryType\" IN ('payment_credit', 'withdrawal_debit')");

            migrationBuilder.AddCheckConstraint(
                name: "CK_payment_ledger_entries_Source",
                table: "payment_ledger_entries",
                sql: "(\"EntryType\" = 'payment_credit' AND \"PaymentId\" IS NOT NULL AND \"TransactionId\" IS NOT NULL AND \"WithdrawalRequestId\" IS NULL AND \"ProviderReference\" IS NOT NULL) OR (\"EntryType\" = 'withdrawal_debit' AND \"PaymentId\" IS NULL AND \"TransactionId\" IS NULL AND \"WithdrawalRequestId\" IS NOT NULL)");

            migrationBuilder.CreateIndex(
                name: "IX_withdrawal_requests_ProcessedByPlatformUserId",
                table: "withdrawal_requests",
                column: "ProcessedByPlatformUserId");

            migrationBuilder.CreateIndex(
                name: "IX_withdrawal_requests_RequestedByUserId",
                table: "withdrawal_requests",
                column: "RequestedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_withdrawal_requests_TenantId",
                table: "withdrawal_requests",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_withdrawal_requests_TenantId_Status_CreatedAt",
                table: "withdrawal_requests",
                columns: new[] { "TenantId", "Status", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_withdrawal_routes_TenantId",
                table: "withdrawal_routes",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_withdrawal_routes_TenantId_CreatedAt",
                table: "withdrawal_routes",
                columns: new[] { "TenantId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_withdrawal_routes_WithdrawalRequestId",
                table: "withdrawal_routes",
                column: "WithdrawalRequestId",
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_payment_ledger_entries_withdrawal_requests_WithdrawalReques~",
                table: "payment_ledger_entries",
                column: "WithdrawalRequestId",
                principalTable: "withdrawal_requests",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_payment_ledger_entries_withdrawal_requests_WithdrawalReques~",
                table: "payment_ledger_entries");

            migrationBuilder.DropTable(
                name: "withdrawal_routes");

            migrationBuilder.DropTable(
                name: "withdrawal_requests");

            migrationBuilder.DropIndex(
                name: "IX_payment_ledger_entries_WithdrawalRequestId",
                table: "payment_ledger_entries");

            migrationBuilder.DropCheckConstraint(
                name: "CK_payment_ledger_entries_EntryType",
                table: "payment_ledger_entries");

            migrationBuilder.DropCheckConstraint(
                name: "CK_payment_ledger_entries_Source",
                table: "payment_ledger_entries");

            migrationBuilder.DropColumn(
                name: "WithdrawalRequestId",
                table: "payment_ledger_entries");

            migrationBuilder.AlterColumn<Guid>(
                name: "TransactionId",
                table: "payment_ledger_entries",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"),
                oldClrType: typeof(Guid),
                oldType: "uuid",
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "ProviderReference",
                table: "payment_ledger_entries",
                type: "character varying(100)",
                maxLength: 100,
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "character varying(100)",
                oldMaxLength: 100,
                oldNullable: true);

            migrationBuilder.AlterColumn<Guid>(
                name: "PaymentId",
                table: "payment_ledger_entries",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"),
                oldClrType: typeof(Guid),
                oldType: "uuid",
                oldNullable: true);

            migrationBuilder.AddCheckConstraint(
                name: "CK_payment_ledger_entries_EntryType",
                table: "payment_ledger_entries",
                sql: "\"EntryType\" IN ('payment_credit')");
        }
    }
}
