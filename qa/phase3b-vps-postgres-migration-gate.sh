#!/usr/bin/env bash
set -euo pipefail

BRANCH="feat/phase-3b-shared-device-attendance"
WORKSPACE="${NF_PHASE3B_WORKSPACE:-$HOME/neverfade-phase3b}"
REPO="$WORKSPACE/backend"
PROJECT="NeverfadePos.Api/NeverfadePos.Api.csproj"
PREVIOUS_MIGRATION="20260902093400_AddTenantBusinessType"
LATEST_MIGRATION="20260903033503_AddSharedDeviceAttendance"
SDK_IMAGE="mcr.microsoft.com/dotnet/sdk:10.0"
PG_IMAGE="postgres:16-alpine"
NUGET_VOLUME="neverfade-phase3b-nuget"
RUN_ID="$(date +%Y%m%d%H%M%S)-$$"
NETWORK="nf-phase3b-db-$RUN_ID"
PG_CONTAINER="nf-phase3b-pg-$RUN_ID"
DB_NAME="neverfade_phase3b"
DB_USER="postgres"
DB_PASSWORD="phase3b-local-only"

fail() {
  printf '\n[FAIL] %s\n' "$1" >&2
  exit 1
}

step() {
  printf '\n==> %s\n' "$1"
}

cleanup() {
  docker rm -f "$PG_CONTAINER" >/dev/null 2>&1 || true
  docker network rm "$NETWORK" >/dev/null 2>&1 || true
}
trap cleanup EXIT

[[ -d "$REPO/.git" ]] || fail "Workspace backend tidak ditemukan di $REPO"
command -v docker >/dev/null 2>&1 || fail "Docker tidak tersedia"
command -v git >/dev/null 2>&1 || fail "git tidak tersedia"

cd "$REPO"

step "Verify isolated workspace is clean and current"
if [[ -n "$(git status --porcelain)" ]]; then
  git status --short
  fail "Workspace backend harus clean"
fi

git fetch origin "$BRANCH"
git switch "$BRANCH"
git pull --ff-only origin "$BRANCH"
printf 'Backend HEAD: %s\n' "$(git rev-parse HEAD)"
printf 'Remote HEAD : %s\n' "$(git rev-parse "origin/$BRANCH")"

step "Create isolated Docker network and disposable PostgreSQL"
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
  if docker exec "$PG_CONTAINER" pg_isready -U "$DB_USER" -d "$DB_NAME" >/dev/null 2>&1; then
    break
  fi
  sleep 1
done

docker exec "$PG_CONTAINER" pg_isready -U "$DB_USER" -d "$DB_NAME" >/dev/null 2>&1 \
  || fail "Disposable PostgreSQL tidak ready"

CONNECTION="Host=$PG_CONTAINER;Port=5432;Database=$DB_NAME;Username=$DB_USER;Password=$DB_PASSWORD"

docker volume inspect "$NUGET_VOLUME" >/dev/null 2>&1 || docker volume create "$NUGET_VOLUME" >/dev/null

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
  run_dotnet "dotnet restore '$PROJECT' >/dev/null && dotnet tool install --global dotnet-ef --version 10.0.9 >/dev/null 2>&1 || true; export PATH=\"\$PATH:/root/.dotnet/tools\"; dotnet ef $* --project '$PROJECT' --startup-project '$PROJECT'"
}

psql_exec() {
  docker exec -e PGPASSWORD="$DB_PASSWORD" "$PG_CONTAINER" \
    psql -v ON_ERROR_STOP=1 -U "$DB_USER" -d "$DB_NAME" "$@"
}

psql_scalar() {
  psql_exec -Atqc "$1"
}

step "Migrate clean database to pre-Phase3B schema"
run_ef "database update $PREVIOUS_MIGRATION"

latest_before="$(psql_scalar 'SELECT "MigrationId" FROM "__EFMigrationsHistory" ORDER BY "MigrationId" DESC LIMIT 1;')"
[[ "$latest_before" == "$PREVIOUS_MIGRATION" ]] \
  || fail "Expected pre-Phase3B migration $PREVIOUS_MIGRATION, got $latest_before"

step "Seed representative legacy tenant/user/employee/attendance"
psql_exec <<'SQL'
INSERT INTO tenants ("Id", "NamaToko", "Slug", "CreatedAt", "Status", "UpdatedAt", "BusinessType")
VALUES (
  '11111111-1111-1111-1111-111111111111',
  'Legacy NeverFade Tenant',
  'legacy-neverfade-tenant',
  '2026-08-01T00:00:00Z',
  'active',
  '2026-08-01T00:00:00Z',
  'general_retail'
);

INSERT INTO users ("Id", "Nama", "Username", "PasswordHash", "Role", "Active", "TenantId", "CreatedAt")
VALUES (
  '22222222-2222-2222-2222-222222222222',
  'Legacy Owner',
  'legacy-owner',
  'legacy-hash',
  'owner',
  true,
  '11111111-1111-1111-1111-111111111111',
  '2026-08-01T00:00:00Z'
);

INSERT INTO karyawans (
  "Id", "Nama", "Jabatan", "Telepon", "Email", "Gaji", "TanggalMasuk", "Status", "Catatan", "TenantId", "CreatedAt"
)
VALUES (
  '33333333-3333-3333-3333-333333333333',
  'Legacy Employee',
  'Kasir',
  '',
  '',
  0,
  '2026-08-01',
  'aktif',
  '',
  '11111111-1111-1111-1111-111111111111',
  '2026-08-01T00:00:00Z'
);

INSERT INTO absensis (
  "Id", "KaryawanId", "Tanggal", "CheckIn", "CheckOut", "TenantId", "CreatedAt"
)
VALUES (
  '44444444-4444-4444-4444-444444444444',
  '33333333-3333-3333-3333-333333333333',
  '2026-09-01',
  '09:05:00',
  '17:02:00',
  '11111111-1111-1111-1111-111111111111',
  '2026-09-01T02:05:00Z'
);
SQL

[[ "$(psql_scalar 'SELECT COUNT(*) FROM tenants WHERE "Id" = '\''11111111-1111-1111-1111-111111111111'\'';')" == "1" ]] \
  || fail "Legacy tenant seed missing"
[[ "$(psql_scalar 'SELECT COUNT(*) FROM absensis WHERE "Id" = '\''44444444-4444-4444-4444-444444444444'\'';')" == "1" ]] \
  || fail "Legacy attendance seed missing"

step "Forward migrate to Phase 3B attendance"
run_ef "database update"

latest_after="$(psql_scalar 'SELECT "MigrationId" FROM "__EFMigrationsHistory" ORDER BY "MigrationId" DESC LIMIT 1;')"
[[ "$latest_after" == "$LATEST_MIGRATION" ]] \
  || fail "Expected latest migration $LATEST_MIGRATION, got $latest_after"

step "Verify new schema and legacy data survival"
new_tables="$(psql_scalar "SELECT COUNT(*) FROM information_schema.tables WHERE table_schema='public' AND table_name IN ('attendance_corrections','attendance_policies','employee_schedule_exceptions','employee_weekly_schedules','shared_pos_devices','shared_pos_sessions','tenant_audit_events');")"
[[ "$new_tables" == "7" ]] || fail "Expected 7 Phase 3B tables, got $new_tables"

karyawan_columns="$(psql_scalar "SELECT COUNT(*) FROM information_schema.columns WHERE table_schema='public' AND table_name='karyawans' AND column_name IN ('PinFingerprint','PinHash','PinUpdatedAt','UserId');")"
[[ "$karyawan_columns" == "4" ]] || fail "Karyawan Phase 3B columns incomplete"

absensi_columns="$(psql_scalar "SELECT COUNT(*) FROM information_schema.columns WHERE table_schema='public' AND table_name='absensis' AND column_name IN ('CheckInAtUtc','CheckOutAtUtc','OutsideSchedule');")"
[[ "$absensi_columns" == "3" ]] || fail "Absensi Phase 3B columns incomplete"

legacy_attendance="$(psql_scalar 'SELECT "CheckIn"::text || '\''|'\'' || "CheckOut"::text FROM absensis WHERE "Id" = '\''44444444-4444-4444-4444-444444444444'\'';')"
[[ "$legacy_attendance" == "09:05:00|17:02:00" ]] \
  || fail "Legacy attendance values changed: $legacy_attendance"

unique_index="$(psql_scalar "SELECT indexdef FROM pg_indexes WHERE schemaname='public' AND tablename='absensis' AND indexname='IX_absensis_TenantId_KaryawanId_Tanggal';")"
[[ "$unique_index" == *"UNIQUE INDEX"* ]] || fail "Attendance unique index not installed"

step "Verify duplicate attendance is rejected"
set +e
psql_exec >/tmp/nf-phase3b-duplicate.out 2>&1 <<'SQL'
INSERT INTO absensis (
  "Id", "KaryawanId", "Tanggal", "CheckIn", "CheckOut", "OutsideSchedule", "TenantId", "CreatedAt"
)
VALUES (
  '55555555-5555-5555-5555-555555555555',
  '33333333-3333-3333-3333-333333333333',
  '2026-09-01',
  '10:00:00',
  NULL,
  false,
  '11111111-1111-1111-1111-111111111111',
  '2026-09-01T03:00:00Z'
);
SQL
duplicate_status=$?
set -e
[[ $duplicate_status -ne 0 ]] || fail "Duplicate attendance unexpectedly succeeded"
grep -qi 'duplicate key value violates unique constraint' /tmp/nf-phase3b-duplicate.out \
  || fail "Duplicate insert failed for unexpected reason"
rm -f /tmp/nf-phase3b-duplicate.out

step "Rollback exactly to pre-Phase3B schema"
run_ef "database update $PREVIOUS_MIGRATION"

latest_rollback="$(psql_scalar 'SELECT "MigrationId" FROM "__EFMigrationsHistory" ORDER BY "MigrationId" DESC LIMIT 1;')"
[[ "$latest_rollback" == "$PREVIOUS_MIGRATION" ]] \
  || fail "Rollback migration history mismatch: $latest_rollback"

remaining_new_tables="$(psql_scalar "SELECT COUNT(*) FROM information_schema.tables WHERE table_schema='public' AND table_name IN ('attendance_corrections','attendance_policies','employee_schedule_exceptions','employee_weekly_schedules','shared_pos_devices','shared_pos_sessions','tenant_audit_events');")"
[[ "$remaining_new_tables" == "0" ]] || fail "Phase 3B tables remain after rollback"

remaining_karyawan_columns="$(psql_scalar "SELECT COUNT(*) FROM information_schema.columns WHERE table_schema='public' AND table_name='karyawans' AND column_name IN ('PinFingerprint','PinHash','PinUpdatedAt','UserId');")"
[[ "$remaining_karyawan_columns" == "0" ]] || fail "Karyawan Phase 3B columns remain after rollback"

remaining_absensi_columns="$(psql_scalar "SELECT COUNT(*) FROM information_schema.columns WHERE table_schema='public' AND table_name='absensis' AND column_name IN ('CheckInAtUtc','CheckOutAtUtc','OutsideSchedule');")"
[[ "$remaining_absensi_columns" == "0" ]] || fail "Absensi Phase 3B columns remain after rollback"

legacy_after_rollback="$(psql_scalar 'SELECT "CheckIn"::text || '\''|'\'' || "CheckOut"::text FROM absensis WHERE "Id" = '\''44444444-4444-4444-4444-444444444444'\'';')"
[[ "$legacy_after_rollback" == "09:05:00|17:02:00" ]] \
  || fail "Legacy attendance changed after rollback"

rollback_index="$(psql_scalar "SELECT indexdef FROM pg_indexes WHERE schemaname='public' AND tablename='absensis' AND indexname='IX_absensis_TenantId_KaryawanId_Tanggal';")"
[[ "$rollback_index" != *"UNIQUE INDEX"* ]] || fail "Legacy attendance index remained unique after rollback"

step "Reapply Phase 3B migration"
run_ef "database update"

latest_reapply="$(psql_scalar 'SELECT "MigrationId" FROM "__EFMigrationsHistory" ORDER BY "MigrationId" DESC LIMIT 1;')"
[[ "$latest_reapply" == "$LATEST_MIGRATION" ]] \
  || fail "Reapply migration history mismatch: $latest_reapply"

[[ "$(psql_scalar "SELECT COUNT(*) FROM information_schema.tables WHERE table_schema='public' AND table_name='shared_pos_sessions';")" == "1" ]] \
  || fail "Phase 3B schema missing after reapply"
[[ "$(psql_scalar 'SELECT COUNT(*) FROM absensis WHERE "Id" = '\''44444444-4444-4444-4444-444444444444'\'';')" == "1" ]] \
  || fail "Legacy attendance missing after reapply"

step "Final repository cleanliness"
if [[ -n "$(git status --porcelain)" ]]; then
  git status --short
  fail "Gate changed repository files"
fi

printf '\nFINAL PHASE 3B DISPOSABLE POSTGRES GATE: PASS\n'
printf 'Backend HEAD : %s\n' "$(git rev-parse HEAD)"
printf 'Forward      : PASS\n'
printf 'Legacy data  : PASS\n'
printf 'Unique guard : PASS\n'
printf 'Rollback     : PASS\n'
printf 'Reapply      : PASS\n'
printf 'Production   : NOT MODIFIED\n'
printf 'Supabase     : NOT USED\n'
