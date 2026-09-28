# NeverFade Sprint 2 — production test account & category matrix

Snapshot: 2026-09-26 WIB. Production test environment is `https://neverfade.dualangka.com`. This document intentionally does **not** store plaintext passwords. Read-only production DB snapshot at creation: five active tenants and seven active tenant user accounts. All five tenants have `Mode=live`; the payment runtime has Live provider enabled. Use only approved, controlled test payments; never assume a test tenant is automatically sandbox.

## Real account inventory

| Category / system type | Tenant slug | Actual active username(s) | Available role(s) | Existing fixtures (snapshot) |
| --- | --- | --- | --- | --- |
| Restaurant/café/warung (`food_beverage`) | `warung-lumpia-beef` | `owner`, `admin`, `kasir` | owner/admin/kasir | 11 products, 3 customers, 1 table, 1 open restaurant order; prior transactions |
| Minimarket/general retail (`general_retail`) | `nf-test-general-20260920` | `test_general` | owner | Empty: 0 products/customers/transactions |
| Fashion/butik/distro (`fashion_retail`) | `nf-test-fashion-20260920` | `test_fashion` | owner | Empty: 0 products/variants/price levels/returns |
| Laundry (`laundry`) | `nf-test-laundry-20260920` | `test_laundry` | owner | Empty: 0 products/customers/work orders |
| Salon and Barbershop (`salon_barbershop`) | `nf-test-salon-20260920` | `test_salon` | owner | Empty: 0 services/customers/transactions |

**Not yet provisioned:** dedicated `dapur`, `laundry_operator`, and retail/fashion/laundry/salon cashier accounts. Restaurant is the only tenant with a dedicated cashier. Do not claim those role journeys PASS from an owner-only login. Salon appointment has a registered capability but no operational appointment UI in this release; barbershop uses the same salon business type, not an isolated tenant.

## Category UAT owner/cashier coverage

| ID | Account(s) | Test flow and acceptance signal |
| --- | --- | --- |
| R1 | F&B `owner` | Products/menu, prices/stock, table assignment, dashboard, transaction detail, reporting, customer, staff/outlet and owner-only finance. Verify links never expose another tenant. |
| R2 | F&B `kasir` | Table-based dine-in order (check current one-table fixture), add/edit items, send to kitchen, pay exact cash, QRIS only under approved Live-mode test, receipt/transaction history. Record order number, cashier, table status, payment and stock. |
| R3 | F&B `admin` | Management vs cashier restrictions, reports, owner-only finance denied; `Status Pembayaran` read-only exception list, no cancel/re-charge buttons. |
| R4 | F&B `dapur` | **BLOCKED** until a separate kitchen operator account and a properly sent order exist. Verify incoming order and state progression without cashier/finance access. |
| M1 | `test_general` | Create one SKU + stock/barcode + customer, cash sale with S2 quote/prepare/commit, receipt/change, stock/history once, repeated click/reload recovery, reports. No fashion returns/variants expectations in this mode. |
| F1 | `test_fashion` | Create product and size/color variants, stock per variant, price levels (e.g., retail/wholesale), checkout at chosen level, change/receipt, inventory, return/exchange linked to original sale. Also verify S2 original-key recovery and owner payment exception view. |
| L1 | `test_laundry` | Create customer and work order; received → in_progress → ready → completed; quantity/weight, pickup and payment, receipt and reporting. Operator-only queue remains **BLOCKED** without `laundry_operator` login. |
| S1 | `test_salon` | Create service/customer, service checkout, cash/controlled QRIS, receipt and reports. Booking/calendar/appointment workflow = **NOT IMPLEMENTED**, not PASS. Repeat service checkout in barbershop-themed tenant/demo after provision if distinct presentation is required. |

## Cross-cutting release gates

- Record `PASS / FAIL / BLOCKED` per case and capture timestamp, username, tenant/outlet, transaction/order ID, before/after stock, and redacted screenshots. Do not share passwords, card/QR secrets or JWTs in UAT evidence.
- Basic F&B `kasir` cash/QRIS smoke confirms legacy payment flow, **not** the new S2 quote-backed retail/fashion cash flow. Prioritize M1 and F1 after fixture setup.
- Verify old QRIS reference is reused after refresh and no extra charge is created. The QRIS path remains the existing provider implementation and Live mode; do not mistake it for a new Sprint 2 provider reconciliation feature.
- Source mocked tests are green, but actual PostgreSQL cash-vs-QRIS stock races and provider settlement UAT remain open; keep the PRs Draft until those financial gates pass.
- Before user UAT on an empty test tenant, provision **test-only fixtures and role accounts**, recording exact credentials securely outside this repository. Do not use real merchants' data or modify existing F&B fixtures unnecessarily.
