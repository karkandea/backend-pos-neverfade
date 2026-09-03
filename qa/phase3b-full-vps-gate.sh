#!/usr/bin/env bash
set -Eeuo pipefail

BRANCH="feat/phase-3b-shared-device-attendance"
BACKEND_REPO="${NF_PHASE3B_BACKEND_REPO:-$HOME/neverfade-phase3b/backend}"
FRONTEND_REPO="${NF_PHASE3B_FRONTEND_REPO:-$HOME/neverfade-phase3b/frontend}"
SDK_IMAGE="mcr.microsoft.com/dotnet/sdk:10.0"
PW_IMAGE="mcr.microsoft.com/playwright:v1.62.0-noble"
PG_IMAGE="postgres:16-alpine"
NUGET_VOLUME="neverfade-phase3b-nuget"
NPM_VOLUME="neverfade-phase3b-npm"
RUN_ID="$(date +%Y%m%d%H%M%S)-$$"
PG_CONTAINER="nf-phase3b-full-pg-$RUN_ID"
BACKEND_CONTAINER="nf-phase3b-full-api-$RUN_ID"
PG_PORT="${NF_PHASE3B_PG_PORT:-55434}"
API_PORT="${NF_PHASE3B_API_PORT:-5012}"
FRONTEND_PORT="${NF_PHASE3B_FRONTEND_PORT:-5273}"
DB_NAME="neverfade_phase3b_full"
DB_USER="postgres"
DB_PASSWORD="phase3b-full-local-only"
QA_DIR="${NF_PHASE3B_QA_DIR:-$HOME/neverfade-pos-qa/phase3b-full-$RUN_ID}"
PROJECT="NeverfadePos.Api/NeverfadePos.Api.csproj"
TEST_PROJECT="NeverfadePos.Api.Tests/NeverfadePos.Api.Tests.csproj"
CONNECTION="Host=127.0.0.1;Port=$PG_PORT;Database=$DB_NAME;Username=$DB_USER;Password=$DB_PASSWORD;Include Error Detail=true"
JWT_KEY="phase3b-tenant-jwt-key-0123456789abcdef0123456789abcdef"
JWT_ISSUER="NeverfadePos.Phase3B.Local"
JWT_AUDIENCE="NeverfadePos.Phase3B.Local.Client"
PLATFORM_JWT_KEY="phase3b-platform-jwt-key-fedcba9876543210fedcba9876543210"
PLATFORM_JWT_ISSUER="NeverfadePos.Platform.Phase3B.Local"
PLATFORM_JWT_AUDIENCE="NeverfadePos.Platform.Phase3B.Local.Client"

fail() {
  printf '\n[FAIL] %s\n' "$1" >&2
  exit 1
}

step() {
  printf '\n==> %s\n' "$1"
}

cleanup() {
  docker rm -f "$BACKEND_CONTAINER" >/dev/null 2>&1 || true
  docker rm -f "$PG_CONTAINER" >/dev/null 2>&1 || true
}
trap cleanup EXIT INT TERM

[[ "$(uname -s)" == "Linux" ]] || fail "Full gate ini memakai Docker host-network dan harus dijalankan di VPS/Linux."
[[ -d "$BACKEND_REPO/.git" ]] || fail "Backend repo tidak ditemukan di $BACKEND_REPO"
[[ -d "$FRONTEND_REPO/.git" ]] || fail "Frontend repo tidak ditemukan di $FRONTEND_REPO"
command -v docker >/dev/null 2>&1 || fail "Docker tidak tersedia"
command -v git >/dev/null 2>&1 || fail "git tidak tersedia"
command -v curl >/dev/null 2>&1 || fail "curl tidak tersedia"
docker info >/dev/null 2>&1 || fail "Docker engine tidak aktif"

mkdir -p "$QA_DIR"

port_in_use() {
  local port="$1"
  if command -v ss >/dev/null 2>&1; then
    ss -ltnH | awk '{print $4}' | grep -Eq "(^|:)$port$"
    return
  fi
  return 1
}

for port in "$PG_PORT" "$API_PORT" "$FRONTEND_PORT"; do
  if port_in_use "$port"; then
    fail "Port $port sedang dipakai. Gate berhenti agar tidak menyentuh service lain."
  fi
done

step "Verify and sync exact Phase 3B branches"
for repo in "$BACKEND_REPO" "$FRONTEND_REPO"; do
  if [[ -n "$(git -C "$repo" status --porcelain)" ]]; then
    git -C "$repo" status --short
    fail "Repo harus clean: $repo"
  fi
  git -C "$repo" fetch origin "$BRANCH"
  git -C "$repo" switch "$BRANCH"
  git -C "$repo" pull --ff-only origin "$BRANCH"
done

BACKEND_HEAD="$(git -C "$BACKEND_REPO" rev-parse HEAD)"
FRONTEND_HEAD="$(git -C "$FRONTEND_REPO" rev-parse HEAD)"
printf 'Backend HEAD : %s\n' "$BACKEND_HEAD"
printf 'Frontend HEAD: %s\n' "$FRONTEND_HEAD"

step "Start disposable PostgreSQL on loopback only"
docker run -d --rm \
  --name "$PG_CONTAINER" \
  --cpus=0.5 \
  --memory=512m \
  -p "127.0.0.1:$PG_PORT:5432" \
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

docker volume inspect "$NUGET_VOLUME" >/dev/null 2>&1 || docker volume create "$NUGET_VOLUME" >/dev/null
docker volume inspect "$NPM_VOLUME" >/dev/null 2>&1 || docker volume create "$NPM_VOLUME" >/dev/null

run_dotnet() {
  docker run --rm \
    --network host \
    --cpus=1 \
    --memory=2g \
    -e DOTNET_CLI_TELEMETRY_OPTOUT=1 \
    -e DOTNET_NOLOGO=1 \
    -e NUGET_PACKAGES=/root/.nuget/packages \
    -e "ConnectionStrings__DefaultConnection=$CONNECTION" \
    -e ASPNETCORE_ENVIRONMENT=Development \
    -e DOTNET_ENVIRONMENT=Development \
    -e "Jwt__Key=$JWT_KEY" \
    -e "Jwt__Issuer=$JWT_ISSUER" \
    -e "Jwt__Audience=$JWT_AUDIENCE" \
    -e "PlatformJwt__Key=$PLATFORM_JWT_KEY" \
    -e "PlatformJwt__Issuer=$PLATFORM_JWT_ISSUER" \
    -e "PlatformJwt__Audience=$PLATFORM_JWT_AUDIENCE" \
    -e Payments__Mode=Disabled \
    -e Payments__LiveEnabled=false \
    -e Payments__SandboxAllowedTenantIds= \
    -e Payments__LiveAllowedTenantIds= \
    -e PlatformBootstrap__Enabled=false \
    -e Bootstrap__Enabled=false \
    -v "$NUGET_VOLUME:/root/.nuget/packages" \
    -v "$BACKEND_REPO:/workspace" \
    -w /workspace \
    "$SDK_IMAGE" bash -lc "$1"
}

step "Backend restore + Release build + runtime tests"
run_dotnet "dotnet restore NeverfadePos.slnx && dotnet build NeverfadePos.slnx --configuration Release --no-restore && dotnet test '$TEST_PROJECT' --configuration Release --no-build --logger 'console;verbosity=normal'"

step "Migrate clean disposable database through latest Phase 3B"
run_dotnet "dotnet restore '$PROJECT' >/dev/null && rm -rf /tmp/dotnet-tools && dotnet tool install --tool-path /tmp/dotnet-tools dotnet-ef --version 10.0.9 >/dev/null && /tmp/dotnet-tools/dotnet-ef database update --project '$PROJECT' --startup-project '$PROJECT' --configuration Release --no-build"

step "Start Phase 3B backend against disposable database"
docker run -d --rm \
  --name "$BACKEND_CONTAINER" \
  --network host \
  --cpus=1 \
  --memory=2g \
  -e DOTNET_CLI_TELEMETRY_OPTOUT=1 \
  -e DOTNET_NOLOGO=1 \
  -e NUGET_PACKAGES=/root/.nuget/packages \
  -e ASPNETCORE_ENVIRONMENT=Development \
  -e DOTNET_ENVIRONMENT=Development \
  -e "ASPNETCORE_URLS=http://127.0.0.1:$API_PORT" \
  -e "ConnectionStrings__DefaultConnection=$CONNECTION" \
  -e "Jwt__Key=$JWT_KEY" \
  -e "Jwt__Issuer=$JWT_ISSUER" \
  -e "Jwt__Audience=$JWT_AUDIENCE" \
  -e "PlatformJwt__Key=$PLATFORM_JWT_KEY" \
  -e "PlatformJwt__Issuer=$PLATFORM_JWT_ISSUER" \
  -e "PlatformJwt__Audience=$PLATFORM_JWT_AUDIENCE" \
  -e Payments__Mode=Disabled \
  -e Payments__LiveEnabled=false \
  -e Payments__SandboxAllowedTenantIds= \
  -e Payments__LiveAllowedTenantIds= \
  -e PlatformBootstrap__Enabled=false \
  -e Bootstrap__Enabled=false \
  -v "$NUGET_VOLUME:/root/.nuget/packages" \
  -v "$BACKEND_REPO:/workspace" \
  -w /workspace \
  "$SDK_IMAGE" \
  dotnet run --project "$PROJECT" --configuration Release --no-build --no-launch-profile \
  >/dev/null

backend_ready=0
for _ in $(seq 1 90); do
  status="$(curl -sS -o /dev/null -w '%{http_code}' --max-time 2 "http://127.0.0.1:$API_PORT/api/products" 2>/dev/null || true)"
  if [[ "$status" == "401" ]]; then
    backend_ready=1
    break
  fi
  if ! docker inspect "$BACKEND_CONTAINER" >/dev/null 2>&1; then
    break
  fi
  sleep 1
done
if [[ "$backend_ready" -ne 1 ]]; then
  docker logs "$BACKEND_CONTAINER" 2>&1 | tail -120 || true
  fail "Backend Phase 3B tidak ready di 127.0.0.1:$API_PORT"
fi

owner_count="$(docker exec -e PGPASSWORD="$DB_PASSWORD" "$PG_CONTAINER" psql -Atqc "SELECT COUNT(*) FROM users WHERE \"Username\"='owner' AND \"Active\"=true;" -U "$DB_USER" -d "$DB_NAME")"
[[ "$owner_count" == "1" ]] || fail "Development seed owner tidak tersedia"

step "Existing backend regression scripts against disposable backend"
docker run --rm \
  --network host \
  --cpus=0.75 \
  --memory=768m \
  -e QA_API_URL="http://127.0.0.1:$API_PORT" \
  -e QA_OWNER_USERNAME=owner \
  -e QA_OWNER_PASSWORD=owner123 \
  -e QA_CONNECTION_STRING="$CONNECTION" \
  -e QA_PLATFORM_JWT_KEY="$PLATFORM_JWT_KEY" \
  -e QA_PLATFORM_JWT_ISSUER="$PLATFORM_JWT_ISSUER" \
  -e QA_PLATFORM_JWT_AUDIENCE="$PLATFORM_JWT_AUDIENCE" \
  -v "$BACKEND_REPO:/root/neverfade-pos-backend:ro" \
  -v "$QA_DIR:/root/neverfade-pos-qa" \
  ubuntu:24.04 bash -lc '
    set -euo pipefail
    export DEBIAN_FRONTEND=noninteractive
    apt-get update -qq
    apt-get install -y -qq curl jq openssl uuid-runtime ca-certificates postgresql-client python3 git >/dev/null
    failures=0
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
      echo "===== $regression ====="
      if bash "/root/neverfade-pos-backend/$regression"; then
        echo "[PASS] $regression"
      else
        echo "[FAIL] $regression"
        failures=$((failures + 1))
      fi
    done
    [[ $failures -eq 0 ]]
  '

QA_KNOWN_TRANSACTION_NO="$(docker exec -e PGPASSWORD="$DB_PASSWORD" "$PG_CONTAINER" psql -Atqc 'SELECT "NoTrx" FROM transactions ORDER BY "CreatedAt" DESC LIMIT 1;' -U "$DB_USER" -d "$DB_NAME")"
printf 'Known QA transaction: %s\n' "${QA_KNOWN_TRANSACTION_NO:-<none>}"

step "Frontend npm ci + build/type + lint + full Playwright regression"
docker run --rm \
  --network host \
  --cpus=1 \
  --memory=3g \
  --shm-size=1g \
  -e CI=1 \
  -e "PLAYWRIGHT_BASE_URL=http://127.0.0.1:$FRONTEND_PORT" \
  -e "VITE_API_URL=http://127.0.0.1:$API_PORT" \
  -e "QA_API_URL=http://127.0.0.1:$API_PORT" \
  -e QA_OWNER_USERNAME=owner \
  -e QA_OWNER_PASSWORD=owner123 \
  -e "QA_KNOWN_TRANSACTION_NO=$QA_KNOWN_TRANSACTION_NO" \
  -e RUN_PRODUCTION_MOBILE_AUDIT=0 \
  -v "$NPM_VOLUME:/root/.npm" \
  -v "$FRONTEND_REPO:/workspace/frontend" \
  -v "$QA_DIR:/workspace/neverfade-pos-qa" \
  -w /workspace/frontend \
  "$PW_IMAGE" bash -lc "
    set -euo pipefail
    npm ci
    npm run build
    npm run lint
    npm run dev -- --host 127.0.0.1 --port '$FRONTEND_PORT' --strictPort >/tmp/nf-phase3b-vite.log 2>&1 &
    vite_pid=\$!
    trap 'kill \$vite_pid >/dev/null 2>&1 || true' EXIT
    ready=0
    for _ in \$(seq 1 60); do
      if node -e 'fetch(\"http://127.0.0.1:$FRONTEND_PORT/login\").then(r=>process.exit(r.ok?0:1)).catch(()=>process.exit(1))'; then
        ready=1
        break
      fi
      sleep 1
    done
    if [[ \$ready -ne 1 ]]; then
      cat /tmp/nf-phase3b-vite.log
      exit 1
    fi
    npx playwright test
  "

step "Final repository cleanliness"
if [[ -n "$(git -C "$BACKEND_REPO" status --porcelain --untracked-files=no)" ]]; then
  git -C "$BACKEND_REPO" status --short
  fail "Backend gate mengubah tracked files"
fi
if [[ -n "$(git -C "$FRONTEND_REPO" status --porcelain --untracked-files=no)" ]]; then
  git -C "$FRONTEND_REPO" status --short
  fail "Frontend gate mengubah tracked files"
fi

printf '\nFINAL PHASE 3B FULL VPS GATE: PASS\n'
printf 'Backend HEAD       : %s\n' "$BACKEND_HEAD"
printf 'Frontend HEAD      : %s\n' "$FRONTEND_HEAD"
printf 'Backend build/tests: PASS\n'
printf 'Clean DB migrate   : PASS\n'
printf 'Backend regressions: PASS\n'
printf 'Frontend build/lint: PASS\n'
printf 'Full Playwright    : PASS\n'
printf 'Production         : NOT MODIFIED\n'
printf 'Supabase           : NOT USED\n'
