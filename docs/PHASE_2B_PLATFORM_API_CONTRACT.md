# NeverFade POS — Phase 2B Platform API Contract

Status: FROZEN FOR PHASE 2B IMPLEMENTATION

Scope: Platform authentication, tenant provisioning, tenant lifecycle, tenant isolation, and platform lifecycle audit only.

This contract supplements but does not modify `CONTRACT.md`. Existing tenant endpoint routes, payloads, and role behavior remain frozen. Phase 2B contains no payment, payout, balance, provider, hard-delete, tenant impersonation, or tenant-business browsing endpoint.

---

## 1. Conventions

### 1.1 Transport and serialization

- Route prefix: `/api/platform`.
- Content type: `application/json` unless an endpoint has no body.
- JSON property casing: `camelCase`, matching existing NeverFade conventions.
- IDs: UUID/Guid serialized as canonical strings.
- Timestamps: UTC ISO-8601 strings, for example `2026-08-10T08:30:00Z`.
- Success bodies are direct DTOs or arrays. No response envelope is introduced.
- Create and lifecycle POST endpoints return `200 OK`, matching existing NeverFade controller conventions.
- Unknown JSON properties do not grant behavior. Security-sensitive values such as `tenantId`, role, status, or audit actor are always server-derived.

### 1.2 Authentication schemes and policies

| Name | Requirement |
|---|---|
| `TenantBearer` | Existing tenant issuer/key/audience, `scope=tenant`, required valid `tenant_id`, role `owner|admin|kasir` |
| `PlatformBearer` | Separate platform issuer, signing key, and audience; `scope=platform`; no `tenant_id`; role `superadmin` |
| `PlatformSuperAdmin` | Authenticated through `PlatformBearer`, active PlatformUser, valid platform scope and role |

The two schemes use completely separate issuers, audiences, and signing keys. A token accepted by one scheme must be rejected by the other.

Old tenant tokens without `scope=tenant` are rejected after deployment. Mandatory re-login is required; there is no compatibility mode.

### 1.3 Platform error DTO

New Phase 2B platform errors and the cross-cutting tenant-suspension error use this direct body:

```json
{
  "code": "TENANT_NOT_FOUND",
  "message": "Tenant tidak ditemukan."
}
```

This is not an envelope around another payload. `code` is a stable machine-readable identifier; `message` is user-readable and may be localized in a future contract. Existing tenant errors unrelated to suspension are not changed by Phase 2B.

ASP.NET automatic model-validation details may include its existing validation response structure, but every service/domain validation listed in this contract must use the specified status and stable code.

### 1.4 Common platform errors

| HTTP | Code | Meaning |
|---:|---|---|
| 400 | `VALIDATION_ERROR` | Request is syntactically valid JSON but violates documented field rules |
| 401 | `PLATFORM_AUTHENTICATION_REQUIRED` | Platform token missing, invalid, expired, wrong issuer/audience/signature, or wrong authentication scheme |
| 403 | `PLATFORM_FORBIDDEN` | Authenticated identity does not satisfy `PlatformSuperAdmin` |
| 404 | `TENANT_NOT_FOUND` | Tenant ID is valid but no tenant exists |
| 409 | `TENANT_ALREADY_ACTIVE` | Activate requested for active tenant |
| 409 | `TENANT_ALREADY_SUSPENDED` | Suspend requested for suspended tenant |
| 409 | `TENANT_SLUG_CONFLICT` | Generated slug still violates the unique constraint due to a race/collision |
| 409 | `OWNER_USERNAME_CONFLICT` | Initial owner username already exists in tenant `users` |
| 500 | `INTERNAL_ERROR` | Unexpected server failure; no internal detail or secret returned |

Malformed route GUIDs do not match the `{tenantId:guid}` route and result in the framework's normal `404` behavior.

### 1.5 Tenant-suspended error

Tenant suspension is checked against current database state during tenant login and every authenticated tenant request. It is not derived only from JWT issuance time.

Suspended tenant login or business API access returns:

- HTTP `403 Forbidden`;
- code `TENANT_SUSPENDED`;
- body:

```json
{
  "code": "TENANT_SUSPENDED",
  "message": "Tenant sedang ditangguhkan."
}
```

This distinguishes suspension from invalid tenant login credentials (`401` under the existing tenant contract) and generic authorization failure (`403` without this code). No tenant POS read-only mode exists.

---

## 2. Frozen DTOs

### 2.1 PlatformLoginRequestDto

```json
{
  "username": "platform.admin",
  "password": "secret-value"
}
```

| Field | Type | Rules |
|---|---|---|
| `username` | string | required; trim before lookup; 1–100 characters after trim |
| `password` | string | required; 1–100 characters; not trimmed or normalized |

### 2.2 PlatformUserDto

```json
{
  "id": "0cfcaf65-e919-4488-9a8b-8c0c8f84fb0c",
  "nama": "NeverFade Platform Admin",
  "username": "platform.admin",
  "role": "superadmin"
}
```

`password` and `passwordHash` are never included.

### 2.3 PlatformLoginResponseDto

```json
{
  "token": "<platform-jwt>",
  "user": {
    "id": "0cfcaf65-e919-4488-9a8b-8c0c8f84fb0c",
    "nama": "NeverFade Platform Admin",
    "username": "platform.admin",
    "role": "superadmin"
  }
}
```

### 2.4 TenantOwnerSummaryDto

```json
{
  "id": "b726a763-814e-441d-b4de-89158653bb26",
  "nama": "Owner Bakso A",
  "username": "owner.bakso.a",
  "active": true
}
```

This is one sanitized owner identity summary, not a tenant user list. For newly provisioned tenants it is the initial owner. For pre-Phase-2B tenants, the implementation selects the earliest `owner` by `createdAt`, then `id`, through an explicit tenant execution scope. It is `null` only for legacy-invalid data where no owner exists. Platform APIs cannot use this projection to browse other tenant users.

### 2.5 PlatformTenantDto

```json
{
  "id": "1f62ba37-d354-48f6-832f-2798a46aa027",
  "namaToko": "Bakso A",
  "slug": "bakso-a",
  "status": "active",
  "owner": {
    "id": "b726a763-814e-441d-b4de-89158653bb26",
    "nama": "Owner Bakso A",
    "username": "owner.bakso.a",
    "active": true
  },
  "createdAt": "2026-08-10T08:30:00Z",
  "updatedAt": "2026-08-10T08:30:00Z"
}
```

`status` is exactly `active` or `suspended`. This DTO never includes products, customers, transactions, a user list, payments, balances, history, or payouts.

### 2.6 CreatePlatformTenantRequestDto

```json
{
  "namaToko": "Bakso A",
  "owner": {
    "nama": "Owner Bakso A",
    "username": "owner.bakso.a",
    "password": "initial-secret"
  }
}
```

Rules:

| Field | Type | Rules |
|---|---|---|
| `namaToko` | string | required; trimmed; 1–200 characters |
| `owner` | object | required |
| `owner.nama` | string | required; trimmed; 1–200 characters |
| `owner.username` | string | required; trimmed; 1–100 characters; globally unique under existing contract |
| `owner.password` | string | required; 8–100 characters; whitespace-only rejected; preserved exactly for hashing |

The request must not contain TenantId, slug, owner role, platform role, lifecycle status, payment state, or payout state. Presence of any of those security-sensitive properties at any casing is rejected with `400 VALIDATION_ERROR`; it is not silently ignored. Tenant ID is generated server-side, role is forced to `owner`, and status is forced to `active`.

The password minimum of eight characters is frozen for new platform provisioning only; it does not silently alter existing tenant user endpoints.

### 2.7 SuspendPlatformTenantRequestDto

```json
{
  "reason": "Pelanggaran kebijakan operasional"
}
```

`reason` is optional. When supplied it is trimmed, must contain 1–500 characters after trim, must be non-sensitive, and is stored only as safe audit metadata. Omitted or JSON `null` becomes no reason. Credentials, tokens, bank data, or secrets are forbidden as reasons.

Activate has no request body.

---

## 3. Slug contract

Repository inspection found `Tenant.Slug`, a required unique database index, and exposure in the frozen tenant entity contract, but no current runtime routing or service use. Phase 2B therefore generates slug server-side and does not accept it from the client.

Generation is deterministic:

1. trim `namaToko`;
2. convert to lowercase invariant;
3. transliterate Unicode letters to their basic Latin representation where possible;
4. replace each run of characters other than ASCII `a-z` or `0-9` with one `-`;
5. trim leading/trailing `-`;
6. use `tenant` if the result is empty;
7. truncate the base as needed to fit the existing 100-character limit;
8. if the base already exists, append `-` plus the new tenant Guid in lowercase `N` format, truncating the base so the final value remains at most 100 characters.

The unique database constraint remains authoritative. A residual race/collision returns `409 TENANT_SLUG_CONFLICT` and provisioning rolls back atomically.

---

## 4. Endpoint contracts

## 4.1 POST `/api/platform/auth/login`

Purpose: authenticate a PlatformUser and issue a platform JWT.

- Authorization: anonymous; platform login endpoint only.
- Request: `PlatformLoginRequestDto`.
- Success: `200 OK`, `PlatformLoginResponseDto`.
- Not found: not exposed separately; unknown username maps to invalid credentials.
- Tenant suspension: not applicable.

Failures:

| HTTP | Code | Condition |
|---:|---|---|
| 400 | `VALIDATION_ERROR` | Missing/invalid username or password shape |
| 401 | `PLATFORM_INVALID_CREDENTIALS` | Username not found or password mismatch |
| 403 | `PLATFORM_USER_INACTIVE` | Credentials valid but PlatformUser is inactive |

Platform JWT claims:

```json
{
  "sub": "0cfcaf65-e919-4488-9a8b-8c0c8f84fb0c",
  "scope": "platform",
  "role": "superadmin",
  "username": "platform.admin",
  "nama": "NeverFade Platform Admin"
}
```

There is no `tenant_id` claim.

Example request:

```http
POST /api/platform/auth/login
Content-Type: application/json

{"username":"platform.admin","password":"secret-value"}
```

Example response:

```http
HTTP/1.1 200 OK
Content-Type: application/json

{"token":"<platform-jwt>","user":{"id":"0cfcaf65-e919-4488-9a8b-8c0c8f84fb0c","nama":"NeverFade Platform Admin","username":"platform.admin","role":"superadmin"}}
```

Invalid-credential response:

```json
{"code":"PLATFORM_INVALID_CREDENTIALS","message":"Username atau password salah."}
```

## 4.2 GET `/api/platform/auth/me`

Purpose: return the current sanitized PlatformUser.

- Authorization: `PlatformBearer` + `PlatformSuperAdmin`.
- Request DTO: none.
- Success: `200 OK`, `PlatformUserDto`.
- Not found/inactive after token issuance: `403 PLATFORM_USER_INACTIVE`.
- Tenant suspension: not applicable.

Failures:

| HTTP | Code | Condition |
|---:|---|---|
| 401 | `PLATFORM_AUTHENTICATION_REQUIRED` | Missing/invalid/expired/wrong-scheme token |
| 403 | `PLATFORM_FORBIDDEN` | Valid authentication does not satisfy platform scope/role |
| 403 | `PLATFORM_USER_INACTIVE` | PlatformUser no longer exists or is inactive |

Example request:

```http
GET /api/platform/auth/me
Authorization: Bearer <platform-jwt>
```

Example response:

```json
{"id":"0cfcaf65-e919-4488-9a8b-8c0c8f84fb0c","nama":"NeverFade Platform Admin","username":"platform.admin","role":"superadmin"}
```

## 4.3 GET `/api/platform/tenants`

Purpose: list control-plane tenant summaries.

- Authorization: `PlatformBearer` + `PlatformSuperAdmin`.
- Request DTO/query parameters: none in Phase 2B.
- Success: `200 OK`, direct JSON array of `PlatformTenantDto` ordered by `createdAt` descending, then `id` ascending.
- Empty result: `200 OK` with `[]`.
- Not found/conflict/tenant suspension: not applicable.
- Owner projection is the narrow summary defined in section 2.4; this endpoint does not expose a user list.

Failures: common platform `401`, `403`, and `500` only.

Example request:

```http
GET /api/platform/tenants
Authorization: Bearer <platform-jwt>
```

Example response:

```json
[
  {
    "id":"1f62ba37-d354-48f6-832f-2798a46aa027",
    "namaToko":"Bakso A",
    "slug":"bakso-a",
    "status":"active",
    "owner":{"id":"b726a763-814e-441d-b4de-89158653bb26","nama":"Owner Bakso A","username":"owner.bakso.a","active":true},
    "createdAt":"2026-08-10T08:30:00Z",
    "updatedAt":"2026-08-10T08:30:00Z"
  }
]
```

## 4.4 GET `/api/platform/tenants/{tenantId}`

Purpose: return one control-plane tenant detail. Phase 2B uses the same `PlatformTenantDto` as the list.

- Authorization: `PlatformBearer` + `PlatformSuperAdmin`.
- Path: `tenantId`, required Guid.
- Request DTO: none.
- Success: `200 OK`, `PlatformTenantDto`.
- Not found: `404 TENANT_NOT_FOUND`.
- Conflict/tenant suspension: not applicable; platform may inspect a suspended tenant's control-plane record.

Failures: common platform `401`, `403`, `404`, and `500`.

Example request:

```http
GET /api/platform/tenants/1f62ba37-d354-48f6-832f-2798a46aa027
Authorization: Bearer <platform-jwt>
```

Example response:

```json
{"id":"1f62ba37-d354-48f6-832f-2798a46aa027","namaToko":"Bakso A","slug":"bakso-a","status":"active","owner":{"id":"b726a763-814e-441d-b4de-89158653bb26","nama":"Owner Bakso A","username":"owner.bakso.a","active":true},"createdAt":"2026-08-10T08:30:00Z","updatedAt":"2026-08-10T08:30:00Z"}
```

Not-found response:

```json
{"code":"TENANT_NOT_FOUND","message":"Tenant tidak ditemukan."}
```

## 4.5 POST `/api/platform/tenants`

Purpose: atomically create an active tenant, its initial owner, initial settings, and one audit event.

- Authorization: `PlatformBearer` + `PlatformSuperAdmin`.
- Request: `CreatePlatformTenantRequestDto`.
- Success: `200 OK`, sanitized `PlatformTenantDto`.
- Server-generated: Tenant ID, slug, owner role=`owner`, tenant status=`active`, timestamps.
- Side effects in one database transaction: insert Tenant; insert initial tenant User; insert Settings; insert exactly one `TENANT_PROVISIONED` PlatformAuditEvent.
- Payment side effects: none.

Initial settings are neutral and contain no demo/business-specific data:

```json
{
  "namaToko": "<provisioned namaToko>",
  "alamat": "",
  "telepon": "",
  "email": "",
  "website": "",
  "headerStruk": "",
  "footerStruk": "",
  "showTax": false,
  "showPoint": false,
  "defaultTax": 0,
  "minStok": 0,
  "poinRate": 0
}
```

No demo products, customers, employees, transactions, or other business data are seeded.

Failures:

| HTTP | Code | Condition |
|---:|---|---|
| 400 | `VALIDATION_ERROR` | Any documented field rule fails |
| 401 | `PLATFORM_AUTHENTICATION_REQUIRED` | Authentication failure |
| 403 | `PLATFORM_FORBIDDEN` | Platform authorization failure |
| 409 | `OWNER_USERNAME_CONFLICT` | Global tenant username already exists |
| 409 | `TENANT_SLUG_CONFLICT` | Residual generated-slug unique conflict |

Any failure rolls back Tenant, owner, Settings, and audit event. Plaintext password must not appear in logs, telemetry, exception messages, audit metadata, response, or persisted columns.

Example request:

```http
POST /api/platform/tenants
Authorization: Bearer <platform-jwt>
Content-Type: application/json

{"namaToko":"Bakso A","owner":{"nama":"Owner Bakso A","username":"owner.bakso.a","password":"initial-secret"}}
```

Example response:

```json
{"id":"1f62ba37-d354-48f6-832f-2798a46aa027","namaToko":"Bakso A","slug":"bakso-a","status":"active","owner":{"id":"b726a763-814e-441d-b4de-89158653bb26","nama":"Owner Bakso A","username":"owner.bakso.a","active":true},"createdAt":"2026-08-10T08:30:00Z","updatedAt":"2026-08-10T08:30:00Z"}
```

Conflict response:

```json
{"code":"OWNER_USERNAME_CONFLICT","message":"Username owner sudah digunakan."}
```

The new tenant can immediately use existing cash POS flows. No gateway/non-cash payment or payout capability is implied or returned.

## 4.6 POST `/api/platform/tenants/{tenantId}/activate`

Purpose: transition `suspended → active` and persist its audit event.

- Authorization: `PlatformBearer` + `PlatformSuperAdmin`.
- Path: `tenantId`, required Guid.
- Request DTO/body: none.
- Success: `200 OK`, updated `PlatformTenantDto`.
- Audit: exactly one `TENANT_ACTIVATED` event with actor and tenant, in the same database transaction as status change.

Failures:

| HTTP | Code | Condition |
|---:|---|---|
| 401 | `PLATFORM_AUTHENTICATION_REQUIRED` | Authentication failure |
| 403 | `PLATFORM_FORBIDDEN` | Authorization failure |
| 404 | `TENANT_NOT_FOUND` | Tenant does not exist |
| 409 | `TENANT_ALREADY_ACTIVE` | Tenant is already active; no audit event created |

Example request:

```http
POST /api/platform/tenants/1f62ba37-d354-48f6-832f-2798a46aa027/activate
Authorization: Bearer <platform-jwt>
```

Example response:

```json
{"id":"1f62ba37-d354-48f6-832f-2798a46aa027","namaToko":"Bakso A","slug":"bakso-a","status":"active","owner":{"id":"b726a763-814e-441d-b4de-89158653bb26","nama":"Owner Bakso A","username":"owner.bakso.a","active":true},"createdAt":"2026-08-10T08:30:00Z","updatedAt":"2026-08-10T09:15:00Z"}
```

Conflict response:

```json
{"code":"TENANT_ALREADY_ACTIVE","message":"Tenant sudah aktif."}
```

## 4.7 POST `/api/platform/tenants/{tenantId}/suspend`

Purpose: transition `active → suspended`, persist the audit event, and immediately deny tenant data-plane access.

- Authorization: `PlatformBearer` + `PlatformSuperAdmin`.
- Path: `tenantId`, required Guid.
- Request: optional `SuspendPlatformTenantRequestDto`; omitted body is equivalent to `reason=null`.
- Success: `200 OK`, updated `PlatformTenantDto`.
- Audit: exactly one `TENANT_SUSPENDED` event in the same transaction; trimmed reason is optional safe metadata.
- Session effect: existing tenant JWTs are rejected on their next request because current tenant status is centrally checked for every tenant data-plane request.

Failures:

| HTTP | Code | Condition |
|---:|---|---|
| 400 | `VALIDATION_ERROR` | Reason is empty after trim, over 500 characters, or rejected as unsafe input |
| 401 | `PLATFORM_AUTHENTICATION_REQUIRED` | Authentication failure |
| 403 | `PLATFORM_FORBIDDEN` | Authorization failure |
| 404 | `TENANT_NOT_FOUND` | Tenant does not exist |
| 409 | `TENANT_ALREADY_SUSPENDED` | Tenant already suspended; no audit event created |

Example request:

```http
POST /api/platform/tenants/1f62ba37-d354-48f6-832f-2798a46aa027/suspend
Authorization: Bearer <platform-jwt>
Content-Type: application/json

{"reason":"Pelanggaran kebijakan operasional"}
```

Example response:

```json
{"id":"1f62ba37-d354-48f6-832f-2798a46aa027","namaToko":"Bakso A","slug":"bakso-a","status":"suspended","owner":{"id":"b726a763-814e-441d-b4de-89158653bb26","nama":"Owner Bakso A","username":"owner.bakso.a","active":true},"createdAt":"2026-08-10T08:30:00Z","updatedAt":"2026-08-10T09:30:00Z"}
```

Conflict response:

```json
{"code":"TENANT_ALREADY_SUSPENDED","message":"Tenant sudah ditangguhkan."}
```

---

## 5. Suspension enforcement contract

Suspension must be enforced centrally on both authentication and authenticated requests:

1. tenant login resolves the tenant user without opening general business access, verifies the password, loads the owning Tenant, and returns `403 TENANT_SUSPENDED` when status is suspended;
2. `TenantBearer` validation establishes identity only when `scope=tenant`, valid `tenant_id`, and tenant role are present;
3. a tenant-status authorization handler/middleware loads current Tenant status for every authenticated tenant request;
4. suspended state returns `403 TENANT_SUSPENDED` before controller/business service execution;
5. query filters remain fail-closed independently, so a middleware defect cannot turn missing context into global access;
6. platform routes do not use this tenant-status policy; superadmin can inspect and reactivate suspended control-plane records;
7. activation permits subsequent tenant login/API access but does not issue a token automatically.

Tenant status must not be accepted from JWT or client input as authoritative. No token-revocation table is required for Phase 2B because every tenant request checks current tenant status.

---

## 6. PlatformUser bootstrap contract

Bootstrap is an explicit operational startup action, not a public endpoint and not normal seed data.

Proposed environment configuration names:

```text
PlatformBootstrap__Enabled=true|false
PlatformBootstrap__Nama=<initial administrator name>
PlatformBootstrap__Username=<initial administrator username>
PlatformBootstrap__Password=<initial administrator password>
```

Rules:

1. default/missing `PlatformBootstrap__Enabled` is disabled;
2. when disabled, no PlatformUser bootstrap attempt occurs;
3. when enabled and no PlatformUser exists, all required values must be present and pass PlatformUser field/password validation;
4. password is hashed immediately with BCrypt and never logged or persisted as plaintext;
5. exactly one active `superadmin` is created transactionally;
6. when any PlatformUser already exists, bootstrap creates nothing, does not compare/update credentials, and emits only a non-secret operational warning;
7. concurrent startup is protected by database uniqueness/transaction handling so at most one bootstrap user is created;
8. no default name, username, or password exists in source, config files, migration, or seed;
9. operators must remove/disable bootstrap configuration after successful creation;
10. no bootstrap endpoint is exposed;
11. production startup may run the mechanism safely under the same rules; secrets come only from secure runtime environment configuration.

Recovery/rotation after the initial user exists is not performed by bootstrap and requires a separate approved operational procedure.

---

## 7. Frozen persistence/domain contract

No migration is created by this document. Future migrations must be non-destructive and preserve existing production data.

### 7.1 Tenant additions

Existing fields remain unchanged. Add:

| Field | Type | Null | Rule |
|---|---|---:|---|
| `Status` | string, max 20 | no | exactly `active` or `suspended`; default/backfill `active` |
| `UpdatedAt` | UTC timestamp | no | initialized from existing `CreatedAt`; updated on lifecycle/name changes |

Constraints/indexes:

- existing unique `Slug` remains;
- database check constraint permits only `active` and `suspended`;
- index on `Status` for platform lifecycle listing/filter readiness;
- no `inactive` value;
- no payment fields.

Relationships and deletion:

- existing Tenant relationships remain;
- tenant hard delete is not exposed;
- existing cascade behaviors must not be invoked by Phase 2B lifecycle operations.

### 7.2 PlatformUser

| Field | Type | Null | Rule |
|---|---|---:|---|
| `Id` | Guid | no | primary key, server-generated |
| `Nama` | string, max 200 | no | trimmed, non-empty |
| `Username` | string, max 100 | no | trimmed, globally unique |
| `PasswordHash` | string | no | BCrypt hash; never returned |
| `Role` | string, max 20 | no | exactly `superadmin` |
| `Active` | bool | no | default true |
| `CreatedAt` | UTC timestamp | no | server-generated |
| `UpdatedAt` | UTC timestamp | no | server-generated and updated on changes |

Constraints/indexes:

- primary key on `Id`;
- unique index on `Username`;
- check constraint `Role = 'superadmin'`;
- optional non-unique index on `Active` is not required for Phase 2B;
- no TenantId and no relationship to tenant business entities.

Delete behavior: no public delete endpoint. PlatformAuditEvent actor relationship uses `Restrict`, so audited PlatformUsers cannot be hard-deleted while referenced.

### 7.3 PlatformAuditEvent

| Field | Type | Null | Rule |
|---|---|---:|---|
| `Id` | Guid | no | primary key, server-generated event ID |
| `ActorPlatformUserId` | Guid | no | FK to PlatformUser |
| `TenantId` | Guid | no | FK to Tenant |
| `EventType` | string, max 50 | no | frozen values below |
| `CreatedAt` | UTC timestamp | no | server-generated event timestamp |
| `Metadata` | JSON/JSONB | yes | safe metadata only; maximum serialized size 2 KiB enforced by application |

Allowed Phase 2B event values:

```text
TENANT_PROVISIONED
TENANT_ACTIVATED
TENANT_SUSPENDED
```

Constraints/indexes:

- check constraint restricts EventType to the three frozen values;
- index `(TenantId, CreatedAt)`;
- index `(ActorPlatformUserId, CreatedAt)`;
- index `(EventType, CreatedAt)`;
- no automatic retention/deletion policy.

Relationships/delete behavior:

- PlatformUser → audit events: one-to-many, delete `Restrict`;
- Tenant → audit events: one-to-many, delete `Restrict`;
- audit events are append-only through application behavior;
- credentials, hashes, tokens, JWT data, or secrets are prohibited in Metadata.

---

## 8. Acceptance-test matrix

| ID | Scenario | Expected result |
|---|---|---|
| P2B-AUTH-001 | Valid active PlatformUser login | 200; platform JWT has platform issuer/audience, `scope=platform`, `superadmin`, no `tenant_id` |
| P2B-AUTH-002 | Unknown platform username or wrong password | 401 `PLATFORM_INVALID_CREDENTIALS`; indistinguishable credential message |
| P2B-AUTH-003 | Valid inactive PlatformUser credentials | 403 `PLATFORM_USER_INACTIVE`; no token |
| P2B-AUTH-004 | Platform token calls tenant endpoint | Rejected; no controller execution or tenant data |
| P2B-AUTH-005 | Tenant token calls platform endpoint | 401/403 per authentication boundary; no platform data |
| P2B-AUTH-006 | Old tenant JWT without `scope` | Rejected; mandatory re-login |
| P2B-AUTH-007 | Platform token contains `tenant_id` | Rejected |
| P2B-BOOT-001 | Bootstrap disabled with empty platform table | No PlatformUser created |
| P2B-BOOT-002 | Bootstrap enabled, valid env, empty table | Exactly one active superadmin with BCrypt hash |
| P2B-BOOT-003 | Bootstrap enabled but secret/config missing | Startup/bootstrap fails safely; no partial user; no secret logged |
| P2B-BOOT-004 | Bootstrap repeated after PlatformUser exists | No second user, no credential overwrite |
| P2B-BOOT-005 | Concurrent first bootstrap | At most one PlatformUser |
| P2B-TEN-001 | Authorized create tenant | 200; Tenant, owner, Settings, and one provision audit event committed atomically |
| P2B-TEN-002 | Create duplicate owner username | 409 `OWNER_USERNAME_CONFLICT`; no partial rows/audit |
| P2B-TEN-003 | Residual slug collision/race | 409 `TENANT_SLUG_CONFLICT`; no partial rows/audit |
| P2B-TEN-004 | Create owner scope | Owner role forced `owner`; TenantId equals server-generated Tenant Id |
| P2B-TEN-005 | Client submits status/role/TenantId/payment extras | 400 `VALIDATION_ERROR`; no rows created |
| P2B-TEN-006 | New tenant state | `active`; no payment fields/account; cash POS available |
| P2B-TEN-007 | Cash transaction before Phase 2C onboarding | Existing cash transaction succeeds for active tenant |
| P2B-TEN-008 | Gateway/non-cash before Phase 2C | No Phase 2B gateway endpoint/capability exists; no payment state fabricated |
| P2B-LIFE-001 | Superadmin suspends active tenant | 200 suspended; one `TENANT_SUSPENDED` audit event |
| P2B-LIFE-002 | Suspend with safe reason | Trimmed reason stored only in safe audit Metadata |
| P2B-LIFE-003 | Suspend already suspended tenant | 409 `TENANT_ALREADY_SUSPENDED`; no extra audit |
| P2B-LIFE-004 | Superadmin activates suspended tenant | 200 active; one `TENANT_ACTIVATED` audit event |
| P2B-LIFE-005 | Activate already active tenant | 409 `TENANT_ALREADY_ACTIVE`; no extra audit |
| P2B-LIFE-006 | Tenant role calls lifecycle endpoint | Rejected; no status/audit mutation |
| P2B-SUSP-001 | Suspended tenant user attempts login | 403 `TENANT_SUSPENDED`, distinct from invalid credentials |
| P2B-SUSP-002 | Existing valid tenant JWT after suspension | Every tenant business request returns 403 `TENANT_SUSPENDED` |
| P2B-SUSP-003 | Suspended tenant attempts read | Rejected; no read-only mode/data returned |
| P2B-SUSP-004 | Reactivated tenant | New login and valid scoped business access work again |
| P2B-ISO-001 | Tenant A reads Tenant B ID | No Tenant B data; existing not-found semantics preserved |
| P2B-ISO-002 | Tenant A writes Tenant B entity | Write guard rejects before SQL |
| P2B-ISO-003 | Missing tenant execution context reads business entities | Zero rows/fail-closed |
| P2B-ISO-004 | Missing tenant execution context writes business entities | Rejected before SQL |
| P2B-ISO-005 | Platform identity queries business DbSets | Zero rows; no implicit bypass |
| P2B-ISO-006 | Trusted provisioning scope targets Tenant A | Can read/write only Tenant A and cannot retarget |
| P2B-API-001 | Tenant list | Direct ordered DTO array; no business/payment data |
| P2B-API-002 | Tenant detail missing | 404 `TENANT_NOT_FOUND` |
| P2B-AUD-001 | Successful provisioning | Exactly one `TENANT_PROVISIONED` with correct actor/tenant/UTC timestamp |
| P2B-AUD-002 | Successful activate/suspend | Status change and exactly one matching event are atomic |
| P2B-AUD-003 | Failed/conflicting lifecycle request | No audit event created |
| P2B-AUD-004 | Audit metadata secret attempt | Secret is rejected/redacted; never persisted or logged |
| P2B-REG-001 | Existing owner/admin/kasir suite | Current backend regression remains green |
| P2B-REG-002 | Existing transaction integrity | Server-calculated amount tests remain green |
| P2B-REG-003 | Migration on production-like schema | Existing data preserved; tenants backfilled active; rollback reviewed |
| P2B-REG-004 | Docker/security gates | Runtime smoke and vulnerability scans remain green |

Cross-tenant read/write, missing-context, token substitution, suspension, and audit-exactly-once failures are release blockers.

---

## 9. Implementation readiness and order

This contract resolves the Phase 2B endpoint, lifecycle, suspension, JWT cutover, bootstrap, owner credential, and audit decisions necessary to begin implementation.

Recommended first implementation unit:

1. add characterization tests for current tenant authorization/filter behavior;
2. introduce typed tenant execution context;
3. change the global filter to fail-closed;
4. add the centralized TenantId write guard;
5. add the negative isolation tests from this contract.

This first unit intentionally precedes PlatformUser/endpoints so the security boundary is fail-closed before the control plane is introduced. It must not create a migration.

Later units may implement persistence/migrations, platform authentication/bootstrap, provisioning, lifecycle APIs, and audit in the reviewed order defined by the design document.

---

## 10. Remaining unresolved decisions

The following do not block the first Phase 2B implementation unit:

1. operational PlatformUser credential recovery/rotation after bootstrap;
2. operational process used by superadmin to communicate the initial owner credential to its intended recipient;
3. future audit UI/export and additional event types;
4. future payment-provider capability and Phase 2C onboarding contract.

None may be silently implemented as part of Phase 2B.
