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
QA_HOME="$WORK_ROOT/qa-home"
LOG_FILE="$RESULT_ROOT/phase3a-local-gate.log"
SUMMARY_FILE="$RESULT_ROOT/summary.txt"
BACKEND_LOG="$RESULT_ROOT/backend.log"
FRONTEND_LOG="$RESULT_ROOT/frontend.log"
BACKEND_PID=""
VITE_PID=""
EF_MODE=""
FAILURES=()
PASSES=()

mkdir -p "$RESULT_ROOT" "$QA_HOME"
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

  if command -v docker >/dev/null 2>&1; then
    docker rm -f "$PG_CONTAINER" >/dev/null 2>&1 || true
  fi

  if git -C "$BACKEND_REPO" rev-parse --git-dir >/dev/null 2>&1; then
    git -C "$BACKEND_REPO" worktree remove --force "$BACKEND_WORK" >/dev/null 2>&1 || true
    git -C "$BACKEND_REPO" worktree prune >/dev/null 2>&1 || true
  fi

  if git -C "$FRONTEND_REPO" rev-parse --git-dir >/dev/null 2>&1; then
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

require_command() {
  local command_name="$1"
  if command -v "$command_name" >/dev/null 2>&1; then
    pass "tool available: $command_name ($(command -v "$command_name"))"
  else
    fail "required tool missing: $command_name"
  fi
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
  export Payments__LiveEnabled="false"
  export Payments__SandboxAllowedTenantIds=""
  export Payments__LiveAllowedTenantIds=""
  export PlatformBootstrap__Enabled="false"
  export Bootstrap__Enabled="false"
}

wait_for_http_status() {
  local url="$1"
  local expected="$2"
  local attempts="${3:-60}"
  local status=""
  local attempt

  for ((attempt = 1; attempt <= attempts; attempt++)); do
    status="$(curl --silent --output /dev/null --write-out '%{http_code}' --max-time 2 "$url" 2>/dev/null || true)"
    if [ "$status" = "$expected" ]; then
      return 0
    fi
    sleep 1
  done

  log "Expected HTTP $expected from $url, last status=$status"
  return 1
}

ef_cmd() {
  if [ "$EF_MODE" = "global" ]; then
    dotnet ef "$@"
  else
    "$TOOLS_DIR/dotnet-ef" "$@"
  fi
}

write_summary() {
  {
    echo "=================================================="
    echo "NEVERFADE POS — PHASE 3A LOCAL GATE SUMMARY"
    echo "=================================================="
    echo "Backend HEAD : ${BACKEND_REMOTE_HEAD:-unresolved}"
    echo "Frontend HEAD: ${FRONTEND_REMOTE_HEAD:-unresolved}"
    echo "Supabase     : NOT USED"
    echo "Results      : $RESULT_ROOT"
    echo
    echo "PASS (${#PASSES[@]}):"
    if [ "${#PASSES[@]}" -eq 0 ]; then
      echo "  - none"
    else
      for item in "${PASSES[@]}"; do
        echo "  - $item"
      done
    fi
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
}

log "=================================================="
log "NEVERFADE POS — PHASE 3A LOCAL MERGE GATE"
log "=================================================="
log "Run ID      : $RUN_ID"
log "Backend repo: $BACKEND_REPO"
log "Frontend    : $FRONTEND_REPO"
log "Results     : $RESULT_ROOT"
log "Supabase    : NOT USED"

for command_name in git dotnet docker node npm curl jq lsof openssl python3; do
  require_command "$command_name"
done

if [ "${#FAILURES[@]}" -gt 0 ]; then
  write_summary
  exit 1
fi

run_logged "dotnet info" dotnet --info || true
run_logged "node version" node --version || true
run_logged "npm version" npm --version || true
run_logged "docker version" docker version || true

if ! docker info >/dev/null 2>&1; then
  fail "Docker engine is not running"
  write_summary
  exit 1
fi
pass "Docker engine running"

for repo in "$BACKEND_REPO" "$FRONTEND_REPO"; do
  if ! git -C "$repo" rev-parse --is-inside-work-tree >/dev/null 2>&1; then
    fail "Git repo not found: $repo"
  fi
done

if [ "${#FAILURES[@]}" -gt 0 ]; then
  write_summary
  exit 1
fi
pass "backend/frontend local repos found"

run_logged_in "backend fetch Phase 3A + main" "$BACKEND_REPO" \
  git fetch origin main "$BACKEND_BRANCH" || {
    write_summary
    exit 1
  }
run_logged_in "frontend fetch Phase 3A + main" "$FRONTEND_REPO" \
  git fetch origin main "$FRONTEND_BRANCH" || {
    write_summary
    exit 1
  }

BACKEND_REMOTE_HEAD="$(git -C "$BACKEND_REPO" rev-parse "origin/$BACKEND_BRANCH")"
FRONTEND_REMOTE_HEAD="$(git -C "$FRONTEND_REPO" rev-parse "origin/$FRONTEND_BRANCH")"
log "Backend HEAD : $BACKEND_REMOTE_HEAD"
log "Frontend HEAD: $FRONTEND_REMOTE_HEAD"

run_logged_in "backend isolated worktree" "$BACKEND_REPO" \
  git worktree add --detach "$BACKEND_WORK" "origin/$BACKEND_BRANCH" || {
    write_summary
    exit 1
  }
run_logged_in "frontend isolated worktree" "$FRONTEND_REPO" \
  git worktree add --detach "$FRONTEND_WORK" "origin/$FRONTEND_BRANCH" || {
    write_summary
    exit 1
  }

if [ "$(git -C "$BACKEND_WORK" rev-parse HEAD)" = "$BACKEND_REMOTE_HEAD" ] &&
   [ "$(git -C "$FRONTEND_WORK" rev-parse HEAD)" = "$FRONTEND_REMOTE_HEAD" ]; then
  pass "isolated worktrees match remote Phase 3A HEADs"
else
  fail "isolated worktree HEAD mismatch"
fi

run_logged_in "backend committed diff check" "$BACKEND_WORK" \
  git diff --check origin/main...HEAD || true
run_logged_in "frontend committed diff check" "$FRONTEND_WORK" \
  git diff --check origin/main...HEAD || true

BACKEND_BUILD_OK=0
if run_logged_in "backend restore" "$BACKEND_WORK" \
  dotnet restore NeverfadePos.slnx; then
  if run_logged_in "backend Release build" "$BACKEND_WORK" \
    dotnet build NeverfadePos.slnx --configuration Release --no-restore; then
    BACKEND_BUILD_OK=1
  fi
fi

if [ "$BACKEND_BUILD_OK" -eq 1 ]; then
  run_logged_in "backend tests" "$BACKEND_WORK" \
    dotnet test NeverfadePos.Api.Tests/NeverfadePos.Api.Tests.csproj \
      --configuration Release \
      --no-build \
      --logger "console;verbosity=normal" || true
else
  fail "backend tests skipped because Release build failed"
fi

if dotnet ef --version >/dev/null 2>&1; then
  EF_MODE="global"
  pass "dotnet-ef available globally"
else
  if run_logged "install isolated dotnet-ef 10.0.9" \
    dotnet tool install dotnet-ef --tool-path "$TOOLS_DIR" --version 10.0.9; then
    EF_MODE="isolated"
  else
    fail "dotnet-ef unavailable"
  fi
fi

MIGRATION_READY=0
SEED_READY=0
BACKEND_READY=0

if [ -n "$EF_MODE" ] && [ "$BACKEND_BUILD_OK" -eq 1 ]; then
  export_common_backend_env
  export ConnectionStrings__DefaultConnection="Host=127.0.0.1;Port=$PG_PORT;Database=$MIGRATION_DB;Username=$PG_USER;Password=$PG_PASSWORD;Include Error Detail=true"

  run_logged_in "EF model has no pending snapshot changes" "$BACKEND_WORK" \
    ef_cmd migrations has-pending-model-changes \
      --project NeverfadePos.Api/NeverfadePos.Api.csproj \
      --startup-project NeverfadePos.Api/NeverfadePos.Api.csproj \
      --configuration Release \
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

    for ((attempt = 1; attempt <= 60; attempt++)); do
      if docker exec "$PG_CONTAINER" pg_isready \
        --username "$PG_USER" \
        --dbname "$MIGRATION_DB" >/dev/null 2>&1; then
        MIGRATION_READY=1
        break
      fi
      sleep 1
    done

    if [ "$MIGRATION_READY" -eq 1 ]; then
      pass "disposable PostgreSQL ready"
    else
      fail "disposable PostgreSQL did not become ready"
      docker logs "$PG_CONTAINER" 2>&1 | tail -100 | tee -a "$LOG_FILE" || true
    fi
  fi
else
  fail "migration gate prerequisites unavailable"
fi

if [ "$MIGRATION_READY" -eq 1 ]; then
  if psql_exec postgres "CREATE DATABASE \"$SEED_DB\";" >/dev/null 2>&1; then
    pass "created isolated seed database"
  else
    fail "failed to create isolated seed database"
  fi

  MIGRATION_CONN="$(connection_string "$MIGRATION_DB")"
  SEED_CONN="$(connection_string "$SEED_DB")"
  export ConnectionStrings__DefaultConnection="$MIGRATION_CONN"

  BASE_READY=0
  if run_logged_in "migrate production-like schema to pre-Phase-3 HEAD" "$BACKEND_WORK" \
    ef_cmd database update "$BASE_MIGRATION" \
      --project NeverfadePos.Api/NeverfadePos.Api.csproj \
      --startup-project NeverfadePos.Api/NeverfadePos.Api.csproj \
      --configuration Release \
      --no-build; then
    BASE_READY=1
  fi

  if [ "$BASE_READY" -eq 1 ]; then
    if psql_exec "$MIGRATION_DB" \
      "INSERT INTO tenants (\"Id\",\"NamaToko\",\"Slug\",\"CreatedAt\",\"Status\",\"UpdatedAt\") VALUES ('aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa','Legacy Phase3A QA','legacy-phase3a-qa',NOW(),'active',NOW());" \
      >/dev/null 2>&1; then
      pass "insert representative existing tenant before Phase 3A migration"
    else
      fail "insert representative existing tenant before Phase 3A migration"
    fi

    PHASE3_APPLIED=0
    if run_logged_in "apply Phase 3A migration" "$BACKEND_WORK" \
      ef_cmd database update "$PHASE3A_MIGRATION" \
        --project NeverfadePos.Api/NeverfadePos.Api.csproj \
        --startup-project NeverfadePos.Api/NeverfadePos.Api.csproj \
        --configuration Release \
        --no-build; then
      PHASE3_APPLIED=1
    fi

    if [ "$PHASE3_APPLIED" -eq 1 ]; then
      LEGACY_TYPE="$(psql_exec "$MIGRATION_DB" \
        "SELECT \"BusinessType\" FROM tenants WHERE \"Id\"='aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa';" \
        2>/dev/null | tail -1 || true)"
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

      if run_logged_in "rollback Phase 3A migration" "$BACKEND_WORK" \
        ef_cmd database update "$BASE_MIGRATION" \
          --project NeverfadePos.Api/NeverfadePos.Api.csproj \
          --startup-project NeverfadePos.Api/NeverfadePos.Api.csproj \
          --configuration Release \
          --no-build; then

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
          ef_cmd database update "$PHASE3A_MIGRATION" \
            --project NeverfadePos.Api/NeverfadePos.Api.csproj \
            --startup-project NeverfadePos.Api/NeverfadePos.Api.csproj \
            --configuration Release \
            --no-build || true
      fi
    fi
  fi

  export ConnectionStrings__DefaultConnection="$SEED_CONN"
  if run_logged_in "migrate clean seed database to Phase 3A" "$BACKEND_WORK" \
    ef_cmd database update "$PHASE3A_MIGRATION" \
      --project NeverfadePos.Api/NeverfadePos.Api.csproj \
      --startup-project NeverfadePos.Api/NeverfadePos.Api.csproj \
      --configuration Release \
      --no-build; then
    SEED_READY=1
  fi
fi

if [ "$SEED_READY" -eq 1 ]; then
  export_common_backend_env
  export ConnectionStrings__DefaultConnection="$(connection_string "$SEED_DB")"
  export ASPNETCORE_URLS="http://127.0.0.1:5012"

  (
    cd "$BACKEND_WORK"
    dotnet run \
      --project NeverfadePos.Api/NeverfadePos.Api.csproj \
      --configuration Release \
      --no-build \
      --no-launch-profile \
      >"$BACKEND_LOG" 2>&1
  ) &
  BACKEND_PID=$!

  if wait_for_http_status "http://127.0.0.1:5012/api/products" "401" 60; then
    BACKEND_READY=1
    pass "local backend started against disposable PostgreSQL"
  else
    fail "local backend failed to start"
    tail -n 120 "$BACKEND_LOG" 2>/dev/null | tee -a "$LOG_FILE" || true
  fi
fi

if [ "$BACKEND_READY" -eq 1 ]; then
  SEED_TYPE="$(psql_exec "$SEED_DB" \
    "SELECT \"BusinessType\" FROM tenants WHERE \"Slug\"='warung-lumpia-beef';" \
    2>/dev/null | tail -1 || true)"
  SEED_USERS="$(psql_exec "$SEED_DB" "SELECT COUNT(*) FROM users;" 2>/dev/null | tail -1 || true)"
  SEED_PRODUCTS="$(psql_exec "$SEED_DB" "SELECT COUNT(*) FROM products;" 2>/dev/null | tail -1 || true)"

  if [ "$SEED_TYPE" = "general_retail" ] &&
     [ "${SEED_USERS:-0}" -ge 3 ] &&
     [ "${SEED_PRODUCTS:-0}" -ge 10 ]; then
    pass "development seed compatible with Phase 3A migration"
  else
    fail "seed compatibility mismatch: type=$SEED_TYPE users=$SEED_USERS products=$SEED_PRODUCTS"
  fi

  mkdir -p "$RESULT_ROOT/backend-regressions"
  ln -s "$BACKEND_WORK" "$QA_HOME/neverfade-pos-backend"
  ln -s "$RESULT_ROOT/backend-regressions" "$QA_HOME/neverfade-pos-qa"

  export QA_BACKEND_ROOT="$BACKEND_WORK"
  export QA_PROJECT_ROOT="$BACKEND_WORK/NeverfadePos.Api"
  export QA_API_URL="http://127.0.0.1:5012"
  export QA_CONNECTION_STRING="$(connection_string "$SEED_DB")"
  export QA_OWNER_USERNAME="owner"
  export QA_OWNER_PASSWORD="owner123"
  export QA_PLATFORM_JWT_KEY="$PlatformJwt__Key"
  export QA_PLATFORM_JWT_ISSUER="$PlatformJwt__Issuer"
  export QA_PLATFORM_JWT_AUDIENCE="$PlatformJwt__Audience"

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
    run_logged "backend regression: $regression" \
      env \
        HOME="$QA_HOME" \
        QA_BACKEND_ROOT="$QA_BACKEND_ROOT" \
        QA_PROJECT_ROOT="$QA_PROJECT_ROOT" \
        QA_API_URL="$QA_API_URL" \
        QA_CONNECTION_STRING="$QA_CONNECTION_STRING" \
        QA_OWNER_USERNAME="$QA_OWNER_USERNAME" \
        QA_OWNER_PASSWORD="$QA_OWNER_PASSWORD" \
        QA_PLATFORM_JWT_KEY="$QA_PLATFORM_JWT_KEY" \
        QA_PLATFORM_JWT_ISSUER="$QA_PLATFORM_JWT_ISSUER" \
        QA_PLATFORM_JWT_AUDIENCE="$QA_PLATFORM_JWT_AUDIENCE" \
        bash "$BACKEND_WORK/$regression" || true
  done
fi

FRONTEND_DEPS_OK=0
if run_logged_in "frontend npm ci" "$FRONTEND_WORK" npm ci; then
  FRONTEND_DEPS_OK=1
fi

if [ "$FRONTEND_DEPS_OK" -eq 1 ]; then
  run_logged_in "frontend type/build" "$FRONTEND_WORK" npm run build || true
  run_logged_in "frontend lint" "$FRONTEND_WORK" npm run lint || true

  if run_logged_in "Playwright Chromium install" "$FRONTEND_WORK" \
    npx playwright install chromium; then

    (
      cd "$FRONTEND_WORK"
      VITE_API_URL="http://127.0.0.1:5012" \
        npm run dev -- --host 127.0.0.1 --port 5273 --strictPort \
        >"$FRONTEND_LOG" 2>&1
    ) &
    VITE_PID=$!

    if wait_for_http_status "http://127.0.0.1:5273/login" "200" 60; then
      pass "local frontend dev server started"

      if [ "$BACKEND_READY" -eq 1 ]; then
        QA_KNOWN_TRANSACTION_NO="$(psql_exec "$SEED_DB" \
          "SELECT \"NoTrx\" FROM transactions ORDER BY \"CreatedAt\" DESC LIMIT 1;" \
          2>/dev/null | tail -1 || true)"
        export QA_KNOWN_TRANSACTION_NO
      else
        QA_KNOWN_TRANSACTION_NO=""
        export QA_KNOWN_TRANSACTION_NO
      fi

      if [ -n "$QA_KNOWN_TRANSACTION_NO" ]; then
        pass "known local transaction available for browser regression"
      else
        fail "known local transaction unavailable for browser regression"
      fi

      run_logged_in "full Playwright regression (desktop/tablet/mobile)" "$FRONTEND_WORK" \
        env \
          QA_API_URL="http://127.0.0.1:5012" \
          QA_OWNER_USERNAME="owner" \
          QA_OWNER_PASSWORD="owner123" \
          QA_KNOWN_TRANSACTION_NO="$QA_KNOWN_TRANSACTION_NO" \
          RUN_PRODUCTION_MOBILE_AUDIT="0" \
          npx playwright test || true
    else
      fail "local frontend dev server failed to start"
      tail -n 120 "$FRONTEND_LOG" 2>/dev/null | tee -a "$LOG_FILE" || true
    fi
  fi
fi

BACKEND_TRACKED_STATUS="$(git -C "$BACKEND_WORK" status --porcelain --untracked-files=no)"
FRONTEND_TRACKED_STATUS="$(git -C "$FRONTEND_WORK" status --porcelain --untracked-files=no)"

if [ -z "$BACKEND_TRACKED_STATUS" ]; then
  pass "backend isolated worktree has no tracked mutations"
else
  fail "backend isolated worktree has tracked mutations"
  printf '%s\n' "$BACKEND_TRACKED_STATUS" | tee -a "$LOG_FILE"
fi

if [ -z "$FRONTEND_TRACKED_STATUS" ]; then
  pass "frontend isolated worktree has no tracked mutations"
else
  fail "frontend isolated worktree has tracked mutations"
  printf '%s\n' "$FRONTEND_TRACKED_STATUS" | tee -a "$LOG_FILE"
fi

write_summary

if [ "${#FAILURES[@]}" -eq 0 ]; then
  exit 0
fi

exit 1
