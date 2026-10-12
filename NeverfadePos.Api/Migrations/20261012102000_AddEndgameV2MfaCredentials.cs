using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NeverfadePos.Api.Migrations;

public partial class AddEndgameV2MfaCredentials : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "mfa_credentials",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uuid", nullable: false),
                TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                UserId = table.Column<Guid>(type: "uuid", nullable: false),
                ProtectedSecret = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: false),
                EnrollmentExpiresAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                EnabledAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                LastUsedStep = table.Column<long>(type: "bigint", nullable: false),
                FailedAttempts = table.Column<int>(type: "integer", nullable: false),
                LockedUntilUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                RecoveryCodeHashesJson = table.Column<string>(type: "text", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_mfa_credentials", x => x.Id);
                table.ForeignKey(
                    name: "FK_mfa_credentials_users_UserId",
                    column: x => x.UserId,
                    principalTable: "users",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateIndex(
            name: "IX_mfa_credentials_UserId",
            table: "mfa_credentials",
            column: "UserId");

        migrationBuilder.CreateIndex(
            name: "IX_mfa_credentials_TenantId_UserId",
            table: "mfa_credentials",
            columns: new[] { "TenantId", "UserId" },
            unique: true);
    }

    protected override void Down(MigrationBuilder migrationBuilder) =>
        migrationBuilder.DropTable(name: "mfa_credentials");
}
