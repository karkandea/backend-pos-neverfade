# Sprint 2 — Core sale and payment recovery

**Source baseline:** backend and frontend `origin/main` merged Sprint 1. Sprint 2 source branch `feat/s2-core-sale-payment-20260926` in isolated worktrees. The agreed sprint plan labels this S2 (19–30 October 2026), but this is an early development checkpoint, not a release-date promise.

**Release invariant:** A single purchase may produce at most one paid sale and one stock decrement. A provider payment whose finality is unknown remains recoverable without creating a second charge. Quoting alone neither sells nor reserves stock.

## Slice 1 — Immutable server-authoritative quote

- `POST /api/v2/sales/quotes` requires authenticated owner/admin/cashier, `core_pos`, authorized selected outlet and matching `outletId` body. Input: product/variant/price-level/quantity, optional customer and bounded `discountPercent`. Monetary subtotals or totals supplied by a client are ignored; discount codes are rejected until a server-side promotion policy exists.
- Resolve the active variant, price level and quantity rules using `IRetailSaleResolver`; validate combined duplicate-line stock. Server computes line/subtotal/discount/configured tax/total with two-decimal rounding. Persist immutable tenant/outlet-scoped JSON snapshot and quote version; expire after five minutes. Return a correlation ID. `GET /api/v2/sales/quotes/{quoteId}` returns snapshot + effective expired state; foreign tenant/outlet returns 404/403. Quote has `stockReserved=false`; no transaction/payment/stock history is written.
- `sale_quotes` migration `20260926110327_AddSprint2SaleQuoteSnapshots` is additive; the historical hosted-checkout migration is present in source and EF history. Data and rollback: use only isolated DB for this candidate; a production rollout requires a fresh backup and a new rehearsal. A schema rollback drops quote records; never auto-run it on production.
- Typed FE transport adapter exists in `src/lib/saleQuote.ts` but **cash/QRIS checkout remains on existing endpoints**. No live merchant is switched to the quote API until idempotent v2 commit, stock finalization and payment recovery pass their gates.

### Evidence, 26 September 2026

- Backend Release build 0 warnings/errors; full suite **183/183 PASS**, including 7 quote tests (pricing tier, server money, customer/outlet guards, combined stock, immutability, expiry and discount bounds). Frontend `npm ci`, build and lint PASS; large Vite bundle warning is advisory.
- Copied the isolated S1 QA DB to dedicated `neverfade_s2_quote_rehearsal`; idempotent migration applied successfully, EF history **22** and new `sale_quotes` initially empty. Production `neverfade_prod` was not read or mutated by this migration run.
- Separate loopback-only S2 QA service port 5133, DB `neverfade_s2_quote_gate`, `Payments__Mode=Disabled`. Latest quote binary deployed to isolated QA after preserving the prior service configuration. Direct API smoke: owner auth, create/read 200, client fake totals ignored, discount computed by server, stock unchanged, foreign tenant read 404. QA quote test record is not a paid sale.

## OPEN / NOT RELEASE-READY

1. Quote-consumption contract with mandatory request idempotency, replay/mismatch handling and no duplicate paid sale.
2. PostgreSQL concurrency/race test: two cashiers, one remaining stock; atomic reservation/finalization and no stock negative or double decrement. Cash and QRIS flows must share the same invariant.
3. Verified Xendit webhook replay/out-of-order and cancel-vs-paid state machine; same attempt after refresh, provider-status reconciliation and unknown-state UX without prompting duplicate payment.
4. FE checkout integration, real money/stock negative tests, formal UAT/security/rollback review and remote CI. GitHub Actions is still externally blocked by billing; local PASS is **not** CI sign-off.

**Status:** Sprint 2 IN PROGRESS. No Sprint 2 merge or production deployment.
