# HANDOFF — neverfade-pos-backend
keluar v3 → masuk v4   |   2026-06-28
commit: 26d2fc9 (main)  |  build: ijo  |  migration: ok  |  seeder: ok

== 0. BOOT — lakuin SEBELUM percaya apapun di doc ini ==
doc ini PETA, bukan kebenaran. kebenaran = code + git + build.

1. git pull
2. dotnet user-secrets set "ConnectionStrings:DefaultConnection" "{connstring}"
3. cd NeverfadePos.Api
4. dotnet build
5. dotnet ef database update
6. dotnet run
7. jalanin VERIFY (sek. 5)

Kalau ada step yang merah → STOP, jangan numpuk fitur baru.

== 1. DONE — kebukti di code ==
- [x] Backend foundation — project, EF Core, JWT, middleware.
- [x] Multi-tenant foundation — CurrentUser + Global Query Filter.
- [x] Authentication — login + me.
- [x] Product CRUD — full CRUD + DTO projection.
- [x] Validation DTO — DataAnnotations.
- [x] Settings API — GET/PUT + owner/admin authorization.
- [x] Customer CRUD — full CRUD.
- [x] Karyawan CRUD — full CRUD + owner/admin authorization.
- [x] Stock History — masuk, keluar, penyesuaian.
- [x] Seeder — tenant demo, users, settings, products, customers, karyawan.
- [x] InitialCreate migration — seluruh entity sudah dibuat.

== 2. CONTRACT — HARAM diubah ==
Sumber kebenaran = CONTRACT.md.

Jangan ubah contract API, DTO, JSON camelCase, tenant filter, ataupun arsitektur tanpa approval overseer.

== 3. NEXT — urut prioritas ==
1. Transaction (STOP & REPORT T1 ke overseer setelah selesai)
2. Users CRUD
3. Absensi
4. Laporan

Tidak ada modul yang sedang setengah jalan.

== 4. GOTCHAS ==
- IgnoreQueryFilters() HANYA dipakai AuthService.
- Product service pakai alias ProductEntity karena bentrok namespace.
- Semua read pakai AsNoTracking + projection DTO.
- Global Query Filter adalah fondasi isolasi tenant.
- InitialCreate sudah mencakup seluruh entity, jangan bikin migration baru kecuali schema berubah.
- tanggal API = CreatedAt (projection, no migration). Berlaku juga untuk transaction.tanggal. KECUALI absensi.tanggal (tanggal hari kerja, beda).

== 5. VERIFY ==
- file smoke test: test.http
- wajib lolos:
  - login 200
  - me 200
  - no-token 401
  - product CRUD
  - settings GET/PUT
  - customer CRUD
  - karyawan CRUD
  - stock history (masuk, keluar, penyesuaian)
  - isolasi tenant
