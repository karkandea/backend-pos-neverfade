#!/usr/bin/env bash

set -Eeuo pipefail

BACKEND_REPO="${BACKEND_REPO:-$HOME/neverfade-pos-backend}"
BACKEND_BRANCH="${BACKEND_BRANCH:-feat/phase-3-business-modes}"
DEFAULT_PORT="${PHASE3A_FRONTEND_PORT:-5273}"

if ! command -v lsof >/dev/null 2>&1; then
  echo "[FAIL] lsof is required to select a free frontend port" >&2
  exit 1
fi

is_port_busy() {
  lsof -nP -iTCP:"$1" -sTCP:LISTEN >/dev/null 2>&1
}

FRONTEND_PORT="$DEFAULT_PORT"
if is_port_busy "$FRONTEND_PORT"; then
  FRONTEND_PORT=""
  for ((candidate = 5274; candidate <= 5373; candidate++)); do
    if ! is_port_busy "$candidate"; then
      FRONTEND_PORT="$candidate"
      break
    fi
  done
fi

if [ -z "$FRONTEND_PORT" ]; then
  echo "[FAIL] no free frontend port found in range 5274-5373" >&2
  exit 1
fi

echo "Using local frontend port: $FRONTEND_PORT"
export PLAYWRIGHT_BASE_URL="http://127.0.0.1:$FRONTEND_PORT"

GATE_SOURCE="$(git -C "$BACKEND_REPO" show "origin/$BACKEND_BRANCH:qa/phase3a-local-gate.sh")"
GATE_SOURCE="${GATE_SOURCE//5273/$FRONTEND_PORT}"
printf '%s\n' "$GATE_SOURCE" | bash
