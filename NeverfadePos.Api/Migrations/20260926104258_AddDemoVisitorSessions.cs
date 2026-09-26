using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NeverfadePos.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddDemoVisitorSessions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "demo_visitor_sessions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CookieHash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    BusinessType = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    LastSeenAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ExpiresAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_demo_visitor_sessions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_demo_visitor_sessions_tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_demo_visitor_sessions_CookieHash_BusinessType",
                table: "demo_visitor_sessions",
                columns: new[] { "CookieHash", "BusinessType" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_demo_visitor_sessions_ExpiresAt",
                table: "demo_visitor_sessions",
                column: "ExpiresAt");

            migrationBuilder.CreateIndex(
                name: "IX_demo_visitor_sessions_TenantId",
                table: "demo_visitor_sessions",
                column: "TenantId",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "demo_visitor_sessions");
        }
    }
}
