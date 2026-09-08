#!/usr/bin/env bash
set -euo pipefail

WORKSPACE="${NF_PHASE3C_WORKSPACE:-$HOME/neverfade-phase3b}"
REPO="$WORKSPACE/backend"
PROJECT="NeverfadePos.Api/NeverfadePos.Api.csproj"
PREVIOUS_MIGRATION="20260908041859_AddPhase3BFinanceWithdrawal"
LATEST_MIGRATION="20260908064352_AddPhase3CFnbRestaurant"
EXPECTED_HEAD="${NF_PHASE3C_EXPECTED_BACKEND_HEAD:-}"
SDK_IMAGE="${NF_DOTNET_IMAGE:-mcr.microsoft.com/dotnet/sdk:10.0}"
PG_IMAGE="${NF_POSTGRES_IMAGE:-postgres:16-alpine}"
RUN_ID="$(date +%Y%m%d%H%M%S)-$$"
NETWORK="nf-phase3c-fnb-db-$RUN_ID"
PG_CONTAINER="nf-phase3c-fnb-pg-$RUN_ID"
DB_NAME="neverfade_phase3c_fnb"
DB_USER="postgres"
DB_PASSWORD="phase3c-fnb-local-only"
NUGET_VOLUME="neverfade-phase3b-nuget"

fail() { printf '\n[FAIL] %s\n' "$1" >&2; exit 1; }
step() { printf '\n==> %s\n' "$1"; }

cleanup() {
  docker rm -f "$PG_CONTAINER" >/dev/null 2>&1 || true
  docker network rm "$NETWORK" >/dev/null 2>&1 || true
}
trap cleanup EXIT

[[ -d "$REPO/.git" ]] || fail "Backend workspace tidak ditemukan."
command -v docker >/dev/null 2>&1 || fail "Docker tidak tersedia."
cd "$REPO"

[[ -z "$(git status --porcelain)" ]] || fail "Backend workspace harus clean."
[[ -n "$EXPECTED_HEAD" ]] || fail "Expected backend HEAD wajib diisi."
[[ "$(git rev-parse HEAD)" == "$EXPECTED_HEAD" ]] ||
  fail "Backend HEAD tidak sesuai expected."

step "Create disposable PostgreSQL"
docker network create "$NETWORK" >/dev/null
docker run -d --rm   --name "$PG_CONTAINER"   --network "$NETWORK"   --cpus=0.5   --memory=512m   -e POSTGRES_DB="$DB_NAME"   -e POSTGRES_USER="$DB_USER"   -e POSTGRES_PASSWORD="$DB_PASSWORD"   "$PG_IMAGE" >/dev/null

for _ in $(seq 1 60); do
  docker exec "$PG_CONTAINER"     pg_isready -U "$DB_USER" -d "$DB_NAME" >/dev/null 2>&1 && break
  sleep 1
done
docker exec "$PG_CONTAINER"   pg_isready -U "$DB_USER" -d "$DB_NAME" >/dev/null 2>&1 ||
  fail "PostgreSQL tidak ready."

CONNECTION="Host=$PG_CONTAINER;Port=5432;Database=$DB_NAME;Username=$DB_USER;Password=$DB_PASSWORD"
docker volume inspect "$NUGET_VOLUME" >/dev/null 2>&1 ||
  docker volume create "$NUGET_VOLUME" >/dev/null

run_dotnet() {
  docker run --rm     --network "$NETWORK"     --cpus=1     --memory=2g     -e DOTNET_CLI_TELEMETRY_OPTOUT=1     -e DOTNET_NOLOGO=1     -e NUGET_PACKAGES=/root/.nuget/packages     -e "ConnectionStrings__DefaultConnection=$CONNECTION"     -v "$NUGET_VOLUME:/root/.nuget/packages"     -v "$REPO:/workspace"     -w /workspace     "$SDK_IMAGE" bash -lc "$1"
}

run_ef() {
  run_dotnet "dotnet restore '$PROJECT' >/dev/null &&     (dotnet tool install --global dotnet-ef --version 10.0.9 >/dev/null 2>&1 ||      dotnet tool update --global dotnet-ef --version 10.0.9 >/dev/null 2>&1) &&     export PATH=\"\$PATH:/root/.dotnet/tools\" &&     dotnet ef $*       --project '$PROJECT'       --startup-project '$PROJECT'       --context AppDbContext"
}

psql_exec() {
  docker exec -i -e PGPASSWORD="$DB_PASSWORD" "$PG_CONTAINER"     psql -v ON_ERROR_STOP=1 -U "$DB_USER" -d "$DB_NAME" "$@"
}

psql_scalar() {
  psql_exec -Atqc "$1"
}

step "Migrate to exact Phase 3B schema"
run_ef "database update $PREVIOUS_MIGRATION"
[[ "$(psql_scalar 'SELECT "MigrationId" FROM "__EFMigrationsHistory" ORDER BY "MigrationId" DESC LIMIT 1;')" == "$PREVIOUS_MIGRATION" ]] ||
  fail "Pre-3C migration history mismatch."

step "Seed representative existing tenant data"
psql_exec <<'SQL'
INSERT INTO tenants
("Id","NamaToko","Slug","CreatedAt","Status","UpdatedAt","BusinessType")
VALUES
('11111111-1111-1111-1111-111111111111',
 'Legacy FNB Tenant',
 'legacy-fnb',
 '2026-09-01T00:00:00Z',
 'active',
 '2026-09-01T00:00:00Z',
 'food_beverage');

INSERT INTO users
("Id","Nama","Username","PasswordHash","Role","Active","TenantId","CreatedAt")
VALUES
('22222222-2222-2222-2222-222222222222',
 'Legacy Owner',
 'legacy-fnb-owner',
 'legacy-hash',
 'owner',
 true,
 '11111111-1111-1111-1111-111111111111',
 '2026-09-01T00:00:00Z');

INSERT INTO products
("Id","Kode","Barcode","Nama","Kategori","HargaModal","HargaJual","Stok",
 "Supplier","Satuan","Deskripsi","TenantId","CreatedAt")
VALUES
('33333333-3333-3333-3333-333333333333',
 'FNB-LEGACY',
 '899000000001',
 'Es Kopi Legacy',
 'Minuman',
 9000,
 18000,
 100,
 'QA',
 'pcs',
 'Legacy product before Phase 3C',
 '11111111-1111-1111-1111-111111111111',
 '2026-09-01T00:00:00Z');
SQL

step "Forward migrate Phase 3C"
run_ef "database update"
[[ "$(psql_scalar 'SELECT "MigrationId" FROM "__EFMigrationsHistory" ORDER BY "MigrationId" DESC LIMIT 1;')" == "$LATEST_MIGRATION" ]] ||
  fail "Latest migration mismatch."

restaurant_tables="$(psql_scalar "SELECT COUNT(*) FROM information_schema.tables WHERE table_schema='public' AND table_name IN ('restaurant_tables','restaurant_orders','restaurant_order_items');")"
[[ "$restaurant_tables" == "3" ]] ||
  fail "Restaurant tables incomplete."

legacy_product="$(psql_scalar "SELECT \"Nama\" FROM products WHERE \"Id\"='33333333-3333-3333-3333-333333333333';")"
[[ "$legacy_product" == "Es Kopi Legacy" ]] ||
  fail "Existing product changed during migration."

open_index="$(psql_scalar "SELECT indexdef FROM pg_indexes WHERE schemaname='public' AND tablename='restaurant_orders' AND indexname='IX_restaurant_orders_TableId';")"
[[ "$open_index" == *"UNIQUE INDEX"* && "$open_index" == *"Status"* && "$open_index" == *"open"* ]] ||
  fail "Open-order partial unique index missing."

transaction_index="$(psql_scalar "SELECT indexdef FROM pg_indexes WHERE schemaname='public' AND tablename='restaurant_orders' AND indexname='IX_restaurant_orders_TransactionId';")"
[[ "$transaction_index" == *"UNIQUE INDEX"* && "$transaction_index" == *"TransactionId"* ]] ||
  fail "Transaction link unique index missing."

step "Seed valid restaurant order"
psql_exec <<'SQL'
INSERT INTO restaurant_tables
("Id","Code","Name","Capacity","Active","SortOrder","UpdatedAt","TenantId","CreatedAt")
VALUES
('44444444-4444-4444-4444-444444444444',
 'A1',
 'Meja A1',
 4,
 true,
 1,
 '2026-09-08T06:00:00Z',
 '11111111-1111-1111-1111-111111111111',
 '2026-09-08T06:00:00Z');

INSERT INTO restaurant_orders
("Id","TableId","OpenedByUserId","TransactionId","OrderNumber","Status",
 "CancellationReason","UpdatedAt","ClosedAt","CancelledAt","TenantId","CreatedAt")
VALUES
('55555555-5555-5555-5555-555555555555',
 '44444444-4444-4444-4444-444444444444',
 '22222222-2222-2222-2222-222222222222',
 NULL,
 'FNB-GATE-001',
 'open',
 NULL,
 '2026-09-08T06:01:00Z',
 NULL,
 NULL,
 '11111111-1111-1111-1111-111111111111',
 '2026-09-08T06:01:00Z');

INSERT INTO restaurant_order_items
("Id","RestaurantOrderId","ProductId","Nama","HargaJual","Qty","Note",
 "KitchenStatus","UpdatedAt","QueuedAt","PreparingAt","ReadyAt","ServedAt",
 "TenantId","CreatedAt")
VALUES
('66666666-6666-6666-6666-666666666666',
 '55555555-5555-5555-5555-555555555555',
 '33333333-3333-3333-3333-333333333333',
 'Es Kopi Legacy',
 18000,
 2,
 'Tanpa es',
 'queued',
 '2026-09-08T06:02:00Z',
 '2026-09-08T06:02:00Z',
 NULL,
 NULL,
 NULL,
 '11111111-1111-1111-1111-111111111111',
 '2026-09-08T06:02:00Z');
SQL

step "Verify DB constraints"
set +e
psql_exec >/tmp/nf3c-duplicate-open.out 2>&1 <<'SQL'
INSERT INTO restaurant_orders
("Id","TableId","OpenedByUserId","TransactionId","OrderNumber","Status",
 "UpdatedAt","TenantId","CreatedAt")
VALUES
('77777777-7777-7777-7777-777777777777',
 '44444444-4444-4444-4444-444444444444',
 '22222222-2222-2222-2222-222222222222',
 NULL,
 'FNB-GATE-002',
 'open',
 '2026-09-08T06:03:00Z',
 '11111111-1111-1111-1111-111111111111',
 '2026-09-08T06:03:00Z');
SQL
duplicate_open=$?
set -e
[[ $duplicate_open -ne 0 ]] ||
  fail "Second open order on same table unexpectedly accepted."
rm -f /tmp/nf3c-duplicate-open.out

set +e
psql_exec >/tmp/nf3c-invalid-order-status.out 2>&1 <<'SQL'
UPDATE restaurant_orders
SET "Status"='bogus'
WHERE "Id"='55555555-5555-5555-5555-555555555555';
SQL
invalid_order=$?
set -e
[[ $invalid_order -ne 0 ]] ||
  fail "Invalid restaurant order status unexpectedly accepted."
rm -f /tmp/nf3c-invalid-order-status.out

set +e
psql_exec >/tmp/nf3c-invalid-kitchen.out 2>&1 <<'SQL'
UPDATE restaurant_order_items
SET "KitchenStatus"='bogus'
WHERE "Id"='66666666-6666-6666-6666-666666666666';
SQL
invalid_kitchen=$?
set -e
[[ $invalid_kitchen -ne 0 ]] ||
  fail "Invalid kitchen status unexpectedly accepted."
rm -f /tmp/nf3c-invalid-kitchen.out

set +e
psql_exec >/tmp/nf3c-invalid-qty.out 2>&1 <<'SQL'
UPDATE restaurant_order_items
SET "Qty"=0
WHERE "Id"='66666666-6666-6666-6666-666666666666';
SQL
invalid_qty=$?
set -e
[[ $invalid_qty -ne 0 ]] ||
  fail "Invalid restaurant qty unexpectedly accepted."
rm -f /tmp/nf3c-invalid-qty.out

step "Rollback exactly Phase 3C"
run_ef "database update $PREVIOUS_MIGRATION"
[[ "$(psql_scalar 'SELECT "MigrationId" FROM "__EFMigrationsHistory" ORDER BY "MigrationId" DESC LIMIT 1;')" == "$PREVIOUS_MIGRATION" ]] ||
  fail "Rollback migration history mismatch."

[[ "$(psql_scalar "SELECT COUNT(*) FROM information_schema.tables WHERE table_schema='public' AND table_name IN ('restaurant_tables','restaurant_orders','restaurant_order_items');")" == "0" ]] ||
  fail "Restaurant tables remain after rollback."

[[ "$(psql_scalar "SELECT COUNT(*) FROM products WHERE \"Id\"='33333333-3333-3333-3333-333333333333';")" == "1" ]] ||
  fail "Existing product missing after rollback."

step "Reapply Phase 3C"
run_ef "database update"
[[ "$(psql_scalar 'SELECT "MigrationId" FROM "__EFMigrationsHistory" ORDER BY "MigrationId" DESC LIMIT 1;')" == "$LATEST_MIGRATION" ]] ||
  fail "Reapply migration history mismatch."

[[ "$(psql_scalar "SELECT COUNT(*) FROM information_schema.tables WHERE table_schema='public' AND table_name IN ('restaurant_tables','restaurant_orders','restaurant_order_items');")" == "3" ]] ||
  fail "Restaurant schema missing after reapply."

step "EF model drift check"
run_ef "migrations has-pending-model-changes"

step "Repository cleanliness"
[[ -z "$(git status --porcelain)" ]] ||
  fail "Gate changed repository files."

printf '\nFINAL PHASE 3C FNB POSTGRES GATE: PASS\n'
printf 'Backend HEAD : %s\n' "$(git rev-parse HEAD)"
printf 'Forward      : PASS\n'
printf 'Legacy data  : PASS\n'
printf 'Constraints  : PASS\n'
printf 'Rollback     : PASS\n'
printf 'Reapply      : PASS\n'
printf 'Model drift  : PASS\n'
printf 'Production   : NOT MODIFIED\n'
printf 'Supabase     : NOT USED\n'
