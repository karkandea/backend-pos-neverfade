#!/usr/bin/env bash

set +u

source "$HOME/neverfade-pos-backend/qa/lib.sh"

TMP_DIR="$(mktemp -d)"
RESULT_DIR="$HOME/neverfade-pos-qa"

PRODUCT_ID=""
PRODUCT_CODE=""
RELATIONAL_DATA_CREATED=0

MANIPULATED_TRX_ID=""
MANIPULATED_TRX_NO=""
UNKNOWN_CUSTOMER_TRX_ID=""
UNKNOWN_CUSTOMER_TRX_NO=""

cleanup() {
  qa_restore_terminal

  if [ -n "$QA_TOKEN" ] &&
     [ -n "$PRODUCT_ID" ] &&
     [ "$RELATIONAL_DATA_CREATED" -eq 0 ]
  then
    qa_request \
      DELETE \
      "/api/products/$PRODUCT_ID" \
      "$TMP_DIR/cleanup-product.json" \
      >/dev/null 2>&1 || true
  fi

  qa_stop_backend
  rm -rf "$TMP_DIR"
}

trap cleanup EXIT INT TERM

mkdir -p "$RESULT_DIR"

echo "=================================================="
echo "NEVERFADE POS — TRANSACTION INTEGRITY REGRESSION"
echo "Run ID: $QA_RUN_ID"
echo "=================================================="

echo
echo "===== STARTUP & LOGIN ====="

qa_start_backend || exit 1
qa_login_owner "$TMP_DIR/login.json" || exit 1

echo
echo "===== CREATE CONTROLLED QA PRODUCT ====="

PRODUCT_CODE="${QA_RUN_ID}_INTEGRITY"
PRODUCT_NAME="${QA_RUN_ID}_Integrity_Product"

jq -n \
  --arg kode "$PRODUCT_CODE" \
  --arg nama "$PRODUCT_NAME" \
  '{
    kode:$kode,
    barcode:"",
    nama:$nama,
    kategori:"QA",
    hargaModal:10000,
    hargaJual:20000,
    stok:5,
    supplier:"QA Supplier",
    satuan:"pcs",
    deskripsi:"Permanent QA transaction-integrity product"
  }' > "$TMP_DIR/product-create.json"

STATUS="$(
  qa_request \
    POST \
    "/api/products" \
    "$TMP_DIR/product-created.json" \
    "$TMP_DIR/product-create.json"
)"

qa_expect_status \
  "Create integrity product" \
  "$STATUS" \
  "200"

PRODUCT_ID="$(
  jq -r '.id // empty' \
    "$TMP_DIR/product-created.json"
)"

if [ -n "$PRODUCT_ID" ]; then
  qa_pass "Integrity product ID tersedia"
else
  qa_fail "Integrity product ID tidak tersedia"
  qa_print_summary
  exit 1
fi

echo
echo "===== CLIENT PRICE AND TOTAL MANIPULATION ====="

jq -n \
  --arg productId "$PRODUCT_ID" \
  --arg productName "$PRODUCT_NAME" \
  '{
    customerId:null,
    items:[
      {
        id:$productId,
        nama:$productName,
        hargaJual:1,
        qty:1,
        subtotal:1
      }
    ],
    subtotal:1,
    disc:0,
    tax:0,
    discAmt:0,
    taxAmt:0,
    total:1,
    metodePembayaran:"Tunai",
    dibayar:1,
    kembalian:0
  }' > "$TMP_DIR/manipulated-transaction.json"

STATUS="$(
  qa_request \
    POST \
    "/api/transactions" \
    "$TMP_DIR/manipulated-transaction-response.json" \
    "$TMP_DIR/manipulated-transaction.json"
)"

if [ "$STATUS" = "400" ] || [ "$STATUS" = "409" ]; then
  qa_pass "Manipulated product price rejected — HTTP $STATUS"
elif [ "$STATUS" = "200" ]; then
  qa_fail "Manipulated product price accepted — HTTP 200"
  echo "[DEFECT] BUG-003: Checkout trusts client hargaJual, subtotal, and total."

  RELATIONAL_DATA_CREATED=1

  MANIPULATED_TRX_ID="$(
    jq -r '.id // empty' \
      "$TMP_DIR/manipulated-transaction-response.json"
  )"

  MANIPULATED_TRX_NO="$(
    jq -r '.noTrx // empty' \
      "$TMP_DIR/manipulated-transaction-response.json"
  )"

  if jq -e \
    '.total == 1 and
     .subtotal == 1 and
     .items[0].hargaJual == 1 and
     .items[0].subtotal == 1' \
    "$TMP_DIR/manipulated-transaction-response.json" \
    >/dev/null 2>&1
  then
    qa_fail "Manipulated monetary values persisted in transaction"
  else
    qa_pass "Server recalculated manipulated monetary values"
  fi
else
  qa_fail "Manipulated price returned unexpected HTTP $STATUS"
fi

STATUS="$(
  qa_request \
    GET \
    "/api/products/$PRODUCT_ID" \
    "$TMP_DIR/product-after-manipulation.json"
)"

qa_expect_status \
  "Get product after manipulated checkout" \
  "$STATUS" \
  "200"

if [ "$RELATIONAL_DATA_CREATED" -eq 1 ]; then
  qa_expect_jq \
    "Manipulated checkout still decremented stock" \
    "$TMP_DIR/product-after-manipulation.json" \
    '.stok == 4'
else
  qa_expect_jq \
    "Rejected manipulation preserves stock" \
    "$TMP_DIR/product-after-manipulation.json" \
    '.stok == 5'
fi

echo
echo "===== UNKNOWN CUSTOMER ID ====="

UNKNOWN_CUSTOMER_ID="$(
  uuidgen |
  tr '[:upper:]' '[:lower:]'
)"

jq -n \
  --arg customerId "$UNKNOWN_CUSTOMER_ID" \
  --arg productId "$PRODUCT_ID" \
  --arg productName "$PRODUCT_NAME" \
  '{
    customerId:$customerId,
    items:[
      {
        id:$productId,
        nama:$productName,
        hargaJual:20000,
        qty:1,
        subtotal:20000
      }
    ],
    subtotal:20000,
    disc:0,
    tax:0,
    discAmt:0,
    taxAmt:0,
    total:20000,
    metodePembayaran:"QRIS",
    dibayar:20000,
    kembalian:0
  }' > "$TMP_DIR/unknown-customer-transaction.json"

STATUS="$(
  qa_request \
    POST \
    "/api/transactions" \
    "$TMP_DIR/unknown-customer-response.json" \
    "$TMP_DIR/unknown-customer-transaction.json"
)"

if [ "$STATUS" = "400" ] || [ "$STATUS" = "404" ]; then
  qa_pass "Unknown customer ID safely rejected — HTTP $STATUS"
elif [ "$STATUS" = "200" ]; then
  qa_fail "Unknown customer ID accepted — HTTP 200"
  echo "[DEFECT] BUG-004: Unknown customerId silently becomes a no-customer transaction."

  RELATIONAL_DATA_CREATED=1

  UNKNOWN_CUSTOMER_TRX_ID="$(
    jq -r '.id // empty' \
      "$TMP_DIR/unknown-customer-response.json"
  )"

  UNKNOWN_CUSTOMER_TRX_NO="$(
    jq -r '.noTrx // empty' \
      "$TMP_DIR/unknown-customer-response.json"
  )"

  if jq -e \
    '.customerId == null and .customerNama == ""' \
    "$TMP_DIR/unknown-customer-response.json" \
    >/dev/null 2>&1
  then
    qa_fail "Unknown customer reference was silently discarded"
  else
    qa_pass "Unknown customer reference was not silently discarded"
  fi
else
  qa_fail "Unknown customer ID returned unexpected HTTP $STATUS"
fi

echo
echo "===== FINAL STOCK AND RELATION PROTECTION ====="

STATUS="$(
  qa_request \
    GET \
    "/api/products/$PRODUCT_ID" \
    "$TMP_DIR/product-final.json"
)"

qa_expect_status \
  "Get integrity product final state" \
  "$STATUS" \
  "200"

FINAL_STOCK="$(
  jq -r '.stok // "unknown"' \
    "$TMP_DIR/product-final.json"
)"

STATUS="$(
  qa_request \
    DELETE \
    "/api/products/$PRODUCT_ID" \
    "$TMP_DIR/product-delete.json"
)"

if [ "$RELATIONAL_DATA_CREATED" -eq 1 ]; then
  qa_expect_status \
    "Delete integrity product with transaction blocked" \
    "$STATUS" \
    "409"
else
  qa_expect_status \
    "Delete unused integrity product" \
    "$STATUS" \
    "200"

  if [ "$STATUS" = "200" ]; then
    PRODUCT_ID=""
  fi
fi

cat > "$RESULT_DIR/transaction-integrity-result.env" <<RESULT
QA_RUN_ID=$QA_RUN_ID
PRODUCT_ID=$PRODUCT_ID
PRODUCT_CODE=$PRODUCT_CODE
FINAL_STOCK=$FINAL_STOCK
MANIPULATED_TRANSACTION_ID=$MANIPULATED_TRX_ID
MANIPULATED_TRANSACTION_NO=$MANIPULATED_TRX_NO
UNKNOWN_CUSTOMER_TRANSACTION_ID=$UNKNOWN_CUSTOMER_TRX_ID
UNKNOWN_CUSTOMER_TRANSACTION_NO=$UNKNOWN_CUSTOMER_TRX_NO
RELATIONAL_DATA_CREATED=$RELATIONAL_DATA_CREATED
RESULT

echo
echo "===== INTEGRITY TEST DATA ====="
echo "Product code              : $PRODUCT_CODE"
echo "Product ID                : $PRODUCT_ID"
echo "Final stock               : $FINAL_STOCK"
echo "Manipulated transaction   : ${MANIPULATED_TRX_NO:-not created}"
echo "Unknown customer transaction: ${UNKNOWN_CUSTOMER_TRX_NO:-not created}"

echo
echo "===== GIT STATUS ====="

git -C "$BACKEND" status --short --branch
git -C "$HOME/neverfade-pos-frontend" status --short --branch

qa_print_summary

if [ "$QA_FAILED" -gt 0 ]; then
  exit 1
fi
