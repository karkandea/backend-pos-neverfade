# Sprint 1 production release exception — 2026-09-26

## Authorization and decision

The product owner explicitly requested merge, production release and continuation to Sprint 2. This is a **manual release exception**, not an assertion that the complete Sprint 1 market-readiness and CI sign-off gates passed. Keep the open closure items tracked and do not describe all categories as market-ready based only on this deployment.

## CI and source evidence

- GitHub Actions did **not execute any jobs**: backend and frontend check annotations say the account is locked because of billing. This is a blocked runner, not a failed source test. Remote CI remains RED/BLOCKED.
- Manual, local equivalent on the release worktree: backend `dotnet restore`, Release build (0 warnings/errors), Release tests **176/176 PASS**; frontend `npm ci`, build, lint, and contract browser checks **2/2 PASS** using the correct Sprint 1 dev server. An initially attempted run accidentally hit an existing, unrelated Phase 1 Vite server on port 5273; it was discarded and rerun on an isolated Sprint 1 server, port 5278.
- Existing Sprint 1 QA evidence: 51/51 exact deployed-asset browser smoke across desktop/tablet/mobile via localhost mirror to actual isolated QA API and PostgreSQL; this did not test the public HTTPS Basic-auth gate, formal UAT or external payment acceptance.

## Data and payment protection

- Verified the current production PostgreSQL database has migration history including the historical 20260913103510_AddHostedXenditCheckout entry; it had been applied from a VPS-only SQL script but was absent from source. Reintroduced its exact ID and equivalent additive DDL in source to preserve clean-install/rollback parity.
- Created a root-only consistent pre-release PostgreSQL custom-format backup under `/opt/neverfade-pos/rollback/s1-20260926/` and restored it to the **separate** `neverfade_s1_release_rehearsal` database. Applied the source-generated idempotent SQL successfully; 21 migrations present, all 5 original tenant rows retained, and the three historical checkout indexes retained. Re-running the reconciled idempotent script passed. Separate isolated QA database clone also reconciled to 21 migrations.
- Built the backend release candidate Docker image and ran it against the clone on loopback port 5124 with payments **Disabled**, a separate JWT and no public route. API startup/OpenAPI HTTP 200. Existing production live Xendit/WA secrets are not inserted into the clone. Production live payment credentials, callback paths and mode must be preserved at cutover.
- Production migration changes are additive. If application rollback is needed, restore the previous container and FE release. **Do not automatically reverse migrations or restore the database over newer transactions.** Database restore needs a separate incident decision and transaction reconciliation.

## Explicitly open after controlled production cutover

GitHub Actions billing/remote CI; safe repeatable demo reset; full six-category business UAT, pricing/payment/provider/device onboarding and formal acceptance; role taxonomy/stylist and remaining cross-module object authorization audit; production R1-R4 regression and live payment/provider checks. Each remains a closure gate; Sprint 1 is not formally marked DONE.

## Release record to complete after cutover

Track backend/frontend source SHAs, image, production backup hash, before/after DB migration counts, previous FE symlink, health probes, user-facing smoke and rollback result. Do not claim production release success until checks actually pass.
