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

- Backend full suite: 157 passed, 0 failed, 0 skipped (InMemory/WebApplicationFactory).
- FE TypeScript/Vite build and ESLint: passed (existing bundle-size advisory remains).
- Local mocked Playwright Chromium restaurant/fashion/retail-return selection: 7 passed, 2 skipped; new Sprint 1 role/outlet and empty-table scenarios: 3 passed. Other browser/device and live API E2E not certified.
- EF migrations, including `AddSprint1UserOutletAssignments`, applied successfully to a fresh **disposable local PostgreSQL 16** container. Existing production DB was not accessed.
- Auth/navigation mixed live suite: 4 passed and 3 not certified in this isolated FE-only run (two need `QA_OWNER_PASSWORD`; advertised admin login needs a live test backend). Keep these as OPEN, not PASS.

## Mandatory exit gates still OPEN

- Complete role taxonomy/restricted operators, delegated admin capability and object-level access on **all** relevant tenant/outlet resources, including reports, payment, restaurant and laundry records.
- Tenant creation idempotency; explicit isolated demo provisioning; timezone, production/demo mode and full onboarding/seed contract.
- Restaurant tables and other operational records currently need verified outlet isolation. Current owner CTA uses existing table API; do not present this as proven per-outlet support.
- Real multi-tenant/multi-outlet PostgreSQL integration, migration backfill/rollback test, FE/BE full browser matrix and role/403 matrix with evidence.
- Reproduce/regress R1 financial 500, R2 missing tables, R3 kitchen after submit, R4 stuck pending; distinguish sprint-scoped fixes from later planned fixes.
- Full QA/UAT traceability, security/observability review, same-SHA release record, and formal sign-off. No deploy/merge until these gates pass.
