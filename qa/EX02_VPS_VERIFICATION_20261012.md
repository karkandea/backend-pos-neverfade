# Neverfade POS — EX02 direct VPS verification (2026-10-12)

**Executor:** authorized Desktop Commander account `eodoravexana@gmail.com`, device `ubuntu`, direct VPS shell. GitHub Actions was **not used as a release gate**. Workspaces isolated at `/opt/neverfade-work/ex02-vps-validation-20261012`. No production merge, release symlink changes, database mutations or deployments.

## Source heads verified

| Change | SHA | Executed checks |
|---|---|---|
| BE #23 MFA | `7f126795bdc243dfb553dd57b181f3dcbd0c8ed6` | .NET Release **226/226 PASS**; MFA-selected **10/10 PASS**; disposable PostgreSQL gate **PASS** |
| BE #24 ops backlog | `2337306fa603df7bc54d4557c4dce684770ec6a8` | .NET Release **217/217 PASS** |
| FE #24 MFA UI | `4d31bd3de7084b2918b4079fad7da355c04ab380` | `npm ci`, ESLint, TypeScript/Vite build **PASS**; targeted Playwright **2/2 PASS** |
| FE #25 gross profit | `a54b2825192a75acb9ed64e81bcac1ad585adbd6` | `npm ci`, ESLint, TypeScript/Vite build **PASS** on exact SHA; targeted Playwright **3/3 PASS** |
| FE #26 owner monitoring | `76522fbf7224cb532f7b60ad8d11eb7c9cd7543f` | `npm ci`, ESLint, TypeScript/Vite build **PASS**; targeted Playwright **5/5 PASS** |

## Integration validation (local QA merges, NOT remote integration-branch merge)

- Frontend QA integration merge: `fc0b1292c7e0a8904cc561a769148a009c574582`. Combined FE #24/#25/#26 **npm ci + prebuild lint + build PASS; 10/10 EX02 Playwright PASS**.
- Backend QA integration merge: `fccc05df672e7fabaeb81ca83d5c84c2572b8c05`. Combined BE #23/#24 **227/227 Release tests PASS**.
- FE test-list merge conflicts were limited to the CI workflow; local QA merge retained all three EX02 specs. No production or shared QA branch was modified.

## PostgreSQL MFA gate (disposable only)

Used a new `postgres:17-alpine` container with an isolated temporary database and loopback-only port. **PASS:** fresh schema apply to 31 migrations; idempotent second apply remained 31; rollback last MFA migration; reapply MFA migration; `dotnet ef migrations has-pending-model-changes` showed no drift. Temporary PostgreSQL container was removed afterward.

## Existing critical browser regressions (combined FE)

The broader Desktop Chromium set (auth/navigation, role/outlet, cash, QRIS) contained 50 test cases. Outcome: **36 passed, 3 failed, 11 skipped**. These are **NOT** an overall PASS.

- Two auth/navigation cases fail immediately because `QA_OWNER_PASSWORD` was not provisioned in the test environment.
- One advertised admin-demo login requires a working seeded backend, but the isolated UI runner used a dummy API URL. It remained on the login page.
- Eleven cases were skipped by test conditions. The suite was not executed against a live-equivalent integration backend.
- This is a QA environment/fixture gap; no claim about the actual live auth flows is made.

## Fixes made due to live tests

- BE #23 `57b9563`, `7f12679`: corrected .NET 10 AES-GCM exception assertion and isolated MFA lockout threshold test fixture after prior replay failures; rerun PASS.
- FE #25 `a54b282`: fixed malformed Rupiah regex in Playwright test; rerun PASS.
- FE #26 `9fb2495`, `76522fb`: immediately refresh on visible tab and added hidden-tab Playwright regression; rerun PASS.
- During testing, an old Vite dev-server process was detected on port 5273. Stopped only that QA-owned process and assigned distinct local ports to each branch, with PID cleanup.

## Outstanding QA before merge / production

- Actual integrated BE+FE E2E on a seeded, **disposable** PostgreSQL database, with owner/admin/outlet fixtures, including the three currently blocked auth cases.
- Full role/tenant/isolation QA; additional tablet/mobile browser coverage; end-to-end notification delivery/worker-outbox feature acceptance.
- Payment provider certification/external integration evidence where required; source tests and mocked Playwright cannot replace this.

**Release posture:** remain Draft; do not merge or promote to QA/production from this evidence alone. CI can remain optional; the established VPS-based release evidence is authoritative once all mandatory gates pass.

**VPS evidence logs:** `/opt/neverfade-work/ex02-vps-validation-20261012/{mfa-dotnet-recheck-full.log,alert-dotnet.log,integrated-be.log,integrated-fe.log,integrated-fe-playwright.log,profit-playwright-final.log,ops-playwright.log,mfa-postgres-gate.log,integrated-core-playwright.log}`.
