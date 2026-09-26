using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NeverfadePos.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddDemoConversionEvents : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "demo_conversion_events",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    SessionId = table.Column<Guid>(type: "uuid", nullable: false),
                    BusinessType = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    EventName = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    Mode = table.Column<string>(type: "character varying(12)", maxLength: 12, nullable: true),
                    Step = table.Column<string>(type: "character varying(48)", maxLength: 48, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_demo_conversion_events", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_demo_conversion_events_CreatedAt_EventName",
                table: "demo_conversion_events",
                columns: new[] { "CreatedAt", "EventName" });

            migrationBuilder.CreateIndex(
                name: "IX_demo_conversion_events_SessionId_CreatedAt",
                table: "demo_conversion_events",
                columns: new[] { "SessionId", "CreatedAt" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "demo_conversion_events");
        }
    }
}
