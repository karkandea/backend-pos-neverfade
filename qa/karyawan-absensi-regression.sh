#!/usr/bin/env bash

set +u

source "$HOME/neverfade-pos-backend/qa/lib.sh"

TMP_DIR="$(mktemp -d)"
KARYAWAN_ID=""

cleanup() {
  qa_restore_terminal

  if [ -n "$QA_TOKEN" ] && [ -n "$KARYAWAN_ID" ]; then
    qa_request \
      DELETE \
      "/api/karyawan/$KARYAWAN_ID" \
      "$TMP_DIR/cleanup-karyawan.json" \
      >/dev/null 2>&1 || true
  fi

  qa_stop_backend
  rm -rf "$TMP_DIR"
}

trap cleanup EXIT INT TERM

echo "=================================================="
echo "NEVERFADE POS — KARYAWAN & ABSENSI REGRESSION"
echo "Run ID: $QA_RUN_ID"
echo "=================================================="

echo
echo "===== STARTUP & LOGIN ====="

qa_start_backend || exit 1
qa_login_owner "$TMP_DIR/login.json" || exit 1

echo
echo "===== KARYAWAN CREATE ====="

KARYAWAN_NAME="${QA_RUN_ID}_Karyawan"
KARYAWAN_EMAIL="$(printf "%s" "$QA_RUN_ID" | tr '[:upper:]' '[:lower:]')@qa.local"
TODAY="$(TZ=Asia/Jakarta date +%Y-%m-%d)"

jq -n \
  --arg nama "$KARYAWAN_NAME" \
  --arg email "$KARYAWAN_EMAIL" \
  --arg tanggalMasuk "$TODAY" \
  '{
    nama:$nama,
    jabatan:"QA Tester",
    telepon:"081200000001",
    email:$email,
    gaji:5000000,
    tanggalMasuk:$tanggalMasuk,
    status:"aktif",
    catatan:"Temporary QA employee"
  }' > "$TMP_DIR/karyawan-create.json"

STATUS="$(
  qa_request \
    POST \
    "/api/karyawan" \
    "$TMP_DIR/karyawan-created.json" \
    "$TMP_DIR/karyawan-create.json"
)"

qa_expect_status \
  "Create karyawan" \
  "$STATUS" \
  "200"

KARYAWAN_ID="$(
  jq -r '.id // empty' \
    "$TMP_DIR/karyawan-created.json"
)"

if [ -n "$KARYAWAN_ID" ]; then
  qa_pass "Created karyawan ID tersedia"
else
  qa_fail "Created karyawan ID tidak tersedia"
  qa_print_summary
  exit 1
fi

if jq -e \
  --arg nama "$KARYAWAN_NAME" \
  --arg email "$KARYAWAN_EMAIL" \
  '.nama == $nama and
   .email == $email and
   .jabatan == "QA Tester" and
   .status == "aktif" and
   .gaji == 5000000' \
  "$TMP_DIR/karyawan-created.json" \
  >/dev/null 2>&1
then
  qa_pass "Created karyawan values benar"
else
  qa_fail "Created karyawan values tidak benar"
fi

echo
echo "===== KARYAWAN READ & FILTER ====="

STATUS="$(
  qa_request \
    GET \
    "/api/karyawan/$KARYAWAN_ID" \
    "$TMP_DIR/karyawan-get.json"
)"

qa_expect_status \
  "Get karyawan by ID" \
  "$STATUS" \
  "200"

STATUS="$(
  qa_request \
    GET \
    "/api/karyawan?search=$KARYAWAN_NAME" \
    "$TMP_DIR/karyawan-search.json"
)"

qa_expect_status \
  "Search karyawan by unique name" \
  "$STATUS" \
  "200"

if jq -e \
  --arg id "$KARYAWAN_ID" \
  'any(.[]; .id == $id)' \
  "$TMP_DIR/karyawan-search.json" \
  >/dev/null 2>&1
then
  qa_pass "Search result contains created karyawan"
else
  qa_fail "Search result does not contain created karyawan"
fi

STATUS="$(
  qa_request \
    GET \
    "/api/karyawan?status=aktif" \
    "$TMP_DIR/karyawan-active.json"
)"

qa_expect_status \
  "Filter active karyawan" \
  "$STATUS" \
  "200"

if jq -e \
  --arg id "$KARYAWAN_ID" \
  'any(.[]; .id == $id)' \
  "$TMP_DIR/karyawan-active.json" \
  >/dev/null 2>&1
then
  qa_pass "Active filter contains created karyawan"
else
  qa_fail "Active filter does not contain created karyawan"
fi

echo
echo "===== KARYAWAN VALIDATION ====="

jq -n \
  --arg tanggalMasuk "$TODAY" \
  '{
    nama:"",
    jabatan:"",
    telepon:"",
    email:"",
    gaji:0,
    tanggalMasuk:$tanggalMasuk,
    status:"aktif",
    catatan:""
  }' > "$TMP_DIR/karyawan-empty.json"

STATUS="$(
  qa_request \
    POST \
    "/api/karyawan" \
    "$TMP_DIR/karyawan-empty-response.json" \
    "$TMP_DIR/karyawan-empty.json"
)"

qa_expect_status \
  "Empty required karyawan fields rejected" \
  "$STATUS" \
  "400"

jq -n \
  --arg tanggalMasuk "$TODAY" \
  '{
    nama:"QA Invalid Email",
    jabatan:"QA",
    telepon:"",
    email:"invalid-email",
    gaji:1000,
    tanggalMasuk:$tanggalMasuk,
    status:"aktif",
    catatan:""
  }' > "$TMP_DIR/karyawan-invalid-email.json"

STATUS="$(
  qa_request \
    POST \
    "/api/karyawan" \
    "$TMP_DIR/karyawan-invalid-email-response.json" \
    "$TMP_DIR/karyawan-invalid-email.json"
)"

qa_expect_status \
  "Invalid karyawan email rejected" \
  "$STATUS" \
  "400"

jq -n \
  --arg tanggalMasuk "$TODAY" \
  '{
    nama:"QA Negative Salary",
    jabatan:"QA",
    telepon:"",
    email:"",
    gaji:-1,
    tanggalMasuk:$tanggalMasuk,
    status:"aktif",
    catatan:""
  }' > "$TMP_DIR/karyawan-negative-gaji.json"

STATUS="$(
  qa_request \
    POST \
    "/api/karyawan" \
    "$TMP_DIR/karyawan-negative-gaji-response.json" \
    "$TMP_DIR/karyawan-negative-gaji.json"
)"

qa_expect_status \
  "Negative salary rejected" \
  "$STATUS" \
  "400"

UNKNOWN_KARYAWAN_ID="$(
  uuidgen |
  tr '[:upper:]' '[:lower:]'
)"

STATUS="$(
  qa_request \
    GET \
    "/api/karyawan/$UNKNOWN_KARYAWAN_ID" \
    "$TMP_DIR/karyawan-unknown.json"
)"

qa_expect_status \
  "Unknown karyawan returns not found" \
  "$STATUS" \
  "404"

echo
echo "===== KARYAWAN UPDATE ====="

UPDATED_NAME="${KARYAWAN_NAME}_Updated"

jq -n \
  --arg nama "$UPDATED_NAME" \
  --arg email "$KARYAWAN_EMAIL" \
  --arg tanggalMasuk "$TODAY" \
  '{
    nama:$nama,
    jabatan:"Senior QA Tester",
    telepon:"081200000002",
    email:$email,
    gaji:6000000,
    tanggalMasuk:$tanggalMasuk,
    status:"aktif",
    catatan:"Updated by QA"
  }' > "$TMP_DIR/karyawan-update.json"

STATUS="$(
  qa_request \
    PUT \
    "/api/karyawan/$KARYAWAN_ID" \
    "$TMP_DIR/karyawan-updated.json" \
    "$TMP_DIR/karyawan-update.json"
)"

qa_expect_status \
  "Update karyawan" \
  "$STATUS" \
  "200"

if jq -e \
  --arg nama "$UPDATED_NAME" \
  '.nama == $nama and
   .jabatan == "Senior QA Tester" and
   .gaji == 6000000' \
  "$TMP_DIR/karyawan-updated.json" \
  >/dev/null 2>&1
then
  qa_pass "Updated karyawan values persisted"
else
  qa_fail "Updated karyawan values not persisted"
fi

echo
echo "===== ABSENSI FLOW ====="

jq -n \
  --arg karyawanId "$KARYAWAN_ID" \
  '{
    karyawanId:$karyawanId,
    foto:null
  }' > "$TMP_DIR/absensi-request.json"

STATUS="$(
  qa_request \
    POST \
    "/api/absensi/checkout" \
    "$TMP_DIR/checkout-before-checkin.json" \
    "$TMP_DIR/absensi-request.json"
)"

qa_expect_status \
  "Checkout before check-in rejected" \
  "$STATUS" \
  "400"

STATUS="$(
  qa_request \
    POST \
    "/api/absensi/checkin" \
    "$TMP_DIR/checkin.json" \
    "$TMP_DIR/absensi-request.json"
)"

qa_expect_status \
  "Check-in karyawan" \
  "$STATUS" \
  "200"

qa_expect_jq \
  "Check-in response shape" \
  "$TMP_DIR/checkin.json" \
  '.ok == true and
   (.checkIn | type == "string") and
   .checkOut == null'

STATUS="$(
  qa_request \
    POST \
    "/api/absensi/checkin" \
    "$TMP_DIR/checkin-repeat.json" \
    "$TMP_DIR/absensi-request.json"
)"

qa_expect_status \
  "Repeated check-in is idempotent" \
  "$STATUS" \
  "200"

STATUS="$(
  qa_request \
    POST \
    "/api/absensi/checkout" \
    "$TMP_DIR/checkout.json" \
    "$TMP_DIR/absensi-request.json"
)"

qa_expect_status \
  "Check-out karyawan" \
  "$STATUS" \
  "200"

qa_expect_jq \
  "Check-out response shape" \
  "$TMP_DIR/checkout.json" \
  '.ok == true and
   (.checkOut | type == "string")'

STATUS="$(
  qa_request \
    POST \
    "/api/absensi/checkout" \
    "$TMP_DIR/checkout-repeat.json" \
    "$TMP_DIR/absensi-request.json"
)"

qa_expect_status \
  "Repeated check-out rejected" \
  "$STATUS" \
  "400"

STATUS="$(
  qa_request \
    GET \
    "/api/absensi?karyawanId=$KARYAWAN_ID" \
    "$TMP_DIR/absensi-list.json"
)"

qa_expect_status \
  "Get absensi by karyawan ID" \
  "$STATUS" \
  "200"

if jq -e \
  --arg id "$KARYAWAN_ID" \
  'type == "array" and
   length >= 1 and
   all(.[]; .karyawanId == $id) and
   any(.[]; .checkIn != null and .checkOut != null)' \
  "$TMP_DIR/absensi-list.json" \
  >/dev/null 2>&1
then
  qa_pass "Absensi check-in and check-out persisted"
else
  qa_fail "Absensi check-in or check-out not persisted"
fi

echo
echo "===== INVALID KARYAWAN ABSENSI ====="

jq -n \
  --arg karyawanId "$UNKNOWN_KARYAWAN_ID" \
  '{
    karyawanId:$karyawanId,
    foto:null
  }' > "$TMP_DIR/unknown-absensi-request.json"

STATUS="$(
  qa_request \
    POST \
    "/api/absensi/checkin" \
    "$TMP_DIR/unknown-checkin.json" \
    "$TMP_DIR/unknown-absensi-request.json"
)"

if [ "$STATUS" = "400" ] || [ "$STATUS" = "404" ]; then
  qa_pass "Unknown karyawan check-in safely rejected — HTTP $STATUS"
elif [ "$STATUS" = "500" ]; then
  qa_fail "Unknown karyawan check-in returns internal server error — HTTP 500"
  echo "[DEFECT] Candidate BUG: invalid karyawanId is not handled before database insert."
else
  qa_fail "Unknown karyawan check-in unexpected HTTP $STATUS"
fi

echo
echo "===== CASCADE DELETE ====="

DELETED_KARYAWAN_ID="$KARYAWAN_ID"

STATUS="$(
  qa_request \
    DELETE \
    "/api/karyawan/$KARYAWAN_ID" \
    "$TMP_DIR/karyawan-delete.json"
)"

qa_expect_status \
  "Delete karyawan with absensi" \
  "$STATUS" \
  "200"

if [ "$STATUS" = "200" ]; then
  KARYAWAN_ID=""
fi

STATUS="$(
  qa_request \
    GET \
    "/api/karyawan/$DELETED_KARYAWAN_ID" \
    "$TMP_DIR/karyawan-after-delete.json"
)"

qa_expect_status \
  "Deleted karyawan no longer available" \
  "$STATUS" \
  "404"

STATUS="$(
  qa_request \
    GET \
    "/api/absensi?karyawanId=$DELETED_KARYAWAN_ID" \
    "$TMP_DIR/absensi-after-delete.json"
)"

qa_expect_status \
  "Get absensi after karyawan deletion" \
  "$STATUS" \
  "200"

qa_expect_jq \
  "Karyawan deletion cascades absensi" \
  "$TMP_DIR/absensi-after-delete.json" \
  'type == "array" and length == 0'

echo
echo "===== GIT STATUS ====="

git -C "$BACKEND" status --short --branch
git -C "$HOME/neverfade-pos-frontend" status --short --branch

qa_print_summary

if [ "$QA_FAILED" -gt 0 ]; then
  exit 1
fi
