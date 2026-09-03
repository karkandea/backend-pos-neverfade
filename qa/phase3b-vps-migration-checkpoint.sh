#!/usr/bin/env bash
set -euo pipefail

BRANCH="feat/phase-3b-shared-device-attendance"
WORKSPACE="${NF_PHASE3B_WORKSPACE:-$HOME/neverfade-phase3b}"
REPO="$WORKSPACE/backend"
PROJECT="NeverfadePos.Api/NeverfadePos.Api.csproj"
TEST_PROJECT="NeverfadePos.Api.Tests/NeverfadePos.Api.Tests.csproj"
MIGRATION_NAME="AddSharedDeviceAttendance"
SDK_IMAGE="mcr.microsoft.com/dotnet/sdk:10.0"

fail() {
  printf '\n[FAIL] %s\n' "$1" >&2
  exit 1
}

step() {
  printf '\n==> %s\n' "$1"
}

[[ -d "$REPO/.git" ]] || fail "Workspace backend tidak ditemukan di $REPO. Jalankan VPS bootstrap preflight dulu."
command -v docker >/dev/null 2>&1 || fail "Docker tidak tersedia."
command -v git >/dev/null 2>&1 || fail "git tidak tersedia."

cd "$REPO"

step "Verify isolated workspace is clean"
if [[ -n "$(git status --porcelain)" ]]; then
  git status --short
  fail "Workspace backend harus clean."
fi

step "Fetch exact Phase 3B attendance branch"
git fetch origin "$BRANCH"
git switch "$BRANCH"
git pull --ff-only origin "$BRANCH"

printf 'Backend HEAD: %s\n' "$(git rev-parse HEAD)"
printf 'Remote HEAD : %s\n' "$(git rev-parse "origin/$BRANCH")"

if [[ -n "$(git status --porcelain)" ]]; then
  git status --short
  fail "Workspace berubah setelah pull."
fi

if find NeverfadePos.Api/Migrations -type f -name "*_${MIGRATION_NAME}.cs" | grep -q .; then
  fail "Migration $MIGRATION_NAME sudah ada. Jangan generate duplikat."
fi

run_dotnet() {
  docker run --rm \
    --cpus=1 \
    --memory=2g \
    -e DOTNET_CLI_TELEMETRY_OPTOUT=1 \
    -e DOTNET_NOLOGO=1 \
    -e NUGET_PACKAGES=/tmp/nuget \
    -e 'ConnectionStrings__DefaultConnection=Host=127.0.0.1;Port=55432;Database=neverfade_phase3b_design;Username=postgres;Password=postgres' \
    -v "$REPO:/workspace" \
    -w /workspace \
    "$SDK_IMAGE" bash -lc "$1"
}

step "Release build before migration generation"
run_dotnet "dotnet restore '$PROJECT' && dotnet build '$PROJECT' --configuration Release --no-restore"

step "Backend tests before migration generation"
run_dotnet "dotnet test '$TEST_PROJECT' --configuration Release"

step "Generate EF migration + designer + model snapshot"
run_dotnet "dotnet tool install --global dotnet-ef --version 10.0.9 >/tmp/dotnet-ef-install.log && export PATH=\"\$PATH:/root/.dotnet/tools\" && dotnet ef migrations add '$MIGRATION_NAME' --project '$PROJECT' --startup-project '$PROJECT' --output-dir Migrations"

step "Verify EF generator changed migrations only"
changed_files="$(git status --porcelain | awk '{print substr($0,4)}')"
[[ -n "$changed_files" ]] || fail "EF tidak menghasilkan migration artifacts."

while IFS= read -r file; do
  [[ -z "$file" ]] && continue
  case "$file" in
    NeverfadePos.Api/Migrations/*) ;;
    *)
      git status --short
      fail "EF mengubah file di luar NeverfadePos.Api/Migrations: $file"
      ;;
  esac
done <<< "$changed_files"

git status --short

generated_main="$(find NeverfadePos.Api/Migrations -type f -name "*_${MIGRATION_NAME}.cs" ! -name '*.Designer.cs' | sort | tail -1)"
[[ -n "$generated_main" ]] || fail "File migration utama tidak ditemukan."
generated_designer="${generated_main%.cs}.Designer.cs"
[[ -f "$generated_designer" ]] || fail "Designer migration tidak ditemukan."
[[ -f NeverfadePos.Api/Migrations/AppDbContextModelSnapshot.cs ]] || fail "Model snapshot tidak ditemukan."

step "Release build after migration generation"
run_dotnet "dotnet build '$PROJECT' --configuration Release"

step "Backend tests after migration generation"
run_dotnet "dotnet test '$TEST_PROJECT' --configuration Release"

step "Verify model has no pending changes"
run_dotnet "dotnet tool install --global dotnet-ef --version 10.0.9 >/tmp/dotnet-ef-install.log && export PATH=\"\$PATH:/root/.dotnet/tools\" && dotnet ef migrations has-pending-model-changes --project '$PROJECT' --startup-project '$PROJECT'"

step "Commit generated migration artifacts locally"
git add NeverfadePos.Api/Migrations
if git diff --cached --quiet; then
  fail "Tidak ada migration artifacts staged."
fi
if git diff --cached --name-only | grep -v '^NeverfadePos.Api/Migrations/' | grep -q .; then
  git diff --cached --name-only
  fail "Staged diff berisi file di luar migrations."
fi

git diff --cached --stat

git -c user.name='Arkan' -c user.email='98953892+karkandea@users.noreply.github.com' \
  commit -m "feat: add shared device attendance migration"

printf '\nMigration commit created locally: %s\n' "$(git rev-parse HEAD)"

step "Push migration commit if VPS GitHub credentials are available"
if git push origin "HEAD:$BRANCH"; then
  PUSH_STATUS="PASS"
else
  PUSH_STATUS="BLOCKED - local commit retained"
fi

printf '\nFINAL PHASE 3B VPS MIGRATION CHECKPOINT: PASS\n'
printf 'Backend HEAD : %s\n' "$(git rev-parse HEAD)"
printf 'Migration    : %s\n' "$generated_main"
printf 'Push         : %s\n' "$PUSH_STATUS"
printf 'Production   : NOT MODIFIED\n'
printf 'Supabase     : NOT USED\n'
