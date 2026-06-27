using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NeverfadePos.Api.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "tenants",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    NamaToko = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Slug = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_tenants", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "customers",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Nama = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Hp = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    Email = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Alamat = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    Poin = table.Column<int>(type: "integer", nullable: false),
                    TotalTransaksi = table.Column<int>(type: "integer", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_customers", x => x.Id);
                    table.ForeignKey(
                        name: "FK_customers_tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "karyawans",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Nama = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Jabatan = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Telepon = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    Email = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Gaji = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    TanggalMasuk = table.Column<DateOnly>(type: "date", nullable: false),
                    Status = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    Catatan = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_karyawans", x => x.Id);
                    table.ForeignKey(
                        name: "FK_karyawans_tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "products",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Kode = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    Barcode = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Nama = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Kategori = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    HargaModal = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    HargaJual = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    Stok = table.Column<int>(type: "integer", nullable: false),
                    Supplier = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Satuan = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    Deskripsi = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_products", x => x.Id);
                    table.ForeignKey(
                        name: "FK_products_tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "settings",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    NamaToko = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Alamat = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    Telepon = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    Email = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Website = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    HeaderStruk = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    FooterStruk = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    ShowTax = table.Column<bool>(type: "boolean", nullable: false),
                    ShowPoint = table.Column<bool>(type: "boolean", nullable: false),
                    DefaultTax = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    MinStok = table.Column<int>(type: "integer", nullable: false),
                    PoinRate = table.Column<int>(type: "integer", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_settings", x => x.Id);
                    table.ForeignKey(
                        name: "FK_settings_tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "users",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Nama = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Username = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    PasswordHash = table.Column<string>(type: "text", nullable: false),
                    Role = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Active = table.Column<bool>(type: "boolean", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_users", x => x.Id);
                    table.ForeignKey(
                        name: "FK_users_tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "transactions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    NoTrx = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    Tanggal = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Kasir = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    KasirId = table.Column<Guid>(type: "uuid", nullable: false),
                    CustomerId = table.Column<Guid>(type: "uuid", nullable: true),
                    CustomerNama = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Subtotal = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    Disc = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    Tax = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    DiscAmt = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    TaxAmt = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    Total = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    MetodePembayaran = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    Dibayar = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    Kembalian = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_transactions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_transactions_customers_CustomerId",
                        column: x => x.CustomerId,
                        principalTable: "customers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_transactions_tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "absensis",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    KaryawanId = table.Column<Guid>(type: "uuid", nullable: false),
                    Tanggal = table.Column<DateOnly>(type: "date", nullable: false),
                    CheckIn = table.Column<TimeOnly>(type: "time without time zone", nullable: true),
                    CheckOut = table.Column<TimeOnly>(type: "time without time zone", nullable: true),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_absensis", x => x.Id);
                    table.ForeignKey(
                        name: "FK_absensis_karyawans_KaryawanId",
                        column: x => x.KaryawanId,
                        principalTable: "karyawans",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_absensis_tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "stock_histories",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ProdukId = table.Column<Guid>(type: "uuid", nullable: false),
                    ProdukNama = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Tipe = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    Jumlah = table.Column<int>(type: "integer", nullable: false),
                    StokAkhir = table.Column<int>(type: "integer", nullable: false),
                    Keterangan = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    User = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_stock_histories", x => x.Id);
                    table.ForeignKey(
                        name: "FK_stock_histories_products_ProdukId",
                        column: x => x.ProdukId,
                        principalTable: "products",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_stock_histories_tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "transaction_items",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TransactionId = table.Column<Guid>(type: "uuid", nullable: false),
                    ProductId = table.Column<Guid>(type: "uuid", nullable: false),
                    Nama = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    HargaJual = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    Qty = table.Column<int>(type: "integer", nullable: false),
                    Subtotal = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_transaction_items", x => x.Id);
                    table.ForeignKey(
                        name: "FK_transaction_items_products_ProductId",
                        column: x => x.ProductId,
                        principalTable: "products",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_transaction_items_tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_transaction_items_transactions_TransactionId",
                        column: x => x.TransactionId,
                        principalTable: "transactions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_absensis_KaryawanId",
                table: "absensis",
                column: "KaryawanId");

            migrationBuilder.CreateIndex(
                name: "IX_absensis_TenantId",
                table: "absensis",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_absensis_TenantId_KaryawanId_Tanggal",
                table: "absensis",
                columns: new[] { "TenantId", "KaryawanId", "Tanggal" });

            migrationBuilder.CreateIndex(
                name: "IX_customers_TenantId",
                table: "customers",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_karyawans_TenantId",
                table: "karyawans",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_products_TenantId",
                table: "products",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_products_TenantId_Kode",
                table: "products",
                columns: new[] { "TenantId", "Kode" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_settings_TenantId",
                table: "settings",
                column: "TenantId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_stock_histories_ProdukId",
                table: "stock_histories",
                column: "ProdukId");

            migrationBuilder.CreateIndex(
                name: "IX_stock_histories_TenantId",
                table: "stock_histories",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_stock_histories_TenantId_ProdukId",
                table: "stock_histories",
                columns: new[] { "TenantId", "ProdukId" });

            migrationBuilder.CreateIndex(
                name: "IX_tenants_Slug",
                table: "tenants",
                column: "Slug",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_transaction_items_ProductId",
                table: "transaction_items",
                column: "ProductId");

            migrationBuilder.CreateIndex(
                name: "IX_transaction_items_TenantId",
                table: "transaction_items",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_transaction_items_TransactionId",
                table: "transaction_items",
                column: "TransactionId");

            migrationBuilder.CreateIndex(
                name: "IX_transactions_CustomerId",
                table: "transactions",
                column: "CustomerId");

            migrationBuilder.CreateIndex(
                name: "IX_transactions_TenantId",
                table: "transactions",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_transactions_TenantId_NoTrx",
                table: "transactions",
                columns: new[] { "TenantId", "NoTrx" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_users_TenantId",
                table: "users",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_users_Username",
                table: "users",
                column: "Username",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "absensis");

            migrationBuilder.DropTable(
                name: "settings");

            migrationBuilder.DropTable(
                name: "stock_histories");

            migrationBuilder.DropTable(
                name: "transaction_items");

            migrationBuilder.DropTable(
                name: "users");

            migrationBuilder.DropTable(
                name: "karyawans");

            migrationBuilder.DropTable(
                name: "products");

            migrationBuilder.DropTable(
                name: "transactions");

            migrationBuilder.DropTable(
                name: "customers");

            migrationBuilder.DropTable(
                name: "tenants");
        }
    }
}
