# Neverfade POS — Full QA Regression Report

**Tanggal:** 5 Agustus 2026  
**Backend branch:** `qa/full-regression`  
**Frontend branch:** `qa/full-regression`  
**Status akhir:** **FAILED — belum direkomendasikan untuk production**

## 1. Executive Summary

Audit mencakup backend API, database behavior, frontend UI, role authorization, responsive viewport, build production, dan backend Docker runtime.

Hasil utama:

- Backend regression: **204 PASS, 7 defect assertions**
- Frontend E2E: **24 PASS, 12 intentional SKIP, 0 unexpected failure**
- Role authorization: **6/6 PASS**
- Backend Docker smoke: **16/16 PASS**
- Backend build: **PASS**
- Frontend production build: **PASS**
- Frontend lint: **FAIL — 8 errors, 4 warnings**
- Confirmed product defects: **7**
- Dependency warning: `npm` melaporkan **4 high-severity vulnerabilities**, belum ditriage lebih lanjut.

Tiga tanda silang pada hasil Playwright adalah expected failures untuk defect yang sudah diketahui:

- `/api/users` tidak tersedia
- halaman riwayat transaksi masih statis
- halaman laporan masih statis

Ketiganya bukan unexpected test failure.

## 2. Confirmed Defects

### BUG-001 — High — User Management API tidak tersedia

Frontend menyediakan halaman Pengguna dan memanggil:

- `GET /api/users`
- `POST /api/users`
- `PUT /api/users/{id}`
- `DELETE /api/users/{id}`

Backend tidak memiliki controller/service endpoint tersebut dan merespons HTTP `404`.

**Dampak:** owner/admin tidak dapat mengelola akun pengguna melalui aplikasi.

---

### BUG-002 — Medium — Check-in dengan karyawan tidak valid menghasilkan HTTP 500

Request check-in menggunakan `karyawanId` acak/nonexistent menghasilkan HTTP `500`, bukan validasi aman `400` atau `404`.

**Dampak:** invalid input dapat menyebabkan internal server error.

---

### BUG-003 — High — Nilai transaksi dapat dimanipulasi dari client

Backend menerima nilai client untuk:

- `hargaJual`
- `subtotal`
- `total`
- nilai finansial terkait

Produk dengan harga asli Rp20.000 berhasil ditransaksikan dengan total Rp1. Transaksi tersimpan dan stok tetap berkurang.

**Dampak:** integritas pendapatan dan transaksi tidak aman. Semua nilai finansial seharusnya dihitung ulang dari data backend.

---

### BUG-004 — Medium — Customer ID tidak valid diterima diam-diam

Checkout dengan `customerId` acak/nonexistent menghasilkan HTTP `200`. Transaksi disimpan sebagai transaksi tanpa pelanggan dan `customerId` menjadi `null`.

**Dampak:** kesalahan referensi customer tidak terdeteksi dan data transaksi menjadi tidak sesuai request.

---

### BUG-005 — Medium — Chart laporan memakai tanggal UTC, bukan kalender WIB

Summary laporan harian menghitung transaksi dengan benar, tetapi chart tujuh hari mengelompokkan data berdasarkan `CreatedAt.Date` UTC.

Contoh hasil:

- Expected omzet chart tanggal aktif: Rp65.001
- Actual: Rp0
- Summary harian tetap benar: Rp65.001 dari 4 transaksi

**Dampak:** chart dapat menampilkan transaksi pada hari yang salah atau tidak menampilkannya sama sekali untuk transaksi di sekitar pergantian hari WIB.

---

### BUG-007 — High — Halaman Riwayat Transaksi belum terhubung ke backend

`TransaksiPage.tsx` masih statis:

- tidak memanggil `/api/transactions`
- search tidak memiliki handler
- tombol Export tidak memiliki handler
- tabel selalu menampilkan `Belum ada data`

Backend sebenarnya memiliki transaksi yang tersimpan.

**Dampak:** pengguna tidak dapat melihat, mencari, atau mengekspor riwayat transaksi dari frontend.

---

### BUG-008 — High — Halaman Laporan belum terhubung ke backend

`LaporanPage.tsx` masih statis:

- tidak memanggil endpoint laporan
- tombol Generate tidak memiliki handler
- grafik tidak dirender
- seluruh ringkasan selalu Rp0/0

Backend report API tersedia dan menghasilkan data.

**Dampak:** fitur laporan utama tidak berfungsi pada halaman frontend.

## 3. Quality and Technical Findings

### Frontend lint

`npm run lint` menghasilkan:

- **8 errors**
- **4 warnings**

Temuan mencakup:

- penggunaan explicit `any`
- dependency `useEffect`
- function yang diakses sebelum deklarasi
- React hook immutability warning

### Frontend dependency audit

Saat instalasi Playwright, `npm` melaporkan:

- **4 high-severity vulnerabilities**

Detail dependency belum dianalisis dengan `npm audit`.

### Frontend deployment artifact

Frontend repository belum memiliki Dockerfile atau konfigurasi container. Docker smoke test hanya dapat dilakukan untuk backend.

### Candidate yang dibatalkan

Credential demo berikut terbukti valid:

- `admin / admin123`
- `kasir / kasir123`

Admin dan kasir berhasil login dan role guard frontend berfungsi pada Desktop, Tablet, dan Mobile.

## 4. Backend Regression Results

| Suite | Pass | Fail | Status |
|---|---:|---:|---|
| Runtime/auth/read baseline | 28 | 1 | Defect ditemukan |
| Product dan stock | 29 | 0 | PASS |
| Customer | 16 | 0 | PASS |
| Karyawan dan absensi | 30 | 1 | Defect ditemukan |
| Normal transaction | 29 | 0 | PASS |
| Transaction integrity | 9 | 4 | Defect ditemukan |
| Settings | 17 | 0 | PASS |
| Laporan | 46 | 1 | Defect ditemukan |
| **Total** | **204** | **7** | Defect assertions confirmed |

## 5. Frontend E2E Results

Final complete Playwright run:

- Total discovered: **36**
- Passed: **24**
- Intentional skipped: **12**
- Unexpected failed: **0**
- Expected known-defect checks: **3**
- Browser engine: Chromium
- Workers: 1

Viewports:

- Desktop: 1440 × 1000
- Tablet: 820 × 1180
- Mobile: 390 × 844

Validated flows:

- unauthenticated redirect
- owner login
- admin login
- kasir login
- logout/session removal
- protected route navigation
- product create/edit/search/delete
- customer create/edit/search/delete
- QRIS checkout
- receipt preview
- owner/admin page access
- kasir restricted-page redirect
- Desktop, Tablet, dan Mobile route rendering

## 6. Docker Smoke Results

Backend Docker smoke test:

- Image build: PASS
- Container startup: PASS
- Unauthorized contract: PASS
- Owner login: PASS
- `/api/auth/me`: PASS
- `/api/products`: PASS
- `/api/settings`: PASS
- `/api/laporan/summary`: PASS
- Response structure checks: PASS
- Fatal container log check: PASS

**Summary: 16 PASS, 0 FAIL**

Image:

`neverfade-pos-backend:qa-smoke`

Runtime port:

`127.0.0.1:8080`

Container otomatis dihapus setelah test.

## 7. Permanent QA Data

### Product-stock regression

- Product code: `QA_20260729_101923_STOCK`
- Product ID: `1617738c-c022-42fb-8b0f-f53240a13646`
- Recorded final stock after backend suite: `7`

### Normal transaction regression

- Product code: `QA_20260729_110749_TRX`
- Product ID: `65b9bb2f-97e7-4425-85b4-d56b959383af`
- Initial recorded final stock after backend suite: `7`
- Reused by frontend QRIS E2E, sehingga stok saat ini dapat lebih rendah.

Transactions:

- `TRX-20260729-0001`
- `TRX-20260729-0002`

### Transaction integrity regression

- Product code: `QA_20260729_111756_INTEGRITY`
- Product ID: `2b5bf6ba-d39c-48ca-878a-eaaab7acd611`
- Recorded final stock: `3`

Transactions:

- Manipulated monetary transaction: `TRX-20260729-0003`
- Unknown-customer transaction: `TRX-20260729-0004`

Frontend E2E juga membuat beberapa transaksi QRIS tambahan menggunakan product fixture transaksi.

## 8. Evidence Locations

External result directory:

`~/neverfade-pos-qa`

Important evidence:

- `product-stock-result.env`
- `transaction-result.env`
- `transaction-integrity-result.env`
- `laporan-result.txt`
- `playwright-result.json`
- `frontend-e2e.log`
- `docker-build.log`
- `docker-container.log`
- `docker-*.json`

Frontend HTML report:

`~/neverfade-pos-frontend/playwright-report/index.html`

Frontend failure evidence for known defects:

`~/neverfade-pos-frontend/test-results`

## 9. Release Recommendation

**Tidak direkomendasikan untuk production sebelum minimal BUG-003 diperbaiki.**

Prioritas perbaikan:

1. Hitung ulang seluruh harga dan total transaksi di backend.
2. Implementasikan backend `/api/users`.
3. Hubungkan halaman Transaksi ke backend.
4. Hubungkan halaman Laporan ke backend.
5. Validasi `customerId` dan `karyawanId` sebelum penyimpanan.
6. Ubah grouping chart laporan dari UTC date ke kalender Asia/Jakarta.
7. Selesaikan lint error dan triage dependency vulnerabilities.

Setelah perbaikan, jalankan seluruh QA suite kembali pada branch khusus perbaikan.
