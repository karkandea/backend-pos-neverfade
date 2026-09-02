#!/usr/bin/env bash
set -euo pipefail

BRANCH="feat/phase-3b-shared-device-attendance"
WORKSPACE="${NF_PHASE3B_WORKSPACE:-$HOME/neverfade-phase3b}"
BACKEND="$WORKSPACE/backend"
FRONTEND="$WORKSPACE/frontend"
BACKEND_REPO="https://github.com/karkandea/backend-pos-neverfade.git"
FRONTEND_REPO="https://github.com/karkandea/frontend-pos-neverfade.git"
BUILD_CPUS="${NF_BUILD_CPUS:-1.0}"
BUILD_MEMORY="${NF_BUILD_MEMORY:-2g}"
SDK_IMAGE="${NF_DOTNET_IMAGE:-mcr.microsoft.com/dotnet/sdk:10.0}"

fail() {
  printf '\n[FAIL] %s\n' "$1" >&2
  exit 1
}

step() {
  printf '\n==> %s\n' "$1"
}

command -v git >/dev/null 2>&1 || fail "git tidak tersedia di VPS."
command -v docker >/dev/null 2>&1 || fail "docker tidak tersedia di VPS."
docker info >/dev/null 2>&1 || fail "Docker Engine tidak aktif atau user tidak punya akses."

step "VPS safety snapshot"
printf 'Workspace : %s\n' "$WORKSPACE"
printf 'CPU limit : %s\n' "$BUILD_CPUS"
printf 'RAM limit : %s\n' "$BUILD_MEMORY"
printf '\nMemory:\n'
free -h || true
printf '\nDisk root:\n'
df -h / || true
printf '\nExisting containers (read-only snapshot):\n'
docker ps --format 'table {{.Names}}\t{{.Image}}\t{{.Status}}' || true

mkdir -p "$WORKSPACE"

sync_repo() {
  local url="$1"
  local path="$2"
  local label="$3"

  if [[ ! -d "$path/.git" ]]; then
    step "Clone $label into isolated workspace"
    git clone --branch "$BRANCH" --single-branch "$url" "$path"
  else
    step "Refresh isolated $label workspace"
    if [[ -n "$(git -C "$path" status --porcelain)" ]]; then
      git -C "$path" status --short
      fail "$label workspace dirty; refusing to overwrite local changes."
    fi
    git -C "$path" fetch origin "$BRANCH"
    git -C "$path" switch "$BRANCH"
    git -C "$path" pull --ff-only origin "$BRANCH"
  fi
}

sync_repo "$BACKEND_REPO" "$BACKEND" "backend"
sync_repo "$FRONTEND_REPO" "$FRONTEND" "frontend"

step "Exact isolated workspace revisions"
printf 'Backend : %s\n' "$(git -C "$BACKEND" rev-parse HEAD)"
printf 'Frontend: %s\n' "$(git -C "$FRONTEND" rev-parse HEAD)"

step "Pull .NET SDK image"
docker pull "$SDK_IMAGE"

step "Backend restore + Release build + tests in isolated SDK container"
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
    dotnet --info | sed -n "1,22p"
    dotnet restore NeverfadePos.Api/NeverfadePos.Api.csproj
    dotnet build NeverfadePos.Api/NeverfadePos.Api.csproj --configuration Release --no-restore
    dotnet test NeverfadePos.Api.Tests/NeverfadePos.Api.Tests.csproj --configuration Release
  '

printf '\nFINAL PHASE 3B VPS BOOTSTRAP PREFLIGHT: PASS\n'
printf 'Backend HEAD : %s\n' "$(git -C "$BACKEND" rev-parse HEAD)"
printf 'Frontend HEAD: %s\n' "$(git -C "$FRONTEND" rev-parse HEAD)"
printf 'Workspace    : %s\n' "$WORKSPACE"
printf 'Production   : NOT MODIFIED\n'
printf 'Supabase     : NOT USED\n'
