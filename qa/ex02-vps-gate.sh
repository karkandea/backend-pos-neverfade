#!/usr/bin/env bash
# Neverfade EX02 VPS QA gate (isolated). Does not touch production containers, databases, or releases.
set -Eeuo pipefail
umask 077

RUN_ID="$(date -u +%Y%m%dT%H%M%SZ)-$$"
ROOT="$HOME/neverfade-ex02-qa/$RUN_ID"
LOG="$ROOT/gate.log"
NET="nf-ex02-$RUN_ID"
PG="nf-ex02-pg-$RUN_ID"
SDK="mcr.microsoft.com/dotnet/sdk:10.0"
PW="mcr.microsoft.com/playwright:v1.62.0-noble"
PG_IMAGE="postgres:16-alpine"
DB_PASS="ex02-disposable-$RUN_ID"
DB_CONN="Host=$PG;Port=5432;Database=ex02_qa;Username=postgres;Password=$DB_PASS"

mkdir -p "$ROOT"
exec > >(tee -a "$LOG") 2>&1

cleanup() {
  local status=$?
  trap - EXIT
  docker rm -f "$PG" >/dev/null 2>&1 || true
  docker network rm "$NET" >/dev/null 2>&1 || true
  echo
  if [ "$status" -eq 0 ]; then
    echo "FINAL NEVERFADE EX02 VPS SOURCE GATE: PASS"
  else
    echo "FINAL NEVERFADE EX02 VPS SOURCE GATE: FAIL (exit $status)"
  fi
  echo "Evidence: $LOG"
  echo "Production: NOT MODIFIED; PostgreSQL: DISPOSABLE ONLY"
  exit "$status"
}
trap cleanup EXIT

for cmd in docker git; do
  command -v "$cmd" >/dev/null || { echo "Missing prerequisite: $cmd"; exit 1; }
done
docker info >/dev/null || { echo "Docker Engine unavailable"; exit 1; }

echo "== EX02 isolated VPS gate =="
echo "Run: $RUN_ID"
echo "Host snapshot:"
free -h || true
df -h "$HOME" || true
docker ps --format 'table {{.Names}}\t{{.Status}}' || true

# Exact SHAs prevent accidentally testing a moving head.
clone_exact() {
  local repo="$1" branch="$2" expected="$3" destination="$4"
  git clone --quiet --depth 1 --single-branch --branch "$branch" \
    "https://github.com/karkandea/$repo.git" "$destination"
  local actual
  actual="$(git -C "$destination" rev-parse HEAD)"
  echo "$repo / $branch: $actual"
  if [ "$actual" != "$expected" ]; then
    echo "Branch advanced. Expected $expected; refusing mixed-SHA test."
    exit 1
  fi
}
clone_exact backend-pos-neverfade feat/endgame-v2-ex02-mfa-crypto-foundation \
  6a99ffc599f50bab61cf279c697310fa1eb7454d "$ROOT/be-mfa"
clone_exact backend-pos-neverfade feat/endgame-v2-ex02-alert-backfill \
  2337306fa603df7bc54d4557c4dce684770ec6a8 "$ROOT/be-alert"
clone_exact frontend-pos-neverfade feat/endgame-v2-ex02-mfa-ui \
  4d31bd3de7084b2918b4079fad7da355c04ab380 "$ROOT/fe-mfa"
clone_exact frontend-pos-neverfade feat/endgame-v2-ex02-profit-report-ux \
  e7186f454cbeaaeef8c4394345940eb764ca3a6b "$ROOT/fe-profit"
clone_exact frontend-pos-neverfade feat/endgame-v2-ex02-ops-alerts-dashboard \
  76522fbf7224cb532f7b60ad8d11eb7c9cd7543f "$ROOT/fe-ops"

docker volume create nf-ex02-nuget >/dev/null
docker volume create nf-ex02-npm >/dev/null

backend_test() {
  local path="$1" label="$2"
  echo "== Backend .NET release tests: $label =="
  docker run --rm --cpus=1 --memory=2g \
    -e DOTNET_CLI_TELEMETRY_OPTOUT=1 -e DOTNET_NOLOGO=1 \
    -v nf-ex02-nuget:/root/.nuget/packages \
    -v "$path:/workspace" -w /workspace "$SDK" \
    bash -lc 'set -euo pipefail
      dotnet restore NeverfadePos.Api.Tests/NeverfadePos.Api.Tests.csproj
      dotnet test NeverfadePos.Api.Tests/NeverfadePos.Api.Tests.csproj \
        -c Release --no-restore --logger "console;verbosity=normal"'
  echo "PASS: $label .NET tests"
}
backend_test "$ROOT/be-mfa" "MFA"
backend_test "$ROOT/be-alert" "Operations alert backlog"

echo "== Disposable PostgreSQL MFA migration forward / repeat / rollback / reapply =="
docker network create "$NET" >/dev/null
docker run -d --rm --name "$PG" --network "$NET" --cpus=0.5 --memory=512m \
  -e POSTGRES_PASSWORD="$DB_PASS" -e POSTGRES_DB=ex02_qa "$PG_IMAGE" >/dev/null
ready=0
for i in $(seq 1 60); do
  if docker exec "$PG" pg_isready -U postgres -d ex02_qa >/dev/null 2>&1; then
    ready=1; break
  fi
  sleep 1
done
[ "$ready" -eq 1 ] || { echo "PostgreSQL not ready"; exit 1; }

pg_sql() {
  docker exec "$PG" psql -Atqc "$1" -U postgres -d ex02_qa
}
ef() {
  docker run --rm --network "$NET" --cpus=1 --memory=2g \
    -e DOTNET_CLI_TELEMETRY_OPTOUT=1 -e DOTNET_NOLOGO=1 \
    -e "ConnectionStrings__DefaultConnection=$DB_CONN" \
    -v nf-ex02-nuget:/root/.nuget/packages \
    -v "$ROOT/be-mfa:/workspace" -w /workspace "$SDK" \
    bash -lc 'set -euo pipefail
      dotnet tool install --global dotnet-ef --version 10.0.9 >/dev/null
      export PATH="$PATH:/root/.dotnet/tools"
      dotnet ef "$@" --project NeverfadePos.Api/NeverfadePos.Api.csproj \
        --startup-project NeverfadePos.Api/NeverfadePos.Api.csproj' _ "$@"
}
ef database update
latest="$(pg_sql 'SELECT "MigrationId" FROM "__EFMigrationsHistory" ORDER BY "MigrationId" DESC LIMIT 1;')"
previous="$(pg_sql 'SELECT "MigrationId" FROM "__EFMigrationsHistory" ORDER BY "MigrationId" DESC OFFSET 1 LIMIT 1;')"
[ "$latest" = "20261012102000_AddEndgameV2MfaCredentials" ] || { echo "Unexpected latest migration: $latest"; exit 1; }
[ -n "$previous" ] || { echo "Missing previous migration"; exit 1; }
[ "$(pg_sql "SELECT COUNT(*) FROM information_schema.tables WHERE table_name='mfa_credentials';")" = 1 ] || exit 1
count="$(pg_sql 'SELECT COUNT(*) FROM "__EFMigrationsHistory";')"
ef database update
[ "$(pg_sql 'SELECT COUNT(*) FROM "__EFMigrationsHistory";')" = "$count" ] || exit 1
ef database update "$previous"
[ "$(pg_sql 'SELECT "MigrationId" FROM "__EFMigrationsHistory" ORDER BY "MigrationId" DESC LIMIT 1;')" = "$previous" ] || exit 1
ef database update
[ "$(pg_sql 'SELECT "MigrationId" FROM "__EFMigrationsHistory" ORDER BY "MigrationId" DESC LIMIT 1;')" = "$latest" ] || exit 1
ef migrations has-pending-model-changes
echo "PASS: disposable PostgreSQL migration (forward/repeat/rollback/reapply/model)"

frontend_test() {
  local path="$1" label="$2" spec="$3"
  echo "== Frontend build + Playwright: $label =="
  local report="$ROOT/report-$label"
  mkdir -p "$report"
  docker run --rm --cpus=1 --memory=3g --shm-size=1g \
    -e CI=1 -e VITE_API_URL=http://127.0.0.1:9999 \
    -e PLAYWRIGHT_BASE_URL=http://127.0.0.1:5273 \
    -e "SPEC=$spec" \
    -v nf-ex02-npm:/root/.npm \
    -v "$path:/workspace/frontend" \
    -v "$report:/workspace/neverfade-pos-qa" \
    -w /workspace/frontend "$PW" bash -lc '
      set -euo pipefail
      npm ci --no-audit --no-fund
      npm run build
      npm run dev -- --host 127.0.0.1 --port 5273 --strictPort > /tmp/ex02-vite.log 2>&1 &
      vite_pid=$!
      trap "kill $vite_pid >/dev/null 2>&1 || true" EXIT
      ready=0
      for i in $(seq 1 60); do
        if node -e "fetch(\"http://127.0.0.1:5273/login\").then(r=>process.exit(r.ok?0:1)).catch(()=>process.exit(1))"; then
          ready=1; break
        fi
        sleep 1
      done
      if [ "$ready" -ne 1 ]; then cat /tmp/ex02-vite.log; exit 1; fi
      npx playwright test "$SPEC" --project="Desktop Chromium"
    '
  echo "PASS: $label frontend + targeted Playwright"
}
frontend_test "$ROOT/fe-mfa" mfa tests/e2e/ex02-mfa.spec.ts
frontend_test "$ROOT/fe-profit" profit tests/e2e/ex02-profit-report.spec.ts
frontend_test "$ROOT/fe-ops" ops tests/e2e/ex02-ops-attention.spec.ts

echo "== Exact-commit component gates complete =="
echo "Note: backend/frontend cross-PR integration, live QA permissions and provider certification remain separate release gates."
