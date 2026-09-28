# NeverFade POS — Endgame v2 implementation alignment

Date: 2026-09-28 WIB. Baseline source: `Neverfade_POS_Endgame_Handoff_v2_2026-09-28`, owned by Product/Arkan.

## Authority and interpretation

The v2 handoff is now the normative **Full Release** product baseline. Its 69 NF requirements / 128 BR stories are not implementation PASS claims. Existing Sprint 1/2 work remains reusable evidence when it satisfies the same BR/NF acceptance; it is not discarded or silently treated as full-release complete. No FULL_RELEASE_REQUIRED item may be moved after GA merely because a merchant can configure it OFF.

Historical `docs/sprint2/*` describes the September payment/checkout testing release and remains useful evidence. It is no longer the complete product end-state definition.

## Rebaseline snapshot

- Baseline requires all five backend business types, with salon and barber as two experience presets on `salon_barbershop`.
- Explicit end-state scope includes public table QR self-order, salon/barber public booking + waitlist, laundry courier lifecycle, loyalty/promo, live QRIS, PDC/EDC, physical fingerprint attendance, physical printer and scanner certification.
- Simulator/mock/browser tests are development evidence only; physical/provider certification is a Full Release gate where specified.
- Current production-testing release predates this package and therefore must not be labelled Full Release even when its current smoke tests pass.

## Existing work that maps to v2 but still needs acceptance evidence

- Tenant/outlet/capability/role foundations → `BR-CORE-01..04` partial evidence.
- Sprint 2 server quote + idempotent cash recovery → `BR-CORE-06`, `BR-CORE-19` partial evidence.
- QRIS attempt persistence/webhook dedupe/attention queue → `BR-CORE-19`, `BR-CORE-26` partial evidence; provider reconciliation/certification remains open.
- Fashion variants/pricing/return records → `BR-FAS-*` partial evidence; full refund/exchange/disposition acceptance remains to be traced story-by-story.
- Restaurant table/order/kitchen/close primitives → `BR-FNB-*` partial evidence; guest QR, split/merge, recipe/waste/course and hardware routing are not implied complete.
- Laundry work order/status/payment primitives → `BR-LAU-*` partial evidence; custody/courier/deposit/reweigh/complaint flows require separate acceptance.
- Salon/barber currently has business capability only; operational appointment/public booking baseline cannot be marked implemented.

## Implementation order from v2

`EX00` is treated as rebaseline/audit, not as the old repo sprint number. Execution follows BR dependencies from the v2 backlog. Source already satisfying acceptance may be marked verified with SHA + environment + test evidence instead of rewritten.

### First aligned engineering slice: BR-CORE-14 / NF-API-01

Started 2026-09-28 because the API error contract is a dependency for later state/recovery flows.

Target from v2:
- target response envelope `{data,meta:{correlationId,asOf,...}}` for v2;
- target error `{code,message,correlationId,details:[...]}`;
- actionable 400/401/403/409/422/429/503 handling without stack/secret leakage;
- additive `/api/v2/context` compatibility adapter; legacy endpoint remains available;
- one FE error interpretation for login/review/refresh/retry/admin actions.

Source work in this slice:
- canonical backend error DTO/writer and correlation response header;
- exception, tenant status, restricted operator, shared POS and JWT auth challenge paths use the same error contract;
- `[ApiController]` validation returns canonical field details;
- additive authenticated `GET /api/v2/context` returns v2 envelope without modifying legacy `/api/tenant/context`;
- restricted kitchen/laundry operator route boundary allows the v2 context read;
- FE tenant context consumes `/api/v2/context`; shared helper maps status to user action and preserves correlation reference;
- tests added for v2 context envelope, unauthenticated no-stack error, capability/validation contract, and ambiguous QRIS 503 correlation.

This slice does **not** make every existing v1 endpoint structurally identical to target OpenAPI, implement `jobGet`, finish cookie/CSRF migration, or complete `BR-CORE-35` contract-test generation. Those remain follow-up work under their v2 dependencies.

## Release discipline

Do not update product status to Full Release from source compilation alone. Story status moves from UNVERIFIED only with the v2 QA evidence fields: environment, FE/BE SHA, migration/seed, actor/role, request/correlation ID, expected/actual and reviewer. Provider/device stories additionally require the specified certification evidence.
