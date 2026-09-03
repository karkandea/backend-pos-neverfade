#!/usr/bin/env bash
set -euo pipefail

BRANCH="feat/phase-3b-shared-device-attendance"

if [[ -d "${NF_BACKEND_REPO:-}/.git" ]]; then
  REPO="$NF_BACKEND_REPO"
elif [[ -d "$HOME/neverfade-pos-backend/.git" ]]; then
  REPO="$HOME/neverfade-pos-backend"
elif [[ -d "$HOME/neverfade-phase3b/backend/.git" ]]; then
  REPO="$HOME/neverfade-phase3b/backend"
else
  printf '[FAIL] Backend repo tidak ditemukan. Cek ~/neverfade-pos-backend atau ~/neverfade-phase3b/backend.\n' >&2
  exit 1
fi

command -v git >/dev/null 2>&1 || { echo '[FAIL] git tidak tersedia.' >&2; exit 1; }
command -v docker >/dev/null 2>&1 || { echo '[FAIL] Docker tidak tersedia / Docker Desktop belum aktif.' >&2; exit 1; }

echo "Using backend repo: $REPO"
cd "$REPO"

if [[ -n "$(git status --porcelain)" ]]; then
  git status --short
  echo '[FAIL] Backend working tree harus clean.' >&2
  exit 1
fi

git fetch origin "$BRANCH"
git switch "$BRANCH"
git pull --ff-only origin "$BRANCH"

# The existing gate expects a workspace with a backend/ child. Reuse the repo
# without copying files by exposing it through an isolated symlink workspace.
LAUNCH_WORKSPACE="${TMPDIR:-/tmp}/neverfade-phase3b-launcher"
rm -rf "$LAUNCH_WORKSPACE"
mkdir -p "$LAUNCH_WORKSPACE"
ln -s "$REPO" "$LAUNCH_WORKSPACE/backend"

export NF_PHASE3B_WORKSPACE="$LAUNCH_WORKSPACE"
exec bash "$REPO/qa/phase3b-vps-postgres-migration-gate.sh"
