#!/usr/bin/env bash

set +u

source "$HOME/neverfade-pos-backend/qa/lib.sh"

TMP_DIR="$(mktemp -d)"
TEST_USERNAME="qa_user_$(date +%Y%m%d_%H%M%S)"
UPDATED_USERNAME="${TEST_USERNAME}_updated"
TEST_PASSWORD="QaUser123!"
NEW_PASSWORD="QaUser456!"

QA_ADMIN_USERNAME="${QA_ADMIN_USERNAME:-admin}"
QA_ADMIN_PASSWORD="${QA_ADMIN_PASSWORD:-admin123}"
QA_KASIR_USERNAME="${QA_KASIR_USERNAME:-kasir}"
QA_KASIR_PASSWORD="${QA_KASIR_PASSWORD:-kasir123}"

cleanup() {
  qa_restore_terminal
  qa_stop_backend
  rm -rf "$TMP_DIR"
}

trap cleanup EXIT INT TERM

login_user() {
  local username="$1"
  local password="$2"
  local output_file="$3"
  local payload

  payload="$(
    jq -n \
      --arg username "$username" \
      --arg password "$password" \
      '{
        username: $username,
        password: $password
      }'
  )"

  curl \
    --silent \
    --output "$output_file" \
    --write-out "%{http_code}" \
    --request POST \
    --header "Content-Type: application/json" \
    --data "$payload" \
    "$QA_API_URL/api/auth/login"
}

request_with_token() {
  local method="$1"
  local path="$2"
  local token="$3"
  local output_file="$4"
  local data_file="${5:-}"

  if [ -n "$data_file" ]; then
    curl \
      --silent \
      --output "$output_file" \
      --write-out "%{http_code}" \
      --request "$method" \
      --header "Authorization: Bearer $token" \
      --header "Content-Type: application/json" \
      --data-binary "@$data_file" \
      "$QA_API_URL$path"
  else
    curl \
      --silent \
      --output "$output_file" \
      --write-out "%{http_code}" \
      --request "$method" \
      --header "Authorization: Bearer $token" \
      "$QA_API_URL$path"
  fi
}

echo "=================================================="
echo "NEVERFADE POS — USER MANAGEMENT REGRESSION"
echo "BUG-001"
echo "Run ID: $QA_RUN_ID"
echo "=================================================="

echo
echo "===== STARTUP & OWNER LOGIN ====="

qa_start_backend || exit 1
qa_login_owner "$TMP_DIR/owner-login.json" || exit 1

echo
echo "===== GET USERS ====="

STATUS="$(
  qa_request \
    GET \
    "/api/users" \
    "$TMP_DIR/users.json"
)"

qa_expect_status \
  "Owner get users" \
  "$STATUS" \
  "200"

qa_expect_jq \
  "Users response is array" \
  "$TMP_DIR/users.json" \
  'type == "array"'

qa_expect_jq \
  "Owner user exists" \
  "$TMP_DIR/users.json" \
  'any(.[]; .username == "owner" and .role == "owner")'

qa_expect_jq \
  "Users response never exposes passwordHash" \
  "$TMP_DIR/users.json" \
  '[.. | objects | has("passwordHash")] | any | not'

echo
echo "===== ROLE AUTHORIZATION ====="

ADMIN_STATUS="$(
  login_user \
    "$QA_ADMIN_USERNAME" \
    "$QA_ADMIN_PASSWORD" \
    "$TMP_DIR/admin-login.json"
)"

qa_expect_status \
  "Admin login" \
  "$ADMIN_STATUS" \
  "200"

ADMIN_TOKEN="$(
  jq -r \
    '.token // empty' \
    "$TMP_DIR/admin-login.json"
)"

if [ -n "$ADMIN_TOKEN" ]; then
  qa_pass "Admin token diterima"
else
  qa_fail "Admin token tidak diterima"
fi

ADMIN_USERS_STATUS="$(
  request_with_token \
    GET \
    "/api/users" \
    "$ADMIN_TOKEN" \
    "$TMP_DIR/admin-users.json"
)"

qa_expect_status \
  "Admin allowed to access users" \
  "$ADMIN_USERS_STATUS" \
  "200"

KASIR_STATUS="$(
  login_user \
    "$QA_KASIR_USERNAME" \
    "$QA_KASIR_PASSWORD" \
    "$TMP_DIR/kasir-login.json"
)"

qa_expect_status \
  "Kasir login" \
  "$KASIR_STATUS" \
  "200"

KASIR_TOKEN="$(
  jq -r \
    '.token // empty' \
    "$TMP_DIR/kasir-login.json"
)"

if [ -n "$KASIR_TOKEN" ]; then
  qa_pass "Kasir token diterima"
else
  qa_fail "Kasir token tidak diterima"
fi

KASIR_USERS_STATUS="$(
  request_with_token \
    GET \
    "/api/users" \
    "$KASIR_TOKEN" \
    "$TMP_DIR/kasir-users.json"
)"

qa_expect_status \
  "Kasir forbidden from users" \
  "$KASIR_USERS_STATUS" \
  "403"

echo
echo "===== CREATE USER ====="

jq -n \
  --arg nama "QA User Management" \
  --arg username "$TEST_USERNAME" \
  --arg password "$TEST_PASSWORD" \
  '{
    nama: $nama,
    username: $username,
    password: $password,
    role: "kasir"
  }' \
  > "$TMP_DIR/create.json"

STATUS="$(
  qa_request \
    POST \
    "/api/users" \
    "$TMP_DIR/create-response.json" \
    "$TMP_DIR/create.json"
)"

qa_expect_status \
  "Create user" \
  "$STATUS" \
  "200"

USER_ID="$(
  jq -r \
    '.id // empty' \
    "$TMP_DIR/create-response.json"
)"

if [ -n "$USER_ID" ]; then
  qa_pass "Created user ID tersedia"
else
  qa_fail "Created user ID tidak tersedia"
fi

if jq -e \
  --arg username "$TEST_USERNAME" \
  '.username == $username and
   .nama == "QA User Management" and
   .role == "kasir" and
   .active == true' \
  "$TMP_DIR/create-response.json" \
  >/dev/null 2>&1
then
  qa_pass "Created user values benar"
else
  qa_fail "Created user values benar"
fi

qa_expect_jq \
  "Create response does not expose passwordHash" \
  "$TMP_DIR/create-response.json" \
  'has("passwordHash") | not'

echo
echo "===== USER LIST PERSISTENCE ====="

STATUS="$(
  qa_request \
    GET \
    "/api/users" \
    "$TMP_DIR/users-after-create.json"
)"

qa_expect_status \
  "Get users after create" \
  "$STATUS" \
  "200"

if jq -e \
  --arg id "$USER_ID" \
  'any(.[]; .id == $id)' \
  "$TMP_DIR/users-after-create.json" \
  >/dev/null 2>&1
then
  qa_pass "Created user appears in list"
else
  qa_fail "Created user missing from list"
fi

qa_expect_jq \
  "User list still does not expose passwordHash" \
  "$TMP_DIR/users-after-create.json" \
  '[.. | objects | has("passwordHash")] | any | not'

echo
echo "===== CREATE VALIDATION ====="

STATUS="$(
  qa_request \
    POST \
    "/api/users" \
    "$TMP_DIR/duplicate-response.json" \
    "$TMP_DIR/create.json"
)"

qa_expect_status \
  "Duplicate username rejected" \
  "$STATUS" \
  "400"

jq -n \
  --arg username "${TEST_USERNAME}_badrole" \
  --arg password "$TEST_PASSWORD" \
  '{
    nama: "QA Invalid Role",
    username: $username,
    password: $password,
    role: "superadmin"
  }' \
  > "$TMP_DIR/invalid-role.json"

STATUS="$(
  qa_request \
    POST \
    "/api/users" \
    "$TMP_DIR/invalid-role-response.json" \
    "$TMP_DIR/invalid-role.json"
)"

qa_expect_status \
  "Invalid role rejected" \
  "$STATUS" \
  "400"

cat > "$TMP_DIR/empty-user.json" <<'JSON'
{
  "nama": "",
  "username": "",
  "password": "",
  "role": ""
}
JSON

STATUS="$(
  qa_request \
    POST \
    "/api/users" \
    "$TMP_DIR/empty-user-response.json" \
    "$TMP_DIR/empty-user.json"
)"

qa_expect_status \
  "Empty required user fields rejected" \
  "$STATUS" \
  "400"

echo
echo "===== INITIAL PASSWORD LOGIN ====="

STATUS="$(
  login_user \
    "$TEST_USERNAME" \
    "$TEST_PASSWORD" \
    "$TMP_DIR/test-user-login.json"
)"

qa_expect_status \
  "Created user can login" \
  "$STATUS" \
  "200"

echo
echo "===== UPDATE WITHOUT PASSWORD ====="

jq -n \
  --arg username "$UPDATED_USERNAME" \
  '{
    nama: "QA User Updated",
    username: $username,
    role: "kasir",
    active: true
  }' \
  > "$TMP_DIR/update-no-password.json"

STATUS="$(
  qa_request \
    PUT \
    "/api/users/$USER_ID" \
    "$TMP_DIR/update-no-password-response.json" \
    "$TMP_DIR/update-no-password.json"
)"

qa_expect_status \
  "Update user without password" \
  "$STATUS" \
  "200"

if jq -e \
  --arg username "$UPDATED_USERNAME" \
  '.username == $username and
   .nama == "QA User Updated" and
   .role == "kasir" and
   .active == true' \
  "$TMP_DIR/update-no-password-response.json" \
  >/dev/null 2>&1
then
  qa_pass "Updated user values persisted"
else
  qa_fail "Updated user values persisted"
fi

qa_expect_jq \
  "Update response does not expose passwordHash" \
  "$TMP_DIR/update-no-password-response.json" \
  'has("passwordHash") | not'

STATUS="$(
  login_user \
    "$UPDATED_USERNAME" \
    "$TEST_PASSWORD" \
    "$TMP_DIR/login-after-no-password-update.json"
)"

qa_expect_status \
  "Password preserved when update omits password" \
  "$STATUS" \
  "200"

echo
echo "===== UPDATE PASSWORD ====="

jq -n \
  --arg username "$UPDATED_USERNAME" \
  --arg password "$NEW_PASSWORD" \
  '{
    nama: "QA User Updated",
    username: $username,
    role: "kasir",
    active: true,
    password: $password
  }' \
  > "$TMP_DIR/update-password.json"

STATUS="$(
  qa_request \
    PUT \
    "/api/users/$USER_ID" \
    "$TMP_DIR/update-password-response.json" \
    "$TMP_DIR/update-password.json"
)"

qa_expect_status \
  "Update user password" \
  "$STATUS" \
  "200"

STATUS="$(
  login_user \
    "$UPDATED_USERNAME" \
    "$TEST_PASSWORD" \
    "$TMP_DIR/login-old-password.json"
)"

qa_expect_status \
  "Old password rejected after password update" \
  "$STATUS" \
  "401"

STATUS="$(
  login_user \
    "$UPDATED_USERNAME" \
    "$NEW_PASSWORD" \
    "$TMP_DIR/login-new-password.json"
)"

qa_expect_status \
  "New password accepted after password update" \
  "$STATUS" \
  "200"

echo
echo "===== ACTIVE STATUS ====="

jq -n \
  --arg username "$UPDATED_USERNAME" \
  '{
    nama: "QA User Updated",
    username: $username,
    role: "kasir",
    active: false
  }' \
  > "$TMP_DIR/deactivate.json"

STATUS="$(
  qa_request \
    PUT \
    "/api/users/$USER_ID" \
    "$TMP_DIR/deactivate-response.json" \
    "$TMP_DIR/deactivate.json"
)"

qa_expect_status \
  "Deactivate user" \
  "$STATUS" \
  "200"

STATUS="$(
  login_user \
    "$UPDATED_USERNAME" \
    "$NEW_PASSWORD" \
    "$TMP_DIR/login-inactive.json"
)"

qa_expect_status \
  "Inactive user cannot login" \
  "$STATUS" \
  "401"

jq -n \
  --arg username "$UPDATED_USERNAME" \
  '{
    nama: "QA User Updated",
    username: $username,
    role: "kasir",
    active: true
  }' \
  > "$TMP_DIR/reactivate.json"

STATUS="$(
  qa_request \
    PUT \
    "/api/users/$USER_ID" \
    "$TMP_DIR/reactivate-response.json" \
    "$TMP_DIR/reactivate.json"
)"

qa_expect_status \
  "Reactivate user" \
  "$STATUS" \
  "200"

echo
echo "===== SELF DELETE PROTECTION ====="

STATUS="$(
  qa_request \
    GET \
    "/api/auth/me" \
    "$TMP_DIR/me.json"
)"

qa_expect_status \
  "Get current owner" \
  "$STATUS" \
  "200"

OWNER_ID="$(
  jq -r \
    '.id // empty' \
    "$TMP_DIR/me.json"
)"

SELF_DELETE_STATUS="$(
  qa_request \
    DELETE \
    "/api/users/$OWNER_ID" \
    "$TMP_DIR/self-delete.json"
)"

qa_expect_status \
  "Current owner cannot delete own account" \
  "$SELF_DELETE_STATUS" \
  "400"

echo
echo "===== UNKNOWN USER ====="

UNKNOWN_ID="00000000-0000-0000-0000-000000000001"

STATUS="$(
  qa_request \
    PUT \
    "/api/users/$UNKNOWN_ID" \
    "$TMP_DIR/unknown-update-response.json" \
    "$TMP_DIR/reactivate.json"
)"

qa_expect_status \
  "Unknown user update returns not found" \
  "$STATUS" \
  "404"

STATUS="$(
  qa_request \
    DELETE \
    "/api/users/$UNKNOWN_ID" \
    "$TMP_DIR/unknown-delete-response.json"
)"

qa_expect_status \
  "Unknown user delete returns not found" \
  "$STATUS" \
  "404"

echo
echo "===== DELETE USER ====="

STATUS="$(
  qa_request \
    DELETE \
    "/api/users/$USER_ID" \
    "$TMP_DIR/delete-response.json"
)"

qa_expect_status \
  "Delete created user" \
  "$STATUS" \
  "200"

STATUS="$(
  qa_request \
    GET \
    "/api/users" \
    "$TMP_DIR/users-after-delete.json"
)"

qa_expect_status \
  "Get users after delete" \
  "$STATUS" \
  "200"

if jq -e \
  --arg id "$USER_ID" \
  'all(.[]; .id != $id)' \
  "$TMP_DIR/users-after-delete.json" \
  >/dev/null 2>&1
then
  qa_pass "Deleted user removed from list"
else
  qa_fail "Deleted user still exists in list"
fi

STATUS="$(
  login_user \
    "$UPDATED_USERNAME" \
    "$NEW_PASSWORD" \
    "$TMP_DIR/login-deleted.json"
)"

qa_expect_status \
  "Deleted user cannot login" \
  "$STATUS" \
  "401"

echo
echo "===== GIT STATUS ====="

git -C "$HOME/neverfade-pos-backend" \
  status --short --branch

qa_print_summary

if [ "$QA_FAILED" -gt 0 ]; then
  exit 1
fi

exit 0
