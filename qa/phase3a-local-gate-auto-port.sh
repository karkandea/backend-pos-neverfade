#!/usr/bin/env bash

set -Eeuo pipefail

BACKEND_REPO="${BACKEND_REPO:-$HOME/neverfade-pos-backend}"
BACKEND_BRANCH="${BACKEND_BRANCH:-feat/phase-3-business-modes}"
DEFAULT_BACKEND_PORT="${PHASE3A_BACKEND_PORT:-5012}"
DEFAULT_FRONTEND_PORT="${PHASE3A_FRONTEND_PORT:-5273}"

if ! command -v lsof >/dev/null 2>&1; then
  echo "[FAIL] lsof is required to select free local ports" >&2
  exit 1
fi

is_port_busy() {
  lsof -nP -iTCP:"$1" -sTCP:LISTEN >/dev/null 2>&1
}

pick_free_port() {
  local preferred="$1"
  local range_start="$2"
  local range_end="$3"
  local candidate

  if ! is_port_busy "$preferred"; then
    printf '%s\n' "$preferred"
    return 0
  fi

  for ((candidate = range_start; candidate <= range_end; candidate++)); do
    if ! is_port_busy "$candidate"; then
      printf '%s\n' "$candidate"
      return 0
    fi
  done

  return 1
}

BACKEND_PORT="$(pick_free_port "$DEFAULT_BACKEND_PORT" 5013 5112 || true)"
FRONTEND_PORT="$(pick_free_port "$DEFAULT_FRONTEND_PORT" 5274 5373 || true)"

if [ -z "$BACKEND_PORT" ]; then
  echo "[FAIL] no free backend port found in range 5012-5112" >&2
  exit 1
fi

if [ -z "$FRONTEND_PORT" ]; then
  echo "[FAIL] no free frontend port found in range 5273-5373" >&2
  exit 1
fi

echo "Using local backend port : $BACKEND_PORT"
echo "Using local frontend port: $FRONTEND_PORT"
export PLAYWRIGHT_BASE_URL="http://127.0.0.1:$FRONTEND_PORT"

GATE_SOURCE="$(git -C "$BACKEND_REPO" show "origin/$BACKEND_BRANCH:qa/phase3a-local-gate.sh")"
GATE_SOURCE="${GATE_SOURCE//5012/$BACKEND_PORT}"
GATE_SOURCE="${GATE_SOURCE//5273/$FRONTEND_PORT}"
printf '%s\n' "$GATE_SOURCE" | bash
