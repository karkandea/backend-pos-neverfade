#!/usr/bin/env bash
# EX02 isolated VPS verification. Native .NET/npm/Playwright; only PostgreSQL uses Docker.
# GitHub Actions is optional. This script NEVER deploys or edits production resources.
set -Eeuo pipefail
umask 077
ROOT="/opt/neverfade-work/ex02-vps-gate-$(date +%Y%m%dT%H%M%S)-$$"
PG_NAME="nf-ex02-gate-pg-$$"
PG_PORT=55437
VITE_PID=""
mkdir -p "$ROOT/neverfade-pos-qa"
LOG="$ROOT/qa-gate.log"
exec > >(tee -a "$LOG") 2>&1
cleanup() {
  rc=$?
  trap - EXIT
  if [ -n "$VITE_PID" ]; then kill "$VITE_PID" >/dev/null 2>&1 || true; fi
  docker rm -f "$PG_NAME" >/dev/null 2>&1 || true
  echo "EX02 VPS GATE EXIT=$rc"
  echo "Evidence: $LOG"
  echo "Production: NOT MODIFIED"
  exit "$rc"
}
trap cleanup EXIT

for tool in git docker dotnet node npm curl openssl psql; do
  command -v "$tool" >/dev/null || { echo "Missing $tool"; exit 1; }
done
docker info >/dev/null
free -h
df -h /
docker ps --format 'table {{.Names}}\t{{.Status}}' | head -30
echo "Using native toolchain: $(node -v), $(dotnet --version)"
echo "VPS gate workspace: $ROOT"

clone_exact() {
  name="$1"; branch="$2"; short="$3"; expected="$4"
  git clone -q --depth 1 --single-branch --branch "$branch" \
    "https://github.com/karkandea/$name.git" "$ROOT/$short"
  actual="$(git -C "$ROOT/$short" rev-parse HEAD)"
  [ "$actual" = "$expected" ] || { echo "Wrong source SHA ($short): $actual != $expected"; exit 1; }
  echo "$short: $actual"
}
clone_exact backend-pos-neverfade feat/endgame-v2-ex02-mfa-crypto-foundation be-mfa 7f126795bdc243dfb553dd57b181f3dcbd0c8ed6
clone_exact backend-pos-neverfade feat/endgame-v2-ex02-alert-backfill be-alert 2337306fa603df7bc54d4557c4dce684770ec6a8
clone_exact frontend-pos-neverfade feat/endgame-v2-ex02-mfa-ui fe-mfa 4d31bd3de7084b2918b4079fad7da355c04ab380
clone_exact frontend-pos-neverfade feat/endgame-v2-ex02-profit-report-ux fe-profit a54b2825192a75acb9ed64e81bcac1ad585adbd6
clone_exact frontend-pos-neverfade feat/endgame-v2-ex02-ops-alerts-dashboard fe-ops 76522fbf7224cb532f7b60ad8d11eb7c9cd7543f

for part in be-mfa be-alert; do
  echo "=== .NET Release suite $part ==="
  (cd "$ROOT/$part"; dotnet test NeverfadePos.Api.Tests/NeverfadePos.Api.Tests.csproj -c Release --logger 'console;verbosity=minimal')
done

# First install the exact Chromium revision expected by Playwright.
(cd "$ROOT/fe-mfa"; npm ci --no-audit --no-fund; npx playwright install chromium --only-shell)
frontend_gate() {
  folder="$1"; spec="$2"; port="$3"
  cd "$ROOT/$folder"
  echo "=== $folder lint/type/build/Playwright ($port) ==="
  if [ "$folder" != fe-mfa ]; then npm ci --no-audit --no-fund; fi
  npm run build # runs lint via prebuild, then TypeScript and Vite
  ./node_modules/.bin/vite --host 127.0.0.1 --port "$port" --strictPort > "$ROOT/$folder-vite.log" 2>&1 &
  VITE_PID=$!
  ready=0
  for n in $(seq 1 40); do
    if ! kill -0 "$VITE_PID" 2>/dev/null; then break; fi
    if curl --silent --fail "http://127.0.0.1:$port/login" >/dev/null; then ready=1; break; fi
    sleep 1
  done
  [ "$ready" = 1 ] || { cat "$ROOT/$folder-vite.log"; exit 1; }
  PLAYWRIGHT_BASE_URL="http://127.0.0.1:$port" npx playwright test "$spec" --project="Desktop Chromium"
  cp "$ROOT/neverfade-pos-qa/playwright-result.json" "$ROOT/$folder-playwright-result.json" 2>/dev/null || true
  kill "$VITE_PID" >/dev/null 2>&1 || true
  wait "$VITE_PID" >/dev/null 2>&1 || true
  VITE_PID=""
  cd "$ROOT"
}
frontend_gate fe-mfa tests/e2e/ex02-mfa.spec.ts 5273
frontend_gate fe-profit tests/e2e/ex02-profit-report.spec.ts 5274
frontend_gate fe-ops tests/e2e/ex02-ops-attention.spec.ts 5275

# Disposable, loopback-only PostgreSQL. No access to production database credentials.
if ss -ltn | grep -q ":$PG_PORT "; then echo "QA port $PG_PORT occupied"; exit 1; fi
PG_PASS="$(openssl rand -hex 12)"
docker run -d --rm --name "$PG_NAME" --cpus=0.5 --memory=512m \
  -p "127.0.0.1:$PG_PORT:5432" -e POSTGRES_DB=nf_ex02_qa \
  -e POSTGRES_PASSWORD="$PG_PASS" postgres:17-alpine >/dev/null
ready=0
for n in $(seq 1 40); do
  if docker exec "$PG_NAME" pg_isready -U postgres -d nf_ex02_qa >/dev/null 2>&1; then ready=1; break; fi
  sleep 1
done
[ "$ready" = 1 ] || { echo "Disposable PG never became ready"; exit 1; }
export ConnectionStrings__DefaultConnection="Host=127.0.0.1;Port=$PG_PORT;Database=nf_ex02_qa;Username=postgres;Password=$PG_PASS"
dotnet tool install --tool-path "$ROOT/.tools" dotnet-ef --version 10.0.9
cd "$ROOT/be-mfa"
ef() { "$ROOT/.tools/dotnet-ef" "$@" --project NeverfadePos.Api/NeverfadePos.Api.csproj --startup-project NeverfadePos.Api/NeverfadePos.Api.csproj; }
sql() { docker exec "$PG_NAME" psql -Atqc "$1" -U postgres -d nf_ex02_qa; }
ef database update
last="$(sql 'SELECT "MigrationId" FROM "__EFMigrationsHistory" ORDER BY "MigrationId" DESC LIMIT 1;')"
prev="$(sql 'SELECT "MigrationId" FROM "__EFMigrationsHistory" ORDER BY "MigrationId" DESC OFFSET 1 LIMIT 1;')"
[ "$last" = 20261012102000_AddEndgameV2MfaCredentials ] && [ -n "$prev" ]
num="$(sql 'SELECT COUNT(*) FROM "__EFMigrationsHistory";')"
ef database update
[ "$(sql 'SELECT COUNT(*) FROM "__EFMigrationsHistory";')" = "$num" ]
ef database update "$prev"
[ "$(sql "SELECT COUNT(*) FROM information_schema.tables WHERE table_name='mfa_credentials';")" = 0 ]
ef database update
[ "$(sql "SELECT COUNT(*) FROM information_schema.tables WHERE table_name='mfa_credentials';")" = 1 ]
ef migrations has-pending-model-changes
echo "FINAL EX02 VPS SOURCE GATE: PASS"
echo "NOTE: isolated live-API tenant/role integration and external payment provider certification require separate QA."
