#!/usr/bin/env bash
set -euo pipefail

WORKSPACE="${NF_PHASE3B_WORKSPACE:-$HOME/neverfade-phase3b}"
REPO="$WORKSPACE/backend"
PROJECT="NeverfadePos.Api/NeverfadePos.Api.csproj"
PREVIOUS_MIGRATION="20260903033503_AddSharedDeviceAttendance"
LATEST_MIGRATION="20260908041859_AddPhase3BFinanceWithdrawal"
EXPECTED_HEAD="${NF_PHASE3B_FINANCE_EXPECTED_BACKEND_HEAD:-}"
SDK_IMAGE="${NF_DOTNET_IMAGE:-mcr.microsoft.com/dotnet/sdk:10.0}"
PG_IMAGE="${NF_POSTGRES_IMAGE:-postgres:16-alpine}"
RUN_ID="$(date +%Y%m%d%H%M%S)-$$"
NETWORK="nf-phase3b-finance-db-$RUN_ID"
PG_CONTAINER="nf-phase3b-finance-pg-$RUN_ID"
DB_NAME="neverfade_phase3b_finance"
DB_USER="postgres"
DB_PASSWORD="phase3b-finance-local-only"
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
[[ "$(git rev-parse HEAD)" == "$EXPECTED_HEAD" ]] || fail "Backend HEAD tidak sesuai expected."

step "Create disposable PostgreSQL"
docker network create "$NETWORK" >/dev/null
docker run -d --rm   --name "$PG_CONTAINER"   --network "$NETWORK"   --cpus=0.5   --memory=512m   -e POSTGRES_DB="$DB_NAME"   -e POSTGRES_USER="$DB_USER"   -e POSTGRES_PASSWORD="$DB_PASSWORD"   "$PG_IMAGE" >/dev/null

for _ in $(seq 1 60); do
  docker exec "$PG_CONTAINER" pg_isready -U "$DB_USER" -d "$DB_NAME" >/dev/null 2>&1 && break
  sleep 1
done
docker exec "$PG_CONTAINER" pg_isready -U "$DB_USER" -d "$DB_NAME" >/dev/null 2>&1 || fail "PostgreSQL tidak ready."

CONNECTION="Host=$PG_CONTAINER;Port=5432;Database=$DB_NAME;Username=$DB_USER;Password=$DB_PASSWORD"
docker volume inspect "$NUGET_VOLUME" >/dev/null 2>&1 || docker volume create "$NUGET_VOLUME" >/dev/null

run_dotnet() {
  docker run --rm     --network "$NETWORK"     --cpus=1     --memory=2g     -e DOTNET_CLI_TELEMETRY_OPTOUT=1     -e DOTNET_NOLOGO=1     -e NUGET_PACKAGES=/root/.nuget/packages     -e "ConnectionStrings__DefaultConnection=$CONNECTION"     -v "$NUGET_VOLUME:/root/.nuget/packages"     -v "$REPO:/workspace"     -w /workspace     "$SDK_IMAGE" bash -lc "$1"
}

run_ef() {
  run_dotnet "dotnet restore '$PROJECT' >/dev/null && (dotnet tool install --global dotnet-ef --version 10.0.9 >/dev/null 2>&1 || dotnet tool update --global dotnet-ef --version 10.0.9 >/dev/null 2>&1) && export PATH=\"\$PATH:/root/.dotnet/tools\" && dotnet ef $* --project '$PROJECT' --startup-project '$PROJECT' --context AppDbContext"
}

psql_exec() {
  docker exec -i -e PGPASSWORD="$DB_PASSWORD" "$PG_CONTAINER"     psql -v ON_ERROR_STOP=1 -U "$DB_USER" -d "$DB_NAME" "$@"
}
psql_scalar() { psql_exec -Atqc "$1"; }

step "Migrate to attendance schema"
run_ef "database update $PREVIOUS_MIGRATION"
[[ "$(psql_scalar 'SELECT "MigrationId" FROM "__EFMigrationsHistory" ORDER BY "MigrationId" DESC LIMIT 1;')" == "$PREVIOUS_MIGRATION" ]]   || fail "Pre-finance migration history mismatch."

step "Seed representative legacy finance data"
psql_exec <<'SQL'
INSERT INTO tenants ("Id","NamaToko","Slug","CreatedAt","Status","UpdatedAt","BusinessType")
VALUES ('11111111-1111-1111-1111-111111111111','Legacy Finance Tenant','legacy-finance','2026-08-01T00:00:00Z','active','2026-08-01T00:00:00Z','general_retail');

INSERT INTO users ("Id","Nama","Username","PasswordHash","Role","Active","TenantId","CreatedAt")
VALUES ('22222222-2222-2222-2222-222222222222','Legacy Owner','legacy-finance-owner','legacy-hash','owner',true,'11111111-1111-1111-1111-111111111111','2026-08-01T00:00:00Z');

INSERT INTO withdrawal_requests ("Id","Amount","Status","RequestedByUserId","UpdatedAt","TenantId","CreatedAt")
VALUES ('33333333-3333-3333-3333-333333333333',150000,'requested','22222222-2222-2222-2222-222222222222','2026-09-01T00:00:00Z','11111111-1111-1111-1111-111111111111','2026-09-01T00:00:00Z');

INSERT INTO withdrawal_routes ("Id","TenantId","WithdrawalRequestId","CreatedAt")
VALUES ('44444444-4444-4444-4444-444444444444','11111111-1111-1111-1111-111111111111','33333333-3333-3333-3333-333333333333','2026-09-01T00:00:00Z');
SQL

step "Forward migrate finance schema"
run_ef "database update"
[[ "$(psql_scalar 'SELECT "MigrationId" FROM "__EFMigrationsHistory" ORDER BY "MigrationId" DESC LIMIT 1;')" == "$LATEST_MIGRATION" ]]   || fail "Latest migration mismatch."

bank_table="$(psql_scalar "SELECT COUNT(*) FROM information_schema.tables WHERE table_schema='public' AND table_name='withdrawal_bank_accounts';")"
[[ "$bank_table" == "1" ]] || fail "withdrawal_bank_accounts missing."

withdrawal_columns="$(psql_scalar "SELECT COUNT(*) FROM information_schema.columns WHERE table_schema='public' AND table_name='withdrawal_requests' AND column_name IN ('CancelledAt','DestinationAccountHolderName','DestinationAccountNumber','DestinationBankName','EvidenceMetadata','ProcessingStartedAt','RejectionReason','TransferReference');")"
[[ "$withdrawal_columns" == "8" ]] || fail "Finance withdrawal columns incomplete."

legacy_status="$(psql_scalar 'SELECT "Status" FROM withdrawal_requests WHERE "Id" = '\''33333333-3333-3333-3333-333333333333'\'';')"
[[ "$legacy_status" == "requested" ]] || fail "Legacy withdrawal status changed."

legacy_destination="$(psql_scalar 'SELECT "DestinationBankName" || '\''|'\'' || "DestinationAccountNumber" || '\''|'\'' || "DestinationAccountHolderName" FROM withdrawal_requests WHERE "Id" = '\''33333333-3333-3333-3333-333333333333'\'';')"
[[ "$legacy_destination" == "||" ]] || fail "Legacy withdrawal destination backfill unexpected: $legacy_destination"

tenant_unique="$(psql_scalar "SELECT indexdef FROM pg_indexes WHERE schemaname='public' AND tablename='withdrawal_bank_accounts' AND indexname='IX_withdrawal_bank_accounts_TenantId';")"
[[ "$tenant_unique" == *"UNIQUE INDEX"* ]] || fail "Tenant payout account unique index missing."

step "Verify expanded lifecycle constraints"
psql_exec <<'SQL'
UPDATE withdrawal_requests
SET "Status"='processing', "ProcessingStartedAt"='2026-09-02T00:00:00Z'
WHERE "Id"='33333333-3333-3333-3333-333333333333';
UPDATE withdrawal_requests
SET "Status"='cancelled', "CancelledAt"='2026-09-03T00:00:00Z'
WHERE "Id"='33333333-3333-3333-3333-333333333333';
UPDATE withdrawal_requests
SET "Status"='requested', "ProcessingStartedAt"=NULL, "CancelledAt"=NULL
WHERE "Id"='33333333-3333-3333-3333-333333333333';
SQL

set +e
psql_exec >/tmp/nf-finance-invalid-status.out 2>&1 <<'SQL'
UPDATE withdrawal_requests
SET "Status"='bogus'
WHERE "Id"='33333333-3333-3333-3333-333333333333';
SQL
invalid_status=$?
set -e
[[ $invalid_status -ne 0 ]] || fail "Invalid withdrawal status unexpectedly accepted."
rm -f /tmp/nf-finance-invalid-status.out

step "Rollback exactly one finance migration"
run_ef "database update $PREVIOUS_MIGRATION"
[[ "$(psql_scalar 'SELECT "MigrationId" FROM "__EFMigrationsHistory" ORDER BY "MigrationId" DESC LIMIT 1;')" == "$PREVIOUS_MIGRATION" ]]   || fail "Rollback migration history mismatch."

[[ "$(psql_scalar "SELECT COUNT(*) FROM information_schema.tables WHERE table_schema='public' AND table_name='withdrawal_bank_accounts';")" == "0" ]]   || fail "Bank account table remains after rollback."
[[ "$(psql_scalar "SELECT COUNT(*) FROM information_schema.columns WHERE table_schema='public' AND table_name='withdrawal_requests' AND column_name='DestinationBankName';")" == "0" ]]   || fail "Finance columns remain after rollback."
[[ "$(psql_scalar 'SELECT COUNT(*) FROM withdrawal_requests WHERE "Id" = '\''33333333-3333-3333-3333-333333333333'\'';')" == "1" ]]   || fail "Legacy withdrawal missing after rollback."

step "Reapply finance migration"
run_ef "database update"
[[ "$(psql_scalar 'SELECT "MigrationId" FROM "__EFMigrationsHistory" ORDER BY "MigrationId" DESC LIMIT 1;')" == "$LATEST_MIGRATION" ]]   || fail "Reapply migration history mismatch."
[[ "$(psql_scalar "SELECT COUNT(*) FROM information_schema.tables WHERE table_schema='public' AND table_name='withdrawal_bank_accounts';")" == "1" ]]   || fail "Finance schema missing after reapply."

step "EF model drift check"
run_ef "migrations has-pending-model-changes"

step "Repository cleanliness"
[[ -z "$(git status --porcelain)" ]] || fail "Gate changed repository files."

printf '\nFINAL PHASE 3B FINANCE POSTGRES GATE: PASS\n'
printf 'Backend HEAD : %s\n' "$(git rev-parse HEAD)"
printf 'Forward      : PASS\n'
printf 'Legacy data  : PASS\n'
printf 'Constraints  : PASS\n'
printf 'Rollback     : PASS\n'
printf 'Reapply      : PASS\n'
printf 'Model drift  : PASS\n'
printf 'Production   : NOT MODIFIED\n'
printf 'Supabase     : NOT USED\n'
