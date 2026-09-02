#!/usr/bin/env bash

QA_BACKEND_ROOT="${QA_BACKEND_ROOT:-$HOME/neverfade-pos-backend}"
QA_PROJECT_ROOT="${QA_PROJECT_ROOT:-$QA_BACKEND_ROOT/NeverfadePos.Api}"
QA_API_URL="${QA_API_URL:-http://localhost:5012}"

# Dummy QA credentials — LOCAL / TEST ONLY.
QA_OWNER_USERNAME="${QA_OWNER_USERNAME:-owner}"
QA_OWNER_PASSWORD="${QA_OWNER_PASSWORD:-owner123}"

QA_RUN_ID="${QA_RUN_ID:-QA_$(date +%Y%m%d_%H%M%S)}"
QA_LOG_FILE="${QA_LOG_FILE:-/tmp/neverfade-pos-qa-backend.log}"

QA_PLATFORM_JWT_KEY="${QA_PLATFORM_JWT_KEY:-$(openssl rand -hex 32)}"
QA_PLATFORM_JWT_ISSUER="${QA_PLATFORM_JWT_ISSUER:-NeverfadePos.Platform.QA}"
QA_PLATFORM_JWT_AUDIENCE="${QA_PLATFORM_JWT_AUDIENCE:-NeverfadePos.Platform.QA.Client}"

QA_STARTED_BACKEND=0
QA_BACKEND_PID=""
QA_TOKEN=""

QA_PASSED=0
QA_FAILED=0
QA_SKIPPED=0

qa_pass() {
  QA_PASSED=$((QA_PASSED + 1))
  echo "[PASS] $1"
}

qa_fail() {
  QA_FAILED=$((QA_FAILED + 1))
  echo "[FAIL] $1"
}

qa_skip() {
  QA_SKIPPED=$((QA_SKIPPED + 1))
  echo "[SKIP] $1"
}

qa_expect_status() {
  local label="$1"
  local actual="$2"
  local expected="$3"

  if [ "$actual" = "$expected" ]; then
    qa_pass "$label — HTTP $actual"
  else
    qa_fail "$label — expected HTTP $expected, actual HTTP $actual"
  fi
}

qa_expect_jq() {
  local label="$1"
  local file_path="$2"
  local filter="$3"

  if jq -e "$filter" "$file_path" >/dev/null 2>&1; then
    qa_pass "$label"
  else
    qa_fail "$label"
  fi
}

qa_start_backend() {
  local current_status
  local attempt
  local status_value
  local ready=0

  current_status="$(
    curl \
      --silent \
      --output /dev/null \
      --write-out "%{http_code}" \
      --max-time 2 \
      "$QA_API_URL/api/products" \
      2>/dev/null
  )"

  if [ "$current_status" = "401" ]; then
    echo "[INFO] Backend sudah aktif; QA tidak akan menghentikannya."
    return 0
  fi

  rm -f "$QA_LOG_FILE"

  cd "$QA_PROJECT_ROOT" || return 1

  ASPNETCORE_ENVIRONMENT=Development \
  PlatformJwt__Key="$QA_PLATFORM_JWT_KEY" \
  PlatformJwt__Issuer="$QA_PLATFORM_JWT_ISSUER" \
  PlatformJwt__Audience="$QA_PLATFORM_JWT_AUDIENCE" \
  dotnet run \
    --no-build \
    >"$QA_LOG_FILE" 2>&1 &

  QA_BACKEND_PID=$!
  QA_STARTED_BACKEND=1

  for ((attempt = 1; attempt <= 30; attempt++)); do
    status_value="$(
      curl \
        --silent \
        --output /dev/null \
        --write-out "%{http_code}" \
        --max-time 2 \
        "$QA_API_URL/api/products" \
        2>/dev/null
    )"

    if [ "$status_value" = "401" ]; then
      ready=1
      break
    fi

    if ! kill -0 "$QA_BACKEND_PID" 2>/dev/null; then
      break
    fi

    sleep 1
  done

  if [ "$ready" -eq 1 ]; then
    qa_pass "Backend development siap"
    return 0
  fi

  qa_fail "Backend development gagal siap"

  tail -n 60 "$QA_LOG_FILE" 2>/dev/null |
    grep -vEi \
      'password|token|authorization|connectionstring|jwt:key|host=.*password'

  return 1
}

qa_stop_backend() {
  local pid_value
  local process_command

  if [ "$QA_STARTED_BACKEND" -ne 1 ]; then
    return 0
  fi

  for pid_value in $(lsof -ti tcp:5012 2>/dev/null | sort -u); do
    process_command="$(
      ps -p "$pid_value" -o command= 2>/dev/null
    )"

    if printf "%s" "$process_command" |
      grep -Eq 'neverfade-pos-backend|NeverfadePos\.Api'
    then
      kill "$pid_value" 2>/dev/null || true
    fi
  done

  if [ -n "$QA_BACKEND_PID" ]; then
    kill "$QA_BACKEND_PID" 2>/dev/null || true
    wait "$QA_BACKEND_PID" 2>/dev/null || true
  fi
}

qa_login_owner() {
  local login_file
  local login_status
  local login_payload

  login_file="$1"

  login_payload="$(
    jq -n \
      --arg username "$QA_OWNER_USERNAME" \
      --arg password "$QA_OWNER_PASSWORD" \
      '{username:$username,password:$password}'
  )"

  login_status="$(
    curl \
      --silent \
      --output "$login_file" \
      --write-out "%{http_code}" \
      --header "Content-Type: application/json" \
      --data "$login_payload" \
      "$QA_API_URL/api/auth/login"
  )"

  unset login_payload

  qa_expect_status \
    "Owner login" \
    "$login_status" \
    "200"

  QA_TOKEN="$(
    jq -r '.token // empty' \
      "$login_file" 2>/dev/null
  )"

  if [ -z "$QA_TOKEN" ]; then
    qa_fail "Owner token tidak diterima"
    return 1
  fi

  qa_pass "Owner token diterima tanpa ditampilkan"
}

qa_request() {
  local method_value="$1"
  local path_value="$2"
  local output_file="$3"
  local data_file="${4:-}"

  if [ -n "$data_file" ]; then
    curl \
      --silent \
      --output "$output_file" \
      --write-out "%{http_code}" \
      --request "$method_value" \
      --header "Authorization: Bearer $QA_TOKEN" \
      --header "Content-Type: application/json" \
      --data-binary "@$data_file" \
      "$QA_API_URL$path_value"
  else
    curl \
      --silent \
      --output "$output_file" \
      --write-out "%{http_code}" \
      --request "$method_value" \
      --header "Authorization: Bearer $QA_TOKEN" \
      "$QA_API_URL$path_value"
  fi
}

qa_print_summary() {
  echo
  echo "=================================================="
  echo "Passed : $QA_PASSED"
  echo "Failed : $QA_FAILED"
  echo "Skipped: $QA_SKIPPED"
  echo "=================================================="
}

qa_restore_terminal() {
  true
}
