# Sprint 1 — Implementation & QA status

**Status: IN PROGRESS — NOT MERGE-READY / NOT RELEASE-READY.** Production was not deployed.

Baseline at start (2026-09-25): backend `4e4c3b1d538089e073e56fcb4bf09360cb0835d5`, frontend `0b38d9315f573577ce0efef41a017641985cf7c2`. Both changes are isolated on `feat/s1-tenant-role-demo-20260925` worktrees. Frozen contracts were not edited.

## Implemented

- Additive tenant context: server role-derived `effectivePermissions`, tenant status and assigned active outlet IDs; original fields retained.
- Guard product write, stock mutation, reports and customer edit/delete at API boundary; cashier catalog search and customer lookup/create retained.
- Existing JWT rejected when account inactive or role changes. Existing `TenantStatusMiddleware` continues to return its established suspension response.
- Owner account protected from admin promotion/change/delete and disabling/demotion through user management.
- Tenant-scoped user/outlet assignment entity, EF migration and existing-user backfill; non-owner outlet list, sale checkout selection and admin outlet updates are assignment-checked. Explicit assignment/revocation endpoint with tenant audit event; newly created staff assigned to default outlet.
- Demo and bootstrap seeded with default outlet; demo staff assigned to it. Platform tenant provisioning already created owner and default outlet before this branch.
- FE permission-aware route guard with legacy context compatibility, cashier landing, owner table creation empty-state CTA, safe outlet selection reset, and user/outlet assignment UI.

## Verification executed

- Backend full suite at checkpoint 2: **159 passed, 0 failed, 0 skipped** (InMemory/WebApplicationFactory); fresh build **0 warnings/0 errors**.
- FE TypeScript/Vite build and ESLint: passed (existing bundle-size advisory remains); request interceptor includes server-validated `X-Outlet-Id` for transaction, payment, restaurant and laundry routes.
- Local mocked Playwright Chromium restaurant/fashion/retail-return selection: 7 passed, 2 skipped; Sprint 1 role/outlet and empty-table scenarios: **9 passed across desktop/tablet/mobile**. Restaurant/laundry existing mocked desktop suite: **7 passed, 1 skipped**. Other browser/device and live API E2E not certified.
- EF migrations, including `AddSprint1UserOutletAssignments`, `AddSprint1RestaurantOutletScope` and `AddSprint1LaundryOutletScope`, applied to **disposable local PostgreSQL 16**. Nonempty restaurant table and laundry order records backfilled to the correct tenant default outlet; laundry migration rolled back and reapplied with its legacy order preserved. EF pending model changes: none. Existing production DB was not accessed.
- Auth/navigation mixed live suite: 4 passed and 3 not certified in this isolated FE-only run (two need `QA_OWNER_PASSWORD`; advertised admin login needs a live test backend). Keep these as OPEN, not PASS.

## Checkpoint 2 — outlet object authorization (2026-09-25)

- `GET /api/transactions`, transaction detail and receipt WhatsApp readiness/send now reject inaccessible outlet objects, while allowing owner selection. Cash checkout keeps its existing outlet snapshot.
- QRIS create/current/status/cancel and expired-payment reconciliation are restricted to the resolved outlet. A pending QRIS in another outlet does not block local checkout; webhook remains trusted cross-outlet processing and is not switched to user outlet context.
- Restaurant table codes are unique within an outlet, orders and kitchen queue/status are limited by the table outlet, and paid transaction association cannot cross outlets.
- Laundry orders have a persisted outlet ID and scoped read/status/payment binding; legacy orders are backfilled transactionally by tenant.
- Added API negative tests: unassigned cashier gets 403 for explicit branch; default branch sees neither restaurant order/kitchen items nor laundry records; cross-outlet object detail/mutation returns 404; owner with selected branch can access its data. `RequireOutletScope` resolves selection server-side.
- Checks are **isolated QA**, not a full release verdict; there has been no production deployment, push or merge at this checkpoint.

## Checkpoint 3 — local live smoke and provisioning retry (2026-09-25)

- Disposable PostgreSQL container `neverfade-s1-smoke-db`, locally bound to 127.0.0.1:55440; isolated API 127.0.0.1:5292 and FE 127.0.0.1:5293. Both UI/API returned HTTP 200. This is **not a public deployment** and must not be treated as persistent UAT infrastructure.
- Opt-in real browser-to-local-API login smoke completed: owner/admin/kasir **3 passed** (Desktop Chromium) with isolated development seed credentials. Never supply these demo credentials for production.
- Platform tenant create has optional `Idempotency-Key` request header. Atomic unique receipt stores only request digest and tenant reference, never owner password; unchanged same-key retry returns existing tenant, changed payload returns 409. FE retains retry key for an unchanged form.
- Full backend API test suite **161 passed, 0 failed, 0 skipped**; latest idempotency migration applied to isolated PostgreSQL. FE build and lint pass. No production deployment, push or merge.
- Remaining proof: concurrent same-key requests against real PostgreSQL, complete tenant onboarding/demo segregation, operator roles, full cross-category/outlet authorization and release/UAT gates.

## Mandatory exit gates still OPEN

- Complete role taxonomy/restricted operators, delegated admin capability and object-level access on **all** relevant tenant/outlet resources, including reports, payment, restaurant and laundry records.
- Idempotency is implemented and API-tested, but PostgreSQL concurrency proof, explicit isolated demo provisioning, timezone, production/demo mode and full onboarding/seed contract are still open.
- Outlet isolation for restaurant tables/orders/kitchen and laundry work orders is now covered by scoped API tests and additive, backfilled migrations. Wider multi-outlet object checks (other modules, reports, shared-POS flows and real PostgreSQL authorization fixtures) still require audit and evidence.
- Real multi-tenant/multi-outlet PostgreSQL integration, migration backfill/rollback test, FE/BE full browser matrix and role/403 matrix with evidence.
- Reproduce/regress R1 financial 500, R2 missing tables, R3 kitchen after submit, R4 stuck pending; distinguish sprint-scoped fixes from later planned fixes.
- Full QA/UAT traceability, security/observability review, same-SHA release record, and formal sign-off. No deploy/merge until these gates pass.
