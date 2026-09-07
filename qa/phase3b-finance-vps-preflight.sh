#!/usr/bin/env bash
set -euo pipefail

BRANCH="feat/phase-3b-finance-withdrawal"
WORKSPACE="${NF_PHASE3B_WORKSPACE:-$HOME/neverfade-phase3b}"
BACKEND="$WORKSPACE/backend"
FRONTEND="$WORKSPACE/frontend"
BACKEND_REPO="https://github.com/karkandea/backend-pos-neverfade.git"
FRONTEND_REPO="https://github.com/karkandea/frontend-pos-neverfade.git"
EXPECTED_BACKEND="${NF_PHASE3B_FINANCE_EXPECTED_BACKEND_HEAD:-}"
EXPECTED_FRONTEND="${NF_PHASE3B_FINANCE_EXPECTED_FRONTEND_HEAD:-}"
ALLOW_OFFLINE="${NF_PHASE3B_ALLOW_OFFLINE_GIT:-0}"
BUILD_CPUS="${NF_BUILD_CPUS:-1.0}"
BUILD_MEMORY="${NF_BUILD_MEMORY:-2g}"
SDK_IMAGE="${NF_DOTNET_IMAGE:-mcr.microsoft.com/dotnet/sdk:10.0}"
PLAYWRIGHT_IMAGE="${NF_PLAYWRIGHT_IMAGE:-mcr.microsoft.com/playwright:v1.62.0-noble}"

fail() {
  printf '\n[FAIL] %s\n' "$1" >&2
  exit 1
}

step() {
  printf '\n==> %s\n' "$1"
}

command -v git >/dev/null 2>&1 || fail "git tidak tersedia."
command -v docker >/dev/null 2>&1 || fail "docker tidak tersedia."
docker info >/dev/null 2>&1 || fail "Docker Engine tidak aktif."

step "Safety snapshot"
printf 'Workspace: %s\n' "$WORKSPACE"
free -h || true
df -h / || true
docker ps --format 'table {{.Names}}\t{{.Image}}\t{{.Status}}' || true

sync_repo() {
  local url="$1"
  local path="$2"
  local label="$3"
  local expected="$4"

  if [[ ! -d "$path/.git" ]]; then
    step "Clone $label"
    timeout 120 git clone --branch "$BRANCH" --single-branch "$url" "$path" ||
      fail "clone $label gagal."
  else
    [[ -z "$(git -C "$path" status --porcelain)" ]] ||
      fail "$label workspace dirty."

    step "Refresh $label"
    local fetched=0
    for attempt in 1 2 3; do
      if timeout 35 git -C "$path" fetch origin "$BRANCH"; then
        fetched=1
        break
      fi
      printf 'WARN: fetch %s attempt %s gagal.\n' "$label" "$attempt"
      sleep 2
    done

    if [[ "$fetched" == "1" ]]; then
      git -C "$path" switch "$BRANCH"
      git -C "$path" merge --ff-only "origin/$BRANCH"
    elif [[ "$ALLOW_OFFLINE" != "1" ]]; then
      fail "fetch $label gagal dan offline fallback tidak diizinkan."
    fi
  fi

  [[ -z "$(git -C "$path" status --porcelain)" ]] ||
    fail "$label workspace dirty setelah sync."

  local actual
  actual="$(git -C "$path" rev-parse HEAD)"
  [[ -n "$expected" ]] ||
    fail "expected HEAD untuk $label wajib diisi."
  [[ "$actual" == "$expected" ]] ||
    fail "$label HEAD $actual tidak sama dengan expected $expected."
}

sync_repo "$BACKEND_REPO" "$BACKEND" "backend" "$EXPECTED_BACKEND"
sync_repo "$FRONTEND_REPO" "$FRONTEND" "frontend" "$EXPECTED_FRONTEND"

step "Exact revisions"
printf 'Backend : %s\n' "$(git -C "$BACKEND" rev-parse HEAD)"
printf 'Frontend: %s\n' "$(git -C "$FRONTEND" rev-parse HEAD)"

step "Backend Release build + full tests"
docker run --rm \
  --cpus="$BUILD_CPUS" \
  --memory="$BUILD_MEMORY" \
  -e DOTNET_CLI_TELEMETRY_OPTOUT=1 \
  -e DOTNET_NOLOGO=1 \
  -v nf_phase3b_nuget:/root/.nuget/packages \
  -v "$BACKEND:/workspace" \
  -w /workspace \
  "$SDK_IMAGE" \
  bash -lc '
    set -euo pipefail
    dotnet restore NeverfadePos.Api/NeverfadePos.Api.csproj
    dotnet build NeverfadePos.Api/NeverfadePos.Api.csproj --configuration Release --no-restore
    dotnet test NeverfadePos.Api.Tests/NeverfadePos.Api.Tests.csproj --configuration Release
  '

step "Frontend npm ci + build + lint + finance Playwright"
docker run --rm \
  --cpus="$BUILD_CPUS" \
  --memory=3g \
  --shm-size=1g \
  -e CI=1 \
  -e PLAYWRIGHT_BASE_URL=http://127.0.0.1:5273 \
  -e PLAYWRIGHT_TEST_TIMEOUT=45000 \
  -e PLAYWRIGHT_EXPECT_TIMEOUT=20000 \
  -v neverfade-phase3b-npm:/root/.npm \
  -v "$FRONTEND:/workspace/frontend" \
  -w /workspace/frontend \
  "$PLAYWRIGHT_IMAGE" \
  bash -lc '
    set -euo pipefail
    npm ci
    npm run build
    npm run lint

    npm run dev -- --host 127.0.0.1 --port 5273 --strictPort >/tmp/nf-vite.log 2>&1 &
    vite_pid=$!
    trap "kill $vite_pid >/dev/null 2>&1 || true" EXIT

    ready=0
    for i in $(seq 1 60); do
      if node -e "fetch(\"http://127.0.0.1:5273/login\").then(r=>process.exit(r.ok?0:1)).catch(()=>process.exit(1))"; then
        ready=1
        break
      fi
      sleep 1
    done

    test "$ready" = "1" || {
      cat /tmp/nf-vite.log
      exit 1
    }

    npx playwright test tests/e2e/finance-withdrawal.spec.ts
  '

printf '\nFINAL PHASE 3B FINANCE PREFLIGHT: PASS\n'
printf 'Backend HEAD : %s\n' "$(git -C "$BACKEND" rev-parse HEAD)"
printf 'Frontend HEAD: %s\n' "$(git -C "$FRONTEND" rev-parse HEAD)"
printf 'Production   : NOT MODIFIED\n'
printf 'Supabase     : NOT USED\n'
