#!/usr/bin/env bash

set +u

source "$HOME/neverfade-pos-backend/qa/lib.sh"

TMP_DIR="$(mktemp -d)"
CUSTOMER_ID=""

cleanup() {
  qa_restore_terminal

  if [ -n "$QA_TOKEN" ] && [ -n "$CUSTOMER_ID" ]; then
    qa_request \
      DELETE \
      "/api/customers/$CUSTOMER_ID" \
      "$TMP_DIR/cleanup-customer.json" \
      >/dev/null 2>&1 || true
  fi

  qa_stop_backend
  rm -rf "$TMP_DIR"
}

trap cleanup EXIT INT TERM

echo "=================================================="
echo "NEVERFADE POS — CUSTOMER REGRESSION"
echo "Run ID: $QA_RUN_ID"
echo "=================================================="

echo
echo "===== STARTUP & LOGIN ====="

qa_start_backend || exit 1
qa_login_owner "$TMP_DIR/login.json" || exit 1

echo
echo "===== CUSTOMER CREATE ====="

CUSTOMER_NAME="${QA_RUN_ID}_Customer"
CUSTOMER_PHONE="0812$(date +%H%M%S)"
CUSTOMER_EMAIL="$(printf "%s" "$QA_RUN_ID" | tr '[:upper:]' '[:lower:]')@qa.local"

jq -n \
  --arg nama "$CUSTOMER_NAME" \
  --arg hp "$CUSTOMER_PHONE" \
  --arg email "$CUSTOMER_EMAIL" \
  '{
    nama:$nama,
    hp:$hp,
    email:$email,
    alamat:"QA Temporary Address"
  }' > "$TMP_DIR/customer-create.json"

STATUS="$(
  qa_request \
    POST \
    "/api/customers" \
    "$TMP_DIR/customer-created.json" \
    "$TMP_DIR/customer-create.json"
)"

qa_expect_status \
  "Create customer" \
  "$STATUS" \
  "200"

CUSTOMER_ID="$(
  jq -r '.id // empty' \
    "$TMP_DIR/customer-created.json"
)"

if [ -n "$CUSTOMER_ID" ]; then
  qa_pass "Created customer ID tersedia"
else
  qa_fail "Created customer ID tidak tersedia"
  qa_print_summary
  exit 1
fi

if jq -e \
  --arg nama "$CUSTOMER_NAME" \
  --arg hp "$CUSTOMER_PHONE" \
  --arg email "$CUSTOMER_EMAIL" \
  '.nama == $nama and
   .hp == $hp and
   .email == $email and
   .poin == 0 and
   .totalTransaksi == 0' \
  "$TMP_DIR/customer-created.json" \
  >/dev/null 2>&1
then
  qa_pass "Created customer values benar"
else
  qa_fail "Created customer values tidak benar"
fi

echo
echo "===== CUSTOMER READ & SEARCH ====="

STATUS="$(
  qa_request \
    GET \
    "/api/customers/$CUSTOMER_ID" \
    "$TMP_DIR/customer-get.json"
)"

qa_expect_status \
  "Get customer by ID" \
  "$STATUS" \
  "200"

STATUS="$(
  qa_request \
    GET \
    "/api/customers?search=$CUSTOMER_NAME" \
    "$TMP_DIR/customer-search.json"
)"

qa_expect_status \
  "Search customer by unique name" \
  "$STATUS" \
  "200"

if jq -e \
  --arg id "$CUSTOMER_ID" \
  'any(.[]; .id == $id)' \
  "$TMP_DIR/customer-search.json" \
  >/dev/null 2>&1
then
  qa_pass "Search result contains created customer"
else
  qa_fail "Search result does not contain created customer"
fi

echo
echo "===== CUSTOMER VALIDATION ====="

jq -n \
  '{
    nama:"QA Invalid Email",
    hp:"081200000000",
    email:"not-an-email",
    alamat:"QA"
  }' > "$TMP_DIR/customer-invalid-email.json"

STATUS="$(
  qa_request \
    POST \
    "/api/customers" \
    "$TMP_DIR/customer-invalid-email-response.json" \
    "$TMP_DIR/customer-invalid-email.json"
)"

qa_expect_status \
  "Invalid customer email rejected" \
  "$STATUS" \
  "400"

jq -n \
  '{
    nama:"",
    hp:"",
    email:"",
    alamat:""
  }' > "$TMP_DIR/customer-required.json"

STATUS="$(
  qa_request \
    POST \
    "/api/customers" \
    "$TMP_DIR/customer-required-response.json" \
    "$TMP_DIR/customer-required.json"
)"

qa_expect_status \
  "Empty required customer fields rejected" \
  "$STATUS" \
  "400"

UNKNOWN_ID="$(
  uuidgen |
  tr '[:upper:]' '[:lower:]'
)"

STATUS="$(
  qa_request \
    GET \
    "/api/customers/$UNKNOWN_ID" \
    "$TMP_DIR/customer-unknown.json"
)"

qa_expect_status \
  "Unknown customer returns not found" \
  "$STATUS" \
  "404"

echo
echo "===== CUSTOMER UPDATE ====="

UPDATED_NAME="${CUSTOMER_NAME}_Updated"

jq -n \
  --arg nama "$UPDATED_NAME" \
  --arg hp "$CUSTOMER_PHONE" \
  --arg email "$CUSTOMER_EMAIL" \
  '{
    nama:$nama,
    hp:$hp,
    email:$email,
    alamat:"QA Updated Address"
  }' > "$TMP_DIR/customer-update.json"

STATUS="$(
  qa_request \
    PUT \
    "/api/customers/$CUSTOMER_ID" \
    "$TMP_DIR/customer-updated.json" \
    "$TMP_DIR/customer-update.json"
)"

qa_expect_status \
  "Update customer" \
  "$STATUS" \
  "200"

if jq -e \
  --arg nama "$UPDATED_NAME" \
  '.nama == $nama and
   .alamat == "QA Updated Address"' \
  "$TMP_DIR/customer-updated.json" \
  >/dev/null 2>&1
then
  qa_pass "Updated customer values persisted"
else
  qa_fail "Updated customer values not persisted"
fi

echo
echo "===== CUSTOMER DELETE ====="

STATUS="$(
  qa_request \
    DELETE \
    "/api/customers/$CUSTOMER_ID" \
    "$TMP_DIR/customer-delete.json"
)"

qa_expect_status \
  "Delete customer" \
  "$STATUS" \
  "200"

if [ "$STATUS" = "200" ]; then
  CUSTOMER_ID=""
fi

DELETED_ID="$(
  jq -r '.id' \
    "$TMP_DIR/customer-created.json"
)"

STATUS="$(
  qa_request \
    GET \
    "/api/customers/$DELETED_ID" \
    "$TMP_DIR/customer-after-delete.json"
)"

qa_expect_status \
  "Deleted customer no longer available" \
  "$STATUS" \
  "404"

echo
echo "===== GIT STATUS ====="

git -C "$BACKEND" status --short --branch
git -C "$HOME/neverfade-pos-frontend" status --short --branch

qa_print_summary

if [ "$QA_FAILED" -gt 0 ]; then
  exit 1
fi
