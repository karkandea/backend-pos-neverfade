# Sprint 1 — Implementation & QA status

**Status: IN PROGRESS — public QA preview deployed; NOT MERGE-READY / NOT PRODUCTION-RELEASE-READY.** Production was not deployed.

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

## Checkpoint 4 — isolated public QA preview (2026-09-26)

- **QA preview deployed (not production, not release sign-off):** `https://neverfade-s1-qa.103-175-207-127.nip.io/qa-access`. Dedicated Nginx virtual host with valid HTTPS and a Basic-auth entry that issues an HttpOnly/Secure/SameSite cookie for UI/API. Anonymous static requests redirect to gate; API rejects requests without gate cookie; failed Basic auth never receives gate cookie. Credentials are managed outside Git.
- Dedicated VPS PostgreSQL database `neverfade_s1_qa`, least-privilege OS/DB user `neverfade_s1_qa`, API loopback `127.0.0.1:5119` via `neverfade-s1-qa.service`, static frontend release. No copy of production records or production credentials. Payments disabled; WA endpoint configured to a non-listening QA loopback target; separate JWT keys. **Legacy staging webhook was intentionally not reused** because it targets production.
- Deployed executable/asset manifest: backend `81dbbf13e3ff2cd8b948f7515de3a8873d230288`, frontend `75d938d2c0e0aad05e814d9e1da73f31da3e4a0c`; all 19 migrations installed against QA database, including platform provisioning idempotency. QA data is seeded separately, never part of a production migration.
- Isolated live smoke through public HTTPS: **9 passed, 0 failed** on Desktop Chromium: demo owner/admin/cashier, five additional category owner accounts (food/beverage, fashion, laundry, salon, barbershop), and real restaurant table→order→send-to-kitchen→queue with cleanup. Initial QA-entry Content-Type issue was fixed and the complete suite rerun green. The five category accounts use manually created *QA-only fixtures*, not the yet-to-be-implemented official demo provisioning contract.
- QA smoke proves the named paths only; remaining mobile/tablet live matrix, full RBAC/UAT/security/performance gates and real concurrent PostgreSQL idempotency proof remain open. Do not merge or deploy production based on this preview.

## Checkpoint 5 — public PostgreSQL authorization matrix and CI fallback (2026-09-26)

- QA fixture creates a second Restaurant outlet and Fashion outlet, with separate owner and a default-outlet-only Restaurant cashier. Restaurant permits the same `A1` table code in each outlet; cashier GET for unassigned branch returns **403**. Fashion owner access with a Restaurant outlet ID returns **404** instead of crossing tenants.
- The **live public HTTPS** isolated smoke suite: **33 passed, 0 failed** across Desktop, Tablet and Mobile Chromium (11 cases × 3). Includes tenant category routing, real kitchen queue send and cleanup, intra-tenant role/outlet assignment, and cross-tenant outlet denial. These are limited path checks rather than full business UAT.
- Both GitHub draft PRs exist (backend #14, frontend #18). GitHub Actions jobs **did not start**: GitHub check annotations explicitly state the account is locked due to a billing issue. These check failures are external infrastructure blockers, not executed failing tests; CI required gate remains open.
- CI-equivalent fallback executed locally on the branch: backend solution restore and Release build **0 warnings/0 errors**, Release test suite **161/161 passed**; frontend `npm ci`, build, lint and Phase 3 contract browser tests **2/2 passed**. Existing Vite chunk-size advisory remains. Local verification does not mark remote GitHub CI green.
- All new tests and this evidence remain on the Sprint 1 feature branch; QA running asset/executable SHAs remain those recorded in checkpoint 4. Production has not been deployed or merged.

## Mandatory exit gates still OPEN

- Complete role taxonomy/restricted operators, delegated admin capability and object-level access on **all** relevant tenant/outlet resources, including reports, payment, restaurant and laundry records.
- Idempotency is implemented and API-tested, but PostgreSQL concurrency proof, explicit isolated demo provisioning, timezone, production/demo mode and full onboarding/seed contract are still open.
- Outlet isolation for restaurant tables/orders/kitchen and laundry work orders is now covered by scoped API tests and additive, backfilled migrations. Wider multi-outlet object checks (other modules, reports, shared-POS flows and real PostgreSQL authorization fixtures) still require audit and evidence.
- Real multi-tenant/multi-outlet PostgreSQL integration, migration backfill/rollback test, FE/BE full browser matrix and role/403 matrix with evidence.
- Reproduce/regress R1 financial 500, R2 missing tables, R3 kitchen after submit, R4 stuck pending; distinguish sprint-scoped fixes from later planned fixes.
- Full QA/UAT traceability, security/observability review, same-SHA release record, and formal sign-off. No deploy/merge until these gates pass.
