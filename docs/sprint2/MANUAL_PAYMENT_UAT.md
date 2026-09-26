# Sprint 2 — payment UAT handoff (NOT release sign-off)

**Do not run this against production until the active frontend/backend release manifests and migration count are confirmed for the S2 candidate.** The earlier production snapshot was S1 (backend `664c624-s1`, frontend `15050a8`, 21 migrations). The S2 code remains in Draft PRs. Use a separate QA tenant/outlet and non-sensitive products. Record environment, build SHAs, tester, UTC/WIB timestamp, sale/quote/payment references, payment mode (Disabled/Sandbox/Live), before/after stock and transaction counts, and redacted screenshots. Never paste API credentials or full webhook tokens.

## Cash — use flagged S2 retail checkout only

- [ ] Check backend `prepare` and `/cash/current` require the assigned tenant/outlet and same cashier; no stock movement before commit.
- [ ] One sale: server quote total/tax/discount match UI; one stock movement, one paid transaction, correct receipt/change.
- [ ] Double-click, timeout, refresh: original quote/version/key reused; **one** sale/stock movement. Confirm successful read-only key lookup after lost response.
- [ ] Open a second session as the **same cashier** while first attempt is prepared: restore same key; no new quote. A different cashier cannot see that pending attempt.
- [ ] Try a second quote/key while first is prepared: reject. After verified server no-sale (e.g. expired quote), explicit `abandon` blocks a delayed original commit and allows a fresh quote.
- [ ] Double-click while the initial server pending check is slow: only one quote/prepare/commit, not two independent attempts.
- [ ] A quote expires *before* prepare is stored: server confirms `abandoned` and blocks both late prepare and late direct commit for the original quote/key. Do not unlock the client after 5xx or unknown status.
- [ ] While cashier A has a prepared attempt, direct v2 commit with a different quote/key is rejected; another cashier's login cannot read cashier A's key.
- [ ] Two cashiers / two quotes against last unit: at most one successful sale, nonnegative stock, only one stock history for the unit. Test direct legacy/v2 routes separately, not just UI.
- [ ] Unavailable DB/HTTP 5xx: **do not** clear the old key, invent a new one, or assert paid without server receipt.

## QRIS — controlled payment environment only

- [ ] Confirm provider mode and allowed tenant in backend before scanning; avoid unintended live-money testing.
- [ ] QRIS amount equals server sale amount; creating/pending survives reload and blocks second charge.
- [ ] Provider accepted create but reply lost: retain original immutable reference; do not offer new QRIS; verified callback recovers the original attempt.
- [ ] Wrong callback token, amount, currency, reference or request ID cannot mark paid or alter stock.
- [ ] Webhook duplicate/out-of-order, paid-before-create-response and cancel-vs-paid: exactly one ledger entry and stock movement; paid is never downgraded by late failure.
- [ ] Expired UI timer without provider confirmation remains non-final; verified provider expiry closes it without stock decrement.
- [ ] Owner/admin attention queue is scoped to the selected outlet and read-only; cashier cannot access.

## Release decision

Record each scenario PASS/FAIL/BLOCKED with evidence; only declare merge/release ready after the real PostgreSQL concurrency gate, payment/provider reconciliation, role/tenant security, rollback drill and an authorized CI sign-off (currently blocked by GitHub billing) have been resolved. Do not substitute mocked browser/API results for actual payment proof.
