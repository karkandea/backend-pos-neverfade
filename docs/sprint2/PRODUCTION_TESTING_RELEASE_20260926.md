# Sprint 2 — production testing deployment (2026-09-26 WIB)

**Status:** deployed for supervised UAT, **not GA / not Sprint 2 sign-off**. Both source PRs remain Draft because real PostgreSQL money/stock concurrency, provider financial UAT and CI billing gate remain open.

## Exact active release

- Production URL: `https://neverfade.dualangka.com/` (also `https://neverfade-pos.103-175-207-127.nip.io/`).
- Backend image: `neverfade-pos-backend:8959913-s2-testing`, source SHA `8959913eccc69d6289b5fac861f5000e87f11c79`, container `neverfade-pos-backend-prod-candidate` binding 127.0.0.1:5091, up with zero restarts at release smoke.
- Frontend release: `/opt/neverfade-pos/frontend/releases/95bdcf4-s2-20260926`, source SHA `95bdcf494d65859ce459dce924755a43b4119ab1`, active via `/opt/neverfade-pos/frontend/current`.
- Frontend was built deliberately with `VITE_S2_CASH_CHECKOUT=enabled` **only for `general_retail` and `fashion_retail` cash checkout**. Restaurant/laundry and QRIS continue their existing FE endpoints.
- Production PostgreSQL `neverfade_prod`: **24** EF migrations through `20260926140901_AddSprint2CashPreparedAttempt`. Source branch remained Draft and was deployed via a documented testing-release exception; no false CI-green claim.
- Provider runtime **preserved unchanged** from S1: `Payments__Mode=Live`, `Payments__LiveEnabled=true`, and existing live-tenant allowlist remains set. This is **NOT sandbox**; avoid unapproved real-money scans.

## Completed gates and evidence

- Current source prior to deployment: Release backend **197/197 PASS**, 0 build errors/warnings, EF model snapshot parity; FE build/lint PASS; flagged cash mocked browsers **33/33 PASS** across desktop/tablet/mobile; default-off QRIS/legacy cash regression **21/21 PASS**. Those are not actual bank/payment settlement tests.
- Fresh pre-cutover production backup and checksum validated: `/opt/neverfade-pos/rollback/s2-final-20260926/prod-pre-cutover.dump` and `.sha256` (0600 backup). Existing older backup also retained in `/opt/neverfade-pos/rollback/s2-20260926/prod-pre-s2.dump`.
- Updated image was started on separate clone-DB canary, provider Disabled there; unauthenticated `/api/auth/me`, cash/current, cash/idempotency and owner attention were 401. No canary live provider call.
- Production migrated **21 → 24**, then 401 boundaries checked for `/api/auth/me`, `/api/v2/sales/cash/current`, `/api/v2/sales/cash/idempotency/...`, `/api/payments/attention`, `/api/v2/sales/quotes/...`. Public `/login`, `/kasir`, `/pembayaran/perlu-perhatian` all HTTP 200; Nginx syntax passed.
- Postcutover DB counters: 5 tenants, 13 transactions, 8 payments, 0 sale_quotes. No synthetic payment/cash sale executed as part of deploy. QA `neverfade-s2-qa.service` remained active. Saved prior backend container is `neverfade-pos-backend-prod-before-s2-20260926` with S1 image `neverfade-pos-backend:664c624-s1`; prior frontend release is `/opt/neverfade-pos/frontend/releases/15050a8-s1-20260926`.

## UAT handoff

Use `docs/sprint2/UAT_CASH_QRIS.md` and record PASS/FAIL/BLOCKED per case. Check cash first in retail/fashion, then QRIS **only on a deliberately approved testing tenant and with explicit understanding of Live mode**. If any attempt is unknown, do not create another charge; recover original reference. The exact running release and the financial result must be recorded before deciding whether to merge or start Sprint 3.

## Rollback notes (operator only)

For application rollback, restore the prior frontend symlink and replace the active backend container with the preserved S1 container, with nginx/app health validation and transaction checks. **Do not automatically restore the old database dump after S2 writes:** the migration is additive, but a database restore would erase subsequent real/test transactions and may break provider reconciliation. Any database restore needs separate authorized downtime and finance reconciliation. Preserve source, image, current logs and snapshots before deciding rollback.
