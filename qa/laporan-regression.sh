#!/usr/bin/env bash

set +u

source "$HOME/neverfade-pos-backend/qa/lib.sh"

TMP_DIR="$(mktemp -d)"
RESULT_DIR="$HOME/neverfade-pos-qa"
VERIFY_RESULT="$RESULT_DIR/laporan-result.txt"

cleanup() {
  qa_restore_terminal
  qa_stop_backend
  rm -rf "$TMP_DIR"
}

trap cleanup EXIT INT TERM

mkdir -p "$RESULT_DIR"

echo "=================================================="
echo "NEVERFADE POS — LAPORAN REGRESSION"
echo "Run ID: $QA_RUN_ID"
echo "=================================================="

echo
echo "===== STARTUP & LOGIN ====="

qa_start_backend || exit 1
qa_login_owner "$TMP_DIR/login.json" || exit 1

echo
echo "===== FETCH TRANSACTION SOURCE DATA ====="

STATUS="$(
  qa_request \
    GET \
    "/api/transactions" \
    "$TMP_DIR/transactions.json"
)"

qa_expect_status \
  "Get all transactions" \
  "$STATUS" \
  "200"

for PERIOD in harian mingguan bulanan tahunan; do
  STATUS="$(
    qa_request \
      GET \
      "/api/laporan/summary?period=$PERIOD" \
      "$TMP_DIR/summary-$PERIOD.json"
  )"

  qa_expect_status \
    "Get summary $PERIOD" \
    "$STATUS" \
    "200"

  STATUS="$(
    qa_request \
      GET \
      "/api/laporan/top-products?period=$PERIOD" \
      "$TMP_DIR/top-$PERIOD.json"
  )"

  qa_expect_status \
    "Get top products $PERIOD" \
    "$STATUS" \
    "200"
done

STATUS="$(
  qa_request \
    GET \
    "/api/laporan/summary?period=invalid-qa-period" \
    "$TMP_DIR/summary-invalid.json"
)"

qa_expect_status \
  "Get summary invalid period" \
  "$STATUS" \
  "200"

STATUS="$(
  qa_request \
    GET \
    "/api/laporan/top-products?period=invalid-qa-period" \
    "$TMP_DIR/top-invalid.json"
)"

qa_expect_status \
  "Get top products invalid period" \
  "$STATUS" \
  "200"

STATUS="$(
  qa_request \
    GET \
    "/api/laporan/chart" \
    "$TMP_DIR/chart.json"
)"

qa_expect_status \
  "Get seven-day chart" \
  "$STATUS" \
  "200"

echo
echo "===== VERIFY REPORT CALCULATIONS ====="

python3 \
  "$HOME/neverfade-pos-backend/qa/verify-laporan.py" \
  "$TMP_DIR" \
  "$VERIFY_RESULT" \
  > "$TMP_DIR/verification-output.txt"

VERIFY_EXIT=$?

while IFS=$'\t' read -r STATE LABEL; do
  case "$STATE" in
    PASS)
      qa_pass "$LABEL"
      ;;
    FAIL)
      qa_fail "$LABEL"
      ;;
  esac
done < "$TMP_DIR/verification-output.txt"

if grep -q \
  '^FAIL.*Chart totals follow Asia/Jakarta calendar dates' \
  "$TMP_DIR/verification-output.txt"
then
  echo
  echo "[DEFECT] Candidate BUG-005:"
  echo "Chart groups transaction totals by UTC date instead of WIB date."
  echo "Evidence: $VERIFY_RESULT"
fi

echo
echo "===== RESULT FILE ====="

cat "$VERIFY_RESULT"

echo
echo "===== GIT STATUS ====="

git -C "$HOME/neverfade-pos-backend" status --short --branch
git -C "$HOME/neverfade-pos-frontend" status --short --branch

qa_print_summary

if [ "$VERIFY_EXIT" -ne 0 ] ||
   [ "$QA_FAILED" -gt 0 ]
then
  exit 1
fi
