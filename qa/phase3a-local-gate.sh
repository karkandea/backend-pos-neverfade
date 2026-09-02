#!/usr/bin/env bash

set -Eeuo pipefail

BACKEND_REPO="${BACKEND_REPO:-$HOME/neverfade-pos-backend}"
FRONTEND_REPO="${FRONTEND_REPO:-$HOME/neverfade-pos-frontend}"
BACKEND_BRANCH="${BACKEND_BRANCH:-feat/phase-3-business-modes}"
FRONTEND_BRANCH="${FRONTEND_BRANCH:-feat/phase-3-business-modes}"
BASE_MIGRATION="20260819071547_AddPaymentRecoveryFields"
PHASE3A_MIGRATION="20260902093400_AddTenantBusinessType"
PG_IMAGE="${PHASE3A_PG_IMAGE:-postgres:16-alpine}"
PG_PORT="${PHASE3A_PG_PORT:-55432}"
PG_CONTAINER="neverfade-phase3a-postgres"
PG_USER="neverfade_phase3a"
PG_PASSWORD="phase3a-local-only"
MIGRATION_DB="neverfade_phase3a_migration"
SEED_DB="neverfade_phase3a_seed"
RUN_ID="$(date +%Y%m%d_%H%M%S)"
RESULT_ROOT="${PHASE3A_RESULT_ROOT:-$HOME/neverfade-pos-qa/phase3a-$RUN_ID}"
WORK_ROOT="$(mktemp -d /tmp/neverfade-phase3a.XXXXXX)"
BACKEND_WORK="$WORK_ROOT/backend"
FRONTEND_WORK="$WORK_ROOT/frontend"
TOOLS_DIR="$WORK_ROOT/dotnet-tools"
LOG_FILE="$RESULT_ROOT/phase3a-local-gate.log"
SUMMARY_FILE="$RESULT_ROOT/summary.txt"
BACKEND_LOG="$RESULT_ROOT/backend.log"
FRONTEND_LOG="$RESULT_ROOT/frontend.log"
BACKEND_PID=""
VITE_PID=""
FAILURES=()
PASSES=()

mkdir -p "$RESULT_ROOT"
mkdir -p "$WORK_ROOT/neverfade-pos-qa"
: > "$LOG_FILE"

log() {
  printf '%s\n' "$*" | tee -a "$LOG_FILE"
}

pass() {
  PASSES+=("$1")
  log "[PASS] $1"
}

fail() {
  FAILURES+=("$1")
  log "[FAIL] $1"
}

require_command() {
  local command_name="$1"
  if command -v "$command_name" >/dev/null 2>&1; then
    pass "tool available: $command_name ($(command -v "$command_name"))"
  else
    fail "required tool missing: $command_name"
  fi
}

cleanup() {
  set +e

  if [ -n "$VITE_PID" ]; then
    kill "$VITE_PID" >/dev/null 2>&1 || true
    wait "$VITE_PID" >/dev/null 2>&1 || true
  fi

  if [ -n "$BACKEND_PID" ]; then
    kill "$BACKEND_PID" >/dev/null 2>&1 || true
    wait "$BACKEND_PID" >/dev/null 2>&1 || true
  fi

  docker rm -f "$PG_CONTAINER" >/dev/null 2>&1 || true

  if [ -d "$BACKEND_REPO/.git" ] || git -C "$BACKEND_REPO" rev-parse --git-dir >/dev/null 2>&1; then
    git -C "$BACKEND_REPO" worktree remove --force "$BACKEND_WORK" >/dev/null 2>&1 || true
    git -C "$BACKEND_REPO" worktree prune >/dev/null 2>&1 || true
  fi

  if [ -d "$FRONTEND_REPO/.git" ] || git -C "$FRONTEND_REPO" rev-parse --git-dir >/dev/null 2>&1; then
    git -C "$FRONTEND_REPO" worktree remove --force "$FRONTEND_WORK" >/dev/null 2>&1 || true
    git -C "$FRONTEND_REPO" worktree prune >/dev/null 2>&1 || true
  fi

  rm -rf "$WORK_ROOT"
}

trap cleanup EXIT INT TERM

run_logged() {
  local label="$1"
  shift
  log ""
  log "===== $label ====="
  if "$@" 2>&1 | tee -a "$LOG_FILE"; then
    pass "$label"
    return 0
  fi

  fail "$label"
  return 1
}

run_logged_in() {
  local label="$1"
  local directory="$2"
  shift 2
  log ""
  log "===== $label ====="
  if (cd "$directory" && "$@") 2>&1 | tee -a "$LOG_FILE"; then
    pass "$label"
    return 0
  fi

  fail "$label"
  return 1
}

psql_exec() {
  local database="$1"
  local sql="$2"
  docker exec \
    -e PGPASSWORD="$PG_PASSWORD" \
    "$PG_CONTAINER" \
    psql \
      --username "$PG_USER" \
      --dbname "$database" \
      --set ON_ERROR_STOP=1 \
      --tuples-only \
      --no-align \
      --command "$sql"
}

connection_string() {
  local database="$1"
  printf 'Host=127.0.0.1;Port=%s;Database=%s;Username=%s;Password=%s;Include Error Detail=true' \
    "$PG_PORT" "$database" "$PG_USER" "$PG_PASSWORD"
}

export_common_backend_env() {
  export ASPNETCORE_ENVIRONMENT=Development
  export DOTNET_ENVIRONMENT=Development
  export Jwt__Key="phase3a-tenant-jwt-key-0123456789abcdef0123456789abcdef"
  export Jwt__Issuer="NeverfadePos.Phase3A.Local"
  export Jwt__Audience="NeverfadePos.Phase3A.Local.Client"
  export PlatformJwt__Key="phase3a-platform-jwt-key-fedcba9876543210fedcba9876543210"
  export PlatformJwt__Issuer="NeverfadePos.Platform.Phase3A.Local"
  export PlatformJwt__Audience="NeverfadePos.Platform.Phase3A.Local.Client"
  export Payments__Mode="Disabled"
  export PlatformBootstrap__Enabled="false"
  export Bootstrap__Enabled="false"
}

wait_for_http_status() {
  local url="$1"
  local expected="$2"
  local attempts="${3:-60}"
  local status=""
  local attempt

  for attempt in $(seq 1 "$attempts"); do
    status="$(curl --silent --output /dev/null --write-out '%{http_code}' --max-time 2 "$url" 2>/dev/null || true)"
    if [ "$status" = "$expected" ]; then
      return 0
    fi
    sleep 1
  done

  log "Expected HTTP $expected from $url, last status=$status"
  return 1
}

log "=================================================="
log "NEVERFADE POS — PHASE 3A LOCAL MERGE GATE"
log "=================================================="
log "Run ID      : $RUN_ID"
log "Backend repo: $BACKEND_REPO"
log "Frontend    : $FRONTEND_REPO"
log "Results     : $RESULT_ROOT"
log "Supabase    : NOT USED"
log ""

require_command git
require_command dotnet
require_command docker
require_command node
require_command npm
require_command curl
require_command jq
require_command lsof
require_command openssl
require_command python3

if [ "${#FAILURES[@]}" -gt 0 ]; then
  log "Pre-flight gagal. Install/enable tool di atas lalu rerun."
  exit 1
fi

if ! docker info >/dev/null 2>&1; then
  fail "Docker engine is not running"
  log "Buka Docker Desktop lalu rerun script."
  exit 1
fi
pass "Docker engine running"

for repo in "$BACKEND_REPO" "$FRONTEND_REPO"; do
  if ! git -C "$repo" rev-parse --is-inside-work-tree >/dev/null 2>&1; then
    fail "Git repo not found: $repo"
    exit 1
  fi
done
pass "backend/frontend local repos found"

run_logged_in "backend fetch" "$BACKEND_REPO" git fetch origin "$BACKEND_BRANCH" || exit 1
run_logged_in "frontend fetch" "$FRONTEND_REPO" git fetch origin "$FRONTEND_BRANCH" || exit 1

BACKEND_REMOTE_HEAD="$(git -C "$BACKEND_REPO" rev-parse "origin/$BACKEND_BRANCH")"
FRONTEND_REMOTE_HEAD="$(git -C "$FRONTEND_REPO" rev-parse "origin/$FRONTEND_BRANCH")"
log "Backend HEAD : $BACKEND_REMOTE_HEAD"
log "Frontend HEAD: $FRONTEND_REMOTE_HEAD"

run_logged_in "backend isolated worktree" "$BACKEND_REPO" git worktree add --detach "$BACKEND_WORK" "origin/$BACKEND_BRANCH" || exit 1
run_logged_in "frontend isolated worktree" "$FRONTEND_REPO" git worktree add --detach "$FRONTEND_WORK" "origin/$FRONTEND_BRANCH" || exit 1

if [ "$(git -C "$BACKEND_WORK" rev-parse HEAD)" != "$BACKEND_REMOTE_HEAD" ]; then
  fail "backend worktree HEAD mismatch"
  exit 1
fi
if [ "$(git -C "$FRONTEND_WORK" rev-parse HEAD)" != "$FRONTEND_REMOTE_HEAD" ]; then
  fail "frontend worktree HEAD mismatch"
  exit 1
fi
pass "isolated worktrees match remote Phase 3A HEADs"

run_logged_in "backend restore" "$BACKEND_WORK" dotnet restore NeverfadePos.slnx || true
run_logged_in "backend build" "$BACKEND_WORK" dotnet build NeverfadePos.slnx --no-restore || true
run_logged_in "backend tests" "$BACKEND_WORK" dotnet test NeverfadePos.Api.Tests/NeverfadePos.Api.Tests.csproj --no-restore || true

if ! run_logged "install local dotnet-ef 10.0.9" dotnet tool install dotnet-ef --tool-path "$TOOLS_DIR" --version 10.0.9; then
  if [ -x "$TOOLS_DIR/dotnet-ef" ]; then
    pass "dotnet-ef already present after install attempt"
  else
    log "dotnet-ef unavailable; migration gates cannot continue."
  fi
fi

if [ ! -x "$TOOLS_DIR/dotnet-ef" ]; then
  fail "migration execution unavailable because dotnet-ef is missing"
else
  export_common_backend_env
  export ConnectionStrings__DefaultConnection="Host=127.0.0.1;Port=$PG_PORT;Database=$MIGRATION_DB;Username=$PG_USER;Password=$PG_PASSWORD;Include Error Detail=true"
  run_logged_in "EF model has no pending snapshot changes" "$BACKEND_WORK" \
    "$TOOLS_DIR/dotnet-ef" migrations has-pending-model-changes \
      --project NeverfadePos.Api/NeverfadePos.Api.csproj \
      --startup-project NeverfadePos.Api/NeverfadePos.Api.csproj \
      --no-build || true

  docker rm -f "$PG_CONTAINER" >/dev/null 2>&1 || true

  if run_logged "start disposable PostgreSQL" docker run \
      --detach \
      --name "$PG_CONTAINER" \
      --publish "127.0.0.1:$PG_PORT:5432" \
      --env "POSTGRES_USER=$PG_USER" \
      --env "POSTGRES_PASSWORD=$PG_PASSWORD" \
      --env "POSTGRES_DB=$MIGRATION_DB" \
      "$PG_IMAGE"; then

    PG_READY=0
    for attempt in $(seq 1 60); do
      if docker exec "$PG_CONTAINER" pg_isready --username "$PG_USER" --dbname "$MIGRATION_DB" >/dev/null 2>&1; then
        PG_READY=1
        break
      fi
      sleep 1
    done

    if [ "$PG_READY" -eq 1 ]; then
      pass "disposable PostgreSQL ready"

      psql_exec postgres "CREATE DATABASE \"$SEED_DB\";" >/dev/null
      pass "created isolated seed database"

      export_common_backend_env

      MIGRATION_CONN="$(connection_string "$MIGRATION_DB")"
      SEED_CONN="$(connection_string "$SEED_DB")"

      export ConnectionStrings__DefaultConnection="$MIGRATION_CONN"

      run_logged_in "migrate production-like schema to pre-Phase-3 HEAD" "$BACKEND_WORK" \
        "$TOOLS_DIR/dotnet-ef" database update "$BASE_MIGRATION" \
          --project NeverfadePos.Api/NeverfadePos.Api.csproj \
          --startup-project NeverfadePos.Api/NeverfadePos.Api.csproj \
          --no-build || true

      if psql_exec "$MIGRATION_DB" \
        "INSERT INTO tenants (\"Id\",\"NamaToko\",\"Slug\",\"CreatedAt\",\"Status\",\"UpdatedAt\") VALUES ('aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa','Legacy Phase3A QA','legacy-phase3a-qa',NOW(),'active',NOW());" \
        >/dev/null 2>&1; then
        pass "insert representative existing tenant before Phase 3A migration"
      else
        fail "insert representative existing tenant before Phase 3A migration"
      fi

      run_logged_in "apply Phase 3A migration" "$BACKEND_WORK" \
        "$TOOLS_DIR/dotnet-ef" database update "$PHASE3A_MIGRATION" \
          --project NeverfadePos.Api/NeverfadePos.Api.csproj \
          --startup-project NeverfadePos.Api/NeverfadePos.Api.csproj \
          --no-build || true

      LEGACY_TYPE="$(psql_exec "$MIGRATION_DB" "SELECT \"BusinessType\" FROM tenants WHERE \"Id\"='aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa';" 2>/dev/null | tail -1 || true)"
      if [ "$LEGACY_TYPE" = "general_retail" ]; then
        pass "existing tenant backfilled to general_retail"
      else
        fail "existing tenant backfill expected general_retail, got '${LEGACY_TYPE:-<empty>}'"
      fi

      if psql_exec "$MIGRATION_DB" \
        "SELECT 1 FROM pg_constraint WHERE conname='CK_tenants_BusinessType' AND pg_get_constraintdef(oid) LIKE '%general_retail%' AND pg_get_constraintdef(oid) LIKE '%food_beverage%' AND pg_get_constraintdef(oid) LIKE '%laundry%' AND pg_get_constraintdef(oid) LIKE '%salon_barbershop%';" \
        2>/dev/null | grep -q '^1$'; then
        pass "BusinessType check constraint installed"
      else
        fail "BusinessType check constraint missing or incomplete"
      fi

      if psql_exec "$MIGRATION_DB" \
        "SELECT 1 FROM pg_constraint WHERE conname='CK_platform_audit_events_EventType' AND pg_get_constraintdef(oid) LIKE '%TENANT_BUSINESS_PROFILE_CHANGED%';" \
        2>/dev/null | grep -q '^1$'; then
        pass "platform audit constraint accepts business-profile event"
      else
        fail "platform audit constraint not reconciled"
      fi

      if psql_exec "$MIGRATION_DB" \
        "UPDATE tenants SET \"BusinessType\"='hotel' WHERE \"Id\"='aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa';" \
        >/dev/null 2>&1; then
        fail "BusinessType database constraint accepted invalid value"
      else
        pass "BusinessType database constraint rejects invalid value"
      fi

      run_logged_in "rollback Phase 3A migration" "$BACKEND_WORK" \
        "$TOOLS_DIR/dotnet-ef" database update "$BASE_MIGRATION" \
          --project NeverfadePos.Api/NeverfadePos.Api.csproj \
          --startup-project NeverfadePos.Api/NeverfadePos.Api.csproj \
          --no-build || true

      if psql_exec "$MIGRATION_DB" \
        "SELECT 1 FROM information_schema.columns WHERE table_name='tenants' AND column_name='BusinessType';" \
        2>/dev/null | grep -q '^1$'; then
        fail "rollback left tenants.BusinessType behind"
      else
        pass "rollback removes tenants.BusinessType"
      fi

      if psql_exec "$MIGRATION_DB" \
        "SELECT 1 FROM pg_constraint WHERE conname='CK_platform_audit_events_EventType' AND pg_get_constraintdef(oid) NOT LIKE '%TENANT_BUSINESS_PROFILE_CHANGED%';" \
        2>/dev/null | grep -q '^1$'; then
        pass "rollback restores pre-Phase-3 audit constraint"
      else
        fail "rollback did not restore pre-Phase-3 audit constraint"
      fi

      run_logged_in "re-apply Phase 3A migration after rollback" "$BACKEND_WORK" \
        "$TOOLS_DIR/dotnet-ef" database update "$PHASE3A_MIGRATION" \
          --project NeverfadePos.Api/NeverfadePos.Api.csproj \
          --startup-project NeverfadePos.Api/NeverfadePos.Api.csproj \
          --no-build || true

      export ConnectionStrings__DefaultConnection="$SEED_CONN"

      run_logged_in "migrate clean seed database to Phase 3A" "$BACKEND_WORK" \
        "$TOOLS_DIR/dotnet-ef" database update "$PHASE3A_MIGRATION" \
          --project NeverfadePos.Api/NeverfadePos.Api.csproj \
          --startup-project NeverfadePos.Api/NeverfadePos.Api.csproj \
          --no-build || true

      export ASPNETCORE_URLS="http://127.0.0.1:5012"
      (
        cd "$BACKEND_WORK"
        dotnet run \
          --project NeverfadePos.Api/NeverfadePos.Api.csproj \
          --no-build \
          >"$BACKEND_LOG" 2>&1
      ) &
      BACKEND_PID=$!

      if wait_for_http_status "http://127.0.0.1:5012/api/products" "401" 60; then
        pass "local backend started against disposable PostgreSQL"
      else
        fail "local backend failed to start"
        tail -n 100 "$BACKEND_LOG" 2>/dev/null | tee -a "$LOG_FILE" || true
      fi

      SEED_TYPE="$(psql_exec "$SEED_DB" "SELECT \"BusinessType\" FROM tenants WHERE \"Slug\"='warung-lumpia-beef';" 2>/dev/null | tail -1 || true)"
      SEED_USERS="$(psql_exec "$SEED_DB" "SELECT COUNT(*) FROM users;" 2>/dev/null | tail -1 || true)"
      SEED_PRODUCTS="$(psql_exec "$SEED_DB" "SELECT COUNT(*) FROM products;" 2>/dev/null | tail -1 || true)"
      if [ "$SEED_TYPE" = "general_retail" ] && [ "${SEED_USERS:-0}" -ge 3 ] && [ "${SEED_PRODUCTS:-0}" -ge 10 ]; then
        pass "development seed compatible with Phase 3A migration"
      else
        fail "seed compatibility mismatch: type=$SEED_TYPE users=$SEED_USERS products=$SEED_PRODUCTS"
      fi

      export QA_BACKEND_ROOT="$BACKEND_WORK"
      export QA_PROJECT_ROOT="$BACKEND_WORK/NeverfadePos.Api"
      export QA_API_URL="http://127.0.0.1:5012"
      export QA_CONNECTION_STRING="$SEED_CONN"
      export QA_OWNER_USERNAME="owner"
      export QA_OWNER_PASSWORD="owner123"

      for regression in \
        qa/product-stock-regression.sh \
        qa/transaction-regression.sh \
        qa/transaction-integrity-regression.sh \
        qa/laporan-regression.sh \
        qa/karyawan-absensi-regression.sh \
        qa/customer-regression.sh \
        qa/settings-regression.sh \
        qa/user-regression.sh
      do
        run_logged_in "backend regression: $regression" "$BACKEND_WORK" bash "$regression" || true
      done
    else
      fail "disposable PostgreSQL did not become ready"
      docker logs "$PG_CONTAINER" 2>&1 | tail -100 | tee -a "$LOG_FILE" || true
    fi
  fi
fi

run_logged_in "frontend npm ci" "$FRONTEND_WORK" npm ci || true
run_logged_in "frontend type/build" "$FRONTEND_WORK" npm run build || true
run_logged_in "frontend lint" "$FRONTEND_WORK" npm run lint || true

if [ -d "$FRONTEND_WORK/node_modules/@playwright/test" ]; then
  run_logged_in "Playwright Chromium install" "$FRONTEND_WORK" npx playwright install chromium || true

  mkdir -p "$RESULT_ROOT/frontend-e2e"
  (
    cd "$FRONTEND_WORK"
    VITE_API_URL="http://127.0.0.1:5012" \
      npm run dev -- --host 127.0.0.1 --port 5273 --strictPort \
      >"$FRONTEND_LOG" 2>&1
  ) &
  VITE_PID=$!

  if wait_for_http_status "http://127.0.0.1:5273/login" "200" 60; then
    pass "local frontend dev server started"

    export QA_API_URL="http://127.0.0.1:5012"
    export QA_OWNER_USERNAME="owner"
    export QA_OWNER_PASSWORD="owner123"

    run_logged_in "Phase 3A browser contract/happy-error paths" "$FRONTEND_WORK" \
      npx playwright test \
        tests/e2e/absensi-contract.spec.ts \
        tests/e2e/platform-control-plane.spec.ts \
        tests/e2e/mobile-role-navigation.spec.ts \
        tests/e2e/navigation-responsive.spec.ts \
        --project="Tablet Chromium" || true

    run_logged_in "checkout + payment recovery browser regression" "$FRONTEND_WORK" \
      npx playwright test \
        tests/e2e/kasir.spec.ts \
        tests/e2e/qris-checkout.spec.ts \
        tests/e2e/transaction-status.spec.ts \
        --project="Desktop Chromium" || true

    run_logged_in "reports + finance browser regression" "$FRONTEND_WORK" \
      npx playwright test \
        tests/e2e/bug-007-transaction-history.spec.ts \
        tests/e2e/bug-008-laporan.spec.ts \
        tests/e2e/finance-withdrawal.spec.ts \
        --project="Desktop Chromium" || true

    run_logged_in "mobile/tablet navigation and checkout regression" "$FRONTEND_WORK" \
      npx playwright test \
        tests/e2e/mobile-ux.spec.ts \
        tests/e2e/mobile-login.spec.ts \
        tests/e2e/mobile-landscape.spec.ts \
        tests/e2e/mobile-role-navigation.spec.ts || true
  else
    fail "local frontend dev server failed to start"
    tail -n 100 "$FRONTEND_LOG" 2>/dev/null | tee -a "$LOG_FILE" || true
  fi
else
  fail "Playwright dependency missing after npm ci"
fi

run_logged_in "backend git diff check" "$BACKEND_WORK" git diff --check || true
run_logged_in "frontend git diff check" "$FRONTEND_WORK" git diff --check || true

BACKEND_DIRTY="$(git -C "$BACKEND_WORK" status --porcelain)"
FRONTEND_DIRTY="$(git -C "$FRONTEND_WORK" status --porcelain)"
if [ -z "$BACKEND_DIRTY" ]; then
  pass "backend isolated worktree remains clean"
else
  fail "backend isolated worktree became dirty"
  printf '%s\n' "$BACKEND_DIRTY" | tee -a "$LOG_FILE"
fi
if [ -z "$FRONTEND_DIRTY" ]; then
  pass "frontend isolated worktree remains clean"
else
  fail "frontend isolated worktree became dirty"
  printf '%s\n' "$FRONTEND_DIRTY" | tee -a "$LOG_FILE"
fi

{
  echo "=================================================="
  echo "NEVERFADE POS — PHASE 3A LOCAL GATE SUMMARY"
  echo "=================================================="
  echo "Backend HEAD : $BACKEND_REMOTE_HEAD"
  echo "Frontend HEAD: $FRONTEND_REMOTE_HEAD"
  echo "Supabase     : NOT USED"
  echo "Results      : $RESULT_ROOT"
  echo
  echo "PASS (${#PASSES[@]}):"
  for item in "${PASSES[@]}"; do
    echo "  - $item"
  done
  echo
  echo "FAIL (${#FAILURES[@]}):"
  if [ "${#FAILURES[@]}" -eq 0 ]; then
    echo "  - none"
  else
    for item in "${FAILURES[@]}"; do
      echo "  - $item"
    done
  fi
  echo
  if [ "${#FAILURES[@]}" -eq 0 ]; then
    echo "FINAL PHASE 3A: PASS — execution gates green"
  else
    echo "FINAL PHASE 3A: FAIL — keep PRs draft / do not merge"
  fi
} | tee "$SUMMARY_FILE" | tee -a "$LOG_FILE"

if [ "${#FAILURES[@]}" -eq 0 ]; then
  exit 0
fi

exit 1
