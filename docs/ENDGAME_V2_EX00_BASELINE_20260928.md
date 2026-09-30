# Neverfade POS Endgame v2 — EX00 Baseline

Verified: 2026-09-28 (Asia/Jakarta)

This file is the execution baseline for the user-provided **Neverfade POS Endgame v2 / Full Release Baseline** package dated 2026-09-28. It records observed repo/VPS facts only. It does **not** mark requirements DONE merely because source files or tests exist.

## Spec baseline

- Package manifest integrity: PASS for all 19 declared files.
- Declared scope: 69 requirements, 128 stories, 656 team subtasks, 183 target API operations, 390 QA/UAT scenarios.
- Full Release remains one scope across general/minimarket, fashion, restaurant, laundry, salon and barber preset experiences.
- Existing `CONTRACT.md` / frozen v1 contract is not modified by this rebaseline. Additive v2 changes use the current v2 path/RFC approach.
- EX00 first obligation: pin actual runtime/source, reproduce R1-R4, map existing implementation to BR tickets, and keep unverified work unverified.

## Current production baseline on VPS

Observed VPS: `103.175.207.127`.

### Production app

- Public frontend: `neverfade.dualangka.com` and `neverfade-pos.103-175-207-127.nip.io`.
- Frontend root: `/opt/neverfade-pos/frontend/current`.
- Current frontend release target: `/opt/neverfade-pos/frontend/releases/95bdcf4-s2-20260926`.
- Production frontend SHA: `95bdcf494d65859ce459dce924755a43b4119ab1`.
- Backend container: `neverfade-pos-backend-prod-candidate`.
- Backend image: `neverfade-pos-backend:8959913-s2-testing`.
- Production backend SHA represented by deployment manifest: `8959913eccc69d6289b5fac861f5000e87f11c79`.
- Production S2 manifest flag: `VITE_S2_CASH_CHECKOUT=enabled`.
- A pre-cutover DB dump/checksum and production migration log exist under `/opt/neverfade-pos/rollback/s2-final-20260926/`.

### Other observed environments

- Demo backend: `neverfade-pos-backend-demo`, image `neverfade-pos-backend:demo-02b9bcb`.
- Demo frontend root: `/var/www/neverfade-demo`.
- Staging backend observed: `neverfade-pos-backend-staging`, image `neverfade-pos-backend:staging-7ac83a7`.
- Existing rehearsal/canary containers are retained; no environment was changed during this baseline pass.

## Repo continuation baseline

The previous primary local working trees were stale on `feat/xendit-hosted-checkout`, so they are not used as the Endgame v2 source of truth.

Continuation branch in both repositories:

`feat/endgame-v2-ex00`

Created from the latest S2 remote branch `feat/s2-core-sale-payment-20260926`.

### Backend

- Continuation head: `7fc0d6f` — v2 API error contract/context envelope alignment.
- Production deployed backend `8959913` is an ancestor of this branch.
- Delta after deployed SHA when baseline was taken:
  - `a039e00` docs: production testing cutover/UAT handoff.
  - `15e4329` docs: production test accounts/category UAT gaps.
  - `7fc0d6f` code: v2 API error contract/context envelope alignment.
- Source tree contains migrations through `20260926140901_AddSprint2CashPreparedAttempt`.
- Exact production `__EFMigrationsHistory` query is still required before BR-OPS-01 can close.

### Frontend

- Continuation head: `77fdb96` — consume context envelope and normalize API errors.
- Production deployed frontend `95bdcf4` is an ancestor of this branch.
- Delta after deployed SHA at baseline: `77fdb96` only.
- `npm ci && npm run build && npm run lint`: PASS on the continuation head.
- Build warning: primary JS chunk is ~598 kB after minification; treat as performance debt, not a functional release PASS/FAIL by itself.
- `npm audit`: 2 transitive findings (1 moderate, 1 high) in `baseline-browser-mapping` / `browserslist`. Do not blind `npm audit fix`; resolve with controlled dependency review.
- Playwright registry: 426 tests across 35 files on the continuation head.

## Existing R1-R4 coverage candidates

These are **coverage candidates**, not release evidence until run against the required isolated/RC environment and recorded with SHA/timestamp.

- R1 Finance 500: `tests/e2e/sprint1-finance-live-smoke.spec.ts`.
- R2 Owner restaurant sees no table / empty state: `tests/e2e/sprint1-role-outlet.spec.ts` and category smoke fixtures.
- R3 Kitchen empty after send: `tests/e2e/sprint1-live-isolated-smoke.spec.ts` sends a real isolated QA order and checks the actual kitchen queue.
- R4 QR pending stuck after cancel/status: `tests/e2e/qris-checkout.spec.ts` contains pending/cancel/reload/recovery cases; an RC/live-equivalent integration run is still required.

## Gate status at baseline time

| Gate | Status | Evidence / remaining work |
|---|---|---|
| Spec package integrity | PASS | 19/19 declared files matched manifest SHA/bytes. |
| Production FE/BE SHA pinned | PASS | FE `95bdcf4`, BE `8959913`. |
| Latest continuation SHA pinned | PASS | FE `77fdb96`, BE `7fc0d6f`. |
| Frontend build | PASS | Vite production build completed. |
| Frontend lint | PASS | ESLint completed with exit 0. |
| Frontend E2E inventory | PASS | 426 tests listed; not equivalent to execution PASS. |
| Dependency audit | OPEN | 1 moderate + 1 high transitive finding. |
| Backend unit/integration tests | NOT_RUN / BLOCKED | Fresh worktree NuGet restore stalled; do not infer code failure or PASS. |
| Disposable PostgreSQL migration gate | NOT_RUN | Must be rerun from continuation SHA. |
| Exact production DB migration history | NOT_RUN | Query `__EFMigrationsHistory` without exposing credentials. |
| R1-R4 RC/isolated runtime reproduction | NOT_RUN | Existing tests identified; must execute and attach evidence. |
| BR-INT-07 vendor/procurement evidence | OPEN | DEC-01/02/03/04 still require real vendor/device/provider evidence. |

## Execution rule from here

1. Finish **BR-OPS-01** evidence: backend test/migration gate, exact deployed migration history, R1-R4 runtime, source-to-story map.
2. Run **BR-INT-07** in parallel for actual payment/device/vendor procurement evidence; mocks do not close physical certification.
3. Reclassify each existing BR story as DONE / PARTIAL / NOT_STARTED only from acceptance + H/E/A evidence.
4. Continue implementation from the first real dependency gap. Do **not** restart EX01 from zero if S1/S2 source already satisfies its acceptance.
5. No production deploy/merge is allowed solely from source presence; use the same-SHA release gates from the Endgame v2 package.
## EX00 evidence update — 2026-09-28

Validated after the initial baseline:

- Production DB history: PASS read-only, 24 migrations; latest `20260926140901_AddSprint2CashPreparedAttempt`.
- Backend current continuation tests: PASS, 200/200, 0 failed, 0 skipped on VPS from Endgame branch source.
- Disposable PostgreSQL migration gate: PASS; clean apply to 24, idempotent second apply remains 24, rollback latest to 23, reapply to 24. Production DB not modified.
- Isolated RC created from QA fixtures only, provider mode Disabled, current code: backend `7fc0d6f`, frontend `77fdb96`; accessed through loopback-only proxy/tunnel.
- R1 finance raw-500 regression: PASS on isolated RC; owner finance endpoints and page load without the historical internal-server-error state.
- R2 restaurant table regression: PASS on isolated RC; `qa.resto` resolves `food_beverage` context and existing table A1 is visible. The pre-existing smoke test required an adapter fix because `/api/v2/context` now returns the canonical `{data,meta}` envelope.
- R3 send-to-kitchen regression: PASS on isolated RC; real QA order is persisted, sent, visible in the kitchen queue, then cleaned up.
- R4 QR pending/cancel recovery browser regression: PASS 3/3 for cancel recovery, refresh without duplicate create, and transient status recovery. This is browser/backend-contract regression evidence only; real live-provider settlement/cancel certification remains OPEN under provider integration gates.

Remaining EX00/early-release concerns: controlled dependency-audit remediation, external provider/device procurement evidence, and story-by-story H/E/A classification beyond R1–R4.

