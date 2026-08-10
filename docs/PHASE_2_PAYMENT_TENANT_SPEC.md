# NeverFade POS — Phase 2 Payment & Tenant Specification

Status: DRAFT / APPROVED CONCEPT
Owner: NeverFade POS
Purpose: Product + Technical Source of Truth
Audience: Product, Backend, Frontend, QA, Codex / AI Developer

---

# 1. Purpose

Dokumen ini menjadi source of truth untuk Phase 2 NeverFade POS:

1. Payment Gateway Integration untuk transaksi POS non-tunai.
2. Merchant Balance dan Pencairan Dana.
3. Tenant Management.
4. Super Admin / Platform Administration.
5. Payment account isolation per tenant.

Developer dan AI coding agent WAJIB membaca dokumen ini sebelum mengimplementasikan fitur terkait.

Jika implementasi existing bertentangan dengan dokumen ini:

1. Jangan mengubah requirement secara sepihak.
2. Jangan membuat asumsi bisnis baru.
3. Laporkan gap terlebih dahulu.
4. Gunakan dokumen ini sebagai product intent.
5. CONTRACT.md existing tetap menjadi source of truth untuk contract backend yang sudah frozen sampai contract Phase 2 resmi ditambahkan.

---

# 2. Product Context

NeverFade POS adalah sistem POS multi-tenant. Setiap tenant merupakan merchant / bisnis terpisah. Data tenant tidak boleh bercampur.

Existing tenant roles: `owner`, `admin`, `kasir`.

Phase 2 memperkenalkan platform-level role `superadmin`. `superadmin` BUKAN bagian dari tenant.

---

# 3. Approved Architecture Principles

## 3.1 NeverFade Tidak Menjadi Custodian Dana

NeverFade tidak membuat sistem wallet internal yang benar-benar menyimpan dana merchant. Dana transaksi non-tunai harus berada pada payment gateway / payment provider yang digunakan NeverFade.

NeverFade hanya membuat payment, menerima payment status, menampilkan balance, menyimpan payment ledger/reference, melakukan reconciliation, mengirim instruksi payout apabila provider mendukung, dan menampilkan payout history. Actual custody, settlement, dan transfer dana tetap dilakukan oleh payment provider.

## 3.2 Payment Account Harus Terisolasi per Tenant

Target architecture:

```text
Payment Gateway Platform Account
|-- Tenant A payment account
|-- Tenant B payment account
|-- Tenant C payment account
```

Setiap tenant harus mempunyai identifier payment account sendiri, misalnya conceptual field `paymentAccountId`. NeverFade tidak boleh memiliki satu saldo global lalu menghitung manual kepemilikan saldo tenant jika provider mendukung sub-account / connected merchant / managed account equivalent. Nama teknis provider-specific ditentukan setelah payment gateway final dikonfirmasi.

## 3.3 Cash Tidak Masuk Payment Gateway Balance

Transaksi tunai tidak membuat payment gateway transaction. Transaksi tetap tercatat sebagai penjualan POS dan masuk laporan, tetapi tidak menambah pending balance, available balance, atau payment gateway balance.

## 3.4 Non-Cash Menggunakan Payment Gateway

Initial target: QRIS dan metode non-tunai lain yang didukung provider.

```text
Customer → Kasir → Pilih Non-Tunai → Create Payment → Customer membayar
→ Payment Provider → Webhook / payment confirmation → Transaction PAID
→ Settlement → Balance tersedia → Payout ke rekening tenant
```

Payment method final mengikuti kemampuan provider. Jangan membuat metode provider-specific sebelum provider diaudit.

---

# 4. Payment State Model

Status POS Transaction dan Payment harus terpisah.

## 4.1 POS Transaction

Conceptual: `pending_payment`, `paid`, `failed`, `cancelled`. Final naming mengikuti backend contract saat implementasi.

## 4.2 Payment

Conceptual: `pending`, `paid`, `failed`, `expired`, `cancelled`. Provider status dimapping ke internal NeverFade status. UI tidak boleh bergantung langsung pada string status provider.

---

# 5. Payment Confirmation

Frontend response setelah create-payment bukan authoritative confirmation. Authoritative confirmation berasal dari mekanisme provider verified, terutama webhook / server-side verification.

Webhook harus authenticated/signature verified sesuai provider, idempotent, aman untuk duplicate callback, dan tidak menggandakan transaction, settlement, atau balance ledger.

---

# 6. Financial Balance Concept

Menu tenant: **Keuangan**.

- Pending Balance: dana transaksi sukses yang belum tersedia untuk payout berdasarkan settlement provider.
- Available Balance: dana yang sudah dapat dicairkan menurut provider.

UI sebaiknya membaca provider balance jika API tersedia. Database NeverFade boleh menyimpan mirror/cache/ledger untuk reporting, reconciliation, references, dan audit, tetapi bukan source of truth actual custody bila provider menyediakan balance ledger.

---

# 7. Tenant Financial UI

Recommended navigation: **Keuangan**, bukan menu utama hanya "Withdraw".

Initial page:

- Balance: Saldo Tersedia, Saldo Pending, Total Pencairan.
- Revenue Breakdown: Tunai hari ini, Non-tunai hari ini. Cash revenue adalah POS information, bukan gateway balance.
- Actions: Tarik Dana hanya jika provider/merchant state memungkinkan.
- History: Pembayaran, Settlement, Pencairan.
- Bank Account: rekening masked, verification status, payout eligibility. Jangan tampilkan full sensitive bank credential.

---

# 8. Payout / Withdrawal

Terminologi UI: Tarik Dana / Pencairan. Internal: Payout.

Desain mendukung Automatic Payout dan Manual Payout; MVP bergantung provider. Manual flow harus memvalidasi available balance, meminta provider payout, dan memperbarui status dari provider/backend—tidak langsung mengurangi saldo berdasarkan frontend assumption.

Conceptual statuses: `requested`, `processing`, `succeeded`, `failed`, `cancelled`. Provider mapping TBD.

---

# 9. Platform Fee

Future-compatible architecture memungkinkan platform fee via split payments tanpa NeverFade menjadi custodian. Fee model belum final. Jangan implementasikan sebelum rate, provider capability, dan accounting flow diputuskan.

---

# 10. Tenant Management

Phase 2 memperkenalkan platform administration. Tenant tidak dibuat owner tenant lain, melainkan NeverFade Platform Admin / Super Admin. Initial onboarding: ADMIN-MANAGED ONBOARDING. Self-service signup bukan MVP.

---

# 11. Role Hierarchy

Platform `superadmin`: manage/create/activate/suspend tenant, provision initial owner, inspect platform tenant status.

Tenant: `owner`, `admin`, `kasir`.

Tenant roles tidak boleh mengakses tenant lain atau platform tenant management. Superadmin authorization harus dibedakan dari tenant roles.

---

# 12. Tenant Provisioning Flow

```text
Super Admin → Create Tenant → Generate Tenant ID → Create Initial Owner
→ Initialize Tenant Settings → Create / Link Payment Account
→ Merchant Verification / KYC → Link Settlement Bank Account
→ Payment Enabled → Payout Enabled → Tenant Operational
```

Provider steps dapat asynchronous. Tenant boleh tercipta saat payment setup belum selesai. Tenant status dan payment status harus terpisah.

Phase 2B provisioning decisions:

- newly provisioned tenant starts with tenant status `active`;
- cash POS operation is allowed immediately;
- gateway/non-cash payment remains disabled until the separate Phase 2C payment onboarding state is active;
- payout remains disabled until future payment onboarding and provider capability allow it;
- initial owner password is supplied by authenticated superadmin, validated and immediately hashed by the backend, and is never stored, logged, or returned as plaintext;
- no mandatory password-reset behavior is included in Phase 2B.

---

# 13. Tenant Status

Phase 2B lifecycle is frozen to exactly `active` and `suspended`. `inactive` is not a Phase 2B status.

A suspended tenant:

- cannot login;
- cannot use an already-issued tenant session;
- cannot access tenant business APIs;
- has no read-only POS mode.

Frontend must receive a stable machine-readable `TENANT_SUSPENDED` error distinct from invalid credentials and generic authorization failure. Hard delete is not a Phase 2B flow.

---

# 14. Payment Onboarding Status

Conceptual: `not_configured`, `onboarding`, `pending_verification`, `active`, `restricted`, `rejected`. Jangan gabungkan `tenant.status` dengan `paymentAccount.status`.

---

# 15. Conceptual Tenant Data

Existing schema wajib diaudit sebelum migration.

```text
Tenant: id, name, status(active|suspended), createdAt, updatedAt
TenantPaymentAccount: id, tenantId, provider, providerAccountId,
  onboardingStatus, paymentEnabled, payoutEnabled, createdAt, updatedAt
TenantBankAccount: providerReference, maskedAccountNumber, bankName,
  accountHolderName, verificationStatus
```

Sensitive provider credentials tidak disimpan plaintext. Platform credential berada di secure server configuration/secret manager.

---

# 16. Conceptual Payment Data

```text
Payment: id, tenantId, transactionId, provider, providerPaymentId,
  method, amount, status, createdAt, paidAt, expiredAt
PaymentEvent/WebhookEvent: providerEventId, tenantId, eventType,
  processingStatus, receivedAt
```

Provider event ID harus usable untuk idempotency.

---

# 17. Conceptual Payout Data

```text
Payout: id, tenantId, providerPayoutId, amount, status,
  destinationReference, requestedBy, requestedAt, completedAt, failureReason
```

Jangan simpan full sensitive bank data jika provider reference cukup.

---

# 18. Super Admin UI — MVP

Area superadmin harus terpisah secara authorization. Tenant listing minimum: Tenant Name/ID, Owner, Tenant Status, Payment Status, Payout Status, Created Date, Action. Actions: Create, View, Activate, Suspend.

Phase 2B create form contains business name and initial owner name/username/password only. Payment setup state is not part of the Phase 2B request or response. Subscription management bukan MVP.

Phase 2B contains no payment fields or payment API. Platform authentication uses a separate `PlatformUser`, authentication scheme, signing key, audience, and issuer. Platform bootstrap is an explicitly enabled, one-time operational process that is allowed only when no PlatformUser exists and never uses demo/default credentials.

---

# 19. Tenant Owner UI

Existing POS tetap tenant application. Navigation baru: **Keuangan**. Owner melihat balance, payment/payout history, payout bank account, dan meminta payout bila manual enabled. Admin/kasir permission TBD; payout default design owner-only, jangan berikan ke kasir.

---

# 20. Multi-Tenant Security Requirements

Semua payment resources server-side wajib tenant scoped. Never trust tenantId, paymentAccountId, atau transaction ownership dari frontend. Backend resolve tenant dari authenticated identity/context dan memastikan Payment, Payout, Balance, Transaction, PaymentAccount milik authenticated tenant. Cross-tenant access adalah RELEASE BLOCKER.

---

# 21. Amount Security

Frontend tidak authoritative untuk product price, subtotal, total, payment amount, available payout balance, atau payout eligibility. Payment amount berasal dari server-calculated transaction total. Payout amount divalidasi server-side terhadap provider/authoritative balance.

---

# 22. Idempotency

Mandatory untuk create payment, process webhook, create payout. Repeated requests tidak boleh double charge, transaction, payout, atau ledger entry. Exact implementation mengikuti provider capabilities.

---

# 23. Reconciliation

NeverFade harus mencocokkan POS Transaction ↔ Payment ↔ Provider Transaction ↔ Settlement ↔ Payout. Provider identifiers wajib dipertahankan; nominal/timestamp saja tidak cukup.

---

# 24. Error Handling

- Payment gagal tidak boleh membuat Transaction paid.
- Payment expired membuat transaction unpaid/expired sesuai final contract.
- Duplicate webhook tidak membuat duplicate state.
- Payout failure mengikuti provider actual state.
- Provider unavailable tidak merusak cash transaction.
- Gateway failure tenant satu tidak mempengaruhi tenant lain.

---

# 25. Observability / Audit

Log minimum: tenant, internal/provider payment ID, transaction ID, internal/provider payout ID, state transition, timestamp. Jangan log access token, API secret, full bank data, atau raw credentials.

Phase 2B additionally persists platform audit events `TENANT_PROVISIONED`, `TENANT_ACTIVATED`, and `TENANT_SUSPENDED`, with event ID, actor PlatformUser ID, tenant ID, event type, UTC timestamp, and optional safe metadata. Credentials and secrets are prohibited. No automatic audit retention/deletion policy is required in Phase 2B.

---

# 26. Provider Adapter Requirement

Business logic tidak tightly coupled ke provider. Target abstraction `PaymentProvider`: CreatePayment, GetPaymentStatus, GetBalance, CreatePayout, GetPayoutStatus, VerifyWebhook. Interface final setelah provider audit. Frontend tidak memanggil privileged provider API langsung.

---

# 27. Current Provider Decision

STATUS: PENDING. Sebelum coding payment, audit provider untuk platform/sub-account, onboarding/KYC, QRIS/payment/webhook, split/fee, balance pending/available, bank management, payout/schedule/webhook, sandbox, idempotency, reconciliation. Jangan implementasikan adapter sebelum audit selesai.

---

# 28. Out of Scope — Current Phase

Tidak termasuk self-registration tenant, subscription billing/plans, subscription suspension, lending/credit, internal wallet custody, inter-tenant/P2P transfer, custom GL, platform fee sebelum rule final, atau multi-provider routing.

---

# 29. Implementation Order

## PHASE 2A — Audit

1. Audit existing Tenant model.
2. Audit TenantId propagation.
3. Audit global tenant filters.
4. Audit authentication claims.
5. Audit owner/admin/kasir authorization.
6. Audit current transaction/payment fields.
7. Audit payment provider capability.

## PHASE 2B — Tenant Control Plane

8. Platform superadmin authentication.
9. Tenant CRUD/lifecycle.
10. Initial owner provisioning.
11. Tenant activate/suspend.
12. Tenant isolation QA.

## PHASE 2C — Payment Foundation

13. Provider adapter.
14. Payment account mapping per tenant.
15. Merchant onboarding/payment status.
16. Payment entity/state.
17. Webhook verification/idempotency.

## PHASE 2D — POS Payment

18. Kasir non-cash create payment.
19. QRIS/payment UI.
20. Pending/success/failure UX.
21. Transaction finalization from verified payment.
22. Payment regression QA.

## PHASE 2E — Finance

23. Balance API.
24. Keuangan page.
25. Payment history.
26. Settlement history if supported.
27. Payout flow/history.
28. Bank destination status.

## PHASE 2F — Final QA

Cross-tenant, financial manipulation, duplicate webhook/payout, payment failure, frontend/backend regression, Docker/runtime, security/dependency scan, independent QA.

---

# 30. Definition of Done

Phase 2 requires provisioned tenant and owner login, verified tenant isolation, isolated payment account mapping, end-to-end non-cash payment with server verification and duplicate-webhook safety, unaffected cash, server-calculated amounts, correct tenant balance isolation, payout safety if included, audit references, no leaked secrets, green frontend/backend/Docker/security gates, and independent QA PASS.

---

# 31. AI Developer Rules

DO: inspect code, CONTRACT.md, dan spec; preserve tenant isolation; implement unit kecil; build/test targeted; use real contracts; report conflicts.

DO NOT: invent provider endpoints/statuses/fees/KYC; create wallet custody; trust frontend tenantId/payment amount; mark paid from redirect; expose provider secret; bypass webhook verification; merge unrelated refactor; expand scope.

---

# 32. Pending Product Decisions

1. Exact provider capability.
2. QRIS/additional MVP methods.
3. Automatic/manual payout.
4. Minimum payout.
5. Payout fee.
6. Platform fee.
7. Owner/admin payout permission.
8. Post-Phase-2B tenant lifecycle expansion, if any. Phase 2B suspension behavior is approved and frozen.
9. KYC UX.
10. Cash-only before verification.
11. SaaS subscription model.

Open decisions tidak boleh diselesaikan diam-diam.

The following Phase 2B decisions are no longer open: lifecycle is only `active|suspended`; new tenants start active; suspension rejects login and all existing-session business access; JWT cutover requires re-login; platform JWT security configuration is fully separate; bootstrap is explicit one-time environment-controlled; initial owner password is supplied by superadmin; and lifecycle audit events are persisted.

---

# 33. Product Decision Summary

APPROVED: multi-tenant; platform superadmin admin-managed onboarding; isolated tenant payment account; no NeverFade custody; non-cash via provider and cash outside balance; menu Keuangan; pending/available balance; Tarik Dana/Pencairan; webhook/server verification authoritative; payment resources tenant scoped; tenant management connected to payment onboarding; provider implementation waits for capability audit.

END OF SPECIFICATION
