# Phase 3E2 — Returns & Exchanges

> Additive slice after merged Phase 3E1. `CONTRACT.md` remains frozen.

## 1. Outcome

Phase 3E2 adds reusable `returns_exchanges` capability for Fashion Retail.
It must preserve the original sale as immutable history.
A return/exchange is a separate auditable record referencing original transaction items.

MVP delivers:
- partial/full merchandise return;
- same-product variant exchange (size/color);
- stock reversal for returned goods;
- stock decrement for replacement variant;
- immutable historical refund/exchange snapshot;
- prevention of over-return and duplicate submission;
- owner/admin management UI.

## 2. Deliberate boundaries

- No Xendit/provider refund API in 3E2.
- Refund amount is recorded for audit; actual payout is handled manually outside NeverFade.
- Exchange is quantity-for-quantity within the same parent product.
- Exchange has no price difference. Upgrade/downgrade uses Return + a new sale.
- Original Transaction and TransactionItem values are never rewritten.

## 3. Capability and authorization

Add `returns_exchanges` to the `fashion_retail` preset.
Other business presets remain unchanged.
Endpoints require `returns_exchanges`; frontend hiding is not authorization.

Mutation is owner/admin only for MVP.
Kasir does not get return/exchange mutation permission in 3E2.

## 4. Return record

`RetailReturn` fields:
- `id`, `tenantId`, `returnNumber`;
- `idempotencyKey` unique per tenant;
- `transactionId`, `transactionNumber` snapshot;
- `type`: `return` or `exchange`;
- `reason`, `notes`;
- `refundAmount` informational/manual-settlement amount;
- `createdByUserId`, `createdByName`, `createdAt`.

`RetailReturnItem` references one original `TransactionItem` and snapshots returned item/variant data.

Return item fields include:
- original transaction item ID;
- product ID/name;
- original variant ID/SKU/label snapshot;
- returned quantity and original actual unit price;
- `restock` flag;
- optional replacement variant ID/SKU/label for exchange.

## 5. Validation rules

- Original transaction must belong to the current tenant and be `paid`.
- Only goods can be returned/exchanged in this slice.
- Returned quantity must be positive whole stock units.
- Cumulative completed return/exchange quantity may never exceed original sold quantity.
- Exchange requires an active replacement variant of the same parent product.
- Replacement variant must differ from the original variant and have enough stock.
- Exchange quantity equals returned quantity.
- If original variant no longer exists, `restock=true` is rejected; `restock=false` remains allowed.
- Duplicate `idempotencyKey` returns the already-created record and never mutates stock twice.

## 6. Stock movements

For `restock=true`, returned tracked goods increment original variant stock (when applicable) and parent aggregate stock exactly once.

For exchange, replacement tracked goods decrement replacement variant stock and parent aggregate stock exactly once.

Each mutation writes StockHistory with explicit return/exchange type and return number.
No historical sale stock movement is edited or deleted.

## 7. Refund amount

For `return`, NeverFade records a proportional historical transaction value, not a current catalog price.
The refund basis is the original line subtotal proportion against the transaction subtotal, multiplied by the original transaction total, then prorated by returned quantity.
This proportionally carries original transaction-level discount/tax into the audit amount.
Cumulative refund amount is capped by the original transaction total.

For `exchange`, `refundAmount = 0`.
Provider/cash payout execution is outside 3E2.

## 8. API direction

- `GET /api/retail/returns?transactionId=...`
- `GET /api/retail/returns/{id}`
- `POST /api/retail/returns`

POST accepts `idempotencyKey`, transaction ID, type, reason/notes, and item lines.

## 9. Frontend UX

Fashion Retail receives navigation item **Retur & Tukar** for owner/admin.
User searches/selects an original paid transaction, chooses eligible item quantity, reason, and either:
- Return: choose whether item is restocked;
- Exchange: choose replacement size/color variant.

UI must clearly state that recorded refund does not execute a provider/bank refund.
Completed records are read-only audit history.

## 10. Acceptance criteria

- Fashion preset includes `returns_exchanges`; unrelated presets do not change.
- Non-capable tenant receives 403.
- Tenant isolation holds for transaction, return, and replacement variant.
- Duplicate submission cannot mutate stock twice.
- Partial return and cumulative remaining quantity are correct.
- Return restock increments original variant + parent exactly once.
- Exchange restocks original and decrements replacement exactly once.
- `restock=false` leaves returned stock unchanged.
- Original transaction snapshots remain unchanged.
- Existing General Retail, Fashion checkout, F&B, Laundry, QRIS, reports, and finance regressions stay green.

## 11. Release gates

Backend unit/API tests, fresh PostgreSQL migration forward/rollback/reapply/model-drift, full backend regression, frontend lint/build, targeted Desktop/Tablet/Mobile browser flows, and full existing Playwright regression must pass before merge-ready.
Production deployment remains a separate decision.
