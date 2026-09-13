# NeverFade POS — Phase 3E Advanced Retail

> Approved product direction for Fashion/Apparel retail. This supplements `PHASE_3_BUSINESS_MODES_SPEC.md`. `CONTRACT.md` remains frozen and must not be edited.

## 1. Outcome

Phase 3E extends the existing retail core without forking checkout or creating a separate frontend application.

Phase 3E1 delivers:
- new business type `fashion_retail`;
- reusable `product_variants` capability;
- reusable `multi_pricing` capability;
- product variants with SKU, barcode, option values, stock, and optional base-price override;
- tenant-defined price levels such as Satuan, Grosir, and Reseller;
- automatic quantity-break pricing;
- manual selection of a configured price level at checkout;
- immutable transaction snapshots for variant and applied pricing.

Phase 3E2 will add `returns_exchanges` and stock reversal/audit. It is intentionally separate from Phase 3E1.
## 2. Business mode and capabilities

`fashion_retail` label: **Fashion / Apparel**.

Preset:
- `core_pos`
- `inventory`
- `customers`
- `reports`
- `attendance`
- `finance_withdrawal`
- `product_variants`
- `multi_pricing`

`general_retail` stays unchanged. Existing tenants remain `general_retail` unless explicitly changed by Platform Super Admin.

Variant/pricing endpoints must return `403 CAPABILITY_NOT_ENABLED` when the corresponding capability is disabled. Capability hiding in the frontend is not authorization.

## 3. Variant model

A product remains the parent catalog item. A variant belongs to exactly one product and tenant.

Variant fields:
- `id`, `productId`;
- `sku`, `barcode`;
- `label`;
- up to three reusable option name/value pairs;
- optional `hargaModal` and `hargaJual` overrides;
- integer `stok`;
- `active`.
For Fashion MVP the UI uses option 1 = Size and option 2 = Color, but the backend fields stay generic.

Inventory rules:
- goods variants use integer stock only;
- variant checkout decrements both variant stock and parent aggregate stock exactly once;
- parent `product.stok` is the aggregate variant stock while variants exist;
- direct parent-level stock adjustment is rejected while variants exist;
- changing/deleting a variant must not create negative stock or detach historical transaction snapshots;
- disabling a variant preserves history and prevents new checkout use.

## 4. Multi-pricing model

A tenant can define reusable price levels, e.g. `Grosir` or `Reseller`.

Price-level fields:
- `id`, `code`, `name`, `sortOrder`, `active`.

A product price rule binds:
- `productId`;
- optional `variantId` (null = product-level fallback);
- `priceLevelId`;
- `minQuantity`;
- `unitPrice`.

Base/default selling price remains `product.hargaJual`, overridden by `variant.hargaJual` when present.
Pricing rules:
- automatic mode chooses the configured eligible rule with the highest `minQuantity <= quantity`;
- if both product-level and variant-specific rules exist for the same level, the variant-specific rule wins;
- manual price-level selection may use a configured level below its automatic minimum quantity;
- arbitrary custom unit-price override is out of scope for 3E1;
- server resolves and validates the final unit price; client totals are not authoritative.

Examples:
- base Satuan: Rp100.000;
- Grosir min 6: Rp85.000;
- Grosir Besar min 24: Rp75.000.

Quantity 1 auto -> Rp100.000. Quantity 6 auto -> Rp85.000. Quantity 24 auto -> Rp75.000. Cashier may manually choose Grosir for quantity 3 only when that level is configured for the item/variant.

## 5. Checkout and historical integrity

`CreateTransactionItemDto` adds optional `variantId` and `priceLevelId`.

Transaction item snapshot adds:
- `variantId`, `variantSku`, `variantLabel`;
- `basePrice`;
- `priceLevelId`, `priceLevelName`;
- existing `hargaJual` remains the actual unit price used;
- existing `subtotal` remains actual unit price x quantity.

Changing catalog prices later must never rewrite historical transaction values.
## 6. API direction

Capability-gated retail endpoints:
- `GET/POST/PUT/DELETE /api/retail/variants`
- `GET/POST/PUT/DELETE /api/retail/price-levels`
- `GET/POST/PUT/DELETE /api/retail/prices`
- `GET /api/retail/catalog`

The existing `/api/products` and `/api/transactions` contracts remain backward-compatible. New JSON fields are additive.

## 7. Frontend UX

Fashion tenants receive a new navigation item **Varian & Harga**.

Management flow:
1. create the parent product using the existing Produk page;
2. open Varian & Harga;
3. add size/color variants with SKU/barcode/stock;
4. create tenant price levels;
5. attach price rules to the parent or a specific variant.

Cashier flow:
- variant products require a variant selection before adding to cart;
- barcode/SKU identifies the exact variant;
- quantity changes recalculate automatic price tiers;
- cart shows applied price level and variant label;
- cashier can switch between Automatic and configured manual price levels;
- unavailable/insufficient-stock variants cannot be sold.
## 8. Acceptance criteria

- `fashion_retail` receives deterministic `product_variants` + `multi_pricing` capabilities.
- Non-capable tenants receive `403 CAPABILITY_NOT_ENABLED` on retail-management endpoints.
- Tenant A cannot read or mutate Tenant B variants, levels, or prices.
- Parent product without variants behaves exactly like Phase 3D.
- Variant product checkout requires a valid active variant.
- Variant stock and parent aggregate stock decrement exactly once.
- Fractional quantities remain rejected for goods.
- Automatic quantity tiers resolve deterministically.
- Manual configured tier selection works even below its automatic threshold.
- Client cannot submit an arbitrary lower price.
- Transaction snapshot preserves variant and applied price after catalog edits.
- Existing cash, QRIS recovery, Laundry, Restaurant, reports, and finance regressions stay green.

## 9. Release gates

Phase 3E1 is not merge-ready until:
1. unit tests cover business preset, price resolution, variant stock rules;
2. API integration covers capability, tenant isolation, invalid variant, insufficient stock, manual/auto price, idempotent payment finalization;
3. migration is tested from the current Phase 3D schema, including rollback/reapply;
4. backend full tests and existing regression scripts pass;
5. frontend build/lint pass;
6. browser tests cover Fashion happy path and error/retry on tablet;
7. existing General Retail, F&B, Laundry checkout regressions pass.

Production deployment is a separate decision after merge. Supabase is not required for development or QA.
