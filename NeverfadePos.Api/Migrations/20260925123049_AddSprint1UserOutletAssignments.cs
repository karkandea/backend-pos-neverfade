using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NeverfadePos.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddSprint1UserOutletAssignments : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "user_outlet_assignments",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    OutletId = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_user_outlet_assignments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_user_outlet_assignments_outlets_OutletId",
                        column: x => x.OutletId,
                        principalTable: "outlets",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_user_outlet_assignments_users_UserId",
                        column: x => x.UserId,
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_user_outlet_assignments_OutletId",
                table: "user_outlet_assignments",
                column: "OutletId");

            migrationBuilder.CreateIndex(
                name: "IX_user_outlet_assignments_TenantId_OutletId",
                table: "user_outlet_assignments",
                columns: new[] { "TenantId", "OutletId" });

            migrationBuilder.CreateIndex(
                name: "IX_user_outlet_assignments_TenantId_UserId_OutletId",
                table: "user_outlet_assignments",
                columns: new[] { "TenantId", "UserId", "OutletId" },
                unique: true);

            // Preserve all existing tenant users' previous access on upgrade;
            // new users require explicit assignment and owners retain full access.
            migrationBuilder.Sql("""
                INSERT INTO user_outlet_assignments
                    ("Id", "UserId", "OutletId", "TenantId", "CreatedAt")
                SELECT md5('s1-assignment:' || u."Id"::text || ':' || o."Id"::text)::uuid,
                       u."Id", o."Id", u."TenantId", NOW()
                FROM users u
                JOIN outlets o ON o."TenantId" = u."TenantId"
                WHERE u."Role" <> 'owner'
                ON CONFLICT ("TenantId", "UserId", "OutletId") DO NOTHING;
                """);

            migrationBuilder.CreateIndex(
                name: "IX_user_outlet_assignments_UserId",
                table: "user_outlet_assignments",
                column: "UserId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "user_outlet_assignments");
        }
    }
}
