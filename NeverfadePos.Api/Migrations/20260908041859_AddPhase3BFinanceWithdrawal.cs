using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NeverfadePos.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddPhase3BFinanceWithdrawal : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_withdrawal_requests_Status",
                table: "withdrawal_requests");

            migrationBuilder.DropCheckConstraint(
                name: "CK_platform_audit_events_EventType",
                table: "platform_audit_events");

            migrationBuilder.AddColumn<DateTime>(
                name: "CancelledAt",
                table: "withdrawal_requests",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "DestinationAccountHolderName",
                table: "withdrawal_requests",
                type: "character varying(200)",
                maxLength: 200,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "DestinationAccountNumber",
                table: "withdrawal_requests",
                type: "character varying(50)",
                maxLength: 50,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "DestinationBankName",
                table: "withdrawal_requests",
                type: "character varying(100)",
                maxLength: 100,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "EvidenceMetadata",
                table: "withdrawal_requests",
                type: "character varying(2000)",
                maxLength: 2000,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "ProcessingStartedAt",
                table: "withdrawal_requests",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "RejectionReason",
                table: "withdrawal_requests",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "TransferReference",
                table: "withdrawal_requests",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "withdrawal_bank_accounts",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    BankName = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    AccountNumber = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    AccountHolderName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    VerificationStatus = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    VerifiedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    VerifiedByPlatformUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    VerificationNote = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_withdrawal_bank_accounts", x => x.Id);
                    table.CheckConstraint("CK_withdrawal_bank_accounts_VerificationStatus", "\"VerificationStatus\" IN ('pending', 'verified', 'rejected')");
                    table.ForeignKey(
                        name: "FK_withdrawal_bank_accounts_platform_users_VerifiedByPlatformU~",
                        column: x => x.VerifiedByPlatformUserId,
                        principalTable: "platform_users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_withdrawal_bank_accounts_tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.AddCheckConstraint(
                name: "CK_withdrawal_requests_Status",
                table: "withdrawal_requests",
                sql: "\"Status\" IN ('requested', 'processing', 'paid', 'rejected', 'cancelled')");

            migrationBuilder.AddCheckConstraint(
                name: "CK_platform_audit_events_EventType",
                table: "platform_audit_events",
                sql: "\"EventType\" IN ('TENANT_PROVISIONED', 'TENANT_ACTIVATED', 'TENANT_SUSPENDED', 'TENANT_BUSINESS_PROFILE_CHANGED', 'WITHDRAWAL_BANK_ACCOUNT_VERIFIED', 'WITHDRAWAL_BANK_ACCOUNT_REJECTED', 'WITHDRAWAL_PROCESSING_STARTED', 'WITHDRAWAL_PAID', 'WITHDRAWAL_REJECTED')");

            migrationBuilder.CreateIndex(
                name: "IX_withdrawal_bank_accounts_TenantId",
                table: "withdrawal_bank_accounts",
                column: "TenantId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_withdrawal_bank_accounts_VerificationStatus",
                table: "withdrawal_bank_accounts",
                column: "VerificationStatus");

            migrationBuilder.CreateIndex(
                name: "IX_withdrawal_bank_accounts_VerifiedByPlatformUserId",
                table: "withdrawal_bank_accounts",
                column: "VerifiedByPlatformUserId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "withdrawal_bank_accounts");

            migrationBuilder.DropCheckConstraint(
                name: "CK_withdrawal_requests_Status",
                table: "withdrawal_requests");

            migrationBuilder.DropCheckConstraint(
                name: "CK_platform_audit_events_EventType",
                table: "platform_audit_events");

            migrationBuilder.DropColumn(
                name: "CancelledAt",
                table: "withdrawal_requests");

            migrationBuilder.DropColumn(
                name: "DestinationAccountHolderName",
                table: "withdrawal_requests");

            migrationBuilder.DropColumn(
                name: "DestinationAccountNumber",
                table: "withdrawal_requests");

            migrationBuilder.DropColumn(
                name: "DestinationBankName",
                table: "withdrawal_requests");

            migrationBuilder.DropColumn(
                name: "EvidenceMetadata",
                table: "withdrawal_requests");

            migrationBuilder.DropColumn(
                name: "ProcessingStartedAt",
                table: "withdrawal_requests");

            migrationBuilder.DropColumn(
                name: "RejectionReason",
                table: "withdrawal_requests");

            migrationBuilder.DropColumn(
                name: "TransferReference",
                table: "withdrawal_requests");

            migrationBuilder.AddCheckConstraint(
                name: "CK_withdrawal_requests_Status",
                table: "withdrawal_requests",
                sql: "\"Status\" IN ('requested', 'paid', 'rejected')");

            migrationBuilder.AddCheckConstraint(
                name: "CK_platform_audit_events_EventType",
                table: "platform_audit_events",
                sql: "\"EventType\" IN ('TENANT_PROVISIONED', 'TENANT_ACTIVATED', 'TENANT_SUSPENDED', 'TENANT_BUSINESS_PROFILE_CHANGED')");
        }
    }
}
