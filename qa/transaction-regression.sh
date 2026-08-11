#!/usr/bin/env bash

set +u

source "$HOME/neverfade-pos-backend/qa/lib.sh"

TMP_DIR="$(mktemp -d)"
RESULT_DIR="$HOME/neverfade-pos-qa"

PRODUCT_ID=""
CUSTOMER_ID=""
TRANSACTION_LOCKED=0

cleanup() {
  qa_restore_terminal

  if [ -n "$QA_TOKEN" ] && [ -n "$CUSTOMER_ID" ]; then
    qa_request \
      DELETE \
      "/api/customers/$CUSTOMER_ID" \
      "$TMP_DIR/cleanup-customer.json" \
      >/dev/null 2>&1 || true
  fi

  if [ -n "$QA_TOKEN" ] &&
     [ -n "$PRODUCT_ID" ] &&
     [ "$TRANSACTION_LOCKED" -eq 0 ]
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
echo "NEVERFADE POS — TRANSACTION REGRESSION"
echo "Run ID: $QA_RUN_ID"
echo "=================================================="

echo
echo "===== STARTUP & LOGIN ====="

qa_start_backend || exit 1
qa_login_owner "$TMP_DIR/login.json" || exit 1

echo
echo "===== CREATE TRANSACTION PRODUCT ====="

PRODUCT_CODE="${QA_RUN_ID}_TRX"
PRODUCT_NAME="${QA_RUN_ID}_Transaction_Product"

jq -n \
  --arg kode "$PRODUCT_CODE" \
  --arg nama "$PRODUCT_NAME" \
  '{
    kode:$kode,
    barcode:"",
    nama:$nama,
    kategori:"QA",
    hargaModal:10000,
    hargaJual:15000,
    stok:10,
    supplier:"QA Supplier",
    satuan:"pcs",
    deskripsi:"Permanent QA transaction product"
  }' > "$TMP_DIR/product-create.json"

STATUS="$(
  qa_request \
    POST \
    "/api/products" \
    "$TMP_DIR/product-created.json" \
    "$TMP_DIR/product-create.json"
)"

qa_expect_status \
  "Create transaction product" \
  "$STATUS" \
  "200"

PRODUCT_ID="$(
  jq -r '.id // empty' \
    "$TMP_DIR/product-created.json"
)"

if [ -n "$PRODUCT_ID" ]; then
  qa_pass "Transaction product ID tersedia"
else
  qa_fail "Transaction product ID tidak tersedia"
  qa_print_summary
  exit 1
fi

echo
echo "===== CREATE CUSTOMER ====="

CUSTOMER_NAME="${QA_RUN_ID}_Transaction_Customer"
CUSTOMER_EMAIL="$(printf "%s" "$QA_RUN_ID" | tr '[:upper:]' '[:lower:]')-trx@qa.local"

jq -n \
  --arg nama "$CUSTOMER_NAME" \
  --arg email "$CUSTOMER_EMAIL" \
  '{
    nama:$nama,
    hp:"081299999999",
    email:$email,
    alamat:"QA Transaction Address"
  }' > "$TMP_DIR/customer-create.json"

STATUS="$(
  qa_request \
    POST \
    "/api/customers" \
    "$TMP_DIR/customer-created.json" \
    "$TMP_DIR/customer-create.json"
)"

qa_expect_status \
  "Create transaction customer" \
  "$STATUS" \
  "200"

CUSTOMER_ID="$(
  jq -r '.id // empty' \
    "$TMP_DIR/customer-created.json"
)"

if [ -n "$CUSTOMER_ID" ]; then
  qa_pass "Transaction customer ID tersedia"
else
  qa_fail "Transaction customer ID tidak tersedia"
  qa_print_summary
  exit 1
fi

echo
echo "===== CHECKOUT WITHOUT CUSTOMER ====="

jq -n \
  --arg productId "$PRODUCT_ID" \
  --arg productName "$PRODUCT_NAME" \
  '{
    customerId:null,
    items:[
      {
        id:$productId,
        nama:$productName,
        hargaJual:15000,
        qty:2,
        subtotal:30000
      }
    ],
    subtotal:30000,
    disc:0,
    tax:0,
    discAmt:0,
    taxAmt:0,
    total:30000,
    metodePembayaran:"Tunai",
    dibayar:50000,
    kembalian:20000
  }' > "$TMP_DIR/transaction-no-customer.json"

STATUS="$(
  qa_request \
    POST \
    "/api/transactions" \
    "$TMP_DIR/transaction-no-customer-response.json" \
    "$TMP_DIR/transaction-no-customer.json"
)"

qa_expect_status \
  "Checkout without customer" \
  "$STATUS" \
  "200"

TRX_NO_CUSTOMER_ID="$(
  jq -r '.id // empty' \
    "$TMP_DIR/transaction-no-customer-response.json"
)"

TRX_NO_CUSTOMER_NO="$(
  jq -r '.noTrx // empty' \
    "$TMP_DIR/transaction-no-customer-response.json"
)"

if [ "$STATUS" = "200" ]; then
  TRANSACTION_LOCKED=1
fi

qa_expect_jq \
  "Checkout without customer response values" \
  "$TMP_DIR/transaction-no-customer-response.json" \
  '.customerId == null and
   .total == 30000 and
   .dibayar == 50000 and
   .kembalian == 20000 and
   (.items | length) == 1 and
   .items[0].qty == 2'

if [ -n "$TRX_NO_CUSTOMER_ID" ] &&
   [ -n "$TRX_NO_CUSTOMER_NO" ]
then
  qa_pass "Transaction ID and number generated"
else
  qa_fail "Transaction ID or number missing"
fi

STATUS="$(
  qa_request \
    GET \
    "/api/products/$PRODUCT_ID" \
    "$TMP_DIR/product-after-first-trx.json"
)"

qa_expect_status \
  "Get product after first checkout" \
  "$STATUS" \
  "200"

qa_expect_jq \
  "First checkout decrements stock to 8" \
  "$TMP_DIR/product-after-first-trx.json" \
  '.stok == 8'

echo
echo "===== CHECKOUT WITH CUSTOMER ====="

jq -n \
  --arg customerId "$CUSTOMER_ID" \
  --arg productId "$PRODUCT_ID" \
  --arg productName "$PRODUCT_NAME" \
  '{
    customerId:$customerId,
    items:[
      {
        id:$productId,
        nama:$productName,
        hargaJual:15000,
        qty:1,
        subtotal:15000
      }
    ],
    subtotal:15000,
    disc:0,
    tax:0,
    discAmt:0,
    taxAmt:0,
    total:15000,
    metodePembayaran:"Tunai",
    dibayar:15000,
    kembalian:0
  }' > "$TMP_DIR/transaction-customer.json"

STATUS="$(
  qa_request \
    POST \
    "/api/transactions" \
    "$TMP_DIR/transaction-customer-response.json" \
    "$TMP_DIR/transaction-customer.json"
)"

qa_expect_status \
  "Checkout with customer" \
  "$STATUS" \
  "200"

TRX_CUSTOMER_ID="$(
  jq -r '.id // empty' \
    "$TMP_DIR/transaction-customer-response.json"
)"

TRX_CUSTOMER_NO="$(
  jq -r '.noTrx // empty' \
    "$TMP_DIR/transaction-customer-response.json"
)"

if jq -e \
  --arg customerId "$CUSTOMER_ID" \
  --arg customerName "$CUSTOMER_NAME" \
  '.customerId == $customerId and
   .customerNama == $customerName and
   .total == 15000 and
   .metodePembayaran == "Tunai"' \
  "$TMP_DIR/transaction-customer-response.json" \
  >/dev/null 2>&1
then
  qa_pass "Checkout customer values benar"
else
  qa_fail "Checkout customer values tidak benar"
fi

STATUS="$(
  qa_request \
    GET \
    "/api/products/$PRODUCT_ID" \
    "$TMP_DIR/product-after-second-trx.json"
)"

qa_expect_status \
  "Get product after second checkout" \
  "$STATUS" \
  "200"

qa_expect_jq \
  "Second checkout decrements stock to 7" \
  "$TMP_DIR/product-after-second-trx.json" \
  '.stok == 7'

STATUS="$(
  qa_request \
    GET \
    "/api/customers/$CUSTOMER_ID" \
    "$TMP_DIR/customer-after-trx.json"
)"

qa_expect_status \
  "Get customer after checkout" \
  "$STATUS" \
  "200"

qa_expect_jq \
  "Customer transaction count updated" \
  "$TMP_DIR/customer-after-trx.json" \
  '.totalTransaksi >= 1'

echo
echo "===== TRANSACTION READ & SEARCH ====="

STATUS="$(
  qa_request \
    GET \
    "/api/transactions/$TRX_CUSTOMER_ID" \
    "$TMP_DIR/transaction-get.json"
)"

qa_expect_status \
  "Get transaction by ID" \
  "$STATUS" \
  "200"

STATUS="$(
  qa_request \
    GET \
    "/api/transactions?search=$TRX_CUSTOMER_NO" \
    "$TMP_DIR/transaction-search.json"
)"

qa_expect_status \
  "Search transaction by number" \
  "$STATUS" \
  "200"

if jq -e \
  --arg id "$TRX_CUSTOMER_ID" \
  'any(.[]; .id == $id)' \
  "$TMP_DIR/transaction-search.json" \
  >/dev/null 2>&1
then
  qa_pass "Search result contains created transaction"
else
  qa_fail "Search result does not contain created transaction"
fi

echo
echo "===== TRANSACTION VALIDATION ====="

jq -n \
  '{
    customerId:null,
    items:[],
    subtotal:0,
    disc:0,
    tax:0,
    discAmt:0,
    taxAmt:0,
    total:0,
    metodePembayaran:"Tunai",
    dibayar:0,
    kembalian:0
  }' > "$TMP_DIR/transaction-empty-items.json"

STATUS="$(
  qa_request \
    POST \
    "/api/transactions" \
    "$TMP_DIR/transaction-empty-items-response.json" \
    "$TMP_DIR/transaction-empty-items.json"
)"

qa_expect_status \
  "Empty transaction items rejected" \
  "$STATUS" \
  "400"

jq -n \
  --arg productId "$PRODUCT_ID" \
  --arg productName "$PRODUCT_NAME" \
  '{
    customerId:null,
    items:[
      {
        id:$productId,
        nama:$productName,
        hargaJual:15000,
        qty:0,
        subtotal:0
      }
    ],
    subtotal:0,
    disc:0,
    tax:0,
    discAmt:0,
    taxAmt:0,
    total:0,
    metodePembayaran:"Tunai",
    dibayar:0,
    kembalian:0
  }' > "$TMP_DIR/transaction-zero-qty.json"

STATUS="$(
  qa_request \
    POST \
    "/api/transactions" \
    "$TMP_DIR/transaction-zero-qty-response.json" \
    "$TMP_DIR/transaction-zero-qty.json"
)"

qa_expect_status \
  "Zero item quantity rejected" \
  "$STATUS" \
  "400"

jq -n \
  --arg productId "$PRODUCT_ID" \
  --arg productName "$PRODUCT_NAME" \
  '{
    customerId:null,
    items:[
      {
        id:$productId,
        nama:$productName,
        hargaJual:15000,
        qty:999,
        subtotal:14985000
      }
    ],
    subtotal:14985000,
    disc:0,
    tax:0,
    discAmt:0,
    taxAmt:0,
    total:14985000,
    metodePembayaran:"Tunai",
    dibayar:14985000,
    kembalian:0
  }' > "$TMP_DIR/transaction-insufficient-stock.json"

STATUS="$(
  qa_request \
    POST \
    "/api/transactions" \
    "$TMP_DIR/transaction-insufficient-stock-response.json" \
    "$TMP_DIR/transaction-insufficient-stock.json"
)"

qa_expect_status \
  "Insufficient stock rejected" \
  "$STATUS" \
  "400"

UNKNOWN_PRODUCT_ID="$(
  uuidgen |
  tr '[:upper:]' '[:lower:]'
)"

jq -n \
  --arg productId "$UNKNOWN_PRODUCT_ID" \
  '{
    customerId:null,
    items:[
      {
        id:$productId,
        nama:"Unknown QA Product",
        hargaJual:1000,
        qty:1,
        subtotal:1000
      }
    ],
    subtotal:1000,
    disc:0,
    tax:0,
    discAmt:0,
    taxAmt:0,
    total:1000,
    metodePembayaran:"Tunai",
    dibayar:1000,
    kembalian:0
  }' > "$TMP_DIR/transaction-unknown-product.json"

STATUS="$(
  qa_request \
    POST \
    "/api/transactions" \
    "$TMP_DIR/transaction-unknown-product-response.json" \
    "$TMP_DIR/transaction-unknown-product.json"
)"

qa_expect_status \
  "Unknown product rejected" \
  "$STATUS" \
  "404"

echo
echo "===== CUSTOMER DELETE SET-NULL ====="

STATUS="$(
  qa_request \
    DELETE \
    "/api/customers/$CUSTOMER_ID" \
    "$TMP_DIR/customer-delete.json"
)"

qa_expect_status \
  "Delete customer linked to transaction" \
  "$STATUS" \
  "200"

if [ "$STATUS" = "200" ]; then
  CUSTOMER_ID=""
fi

STATUS="$(
  qa_request \
    GET \
    "/api/transactions/$TRX_CUSTOMER_ID" \
    "$TMP_DIR/transaction-after-customer-delete.json"
)"

qa_expect_status \
  "Transaction remains after customer deletion" \
  "$STATUS" \
  "200"

qa_expect_jq \
  "Deleted customer relation becomes null" \
  "$TMP_DIR/transaction-after-customer-delete.json" \
  '.customerId == null'

echo
echo "===== PRODUCT RELATION PROTECTION ====="

STATUS="$(
  qa_request \
    DELETE \
    "/api/products/$PRODUCT_ID" \
    "$TMP_DIR/product-delete.json"
)"

qa_expect_status \
  "Delete product linked to transaction blocked" \
  "$STATUS" \
  "409"

cat > "$RESULT_DIR/transaction-result.env" <<RESULT
QA_RUN_ID=$QA_RUN_ID
PRODUCT_ID=$PRODUCT_ID
PRODUCT_CODE=$PRODUCT_CODE
FINAL_STOCK=7
TRANSACTION_WITHOUT_CUSTOMER_ID=$TRX_NO_CUSTOMER_ID
TRANSACTION_WITHOUT_CUSTOMER_NO=$TRX_NO_CUSTOMER_NO
TRANSACTION_WITH_CUSTOMER_ID=$TRX_CUSTOMER_ID
TRANSACTION_WITH_CUSTOMER_NO=$TRX_CUSTOMER_NO
CUSTOMER_CLEANUP=DELETED_SET_NULL
PRODUCT_CLEANUP=PERMANENT_RELATIONAL_QA_DATA
RESULT

echo
echo "===== PERMANENT QA DATA ====="
echo "Product code : $PRODUCT_CODE"
echo "Product ID   : $PRODUCT_ID"
echo "Final stock  : 7"
echo "Transaction 1: $TRX_NO_CUSTOMER_NO"
echo "Transaction 2: $TRX_CUSTOMER_NO"
echo "Customer     : deleted; relation set to null"

echo
echo "===== GIT STATUS ====="

git -C "$BACKEND" status --short --branch
git -C "$HOME/neverfade-pos-frontend" status --short --branch

qa_print_summary

if [ "$QA_FAILED" -gt 0 ]; then
  exit 1
fi
