using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NeverfadePos.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddEndgameV2Jobs : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "jobs",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    OutletId = table.Column<Guid>(type: "uuid", nullable: true),
                    ActorUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    Kind = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    State = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    ResultReference = table.Column<Guid>(type: "uuid", nullable: true),
                    CorrelationId = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_jobs", x => x.Id);
                    table.CheckConstraint("CK_jobs_State", "\"State\" IN ('queued', 'running', 'succeeded', 'failed')");
                    table.ForeignKey(
                        name: "FK_jobs_outlets_OutletId",
                        column: x => x.OutletId,
                        principalTable: "outlets",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_jobs_tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_jobs_users_ActorUserId",
                        column: x => x.ActorUserId,
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateIndex(
                name: "IX_jobs_ActorUserId",
                table: "jobs",
                column: "ActorUserId");

            migrationBuilder.CreateIndex(
                name: "IX_jobs_OutletId",
                table: "jobs",
                column: "OutletId");

            migrationBuilder.CreateIndex(
                name: "IX_jobs_TenantId_ActorUserId_CreatedAt_Id",
                table: "jobs",
                columns: new[] { "TenantId", "ActorUserId", "CreatedAt", "Id" });

            migrationBuilder.CreateIndex(
                name: "IX_jobs_TenantId_CreatedAt_Id",
                table: "jobs",
                columns: new[] { "TenantId", "CreatedAt", "Id" });

            migrationBuilder.CreateIndex(
                name: "IX_jobs_TenantId_OutletId_CreatedAt_Id",
                table: "jobs",
                columns: new[] { "TenantId", "OutletId", "CreatedAt", "Id" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "jobs");
        }
    }
}
