#!/usr/bin/env bash

set +u

source "$HOME/neverfade-pos-backend/qa/lib.sh"

TMP_DIR="$(mktemp -d)"
RESULT_DIR="$HOME/neverfade-pos-qa"

CRUD_PRODUCT_ID=""
HISTORY_PRODUCT_ID=""
HISTORY_PRODUCT_CODE=""
HISTORY_LOCKED=0

cleanup_test_data() {
  if [ -z "$QA_TOKEN" ]; then
    return
  fi

  if [ -n "$CRUD_PRODUCT_ID" ]; then
    qa_request \
      DELETE \
      "/api/products/$CRUD_PRODUCT_ID" \
      "$TMP_DIR/cleanup-crud.json" \
      >/dev/null 2>&1 || true
  fi

  if [ -n "$HISTORY_PRODUCT_ID" ] &&
     [ "$HISTORY_LOCKED" -eq 0 ]
  then
    qa_request \
      DELETE \
      "/api/products/$HISTORY_PRODUCT_ID" \
      "$TMP_DIR/cleanup-history-product.json" \
      >/dev/null 2>&1 || true
  fi
}

finish() {
  qa_restore_terminal
  cleanup_test_data
  qa_stop_backend
  rm -rf "$TMP_DIR"
}

trap finish EXIT INT TERM

mkdir -p "$RESULT_DIR"

echo "=================================================="
echo "NEVERFADE POS — PRODUCT & STOCK REGRESSION"
echo "Run ID: $QA_RUN_ID"
echo "=================================================="

echo
echo "===== STARTUP & LOGIN ====="

qa_start_backend || exit 1
qa_login_owner "$TMP_DIR/login.json" || exit 1

echo
echo "===== PRODUCT CRUD ====="

CRUD_CODE="${QA_RUN_ID}_CRUD"
CRUD_NAME="${QA_RUN_ID}_Product"

jq -n \
  --arg kode "$CRUD_CODE" \
  --arg nama "$CRUD_NAME" \
  '{
    kode:$kode,
    barcode:"",
    nama:$nama,
    kategori:"QA",
    hargaModal:5000,
    hargaJual:10000,
    stok:5,
    supplier:"QA Supplier",
    satuan:"pcs",
    deskripsi:"Temporary QA CRUD product"
  }' > "$TMP_DIR/product-create.json"

STATUS="$(
  qa_request \
    POST \
    "/api/products" \
    "$TMP_DIR/product-created.json" \
    "$TMP_DIR/product-create.json"
)"

qa_expect_status \
  "Create product" \
  "$STATUS" \
  "200"

CRUD_PRODUCT_ID="$(
  jq -r '.id // empty' \
    "$TMP_DIR/product-created.json"
)"

if [ -n "$CRUD_PRODUCT_ID" ]; then
  qa_pass "Created product ID tersedia"
else
  qa_fail "Created product ID tidak tersedia"
fi

if jq -e \
  --arg kode "$CRUD_CODE" \
  --arg nama "$CRUD_NAME" \
  '.kode == $kode and
   .nama == $nama and
   .stok == 5 and
   .hargaJual == 10000' \
  "$TMP_DIR/product-created.json" \
  >/dev/null 2>&1
then
  qa_pass "Created product values benar"
else
  qa_fail "Created product values tidak benar"
fi

STATUS="$(
  qa_request \
    GET \
    "/api/products/$CRUD_PRODUCT_ID" \
    "$TMP_DIR/product-get.json"
)"

qa_expect_status \
  "Get product by ID" \
  "$STATUS" \
  "200"

STATUS="$(
  qa_request \
    GET \
    "/api/products?search=$CRUD_CODE" \
    "$TMP_DIR/product-search.json"
)"

qa_expect_status \
  "Search product by unique code" \
  "$STATUS" \
  "200"

if jq -e \
  --arg id "$CRUD_PRODUCT_ID" \
  'any(.[]; .id == $id)' \
  "$TMP_DIR/product-search.json" \
  >/dev/null 2>&1
then
  qa_pass "Search result contains created product"
else
  qa_fail "Search result does not contain created product"
fi

STATUS="$(
  qa_request \
    POST \
    "/api/products" \
    "$TMP_DIR/product-duplicate.json" \
    "$TMP_DIR/product-create.json"
)"

qa_expect_status \
  "Duplicate product code rejected" \
  "$STATUS" \
  "400"

jq -n \
  --arg kode "${QA_RUN_ID}_NEGATIVE" \
  '{
    kode:$kode,
    barcode:"",
    nama:"QA Negative Stock",
    kategori:"QA",
    hargaModal:1000,
    hargaJual:2000,
    stok:-1,
    supplier:"",
    satuan:"pcs",
    deskripsi:""
  }' > "$TMP_DIR/product-invalid.json"

STATUS="$(
  qa_request \
    POST \
    "/api/products" \
    "$TMP_DIR/product-invalid-response.json" \
    "$TMP_DIR/product-invalid.json"
)"

qa_expect_status \
  "Negative initial stock rejected" \
  "$STATUS" \
  "400"

jq -n \
  --arg kode "$CRUD_CODE" \
  --arg nama "${CRUD_NAME}_Updated" \
  '{
    kode:$kode,
    barcode:"QA-BARCODE",
    nama:$nama,
    kategori:"QA Updated",
    hargaModal:6000,
    hargaJual:12000,
    stok:8,
    supplier:"QA Supplier Updated",
    satuan:"box",
    deskripsi:"Updated by QA regression"
  }' > "$TMP_DIR/product-update.json"

STATUS="$(
  qa_request \
    PUT \
    "/api/products/$CRUD_PRODUCT_ID" \
    "$TMP_DIR/product-updated.json" \
    "$TMP_DIR/product-update.json"
)"

qa_expect_status \
  "Update product" \
  "$STATUS" \
  "200"

if jq -e \
  '.stok == 8 and
   .hargaJual == 12000 and
   .kategori == "QA Updated"' \
  "$TMP_DIR/product-updated.json" \
  >/dev/null 2>&1
then
  qa_pass "Updated product values persisted"
else
  qa_fail "Updated product values not persisted"
fi

STATUS="$(
  qa_request \
    DELETE \
    "/api/products/$CRUD_PRODUCT_ID" \
    "$TMP_DIR/product-delete.json"
)"

qa_expect_status \
  "Delete product without relations" \
  "$STATUS" \
  "200"

if [ "$STATUS" = "200" ]; then
  CRUD_PRODUCT_ID=""
fi

STATUS="$(
  qa_request \
    GET \
    "/api/products/$(
      jq -r '.id' "$TMP_DIR/product-created.json"
    )" \
    "$TMP_DIR/product-after-delete.json"
)"

qa_expect_status \
  "Deleted product no longer available" \
  "$STATUS" \
  "404"

echo
echo "===== STOCK HISTORY ====="

HISTORY_PRODUCT_CODE="${QA_RUN_ID}_STOCK"

jq -n \
  --arg kode "$HISTORY_PRODUCT_CODE" \
  --arg nama "${QA_RUN_ID}_Stock_Product" \
  '{
    kode:$kode,
    barcode:"",
    nama:$nama,
    kategori:"QA",
    hargaModal:5000,
    hargaJual:10000,
    stok:10,
    supplier:"QA Supplier",
    satuan:"pcs",
    deskripsi:"Permanent QA stock-history product"
  }' > "$TMP_DIR/history-product-create.json"

STATUS="$(
  qa_request \
    POST \
    "/api/products" \
    "$TMP_DIR/history-product-created.json" \
    "$TMP_DIR/history-product-create.json"
)"

qa_expect_status \
  "Create stock test product" \
  "$STATUS" \
  "200"

HISTORY_PRODUCT_ID="$(
  jq -r '.id // empty' \
    "$TMP_DIR/history-product-created.json"
)"

jq -n \
  --arg produkId "$HISTORY_PRODUCT_ID" \
  '{
    produkId:$produkId,
    tipe:"masuk",
    jumlah:5,
    stokFinal:null,
    keterangan:"QA stock masuk"
  }' > "$TMP_DIR/stock-in.json"

STATUS="$(
  qa_request \
    POST \
    "/api/stock-history" \
    "$TMP_DIR/stock-in-response.json" \
    "$TMP_DIR/stock-in.json"
)"

qa_expect_status \
  "Stock masuk" \
  "$STATUS" \
  "200"

if [ "$STATUS" = "200" ]; then
  HISTORY_LOCKED=1
fi

qa_expect_jq \
  "Stock masuk final quantity 15" \
  "$TMP_DIR/stock-in-response.json" \
  '.stokAkhir == 15 and .tipe == "masuk"'

jq -n \
  --arg produkId "$HISTORY_PRODUCT_ID" \
  '{
    produkId:$produkId,
    tipe:"keluar",
    jumlah:3,
    stokFinal:null,
    keterangan:"QA stock keluar"
  }' > "$TMP_DIR/stock-out.json"

STATUS="$(
  qa_request \
    POST \
    "/api/stock-history" \
    "$TMP_DIR/stock-out-response.json" \
    "$TMP_DIR/stock-out.json"
)"

qa_expect_status \
  "Stock keluar" \
  "$STATUS" \
  "200"

qa_expect_jq \
  "Stock keluar final quantity 12" \
  "$TMP_DIR/stock-out-response.json" \
  '.stokAkhir == 12 and .tipe == "keluar"'

jq -n \
  --arg produkId "$HISTORY_PRODUCT_ID" \
  '{
    produkId:$produkId,
    tipe:"penyesuaian",
    jumlah:0,
    stokFinal:7,
    keterangan:"QA stock adjustment"
  }' > "$TMP_DIR/stock-adjustment.json"

STATUS="$(
  qa_request \
    POST \
    "/api/stock-history" \
    "$TMP_DIR/stock-adjustment-response.json" \
    "$TMP_DIR/stock-adjustment.json"
)"

qa_expect_status \
  "Stock adjustment" \
  "$STATUS" \
  "200"

qa_expect_jq \
  "Stock adjustment final quantity 7" \
  "$TMP_DIR/stock-adjustment-response.json" \
  '.stokAkhir == 7 and .tipe == "penyesuaian"'

jq -n \
  --arg produkId "$HISTORY_PRODUCT_ID" \
  '{
    produkId:$produkId,
    tipe:"keluar",
    jumlah:9999,
    stokFinal:null,
    keterangan:"QA excessive stock-out"
  }' > "$TMP_DIR/stock-excess.json"

STATUS="$(
  qa_request \
    POST \
    "/api/stock-history" \
    "$TMP_DIR/stock-excess-response.json" \
    "$TMP_DIR/stock-excess.json"
)"

qa_expect_status \
  "Excessive stock-out rejected" \
  "$STATUS" \
  "400"

jq -n \
  --arg produkId "$HISTORY_PRODUCT_ID" \
  '{
    produkId:$produkId,
    tipe:"masuk",
    jumlah:-1,
    stokFinal:null,
    keterangan:"QA negative stock"
  }' > "$TMP_DIR/stock-negative.json"

STATUS="$(
  qa_request \
    POST \
    "/api/stock-history" \
    "$TMP_DIR/stock-negative-response.json" \
    "$TMP_DIR/stock-negative.json"
)"

qa_expect_status \
  "Negative stock quantity rejected" \
  "$STATUS" \
  "400"

STATUS="$(
  qa_request \
    GET \
    "/api/stock-history?produkId=$HISTORY_PRODUCT_ID" \
    "$TMP_DIR/stock-list.json"
)"

qa_expect_status \
  "Get product stock history" \
  "$STATUS" \
  "200"

if jq -e \
  --arg id "$HISTORY_PRODUCT_ID" \
  'type == "array" and
   length >= 3 and
   all(.[]; .produkId == $id)' \
  "$TMP_DIR/stock-list.json" \
  >/dev/null 2>&1
then
  qa_pass "Stock history filter and count valid"
else
  qa_fail "Stock history filter or count invalid"
fi

STATUS="$(
  qa_request \
    GET \
    "/api/products/$HISTORY_PRODUCT_ID" \
    "$TMP_DIR/history-product-final.json"
)"

qa_expect_status \
  "Get final stock product" \
  "$STATUS" \
  "200"

qa_expect_jq \
  "Product stock persisted as 7" \
  "$TMP_DIR/history-product-final.json" \
  '.stok == 7'

STATUS="$(
  qa_request \
    DELETE \
    "/api/products/$HISTORY_PRODUCT_ID" \
    "$TMP_DIR/history-product-delete.json"
)"

qa_expect_status \
  "Delete product with stock history blocked" \
  "$STATUS" \
  "409"

cat > "$RESULT_DIR/product-stock-result.env" <<RESULT
QA_RUN_ID=$QA_RUN_ID
HISTORY_PRODUCT_ID=$HISTORY_PRODUCT_ID
HISTORY_PRODUCT_CODE=$HISTORY_PRODUCT_CODE
FINAL_STOCK=7
CLEANUP_STATUS=PERMANENT_RELATIONAL_QA_DATA
RESULT

echo
echo "===== PERMANENT QA DATA ====="
echo "Product code : $HISTORY_PRODUCT_CODE"
echo "Product ID   : $HISTORY_PRODUCT_ID"
echo "Final stock  : 7"
echo "Reason       : Product has stock-history relations"

echo
echo "===== GIT STATUS ====="

git -C "$BACKEND" status --short --branch
git -C "$HOME/neverfade-pos-frontend" status --short --branch

qa_print_summary

if [ "$QA_FAILED" -gt 0 ]; then
  exit 1
fi
