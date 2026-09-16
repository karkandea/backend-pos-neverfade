using System;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using NeverfadePos.Api.Data;

#nullable disable

namespace NeverfadePos.Api.Migrations;

[DbContext(typeof(AppDbContext))]
[Migration("20260916100000_AddOutletWhatsappArchitecture")]
public partial class AddOutletWhatsappArchitecture : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "outlets",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uuid", nullable: false),
                Code = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                Address = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                Phone = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                IsDefault = table.Column<bool>(type: "boolean", nullable: false),
                Active = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_outlets", x => x.Id);
                table.ForeignKey(
                    name: "FK_outlets_tenants_TenantId",
                    column: x => x.TenantId,
                    principalTable: "tenants",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateIndex(
            name: "IX_outlets_TenantId",
            table: "outlets",
            column: "TenantId");

        migrationBuilder.CreateIndex(
            name: "IX_outlets_TenantId_Code",
            table: "outlets",
            columns: new[] { "TenantId", "Code" },
            unique: true);

        migrationBuilder.CreateIndex(
            name: "IX_outlets_TenantId_IsDefault",
            table: "outlets",
            columns: new[] { "TenantId", "IsDefault" },
            unique: true,
            filter: "\"IsDefault\" = TRUE");

        migrationBuilder.Sql(
            """
            INSERT INTO outlets
                ("Id", "Code", "Name", "Address", "Phone", "IsDefault", "Active", "TenantId", "CreatedAt")
            SELECT
                md5('neverfade-default-outlet:' || t."Id"::text)::uuid,
                'MAIN',
                t."NamaToko",
                '',
                '',
                TRUE,
                TRUE,
                t."Id",
                NOW()
            FROM tenants t
            WHERE NOT EXISTS (
                SELECT 1
                FROM outlets o
                WHERE o."TenantId" = t."Id"
            );
            """);

        migrationBuilder.CreateTable(
            name: "whatsapp_senders",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uuid", nullable: false),
                OutletId = table.Column<Guid>(type: "uuid", nullable: false),
                Provider = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                SessionName = table.Column<string>(type: "character varying(220)", maxLength: 220, nullable: false),
                DisplayName = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                PhoneNumber = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                LastStatus = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                IsDefault = table.Column<bool>(type: "boolean", nullable: false),
                Active = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                LastConnectedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_whatsapp_senders", x => x.Id);
                table.ForeignKey(
                    name: "FK_whatsapp_senders_outlets_OutletId",
                    column: x => x.OutletId,
                    principalTable: "outlets",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
                table.ForeignKey(
                    name: "FK_whatsapp_senders_tenants_TenantId",
                    column: x => x.TenantId,
                    principalTable: "tenants",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateIndex(
            name: "IX_whatsapp_senders_OutletId",
            table: "whatsapp_senders",
            column: "OutletId");

        migrationBuilder.CreateIndex(
            name: "IX_whatsapp_senders_SessionName",
            table: "whatsapp_senders",
            column: "SessionName",
            unique: true);

        migrationBuilder.CreateIndex(
            name: "IX_whatsapp_senders_TenantId",
            table: "whatsapp_senders",
            column: "TenantId");

        migrationBuilder.CreateIndex(
            name: "IX_whatsapp_senders_TenantId_OutletId_IsDefault",
            table: "whatsapp_senders",
            columns: new[] { "TenantId", "OutletId", "IsDefault" },
            unique: true,
            filter: "\"IsDefault\" = TRUE AND \"Active\" = TRUE");

        migrationBuilder.AddColumn<Guid>(
            name: "OutletId",
            table: "transactions",
            type: "uuid",
            nullable: true);

        migrationBuilder.Sql(
            """
            UPDATE transactions tr
            SET "OutletId" = o."Id"
            FROM outlets o
            WHERE tr."TenantId" = o."TenantId"
              AND o."IsDefault" = TRUE
              AND tr."OutletId" IS NULL;
            """);

        migrationBuilder.CreateIndex(
            name: "IX_transactions_OutletId",
            table: "transactions",
            column: "OutletId");

        migrationBuilder.AddForeignKey(
            name: "FK_transactions_outlets_OutletId",
            table: "transactions",
            column: "OutletId",
            principalTable: "outlets",
            principalColumn: "Id",
            onDelete: ReferentialAction.Restrict);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropForeignKey(
            name: "FK_transactions_outlets_OutletId",
            table: "transactions");

        migrationBuilder.DropTable(
            name: "whatsapp_senders");

        migrationBuilder.DropIndex(
            name: "IX_transactions_OutletId",
            table: "transactions");

        migrationBuilder.DropColumn(
            name: "OutletId",
            table: "transactions");

        migrationBuilder.DropTable(
            name: "outlets");
    }
}
