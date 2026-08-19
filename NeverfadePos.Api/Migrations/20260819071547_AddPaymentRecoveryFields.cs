using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NeverfadePos.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddPaymentRecoveryFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "ExpiresAt",
                table: "payments",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "QrString",
                table: "payments",
                type: "text",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ExpiresAt",
                table: "payments");

            migrationBuilder.DropColumn(
                name: "QrString",
                table: "payments");
        }
    }
}
