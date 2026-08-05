#!/usr/bin/env bash

set +u

source "$HOME/neverfade-pos-backend/qa/lib.sh"

TMP_DIR="$(mktemp -d)"
ORIGINAL_CAPTURED=0
RESTORED=0

restore_settings() {
  if [ "$ORIGINAL_CAPTURED" -ne 1 ] ||
     [ "$RESTORED" -eq 1 ] ||
     [ -z "$QA_TOKEN" ]
  then
    return
  fi

  STATUS="$(
    qa_request \
      PUT \
      "/api/settings" \
      "$TMP_DIR/settings-cleanup-response.json" \
      "$TMP_DIR/settings-original.json"
  )"

  if [ "$STATUS" = "200" ]; then
    RESTORED=1
    echo "[CLEANUP] Original settings restored."
  else
    echo "[CLEANUP-FAIL] Original settings restore returned HTTP $STATUS."
  fi
}

cleanup() {
  qa_restore_terminal
  restore_settings
  qa_stop_backend
  rm -rf "$TMP_DIR"
}

trap cleanup EXIT INT TERM

echo "=================================================="
echo "NEVERFADE POS — SETTINGS REGRESSION"
echo "Run ID: $QA_RUN_ID"
echo "=================================================="

echo
echo "===== STARTUP & LOGIN ====="

qa_start_backend || exit 1
qa_login_owner "$TMP_DIR/login.json" || exit 1

echo
echo "===== CAPTURE ORIGINAL SETTINGS ====="

STATUS="$(
  qa_request \
    GET \
    "/api/settings" \
    "$TMP_DIR/settings-original.json"
)"

qa_expect_status \
  "Get original settings" \
  "$STATUS" \
  "200"

qa_expect_jq \
  "Original settings response shape" \
  "$TMP_DIR/settings-original.json" \
  'has("namaToko") and
   has("alamat") and
   has("telepon") and
   has("email") and
   has("website") and
   has("headerStruk") and
   has("footerStruk") and
   has("showTax") and
   has("showPoint") and
   has("defaultTax") and
   has("minStok") and
   has("poinRate")'

if [ "$STATUS" != "200" ]; then
  qa_print_summary
  exit 1
fi

ORIGINAL_CAPTURED=1

echo
echo "===== UPDATE SETTINGS ====="

QA_STORE_NAME="${QA_RUN_ID}_Store"
QA_EMAIL="$(printf "%s" "$QA_RUN_ID" | tr '[:upper:]' '[:lower:]')-settings@qa.local"

jq \
  --arg namaToko "$QA_STORE_NAME" \
  --arg email "$QA_EMAIL" \
  '
  .namaToko = $namaToko |
  .alamat = "QA Temporary Address" |
  .telepon = "081200000099" |
  .email = $email |
  .website = "https://qa.local" |
  .headerStruk = "QA HEADER" |
  .footerStruk = "QA FOOTER" |
  .showTax = true |
  .showPoint = true |
  .defaultTax = 11 |
  .minStok = 9 |
  .poinRate = 250
  ' \
  "$TMP_DIR/settings-original.json" \
  > "$TMP_DIR/settings-updated-payload.json"

STATUS="$(
  qa_request \
    PUT \
    "/api/settings" \
    "$TMP_DIR/settings-update-response.json" \
    "$TMP_DIR/settings-updated-payload.json"
)"

qa_expect_status \
  "Update settings" \
  "$STATUS" \
  "200"

qa_expect_jq \
  "Update settings response shape" \
  "$TMP_DIR/settings-update-response.json" \
  '.ok == true'

STATUS="$(
  qa_request \
    GET \
    "/api/settings" \
    "$TMP_DIR/settings-after-update.json"
)"

qa_expect_status \
  "Get updated settings" \
  "$STATUS" \
  "200"

if jq -e \
  --arg namaToko "$QA_STORE_NAME" \
  --arg email "$QA_EMAIL" \
  '.namaToko == $namaToko and
   .email == $email and
   .showTax == true and
   .showPoint == true and
   .defaultTax == 11 and
   .minStok == 9 and
   .poinRate == 250' \
  "$TMP_DIR/settings-after-update.json" \
  >/dev/null 2>&1
then
  qa_pass "Updated settings values persisted"
else
  qa_fail "Updated settings values not persisted"
fi

echo
echo "===== SETTINGS VALIDATION ====="

jq \
  '.namaToko = ""' \
  "$TMP_DIR/settings-updated-payload.json" \
  > "$TMP_DIR/settings-empty-name.json"

STATUS="$(
  qa_request \
    PUT \
    "/api/settings" \
    "$TMP_DIR/settings-empty-name-response.json" \
    "$TMP_DIR/settings-empty-name.json"
)"

qa_expect_status \
  "Empty store name rejected" \
  "$STATUS" \
  "400"

jq \
  '.email = "invalid-email"' \
  "$TMP_DIR/settings-updated-payload.json" \
  > "$TMP_DIR/settings-invalid-email.json"

STATUS="$(
  qa_request \
    PUT \
    "/api/settings" \
    "$TMP_DIR/settings-invalid-email-response.json" \
    "$TMP_DIR/settings-invalid-email.json"
)"

qa_expect_status \
  "Invalid settings email rejected" \
  "$STATUS" \
  "400"

jq \
  '.defaultTax = -1' \
  "$TMP_DIR/settings-updated-payload.json" \
  > "$TMP_DIR/settings-negative-tax.json"

STATUS="$(
  qa_request \
    PUT \
    "/api/settings" \
    "$TMP_DIR/settings-negative-tax-response.json" \
    "$TMP_DIR/settings-negative-tax.json"
)"

qa_expect_status \
  "Negative default tax rejected" \
  "$STATUS" \
  "400"

jq \
  '.minStok = -1' \
  "$TMP_DIR/settings-updated-payload.json" \
  > "$TMP_DIR/settings-negative-stock.json"

STATUS="$(
  qa_request \
    PUT \
    "/api/settings" \
    "$TMP_DIR/settings-negative-stock-response.json" \
    "$TMP_DIR/settings-negative-stock.json"
)"

qa_expect_status \
  "Negative minimum stock rejected" \
  "$STATUS" \
  "400"

jq \
  '.poinRate = -1' \
  "$TMP_DIR/settings-updated-payload.json" \
  > "$TMP_DIR/settings-negative-point.json"

STATUS="$(
  qa_request \
    PUT \
    "/api/settings" \
    "$TMP_DIR/settings-negative-point-response.json" \
    "$TMP_DIR/settings-negative-point.json"
)"

qa_expect_status \
  "Negative point rate rejected" \
  "$STATUS" \
  "400"

echo
echo "===== RESTORE ORIGINAL SETTINGS ====="

STATUS="$(
  qa_request \
    PUT \
    "/api/settings" \
    "$TMP_DIR/settings-restore-response.json" \
    "$TMP_DIR/settings-original.json"
)"

qa_expect_status \
  "Restore original settings" \
  "$STATUS" \
  "200"

if [ "$STATUS" = "200" ]; then
  RESTORED=1
fi

STATUS="$(
  qa_request \
    GET \
    "/api/settings" \
    "$TMP_DIR/settings-restored.json"
)"

qa_expect_status \
  "Get restored settings" \
  "$STATUS" \
  "200"

if jq -e \
  --slurpfile original "$TMP_DIR/settings-original.json" \
  '. == $original[0]' \
  "$TMP_DIR/settings-restored.json" \
  >/dev/null 2>&1
then
  qa_pass "Original settings restored exactly"
else
  qa_fail "Restored settings differ from original"
fi

echo
echo "===== GIT STATUS ====="

git -C "$BACKEND" status --short --branch
git -C "$HOME/neverfade-pos-frontend" status --short --branch

qa_print_summary

if [ "$QA_FAILED" -gt 0 ]; then
  exit 1
fi
