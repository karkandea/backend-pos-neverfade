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

## Checkpoint 6 — dedicated kitchen operator and QA promotion (2026-09-26)

- New `dapur` role is explicitly accepted by JWT only while the user remains active and has the same role; server permission catalog grants only `outlets.read` and `restaurant.kitchen.operate` when the tenant has `kitchen_queue`. Owner/admin can create and assign the role through existing user/outlet management. Role changes invalidate the old JWT.
- A deny-by-default operator HTTP gate permits only `GET /api/auth/me`, tenant context, assigned outlet listing, plus dedicated `GET /api/restaurant/kitchen/operator` and `POST /api/restaurant/kitchen/operator/items/{id}/status`. Existing cashier, orders, transaction, catalog, report and platform routes are not made available. Restaurant service accepts this role **only** for kitchen queue/status operations; all original operational routes remain unchanged.
- Dedicated kitchen response DTO intentionally excludes selling price, subtotal, transaction/payment fields and customer details. The original kitchen route remains the existing contract for owner/admin/cashier; dedicated operator route uses server-validated tenant/outlet scope and capability. Frontend lands on `/dapur`, renders restricted nav and calls operator endpoints only.
- Backend Release build **0 warnings/0 errors**, full tests **162/162 PASS**; frontend build/lint PASS; mocked Sprint 1 FE scenarios **12/12 PASS** across desktop/tablet/mobile. Backend negative test includes hidden financial fields, denied direct endpoints and wrong outlet, allowed kitchen status transition and old JWT revocation.
- Promoted verified QA-only release to `20260926-s1-kitchen`, backend SHA `c8044242bb76b1adbcbc9c1d8085e5b0438f340b`, frontend SHA `4e7124c00cefea20e33c9814591897e2c9237e7d`. Previous QA service/Nginx configs and release retained for rollback. No schema migration was necessary for the new role. QA operator fixture is explicitly separate from merchant production.
- Public isolated HTTPS browser smoke re-run **36/36 PASS** across desktop/tablet/mobile, including dedicated kitchen operator sign-in and denied access. This is still a QA preview, not sign-off of all merchant/operator flows. The remote GitHub Actions billing lock and other open gates remain unchanged.

## Checkpoint 7 — provisioning concurrency proof (2026-09-26)

- Found a **test harness version mismatch**: local loopback QA API process launched 2026-09-25 before the idempotency feature was compiled, while local DLL had been rebuilt 2026-09-26. The old process yielded seven 409 slug conflicts for eight concurrent requests and zero idempotency receipts. This was not a valid test of current source; it was restarted against the same isolated local PostgreSQL 16 database using the new binary.
- On the current binary, the opt-in PostgreSQL concurrency test issued **eight simultaneous, identical-key platform provisioning requests**: all returned HTTP 200 with the *same tenant ID*, and the DB recorded exactly **one provisioning receipt**. Same key with a changed payload returned 409 `IDEMPOTENCY_KEY_REUSED`. The test is stored at `tests/e2e/sprint1-platform-postgres-concurrency.spec.ts` in the frontend repo and explicitly requires isolated QA environment/credentials; it is skipped in default suites.
- This closes the **specific same-key concurrent PostgreSQL provisioning proof**, not the broader onboarding/demo mode or live release gates. Production and public QA were not changed by this local database proof.

## Checkpoint 8 — explicit live/demo provisioning foundation (2026-09-26)

- Additive tenant `Mode` (`live` default / `demo`) and `TimeZoneId` (`Asia/Jakarta` default), database mode check and verified EF migration `AddSprint1TenantModeAndTimezone`. Existing tenant rows backfill to live/Jakarta; no legacy data deletion. Platform create accepts explicit mode/timezone, rejects invalid mode/timezone, returns them in platform DTO and tenant context. FE create/detail exposes mode and Indonesian timezone choices.
- New tenant creation seeds a **minimal, type-specific** sample product catalog for all five supported business types **only when explicitly demo**. F&B demo additionally creates A1/A2 for its default outlet. No demo product/table rows are provisioned for a new live tenant. Existing development sample fixture was not modified into a demo-mode merchant. The demo creation/receipt/audit records share the provisioning transaction.
- Demo tenant QRIS capabilities are disabled, and the QRIS create service rejects demo tenant with `PAYMENT_DEMO_TENANT_FORBIDDEN` before provider call. This is a server gate, not just a hidden FE button. Live-tenant payment behavior is unchanged.
- Focused InMemory API tests of five catalog modes, empty live mode, and validation: **7/7 PASS**. Demo provider QRIS-denial API test **PASS**. Full backend Release build: **0 warnings/0 errors**; full test suite **170/170 PASS**. Disposable PostgreSQL 16 migration applied with pre-existing rows preserved and no pending model changes. Opt-in **real PostgreSQL** provisioning test created five different-category demo tenants and one live restaurant; each demo had its distinct two sample products, F&B had two demo tables, the live restaurant had zero sample products; QRIS capabilities false: **PASS**. Public VPS QA was not updated by this local exercise.
- The demo reset API/semantics, persistent business onboarding checklist, user/device/payment setup and formal UAT **remain open**. Do not advertise this minimal seed as full endgame demo readiness or deploy production until those gates pass.

## Checkpoint 9 — demo QA promotion and outlet-scoped reports (2026-09-26)

- Promoted the previously tested explicit demo/live provisioning binary and frontend to the **isolated public QA** release `20260926-s1-demo` only: backend `7506d1cf7584ed9e611f6ac93525fe832823ba90`, frontend `f6752993816b317fc0a776daa0c4d16da65962a8`. A dedicated QA PostgreSQL dump was saved before applying `AddSprint1TenantModeAndTimezone`; migration succeeded, pre-existing six QA tenants backfilled to `live`, service active and API/QA-entry HTTP 200. Prior kitchen QA release/config preserved for rollback. No production DB/service/release was touched. Public platform demo tenant creation was **not rerun in this promotion**; its five-mode API tests and real disposable PostgreSQL provisioning proof are recorded at checkpoint 8. Do not claim full public demo UAT yet.
- Found that report summary/chart/top-products read tenant-wide paid transactions regardless of admin outlet assignments. Added a server-side visible-outlet filter to all three endpoints: owner without header sees tenant-wide historical totals, owner with explicit `X-Outlet-Id` sees the selected own outlet, admin without header sees only assigned outlets, and admin with inaccessible selection gets 403. Invalid GUID gets 400; unknown/foreign owner outlet gets 404. Historical inactive outlets remain readable where authorized.
- The report UI now exposes an independent `Cakupan outlet` filter: owner `Semua outlet`, admin `Semua outlet tugas`, or one named accessible outlet. All three report requests receive the explicit chosen outlet header; checkout outlet selection is not silently repurposed as a reporting scope.
- New negative API regression proves two-outlet transaction totals, chart and top products cannot leak to an unassigned admin. Backend **171/171 tests PASS**, FE build/lint PASS and Sprint 1 mocked UI **15/15 PASS across desktop/tablet/mobile**. This report patch is source-verified, not yet promoted to public QA at this checkpoint.

## Checkpoint 10 — report QA promotion (2026-09-26)

- Promoted verified source report authorization and explicit FE filter to the **isolated QA** release `20260926-s1-reports`: backend `a18517c0f5f4b39c6b570387721eb37ed76537f5`, frontend `da7e4cc03665d9474c5d564bdcfbc1ef09e6321a`. The existing additive tenant-mode schema stays in place; no new migration for this report patch. Dedicated QA service `active`, loopback OpenAPI HTTP 200 and Nginx syntax check succeeded. Previous demo QA service/Nginx configs and artifacts retained for application rollback. Production untouched.
- Source gates: backend **171/171 PASS**, frontend build/lint PASS, mocked Sprint 1 outlet/report UX **15/15 PASS across desktop/tablet/mobile**. Separate public browser/manual UAT of this exact report release is **not yet certified**; do not represent the older 33/36 test numbers as this release's browser result.
- Ongoing external blocker: GitHub Actions jobs cannot start because of account billing lock; local tests are not remote CI sign-off. Neither PR merged.

## Checkpoint 11 — restricted laundry operator foundation (2026-09-26)

- Added `laundry_operator` dedicated role (permission `outlets.read` + business-capability-gated `laundry.work.operate`), assigned outlet checks, revocable JWT, and fail-closed HTTP route gate. The operator accesses only `GET /api/laundry/operator`, `GET /api/laundry/operator/{id}` and `POST /api/laundry/operator/{id}/status`; existing create/order/payment/customer/transaction endpoints remain unavailable.
- Operator work-order DTO excludes prices, totals, payment status, transaction reference and unnecessary customer phone. Restricted transitions are **received → in_progress → ready**; completion/cancellation and payment remain cashier/owner workflow. Status history and tenant audit retain the individual operator actor.
- Separate FE `/laundry/antrean` work queue and restricted sidebar/mobile navigation; user-management role option and post-login landing added. Source regression verifies redaction, denied finance/checkout/other outlet, allowed work transitions and JWT invalidation after role change.
- Backend **172/172 PASS**, frontend build/lint PASS; Sprint 1 mocked desktop/tablet/mobile **18/18 PASS**. No schema migration. This source checkpoint is not yet evidence of public QA browser UAT or full demo reset/onboarding readiness.

## Checkpoint 12 — laundry operator isolated QA preview (2026-09-26)

- Promoted source-verified restricted laundry operator to dedicated public QA release `20260926-s1-laundry`: backend `9ba12e3c9686bf8cdb59c9bdb8d186b1ca9f7ab1`, frontend `c790125745d4b8e4cac3f96b50f976581cd33f11`. No schema migration, production change, or merge. Previous report release/config retained for rollback; QA service active and OpenAPI HTTP 200.
- Created **QA-only** `qa.laundry.operator` in the isolated `qa-s1-laundry` tenant, assigned to its default outlet; this fixture reuses that QA tenant's owner test-password hash. No real merchant credential/data is copied. Direct isolated QA API smoke proved operator login/context 200; restricted queue 200 without price/payment/contact fields, and legacy transaction and work-order endpoints 403.
- Source gates: backend **172/172 PASS**; FE build/lint PASS and mocked Sprint 1 UX **18/18 PASS across desktop/tablet/mobile**. Public HTTPS browser-specific UAT of this exact new binary and complete laundry handoff remains **open**. Operator changes do not imply the full laundry S7–S8 product is ready.

## Checkpoint 13 — persisted-state-derived merchant setup checklist (2026-09-26)

- Added read-only `GET /api/tenant/onboarding`, **owner/admin only**. Computes completion from persisted tenant-owned settings, active default outlet, catalog and category-specific restaurant table/laundry service/salon service; optional cash availability and staff outlet assignment shown separately. No client-supplied completion flags or database migration. Server response includes mode, category, required progress and safe in-app action paths. Never claims release certification or provider payment activation.
- FE `/mulai` exposes `Setup Usaha`, linked required steps, refresh and explicit demo/live messaging. Operator and cashier route guards remain closed. F&B, laundry and salon requirements use their respective modules; demo fixture data is shown only in demo tenant, never copied into live merchants.
- Focused API tests **4/4 PASS** for persisted profile and role boundary, plus category-specific step selection. Full Release backend **176/176 PASS**; FE build/lint PASS and Sprint 1 mock UI **24/24 PASS** across desktop/tablet/mobile. This source checkpoint still requires the exact SHA QA deployment and live browser proof before release certification.

## Mandatory exit gates still OPEN

- Complete role taxonomy (notably stylist/booking and per-station extensions), delegated admin capability and object-level access on **all** remaining relevant tenant/outlet resources. Report summary/chart/top-products now enforce outlet scope in source; other reports, stock and shared-POS flows still require end-to-end audit.
- Idempotency and explicit demo/live provisioning with timezone and initial five-mode isolated seed are proven. Persisted-state checklist is implemented; safe repeatable demo reset, payment/provider/device onboarding, owner acknowledgement and full seed contract remain open.
- Outlet isolation for restaurant tables/orders/kitchen and laundry work orders is now covered by scoped API tests and additive, backfilled migrations. Wider multi-outlet object checks (other modules, reports, shared-POS flows and real PostgreSQL authorization fixtures) still require audit and evidence.
- Real multi-tenant/multi-outlet PostgreSQL integration, migration backfill/rollback test, FE/BE full browser matrix and role/403 matrix with evidence.
- Reproduce/regress R1 financial 500, R2 missing tables, R3 kitchen after submit, R4 stuck pending; distinguish sprint-scoped fixes from later planned fixes.
- Full QA/UAT traceability, security/observability review, same-SHA release record, and formal sign-off. No deploy/merge until these gates pass.
