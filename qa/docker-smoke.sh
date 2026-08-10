#!/usr/bin/env bash

set +u

BACKEND="$HOME/neverfade-pos-backend"
PROJECT="$BACKEND/NeverfadePos.Api"
RESULT_DIR="$HOME/neverfade-pos-qa"

source "$BACKEND/qa/lib.sh"

IMAGE_NAME="neverfade-pos-backend:qa-smoke"
CONTAINER_NAME="neverfade-pos-backend-qa-smoke"
BASE_URL="http://127.0.0.1:8080"

ENV_FILE="$(
  mktemp /tmp/neverfade-docker-env.XXXXXX
)"

PASS_COUNT=0
FAIL_COUNT=0

pass() {
  PASS_COUNT=$((PASS_COUNT + 1))
  echo "[PASS] $1"
}

fail() {
  FAIL_COUNT=$((FAIL_COUNT + 1))
  echo "[FAIL] $1"
}

cleanup() {
  docker rm \
    --force \
    "$CONTAINER_NAME" \
    >/dev/null 2>&1 || true

  rm -f "$ENV_FILE"
}

trap cleanup EXIT INT TERM

mkdir -p "$RESULT_DIR"

echo "=================================================="
echo "NEVERFADE POS — DOCKER BACKEND SMOKE TEST"
echo "=================================================="

echo
echo "===== PRE-FLIGHT ====="

if lsof \
  -tiTCP:8080 \
  -sTCP:LISTEN \
  >/dev/null 2>&1
then
  fail "Port 8080 sedang digunakan."
  lsof -nP -iTCP:8080 -sTCP:LISTEN
  exit 1
fi

pass "Port 8080 tersedia"

if ! docker info >/dev/null 2>&1; then
  fail "Docker engine tidak tersedia"
  exit 1
fi

pass "Docker engine tersedia"

echo
echo "===== LOAD RUNTIME CONFIG ====="

if [ -n "${QA_CONNECTION_STRING:-}" ]; then
  CONNECTION_STRING="$QA_CONNECTION_STRING"
  SECRET_OUTPUT=""
else
  SECRET_OUTPUT="$(
    cd "$PROJECT" &&
    dotnet user-secrets list 2>/dev/null
  )"

  CONNECTION_STRING="$(
    printf "%s\n" "$SECRET_OUTPUT" |
      grep \
        '^ConnectionStrings:DefaultConnection = ' |
      head -1 |
      cut -d= -f2- |
      sed 's/^ //'
  )"
fi

if [ -z "$CONNECTION_STRING" ]; then
  fail "ConnectionStrings:DefaultConnection tidak ditemukan"
  exit 1
fi

pass "Database connection configuration ditemukan"

chmod 600 "$ENV_FILE"

{
  echo "ASPNETCORE_ENVIRONMENT=Development"
  echo "DOTNET_ENVIRONMENT=Development"

  printf \
    'ConnectionStrings__DefaultConnection=%s\n' \
    "$CONNECTION_STRING"

  printf \
    'PlatformJwt__Key=%s\n' \
    "$QA_PLATFORM_JWT_KEY"

  printf \
    'PlatformJwt__Issuer=%s\n' \
    "$QA_PLATFORM_JWT_ISSUER"

  printf \
    'PlatformJwt__Audience=%s\n' \
    "$QA_PLATFORM_JWT_AUDIENCE"
} > "$ENV_FILE"

unset CONNECTION_STRING
unset SECRET_OUTPUT

echo
echo "===== BUILD IMAGE ====="

docker rm \
  --force \
  "$CONTAINER_NAME" \
  >/dev/null 2>&1 || true

if docker build \
  --tag "$IMAGE_NAME" \
  "$BACKEND" \
  2>&1 |
  tee "$RESULT_DIR/docker-build.log"
then
  pass "Docker image berhasil dibuild"
else
  fail "Docker image gagal dibuild"
  exit 1
fi

echo
echo "===== START CONTAINER ====="

CONTAINER_ID="$(
  docker run \
    --detach \
    --name "$CONTAINER_NAME" \
    --env-file "$ENV_FILE" \
    --publish "127.0.0.1:8080:8080" \
    "$IMAGE_NAME"
)"

if [ -z "$CONTAINER_ID" ]; then
  fail "Container gagal dibuat"
  exit 1
fi

pass "Container berhasil dibuat"

echo
echo "===== WAIT FOR APPLICATION ====="

READY=0

for ATTEMPT in $(seq 1 90); do
  if ! docker inspect \
    --format '{{.State.Running}}' \
    "$CONTAINER_NAME" \
    2>/dev/null |
    grep -q '^true$'
  then
    fail "Container berhenti saat startup."
    docker logs "$CONTAINER_NAME"
    exit 1
  fi

  STATUS="$(
    curl \
      --silent \
      --output /dev/null \
      --write-out "%{http_code}" \
      "$BASE_URL/api/products" \
      2>/dev/null || true
  )"

  if [ "$STATUS" = "401" ]; then
    READY=1
    break
  fi

  sleep 1
done

if [ "$READY" -ne 1 ]; then
  fail "Application tidak siap dalam batas waktu"
  docker logs "$CONTAINER_NAME"
  exit 1
fi

pass "Application siap dan endpoint merespons"

echo
echo "===== UNAUTHORIZED CONTRACT ====="

UNAUTHORIZED_STATUS="$(
  curl \
    --silent \
    --output "$RESULT_DIR/docker-unauthorized.json" \
    --write-out "%{http_code}" \
    "$BASE_URL/api/products"
)"

if [ "$UNAUTHORIZED_STATUS" = "401" ]; then
  pass "Protected endpoint menolak request tanpa token"
else
  fail "Protected endpoint tanpa token — HTTP $UNAUTHORIZED_STATUS"
fi

echo
echo "===== OWNER LOGIN ====="

LOGIN_PAYLOAD="$(
  jq -n \
    --arg username "$QA_OWNER_USERNAME" \
    --arg password "$QA_OWNER_PASSWORD" \
    '{
      username: $username,
      password: $password
    }'
)"

LOGIN_STATUS="$(
  curl \
    --silent \
    --show-error \
    --output "$RESULT_DIR/docker-login.json" \
    --write-out "%{http_code}" \
    --request POST \
    --header "Content-Type: application/json" \
    --data "$LOGIN_PAYLOAD" \
    "$BASE_URL/api/auth/login"
)"

unset LOGIN_PAYLOAD

if [ "$LOGIN_STATUS" != "200" ]; then
  fail "Owner login — HTTP $LOGIN_STATUS"

  cat "$RESULT_DIR/docker-login.json"

  echo
  echo "===== CONTAINER LOG TAIL ====="

  docker logs \
    --tail 80 \
    "$CONTAINER_NAME" \
    2>&1 |
    grep -vEi \
      'password|token|authorization|connectionstring|jwt:key|host=.*password'

  exit 1
fi

TOKEN="$(
  jq -r \
    '.token // empty' \
    "$RESULT_DIR/docker-login.json"
)"

if [ -z "$TOKEN" ]; then
  fail "Login response tidak mengandung token"
  exit 1
fi

pass "Owner login melalui container"

request_authenticated() {
  TEST_NAME="$1"
  PATH_VALUE="$2"
  OUTPUT_FILE="$3"

  STATUS="$(
    curl \
      --silent \
      --show-error \
      --output "$OUTPUT_FILE" \
      --write-out "%{http_code}" \
      --header "Authorization: Bearer $TOKEN" \
      "$BASE_URL$PATH_VALUE"
  )"

  if [ "$STATUS" = "200" ]; then
    pass "$TEST_NAME"
  else
    fail "$TEST_NAME — HTTP $STATUS"
  fi
}

echo
echo "===== AUTHENTICATED ENDPOINTS ====="

request_authenticated \
  "Auth me endpoint" \
  "/api/auth/me" \
  "$RESULT_DIR/docker-auth-me.json"

request_authenticated \
  "Products endpoint" \
  "/api/products" \
  "$RESULT_DIR/docker-products.json"

request_authenticated \
  "Settings endpoint" \
  "/api/settings" \
  "$RESULT_DIR/docker-settings.json"

request_authenticated \
  "Report summary endpoint" \
  "/api/laporan/summary?period=harian" \
  "$RESULT_DIR/docker-report-summary.json"

echo
echo "===== RESPONSE VALIDATION ====="

if jq -e \
  'type == "array"' \
  "$RESULT_DIR/docker-products.json" \
  >/dev/null 2>&1
then
  pass "Products response berbentuk array"
else
  fail "Products response bukan array"
fi

if jq -e \
  '.namaToko != null' \
  "$RESULT_DIR/docker-settings.json" \
  >/dev/null 2>&1
then
  pass "Settings response mengandung namaToko"
else
  fail "Settings response tidak mengandung namaToko"
fi

if jq -e \
  'has("omzet") and has("transaksi") and has("avg")' \
  "$RESULT_DIR/docker-report-summary.json" \
  >/dev/null 2>&1
then
  pass "Report summary memiliki struktur yang benar"
else
  fail "Report summary memiliki struktur tidak sesuai"
fi

echo
echo "===== CONTAINER LOG CHECK ====="

docker logs "$CONTAINER_NAME" \
  > "$RESULT_DIR/docker-container.log" \
  2>&1

if grep -Eiq \
  'Unhandled exception|Application startup exception|fail:' \
  "$RESULT_DIR/docker-container.log"
then
  fail "Container log mengandung fatal/error entry"

  grep -Ein \
    'Unhandled exception|Application startup exception|fail:' \
    "$RESULT_DIR/docker-container.log" |
    head -30
else
  pass "Container log tidak mengandung fatal error"
fi

echo
echo "=================================================="
echo "DOCKER SMOKE SUMMARY"
echo "PASS : $PASS_COUNT"
echo "FAIL : $FAIL_COUNT"
echo "=================================================="

echo
echo "Build log     : $RESULT_DIR/docker-build.log"
echo "Container log : $RESULT_DIR/docker-container.log"
echo "Responses     : $RESULT_DIR/docker-*.json"

if [ "$FAIL_COUNT" -gt 0 ]; then
  exit 1
fi

exit 0
