#!/usr/bin/env bash
set -euo pipefail

REPO="${NF_PHASE3D_BACKEND_REPO:-$HOME/neverfade-pos-backend}"
PROJECT="NeverfadePos.Api/NeverfadePos.Api.csproj"
PREVIOUS_MIGRATION="20260908064352_AddPhase3CFnbRestaurant"
LATEST_MIGRATION="20260908102646_AddPhase3DLaundry"
EXPECTED_HEAD="${NF_PHASE3D_EXPECTED_BACKEND_HEAD:-}"
SDK_IMAGE="${NF_DOTNET_IMAGE:-mcr.microsoft.com/dotnet/sdk:10.0}"
PG_IMAGE="${NF_POSTGRES_IMAGE:-postgres:16-alpine}"
RUN_ID="$(date +%Y%m%d%H%M%S)-$$"
NETWORK="nf-phase3d-laundry-db-$RUN_ID"
PG_CONTAINER="nf-phase3d-laundry-pg-$RUN_ID"
DB_NAME="neverfade_phase3d_laundry"
DB_USER="postgres"
DB_PASSWORD="phase3d-laundry-local-only"
NUGET_VOLUME="neverfade-phase3d-nuget"

fail() { printf '\n[FAIL] %s\n' "$1" >&2; exit 1; }
step() { printf '\n==> %s\n' "$1"; }

cleanup() {
  docker rm -f "$PG_CONTAINER" >/dev/null 2>&1 || true
  docker network rm "$NETWORK" >/dev/null 2>&1 || true
}
trap cleanup EXIT

[[ -d "$REPO/.git" ]] || fail "Backend repo tidak ditemukan."
command -v docker >/dev/null 2>&1 || fail "Docker tidak tersedia."
cd "$REPO"

[[ -z "$(git status --porcelain)" ]] ||
  fail "Backend repo harus clean."
[[ -n "$EXPECTED_HEAD" ]] ||
  fail "Expected backend HEAD wajib diisi."
[[ "$(git rev-parse HEAD)" == "$EXPECTED_HEAD" ]] ||
  fail "Backend HEAD tidak sesuai expected."

step "Create disposable PostgreSQL"
docker network create "$NETWORK" >/dev/null
docker run -d --rm \
  --name "$PG_CONTAINER" \
  --network "$NETWORK" \
  --cpus=0.5 \
  --memory=512m \
  -e POSTGRES_DB="$DB_NAME" \
  -e POSTGRES_USER="$DB_USER" \
  -e POSTGRES_PASSWORD="$DB_PASSWORD" \
  "$PG_IMAGE" >/dev/null

for _ in $(seq 1 60); do
  docker exec "$PG_CONTAINER" \
    pg_isready -U "$DB_USER" -d "$DB_NAME" >/dev/null 2>&1 &&
    break
  sleep 1
done

docker exec "$PG_CONTAINER" \
  pg_isready -U "$DB_USER" -d "$DB_NAME" >/dev/null 2>&1 ||
  fail "PostgreSQL tidak ready."

CONNECTION="Host=$PG_CONTAINER;Port=5432;Database=$DB_NAME;Username=$DB_USER;Password=$DB_PASSWORD"

docker volume inspect "$NUGET_VOLUME" >/dev/null 2>&1 ||
  docker volume create "$NUGET_VOLUME" >/dev/null

run_dotnet() {
  docker run --rm \
    --network "$NETWORK" \
    --cpus=1 \
    --memory=2g \
    -e DOTNET_CLI_TELEMETRY_OPTOUT=1 \
    -e DOTNET_NOLOGO=1 \
    -e NUGET_PACKAGES=/root/.nuget/packages \
    -e "ConnectionStrings__DefaultConnection=$CONNECTION" \
    -v "$NUGET_VOLUME:/root/.nuget/packages" \
    -v "$REPO:/workspace" \
    -w /workspace \
    "$SDK_IMAGE" bash -lc "$1"
}

run_ef() {
  run_dotnet "dotnet restore '$PROJECT' >/dev/null && \
    (dotnet tool install --global dotnet-ef --version 10.0.9 >/dev/null 2>&1 || \
     dotnet tool update --global dotnet-ef --version 10.0.9 >/dev/null 2>&1) && \
    export PATH=\"\$PATH:/root/.dotnet/tools\" && \
    dotnet ef $* \
      --project '$PROJECT' \
      --startup-project '$PROJECT' \
      --context AppDbContext"
}

psql_exec() {
  docker exec -i -e PGPASSWORD="$DB_PASSWORD" "$PG_CONTAINER" \
    psql -v ON_ERROR_STOP=1 -U "$DB_USER" -d "$DB_NAME" "$@"
}

psql_scalar() {
  psql_exec -Atqc "$1"
}

step "Migrate to exact Phase 3C schema"
run_ef "database update $PREVIOUS_MIGRATION"

[[ "$(psql_scalar 'SELECT "MigrationId" FROM "__EFMigrationsHistory" ORDER BY "MigrationId" DESC LIMIT 1;')" == "$PREVIOUS_MIGRATION" ]] ||
  fail "Pre-3D migration history mismatch."

step "Seed representative pre-Phase-3D data"
psql_exec <<'SQL'
INSERT INTO tenants
("Id","NamaToko","Slug","CreatedAt","Status","UpdatedAt","BusinessType")
VALUES
('11111111-1111-1111-1111-111111111111',
 'Legacy Laundry Tenant',
 'legacy-laundry',
 '2026-09-01T00:00:00Z',
 'active',
 '2026-09-01T00:00:00Z',
 'laundry');

INSERT INTO users
("Id","Nama","Username","PasswordHash","Role","Active","TenantId","CreatedAt")
VALUES
('22222222-2222-2222-2222-222222222222',
 'Legacy Owner',
 'legacy-laundry-owner',
 'legacy-hash',
 'owner',
 true,
 '11111111-1111-1111-1111-111111111111',
 '2026-09-01T00:00:00Z');

INSERT INTO customers
("Id","Nama","Hp","Email","Alamat","Poin","TotalTransaksi","TenantId","CreatedAt")
VALUES
('33333333-3333-3333-3333-333333333333',
 'Legacy Customer',
 '081234567890',
 'legacy@example.test',
 'Jakarta',
 0,
 1,
 '11111111-1111-1111-1111-111111111111',
 '2026-09-01T00:00:00Z');

INSERT INTO products
("Id","Kode","Barcode","Nama","Kategori","HargaModal","HargaJual","Stok",
 "Supplier","Satuan","Deskripsi","TenantId","CreatedAt")
VALUES
('44444444-4444-4444-4444-444444444444',
 'LEGACY-GOODS',
 '899000000009',
 'Legacy Detergent',
 'Laundry',
 5000,
 10000,
 17,
 'QA',
 'pcs',
 'Legacy product before Phase 3D',
 '11111111-1111-1111-1111-111111111111',
 '2026-09-01T00:00:00Z');

INSERT INTO transactions
("Id","NoTrx","Tanggal","Kasir","KasirId","CustomerId","CustomerNama",
 "Subtotal","Disc","Tax","DiscAmt","TaxAmt","Total","MetodePembayaran",
 "Dibayar","Kembalian","TenantId","CreatedAt","FinalizedAt","Status")
VALUES
('55555555-5555-5555-5555-555555555555',
 'TRX-LEGACY-P3D',
 '2026-09-01T01:00:00Z',
 'Legacy Owner',
 '22222222-2222-2222-2222-222222222222',
 '33333333-3333-3333-3333-333333333333',
 'Legacy Customer',
 30000,
 0,
 0,
 0,
 0,
 30000,
 'tunai',
 30000,
 0,
 '11111111-1111-1111-1111-111111111111',
 '2026-09-01T01:00:00Z',
 '2026-09-01T01:00:01Z',
 'paid');

INSERT INTO transaction_items
("Id","TransactionId","ProductId","Nama","HargaJual","Qty","Subtotal","TenantId","CreatedAt")
VALUES
('66666666-6666-6666-6666-666666666666',
 '55555555-5555-5555-5555-555555555555',
 '44444444-4444-4444-4444-444444444444',
 'Legacy Detergent',
 10000,
 3,
 30000,
 '11111111-1111-1111-1111-111111111111',
 '2026-09-01T01:00:00Z');
SQL

step "Forward migrate Phase 3D"
run_ef "database update"

[[ "$(psql_scalar 'SELECT "MigrationId" FROM "__EFMigrationsHistory" ORDER BY "MigrationId" DESC LIMIT 1;')" == "$LATEST_MIGRATION" ]] ||
  fail "Latest migration mismatch."

[[ "$(psql_scalar "SELECT COUNT(*) FROM information_schema.tables WHERE table_schema='public' AND table_name IN ('laundry_work_orders','laundry_work_order_items','laundry_work_order_status_history');")" == "3" ]] ||
  fail "Laundry tables incomplete."

legacy_product="$(psql_scalar "SELECT \"Type\" || '|' || \"TracksStock\" || '|' || \"QuantityPrecision\" || '|' || \"Stok\" FROM products WHERE \"Id\"='44444444-4444-4444-4444-444444444444';")"
[[ "$legacy_product" == "goods|true|0|17" ]] ||
  fail "Legacy product backfill mismatch: $legacy_product"

legacy_item="$(psql_scalar "SELECT \"ProductType\" || '|' || \"TracksStock\" || '|' || \"QuantityPrecision\" || '|' || \"Quantity\" || '|' || \"Unit\" FROM transaction_items WHERE \"Id\"='66666666-6666-6666-6666-666666666666';")"
[[ "$legacy_item" == "goods|true|0|3.000|pcs" ]] ||
  fail "Legacy transaction item backfill mismatch: $legacy_item"

transaction_index="$(psql_scalar "SELECT indexdef FROM pg_indexes WHERE schemaname='public' AND tablename='laundry_work_orders' AND indexname='IX_laundry_work_orders_TransactionId';")"
[[ "$transaction_index" == *"UNIQUE INDEX"* && "$transaction_index" == *"TransactionId"* ]] ||
  fail "Laundry transaction unique index missing."

step "Seed valid Phase 3D service and work order"
psql_exec <<'SQL'
INSERT INTO products
("Id","Kode","Barcode","Nama","Kategori","HargaModal","HargaJual","Stok",
 "Supplier","Satuan","Deskripsi","Type","TracksStock","QuantityPrecision",
 "TenantId","CreatedAt")
VALUES
('77777777-7777-7777-7777-777777777777',
 'LDR-SVC-GATE',
 '',
 'Cuci Kering',
 'Laundry',
 0,
 12000,
 0,
 '',
 'kg',
 'Service gate row',
 'service',
 false,
 2,
 '11111111-1111-1111-1111-111111111111',
 '2026-09-08T00:00:00Z');

INSERT INTO laundry_work_orders
("Id","CustomerId","CreatedByUserId","TransactionId","OrderNumber","Status",
 "PaymentStatus","Notes","CancellationReason","PromisedAt","UpdatedAt",
 "PaidAt","CompletedAt","CancelledAt","TenantId","CreatedAt")
VALUES
('88888888-8888-8888-8888-888888888888',
 '33333333-3333-3333-3333-333333333333',
 '22222222-2222-2222-2222-222222222222',
 '55555555-5555-5555-5555-555555555555',
 'LDR-GATE-001',
 'ready',
 'paid',
 '',
 NULL,
 '2026-09-08T12:00:00Z',
 '2026-09-08T10:00:00Z',
 '2026-09-08T10:00:00Z',
 NULL,
 NULL,
 '11111111-1111-1111-1111-111111111111',
 '2026-09-08T08:00:00Z');

INSERT INTO laundry_work_order_items
("Id","LaundryWorkOrderId","ProductId","Nama","ProductType","Unit","Quantity",
 "QuantityPrecision","UnitPrice","Subtotal","TenantId","CreatedAt")
VALUES
('99999999-9999-9999-9999-999999999999',
 '88888888-8888-8888-8888-888888888888',
 '77777777-7777-7777-7777-777777777777',
 'Cuci Kering',
 'service',
 'kg',
 2.5,
 2,
 12000,
 30000,
 '11111111-1111-1111-1111-111111111111',
 '2026-09-08T08:00:00Z');
SQL

[[ "$(psql_scalar "SELECT \"Quantity\" FROM laundry_work_order_items WHERE \"Id\"='99999999-9999-9999-9999-999999999999';")" == "2.500" ]] ||
  fail "Decimal service quantity not persisted."

step "Verify Phase 3D DB constraints"

set +e
psql_exec >/tmp/nf3d-service-stock.out 2>&1 <<'SQL'
UPDATE products
SET "TracksStock"=true
WHERE "Id"='77777777-7777-7777-7777-777777777777';
SQL
service_stock=$?
set -e
[[ $service_stock -ne 0 ]] ||
  fail "Service product unexpectedly accepted TracksStock=true."
rm -f /tmp/nf3d-service-stock.out

set +e
psql_exec >/tmp/nf3d-goods-precision.out 2>&1 <<'SQL'
UPDATE products
SET "QuantityPrecision"=2
WHERE "Id"='44444444-4444-4444-4444-444444444444';
SQL
goods_precision=$?
set -e
[[ $goods_precision -ne 0 ]] ||
  fail "Goods product unexpectedly accepted decimal precision."
rm -f /tmp/nf3d-goods-precision.out

set +e
psql_exec >/tmp/nf3d-item-quantity.out 2>&1 <<'SQL'
UPDATE transaction_items
SET "Quantity"=0
WHERE "Id"='66666666-6666-6666-6666-666666666666';
SQL
invalid_item_quantity=$?
set -e
[[ $invalid_item_quantity -ne 0 ]] ||
  fail "Transaction item unexpectedly accepted zero Quantity."
rm -f /tmp/nf3d-item-quantity.out

set +e
psql_exec >/tmp/nf3d-order-status.out 2>&1 <<'SQL'
UPDATE laundry_work_orders
SET "Status"='bogus'
WHERE "Id"='88888888-8888-8888-8888-888888888888';
SQL
invalid_order_status=$?
set -e
[[ $invalid_order_status -ne 0 ]] ||
  fail "Laundry order unexpectedly accepted invalid status."
rm -f /tmp/nf3d-order-status.out

set +e
psql_exec >/tmp/nf3d-work-item-quantity.out 2>&1 <<'SQL'
UPDATE laundry_work_order_items
SET "Quantity"=0
WHERE "Id"='99999999-9999-9999-9999-999999999999';
SQL
invalid_work_item_quantity=$?
set -e
[[ $invalid_work_item_quantity -ne 0 ]] ||
  fail "Laundry item unexpectedly accepted zero quantity."
rm -f /tmp/nf3d-work-item-quantity.out

set +e
psql_exec >/tmp/nf3d-duplicate-transaction.out 2>&1 <<'SQL'
INSERT INTO laundry_work_orders
("Id","CustomerId","CreatedByUserId","TransactionId","OrderNumber","Status",
 "PaymentStatus","Notes","PromisedAt","UpdatedAt","TenantId","CreatedAt")
VALUES
('aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa',
 '33333333-3333-3333-3333-333333333333',
 '22222222-2222-2222-2222-222222222222',
 '55555555-5555-5555-5555-555555555555',
 'LDR-GATE-002',
 'ready',
 'paid',
 '',
 '2026-09-08T12:00:00Z',
 '2026-09-08T10:00:00Z',
 '11111111-1111-1111-1111-111111111111',
 '2026-09-08T08:00:00Z');
SQL
duplicate_transaction=$?
set -e
[[ $duplicate_transaction -ne 0 ]] ||
  fail "Same transaction unexpectedly linked to two laundry work orders."
rm -f /tmp/nf3d-duplicate-transaction.out

step "Rollback exactly Phase 3D"
run_ef "database update $PREVIOUS_MIGRATION"

[[ "$(psql_scalar 'SELECT "MigrationId" FROM "__EFMigrationsHistory" ORDER BY "MigrationId" DESC LIMIT 1;')" == "$PREVIOUS_MIGRATION" ]] ||
  fail "Rollback migration history mismatch."

[[ "$(psql_scalar "SELECT COUNT(*) FROM information_schema.tables WHERE table_schema='public' AND table_name IN ('laundry_work_orders','laundry_work_order_items','laundry_work_order_status_history');")" == "0" ]] ||
  fail "Laundry tables remain after rollback."

[[ "$(psql_scalar "SELECT COUNT(*) FROM information_schema.columns WHERE table_schema='public' AND table_name='products' AND column_name IN ('Type','TracksStock','QuantityPrecision');")" == "0" ]] ||
  fail "Phase 3D product columns remain after rollback."

[[ "$(psql_scalar "SELECT COUNT(*) FROM products WHERE \"Id\"='44444444-4444-4444-4444-444444444444' AND \"Stok\"=17;")" == "1" ]] ||
  fail "Legacy product missing or changed after rollback."

[[ "$(psql_scalar "SELECT COUNT(*) FROM transaction_items WHERE \"Id\"='66666666-6666-6666-6666-666666666666' AND \"Qty\"=3;")" == "1" ]] ||
  fail "Legacy transaction item missing or changed after rollback."

step "Reapply Phase 3D"
run_ef "database update"

[[ "$(psql_scalar 'SELECT "MigrationId" FROM "__EFMigrationsHistory" ORDER BY "MigrationId" DESC LIMIT 1;')" == "$LATEST_MIGRATION" ]] ||
  fail "Reapply migration history mismatch."

[[ "$(psql_scalar "SELECT COUNT(*) FROM information_schema.tables WHERE table_schema='public' AND table_name IN ('laundry_work_orders','laundry_work_order_items','laundry_work_order_status_history');")" == "3" ]] ||
  fail "Laundry schema missing after reapply."

[[ "$(psql_scalar "SELECT \"ProductType\" || '|' || \"Quantity\" || '|' || \"Unit\" FROM transaction_items WHERE \"Id\"='66666666-6666-6666-6666-666666666666';")" == "goods|3.000|pcs" ]] ||
  fail "Legacy transaction item backfill mismatch after reapply."

step "EF model drift check"
run_ef "migrations has-pending-model-changes"

step "Repository cleanliness"
[[ -z "$(git status --porcelain)" ]] ||
  fail "Gate changed repository files."

printf '\nFINAL PHASE 3D LAUNDRY POSTGRES GATE: PASS\n'
printf 'Backend HEAD : %s\n' "$(git rev-parse HEAD)"
printf 'Forward      : PASS\n'
printf 'Legacy data  : PASS\n'
printf 'Decimal qty  : PASS\n'
printf 'Constraints  : PASS\n'
printf 'Rollback     : PASS\n'
printf 'Reapply      : PASS\n'
printf 'Model drift  : PASS\n'
printf 'Production   : NOT MODIFIED\n'
printf 'Supabase     : NOT USED\n'
