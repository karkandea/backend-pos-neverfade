>>> MULAI

# CONTRACT — neverfade-pos-backend
FROZEN — JANGAN EDIT. Ini sumber kebenaran permanen. Kalau ada yg ngerasa perlu ngubah, STOP & lapor user (overseer).

## KONTEKS
Backend untuk frontend POS vanilla JS yg udah jadi. FE manggil REST API via fetch ke BASE url (override lewat window.NF_API_URL).
JANGAN ubah kontrak API. Nama field JSON harus PERSIS (camelCase), beda dikit FE pecah.

## STACK & ATURAN
- .NET 10, ASP.NET Core Web API, LAYERED: Controllers / Services / Data / DTOs / Auth. NO Repository Pattern, EF langsung.
- EF Core + Npgsql → Supabase (Postgres biasa, BUKAN Supabase Auth, BUKAN RLS).
- Connection string dari User Secrets (dev) / Env (prod). JANGAN hardcode, JANGAN ke appsettings, JANGAN commit.
- Auth: JWT bikinan sendiri + BCrypt. JSON camelCase ON. CORS allow all (dev).
- Multi-tenant: SEMUA tabel ada TenantId. JWT bawa claim tenant_id. EF GLOBAL QUERY FILTER, tenant dari token bukan client.
- PK semua uuid (Guid). Business code (kode produk, no trx) = string unik PER-TENANT.
- Skip foto absensi (kolom nullable). Belum ada register. Seed 1 tenant demo.

## AUTH
- POST /api/auth/login {username,password} → cari user by username (GLOBAL UNIK), verify BCrypt → {token, user:{id,nama,username,role}}. JWT claim: sub=userId, tenant_id, role, nama, username.
- GET /api/auth/me → {id,nama,username,role} dari token. 401 kalau invalid.
- Role: owner | admin | kasir. ENFORCE di server: kasir DILARANG (403) akses users, settings(PUT), karyawan, absensi. owner & admin full.

## ENTITIES (field JSON yg dilihat FE)
tenant      : id, namaToko, slug, createdAt
user        : id, nama, username, passwordHash(JANGAN dikirim ke client), role, active, createdAt
settings    : (1 row/tenant) namaToko, alamat, telepon, email, website, headerStruk, footerStruk, showTax(bool), showPoint(bool), defaultTax, minStok, poinRate
product     : id, kode, barcode, nama, kategori, hargaModal, hargaJual, stok, supplier, satuan, deskripsi, createdAt
stockHistory: id, produkId, produkNama, tipe, jumlah, stokAkhir, keterangan, tanggal, user
customer    : id, nama, hp, email, alamat, poin, totalTransaksi, createdAt
transaction : id, noTrx, tanggal, kasir, kasirId, customerId, customerNama, items[], subtotal, disc, tax, discAmt, taxAmt, total, metodePembayaran, dibayar, kembalian
  trxItem (tabel sendiri, DIKIRIM sbg array "items"): {id(=produkId), nama, hargaJual, qty, subtotal}
karyawan    : id, nama, jabatan, telepon, email, gaji, tanggalMasuk, status, catatan
absensi     : id, karyawanId, tanggal, checkIn, checkOut (+ GET join: karyawanNama, jabatan)

## ENDPOINTS (prefix /api, semua butuh JWT kecuali login)
AUTH
  POST /auth/login · GET /auth/me
SETTINGS
  GET /settings → settings · PUT /settings [owner/admin] → {ok:true}
PRODUCTS
  GET /products?search=&kategori= · GET /products/:id · POST /products (kode unik per-tenant) · PUT /products/:id · DELETE /products/:id
STOCK HISTORY
  GET /stock-history?produkId= (terbaru dulu)
  POST /stock-history {produkId,tipe,jumlah,stokFinal,keterangan}
    tipe masuk → stokAkhir=stok+jumlah | keluar → stok-jumlah | penyesuaian → stokAkhir=stokFinal, jumlah=stokFinal-stok
    stok TIDAK boleh negatif. Update product.stok + insert history. → {id,stokAkhir,jumlah}
CUSTOMERS
  GET /customers?search= · GET /customers/:id · POST /customers (nama&hp wajib) · PUT /customers/:id · DELETE /customers/:id
TRANSACTIONS
  GET /transactions?search=&startDate=&endDate= (terbaru dulu) · GET /transactions/:id (+items[])
  POST /transactions {customerId, items[{id,nama,hargaJual,qty,subtotal}], subtotal,disc,tax,discAmt,taxAmt,total,metodePembayaran,dibayar,kembalian}
    → 1 DB TRANSACTION (atomic):
      1. noTrx = "TRX-YYYYMMDD-0001" (sequence harian per-tenant)
      2. insert transaction + trx_items
      3. tiap item: product.stok -= qty (clamp min 0) + insert stock_history tipe="transaksi", ket="Transaksi {noTrx}"
      4. kalau ada customerId: poin += floor(total/1000)*poinRate ; totalTransaksi += 1
    → {id, noTrx, total}. items kosong → error.
USERS [owner/admin]
  GET /users (TANPA password) · POST /users {nama,username,password,role} (username unik) · PUT /users/:id {nama,username,role,active,password?} · DELETE /users/:id (gak boleh hapus diri sendiri)
KARYAWAN [owner/admin]
  GET /karyawan?search=&status= · POST (nama&jabatan wajib) · PUT /:id · DELETE /:id
ABSENSI [owner/admin]
  GET /absensi?karyawanId=&tanggal=&startDate=&endDate= (join karyawanNama,jabatan; terbaru dulu)
  POST /absensi/checkin {karyawanId,foto(abaikan)} → upsert hari ini, checkIn=jam skrg → {ok:true,checkIn,fotoUrl:null}
  POST /absensi/checkout {karyawanId,foto(abaikan)} → checkOut=jam skrg → {ok:true,checkOut,fotoUrl:null}
  tanggal "YYYY-MM-DD", jam "HH:mm".
LAPORAN
  GET /laporan/summary?period=harian|mingguan|bulanan|tahunan → {omzet,transaksi,avg,pelanggan}
  GET /laporan/chart → 7 hari [{date:"YYYY-MM-DD",label:"Sen",total}]
  GET /laporan/top-products?period=... → [{nama,qty,revenue}] sort qty desc, top 10
  period→mulai: harian=hari ini, mingguan=7hr lalu, bulanan=awal bulan, tahunan=awal tahun.

## SEED (1 tenant demo, samain biar FE langsung jalan)
tenant: namaToko="WARUNG LUMPIA BEEF", slug="warung-lumpia-beef"
users (BCrypt): owner/owner123 (owner,"Administrator") · admin/admin123 (admin,"Admin Toko") · kasir/kasir123 (kasir,"Kasir Utama")
settings: namaToko sama, alamat "Jl. Kuliner No.1, Kota Anda", telepon "081234567890", email "info@lumpiabeef.id", headerStruk "Terima kasih telah berkunjung!", footerStruk "Barang yang sudah dibeli tidak dapat dikembalikan.", showTax false, showPoint true, defaultTax 0, minStok 5, poinRate 1
produk (10): Lumpia Beef Original/Pedas, Lumpia Ubi Ungu, Burger Beef Klasik/Double, Burger Crispy Chicken, Paket Hemat 3 Lumpia, Combo Burger+Lumpia, Es Teh Manis, Es Jeruk Peras (kategori Lumpia/Burger/Paket/Minuman, harga & stok wajar)
customer (3): Budi Santoso(150), Siti Rahma(80), Ahmad Fauzi(200)
karyawan (4): Dewi Safitri(Kasir), Budi Santoso(Staff Gudang), Sari Indah(Kasir), Rizki Pratama(Supervisor) — aktif

## PAGAR KERAS (langgar = lapor overseer dulu)
JSON camelCase · PK Guid · TenantId semua tabel · global query filter (tenant dari JWT) · JWT claim {sub,tenant_id,role,username,nama} · DTO no PasswordHash · no hardcode connstring · Controller→Service→EF (no repository) · IgnoreQueryFilters() HANYA di AuthService.

<<< SELESAI