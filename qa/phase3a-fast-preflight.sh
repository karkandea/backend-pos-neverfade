#!/usr/bin/env bash

set -Eeuo pipefail

BACKEND_REPO="${BACKEND_REPO:-$HOME/neverfade-pos-backend}"
FRONTEND_REPO="${FRONTEND_REPO:-$HOME/neverfade-pos-frontend}"
BACKEND_BRANCH="${BACKEND_BRANCH:-feat/phase-3-business-modes}"
FRONTEND_BRANCH="${FRONTEND_BRANCH:-feat/phase-3-business-modes}"
WORK_ROOT="$(mktemp -d /tmp/neverfade-phase3a-fast.XXXXXX)"
BACKEND_WORK="$WORK_ROOT/backend"
FRONTEND_WORK="$WORK_ROOT/frontend"
FAILURES=0

cleanup() {
  set +e
  git -C "$BACKEND_REPO" worktree remove --force "$BACKEND_WORK" >/dev/null 2>&1 || true
  git -C "$FRONTEND_REPO" worktree remove --force "$FRONTEND_WORK" >/dev/null 2>&1 || true
  git -C "$BACKEND_REPO" worktree prune >/dev/null 2>&1 || true
  git -C "$FRONTEND_REPO" worktree prune >/dev/null 2>&1 || true
  rm -rf "$WORK_ROOT"
}
trap cleanup EXIT INT TERM

run() {
  local label="$1"
  local dir="$2"
  shift 2
  echo
  echo "===== $label ====="
  if (cd "$dir" && "$@"); then
    echo "[PASS] $label"
  else
    echo "[FAIL] $label"
    FAILURES=$((FAILURES + 1))
  fi
}

echo "=================================================="
echo "NEVERFADE POS — PHASE 3A FAST PREFLIGHT"
echo "=================================================="
echo "Supabase: NOT USED"

git -C "$BACKEND_REPO" fetch origin main "$BACKEND_BRANCH"
git -C "$FRONTEND_REPO" fetch origin main "$FRONTEND_BRANCH"

BACKEND_HEAD="$(git -C "$BACKEND_REPO" rev-parse "origin/$BACKEND_BRANCH")"
FRONTEND_HEAD="$(git -C "$FRONTEND_REPO" rev-parse "origin/$FRONTEND_BRANCH")"

echo "Backend HEAD : $BACKEND_HEAD"
echo "Frontend HEAD: $FRONTEND_HEAD"

git -C "$BACKEND_REPO" worktree add --detach "$BACKEND_WORK" "$BACKEND_HEAD"
git -C "$FRONTEND_REPO" worktree add --detach "$FRONTEND_WORK" "$FRONTEND_HEAD"

run "backend diff check" "$BACKEND_WORK" git diff --check origin/main...HEAD
run "backend restore" "$BACKEND_WORK" dotnet restore NeverfadePos.slnx
run "backend Release build" "$BACKEND_WORK" dotnet build NeverfadePos.slnx --configuration Release --no-restore

if [ "$FAILURES" -eq 0 ]; then
  run "backend tests" "$BACKEND_WORK" dotnet test NeverfadePos.Api.Tests/NeverfadePos.Api.Tests.csproj --configuration Release --no-build --logger "console;verbosity=normal"
else
  echo "[SKIP] backend tests because build/restore failed"
fi

run "frontend diff check" "$FRONTEND_WORK" git diff --check origin/main...HEAD
run "frontend npm ci" "$FRONTEND_WORK" npm ci
run "frontend type/build" "$FRONTEND_WORK" npm run build
run "frontend lint" "$FRONTEND_WORK" npm run lint

echo
echo "=================================================="
if [ "$FAILURES" -eq 0 ]; then
  echo "FINAL FAST PREFLIGHT: PASS"
  echo "Backend HEAD : $BACKEND_HEAD"
  echo "Frontend HEAD: $FRONTEND_HEAD"
  exit 0
fi

echo "FINAL FAST PREFLIGHT: FAIL ($FAILURES gate(s))"
echo "Backend HEAD : $BACKEND_HEAD"
echo "Frontend HEAD: $FRONTEND_HEAD"
exit 1
