# Sprint 2 — Kasir cash/QRIS UAT checklist

Status: **manual UAT pending**. Source and mocked-browser PASS are not financial sign-off. Do not use actual customer's money or live provider credentials for these scenarios. Keep testing inside the expressly designated testing/sandbox tenant, with a unique test outlet, test products and a recorded before/after stock/ledger snapshot.

## Setup / prerequisites

- Verify running API build, frontend build, 24 EF migration records (including `20260926140901_AddSprint2CashPreparedAttempt`) **on the intended testing database**. If the active system still reports 21 migrations or S1 image, stop: Sprint 2 is **not deployed there**. Do not attempt manual migration as part of cash/QRIS checkout.
- For v2 cash use an intentionally rebuilt QA frontend with `VITE_S2_CASH_CHECKOUT=enabled`; it applies **only** to `general_retail` and `fashion_retail` cash. Flag-off cash, restaurant, laundry and all QRIS still use legacy routes. Record the actual flag/build, not the source branch alone.
- Use two distinct test cashier accounts with the correct outlet assignments, owner/admin, two test outlets A/B, one product with 10 units plus a second product with **exactly one** unit. Record each initial stock count, transaction count, stock-history count and payment/ledger count in the same tenant/outlet.
- QRIS provider testing must be **sandbox**; confirm the UI explicitly says Sandbox and the configured provider credentials are sandbox before scanning. If provider is Disabled, verify disabled UX only and record QRIS financial UAT as BLOCKED (not PASS). A real payment must only be attempted through a separately approved live-money test.

## Retail cash — same key, same transaction

| ID | Procedure | Expected evidence |
|---|---|---|
| C01 | Cashier A selects one retail item and exact cash amount; server quote agrees with displayed amount/tax. | One quote, one original prepared key, one paid transaction/receipt. Stock and stock history decrement **once**. |
| C02 | Click **Proses Transaksi** twice rapidly while server preflight is delayed. | One quote/prepare/commit, one transaction number and one stock movement. |
| C03 | Simulate lost/timeout `prepare` response, close tab and login as **same cashier A** from another browser, same outlet. | `/cash/current` returns original quote/version/key/amount; retry uses identical key, no fresh quote/charge. |
| C04 | Simulate commit accepted but HTTP reply lost, reopen page. | Read-only key lookup restores committed receipt, no second POST or stock decrement. |
| C05 | Make cash quote expire before prepare, or change price/available stock before commit. | Server explicitly rejects; no paid sale or stock change. Only verified lock-serialized `abandon` may release local pending attempt. |
| C06 | Prepare a pending attempt, attempt different quote/key or QRIS in that cashier/outlet. | New checkout blocked until original attempt is resolved. A direct v2 cash API attempt with a second quote/key also conflicts. |
| C07 | Change to cashier B on same browser and tenant; switch to outlet B. | Cashier B never receives A's pending key; wrong outlet cannot read A's original prepared attempt. |
| C08 | Cashier A abandons a **confirmed no-sale** attempt and a delayed original commit arrives. | `abandoned` remains terminal; delayed commit rejected, zero paid sale. If commit happened first, abandon conflicts and existing receipt remains. |
| C09 | Two cashiers attempt the single remaining stock concurrently, and a repeated same-key POST is sent simultaneously. | At most one successful paid sale for last unit, no negative stock; same-key retries return one original receipt. Requires **real PostgreSQL** test, not browser mocks. |

## QRIS — provider state is authoritative

| ID | Procedure | Expected evidence |
|---|---|---|
| Q01 | Open QRIS where provider is Disabled/Sandbox. | Disabled hides QRIS; Sandbox displays unmistakable test warning. No accidental live charge. |
| Q02 | Create one sandbox QRIS and refresh cashier page. | Same payment ID/reference reappears, no second provider payment request. |
| Q03 | Provider accepts create but HTTP response is lost. | Cashier sees original `creating` reference and cannot start a new QRIS; verified provider callback resolves original attempt. |
| Q04 | Send duplicate `payment.capture`, then a late failure/expired event. | Payment/transaction stay paid; ledger and stock finalized only once. |
| Q05 | Let UI QR expiry pass without provider finality. | UI displays uncertainty, **not** failed/cancelled by timer alone. |
| Q06 | Cancel from UI while provider may be capturing; verify status with provider. | A confirmed paid request must not be cancelled or downgraded; a confirmed cancellation must not be treated as paid. |
| Q07 | Owner/admin opens **Status Pembayaran** for the selected outlet; cashier tries same route. | Owner sees unresolved references/reasons but no charge/cancel action; cashier denied, outlet B cannot view outlet A. |
| Q08 | Cash and QRIS simultaneously compete for single last unit. | At most one paid sale/one stock decrement; inspect actual DB transaction/stock/ledger records. **Real PostgreSQL plus sandbox provider** gate remains mandatory. |

## Record the result

For each case write `PASS / FAIL / BLOCKED`, date/time, tenant/outlet, test user role, actual API/FE release SHAs, correlation/reference/transaction ID (masked when shared), before/after stock, transaction and payment/ledger counts, and screenshots or request trace. Do not capture provider secrets, JWTs, real phone numbers, or bank details.

**Release rule:** Failed/missing financial or concurrency evidence keeps Sprint 2 in Draft. Do not interpret a sandbox UI PASS or a completed mock test as six-category go-to-market approval.
