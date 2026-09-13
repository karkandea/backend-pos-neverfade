using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NeverfadePos.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddHostedXenditCheckout : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "CheckoutUrl",
                table: "payments",
                type: "character varying(2000)",
                maxLength: 2000,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ProviderSessionId",
                table: "payments",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "ProviderPaymentRequestId",
                table: "payment_routes",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character varying(100)",
                oldMaxLength: 100);

            migrationBuilder.AddColumn<string>(
                name: "ProviderReferenceId",
                table: "payment_routes",
                type: "character varying(255)",
                maxLength: 255,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ProviderSessionId",
                table: "payment_routes",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_payments_ProviderSessionId",
                table: "payments",
                column: "ProviderSessionId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_payment_routes_Provider_ProviderReferenceId",
                table: "payment_routes",
                columns: new[] { "Provider", "ProviderReferenceId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_payment_routes_Provider_ProviderSessionId",
                table: "payment_routes",
                columns: new[] { "Provider", "ProviderSessionId" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_payments_ProviderSessionId",
                table: "payments");

            migrationBuilder.DropIndex(
                name: "IX_payment_routes_Provider_ProviderReferenceId",
                table: "payment_routes");

            migrationBuilder.DropIndex(
                name: "IX_payment_routes_Provider_ProviderSessionId",
                table: "payment_routes");

            migrationBuilder.DropColumn(
                name: "CheckoutUrl",
                table: "payments");

            migrationBuilder.DropColumn(
                name: "ProviderSessionId",
                table: "payments");

            migrationBuilder.DropColumn(
                name: "ProviderReferenceId",
                table: "payment_routes");

            migrationBuilder.DropColumn(
                name: "ProviderSessionId",
                table: "payment_routes");

            migrationBuilder.AlterColumn<string>(
                name: "ProviderPaymentRequestId",
                table: "payment_routes",
                type: "character varying(100)",
                maxLength: 100,
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "character varying(100)",
                oldMaxLength: 100,
                oldNullable: true);
        }
    }
}
