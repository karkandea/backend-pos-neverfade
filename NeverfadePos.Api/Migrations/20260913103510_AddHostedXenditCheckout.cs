using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using NeverfadePos.Api.Data;

namespace NeverfadePos.Api.Migrations;

/// <summary>
/// Reconciles a migration applied to production on 2026-09-13 from a hosted SQL
/// script, but absent from the original source history. Preserve its exact ID
/// and database operations for idempotent rollout and clean-install parity.
/// These legacy provider fields deliberately remain unmapped by the current
/// QRIS-only runtime model; never drop them while old releases can roll back.
/// </summary>
[DbContext(typeof(AppDbContext))]
[Migration("20260913103510_AddHostedXenditCheckout")]
public sealed class AddHostedXenditCheckout : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("ALTER TABLE payments ADD \"CheckoutUrl\" character varying(2000);");
        migrationBuilder.Sql("ALTER TABLE payments ADD \"ProviderSessionId\" character varying(100);");
        migrationBuilder.Sql("ALTER TABLE payment_routes ALTER COLUMN \"ProviderPaymentRequestId\" DROP NOT NULL;");
        migrationBuilder.Sql("ALTER TABLE payment_routes ADD \"ProviderReferenceId\" character varying(255);");
        migrationBuilder.Sql("ALTER TABLE payment_routes ADD \"ProviderSessionId\" character varying(100);");
        migrationBuilder.Sql("CREATE UNIQUE INDEX \"IX_payments_ProviderSessionId\" ON payments (\"ProviderSessionId\");");
        migrationBuilder.Sql("CREATE UNIQUE INDEX \"IX_payment_routes_Provider_ProviderReferenceId\" ON payment_routes (\"Provider\", \"ProviderReferenceId\");");
        migrationBuilder.Sql("CREATE UNIQUE INDEX \"IX_payment_routes_Provider_ProviderSessionId\" ON payment_routes (\"Provider\", \"ProviderSessionId\");");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("DROP INDEX IF EXISTS \"IX_payment_routes_Provider_ProviderSessionId\";");
        migrationBuilder.Sql("DROP INDEX IF EXISTS \"IX_payment_routes_Provider_ProviderReferenceId\";");
        migrationBuilder.Sql("DROP INDEX IF EXISTS \"IX_payments_ProviderSessionId\";");
        migrationBuilder.Sql("ALTER TABLE payment_routes DROP COLUMN IF EXISTS \"ProviderSessionId\";");
        migrationBuilder.Sql("ALTER TABLE payment_routes DROP COLUMN IF EXISTS \"ProviderReferenceId\";");
        migrationBuilder.Sql("ALTER TABLE payment_routes ALTER COLUMN \"ProviderPaymentRequestId\" SET NOT NULL;");
        migrationBuilder.Sql("ALTER TABLE payments DROP COLUMN IF EXISTS \"ProviderSessionId\";");
        migrationBuilder.Sql("ALTER TABLE payments DROP COLUMN IF EXISTS \"CheckoutUrl\";");
    }
}
