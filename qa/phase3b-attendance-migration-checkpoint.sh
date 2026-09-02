#!/usr/bin/env bash
set -euo pipefail

BRANCH="feat/phase-3b-shared-device-attendance"
REPO="${NF_BACKEND_REPO:-$HOME/neverfade-pos-backend}"
PROJECT="NeverfadePos.Api/NeverfadePos.Api.csproj"
TEST_PROJECT="NeverfadePos.Api.Tests/NeverfadePos.Api.Tests.csproj"
MIGRATION_NAME="AddSharedDeviceAttendance"

fail() {
  printf '\n[FAIL] %s\n' "$1" >&2
  exit 1
}

step() {
  printf '\n==> %s\n' "$1"
}

[[ -d "$REPO/.git" ]] || fail "Backend repo tidak ditemukan di $REPO"
cd "$REPO"

step "Verify clean local repository"
if [[ -n "$(git status --porcelain)" ]]; then
  git status --short
  fail "Working tree backend harus clean sebelum checkpoint."
fi

step "Fetch exact Phase 3B attendance branch"
git fetch origin "$BRANCH"

if git show-ref --verify --quiet "refs/heads/$BRANCH"; then
  git switch "$BRANCH"
else
  git switch --track -c "$BRANCH" "origin/$BRANCH"
fi

git pull --ff-only origin "$BRANCH"

if [[ -n "$(git status --porcelain)" ]]; then
  git status --short
  fail "Working tree berubah setelah pull; abort."
fi

printf 'Backend HEAD: %s\n' "$(git rev-parse HEAD)"
printf 'Remote HEAD : %s\n' "$(git rev-parse "origin/$BRANCH")"

command -v dotnet >/dev/null 2>&1 || fail ".NET SDK tidak tersedia."

dotnet --info | sed -n '1,22p'

# Design-time factory only needs a syntactically valid Npgsql connection string.
# No connection to this database is made during migration generation.
export ConnectionStrings__DefaultConnection="${ConnectionStrings__DefaultConnection:-Host=127.0.0.1;Port=55432;Database=neverfade_phase3b_design;Username=postgres;Password=postgres}"

step "Restore backend"
dotnet restore "$PROJECT"

step "Release build before migration generation"
dotnet build "$PROJECT" --configuration Release --no-restore

step "Backend tests before migration generation"
dotnet test "$TEST_PROJECT" --configuration Release

if ls NeverfadePos.Api/Migrations/*_"$MIGRATION_NAME"*.cs >/dev/null 2>&1; then
  fail "Migration $MIGRATION_NAME sudah ada. Jangan generate duplikat."
fi

step "Generate EF migration + designer + model snapshot"
dotnet ef migrations add "$MIGRATION_NAME" \
  --project "$PROJECT" \
  --startup-project "$PROJECT" \
  --output-dir Migrations

step "Verify generator touched migrations only"
changed_files="$(git status --porcelain | sed -E 's/^.. //')"
if [[ -z "$changed_files" ]]; then
  fail "EF tidak menghasilkan perubahan migration."
fi

while IFS= read -r file; do
  [[ -n "$file" ]] || continue
  case "$file" in
    NeverfadePos.Api/Migrations/*) ;;
    *)
      git status --short
      fail "EF checkpoint mengubah file di luar NeverfadePos.Api/Migrations: $file"
      ;;
  esac
done <<< "$changed_files"

git status --short

generated_main="$(ls NeverfadePos.Api/Migrations/*_"$MIGRATION_NAME".cs 2>/dev/null | grep -v '\.Designer\.cs$' | sort | tail -1 || true)"
[[ -n "$generated_main" ]] || fail "File migration utama tidak ditemukan."

generated_designer="${generated_main%.cs}.Designer.cs"
[[ -f "$generated_designer" ]] || fail "Designer migration tidak ditemukan: $generated_designer"
[[ -f NeverfadePos.Api/Migrations/AppDbContextModelSnapshot.cs ]] || fail "Model snapshot tidak ditemukan."

step "Release build after migration generation"
dotnet build "$PROJECT" --configuration Release --no-restore

step "Backend tests after migration generation"
dotnet test "$TEST_PROJECT" --configuration Release

step "Commit generated migration artifacts"
git add NeverfadePos.Api/Migrations

if git diff --cached --quiet; then
  fail "Tidak ada migration artifacts untuk di-commit."
fi

if git diff --cached --name-only | grep -v '^NeverfadePos.Api/Migrations/' | grep -q .; then
  git diff --cached --name-only
  fail "Staged diff mengandung file di luar migrations."
fi

git diff --cached --stat
git commit -m "feat: add shared device attendance migration"

step "Push Phase 3B attendance branch"
git push origin "HEAD:$BRANCH"

printf '\nFINAL PHASE 3B ATTENDANCE MIGRATION CHECKPOINT: PASS\n'
printf 'Pushed HEAD: %s\n' "$(git rev-parse HEAD)"
printf 'Migration : %s\n' "$generated_main"
printf 'Supabase  : NOT USED\n'
