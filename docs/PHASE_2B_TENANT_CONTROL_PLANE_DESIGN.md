# NeverFade POS — Phase 2B Tenant Control Plane Design

Status: PROPOSED DESIGN — NO IMPLEMENTATION AUTHORIZED

Owner: NeverFade POS

Purpose: Resolve the tenant-isolation and platform-identity blockers found in Phase 2A before implementation begins.

Source of truth precedence:

1. `CONTRACT.md` remains frozen for all existing endpoints and tenant behavior.
2. `docs/PHASE_2_PAYMENT_TENANT_SPEC.md` defines approved Phase 2 product intent.
3. This document proposes the Phase 2B implementation architecture. It does not amend the frozen contract by itself.

---

## 1. Scope and non-goals

Phase 2B introduces a platform control plane for tenant administration while preserving the existing tenant POS data plane.

In scope:

- fail-closed tenant reads and writes;
- a separate platform identity and `superadmin` role;
- distinguishable tenant and platform JWTs;
- explicit platform API boundaries;
- tenant lifecycle management;
- trusted initial tenant provisioning;
- tenant isolation regression coverage;
- a safe execution model for future system operations.

Not in scope:

- payment-provider adapters or provider-specific behavior;
- payment, payout, balance, settlement, or webhook implementation;
- changing existing endpoint DTOs or behavior;
- self-service tenant registration;
- subscription management;
- implicit platform access to tenant business data.

---

## 2. Phase 2A blockers being resolved

The existing application has these relevant characteristics:

1. `User` inherits tenant-scoped `BaseEntity`; it cannot safely represent a platform user with no tenant.
2. every current JWT contains `tenant_id`, and there is no identity-scope claim;
3. the global filter currently becomes unrestricted when `CurrentUser.TenantId` is missing;
4. `TenantId` is populated manually by services, with no centralized write invariant;
5. `IgnoreQueryFilters()` is used by authentication and must not become the general platform/system access mechanism;
6. there is no tenant lifecycle, platform authentication, or tenant-provisioning boundary.

This design resolves those blockers without granting `superadmin` general access to tenant business records.

---

## 3. Plane separation

NeverFade will have two explicit security planes.

### 3.1 Tenant data plane

Used by the existing POS application and existing `/api/...` endpoints.

- identities: `owner`, `admin`, `kasir`;
- identity scope: `tenant`;
- `tenant_id`: mandatory;
- access: only records whose `TenantId` equals the authenticated tenant;
- existing endpoint contracts remain unchanged until a separate approved contract update.

### 3.2 Platform control plane

Used by NeverFade platform administrators under `/api/platform/...`.

- identity: separate `PlatformUser`;
- role: `superadmin`;
- identity scope: `platform`;
- `tenant_id`: prohibited;
- access: platform control-plane records and tenant lifecycle operations only;
- no implicit read or write access to Products, Customers, Transactions, Users, Payments, Payouts, or other tenant business data.

The control plane may create and manage a `Tenant` aggregate and initiate a trusted provisioning operation. That authority is not equivalent to impersonating a tenant or disabling tenant filters.

---

## 4. Proposed identity model

### 4.1 Tenant identity

Tenant users remain represented by the existing `User` entity.

Required authenticated properties:

```text
subject/user id
scope = tenant
tenant_id = required Guid
role = owner | admin | kasir
username
nama
```

An identity is invalid as a tenant identity when:

- `scope` is absent or not `tenant` after the Phase 2B cutover;
- `tenant_id` is absent or malformed;
- role is not one of the tenant roles;
- the token is presented to a platform-only endpoint.

### 4.2 Platform identity

Platform administrators are represented by a new `PlatformUser` entity and are not rows in the existing `users` table.

Required authenticated properties:

```text
subject/platform user id
scope = platform
role = superadmin
username
nama
```

A platform identity must not contain `tenant_id`. Presence of a tenant claim on a platform token is invalid, not ignored.

### 4.3 PlatformUser conceptual schema

```text
PlatformUser
- Id: Guid, primary key
- Nama: string
- Username: string, globally unique
- PasswordHash: string, never returned
- Role: string constrained by application/domain rule to superadmin
- Active: bool
- CreatedAt: UTC timestamp
- UpdatedAt: UTC timestamp
```

`PlatformUser` does not inherit `BaseEntity`, has no `TenantId`, and has no navigation to tenant business data. Initial platform-user bootstrap credentials must use an approved secure operational procedure; they must not be demo-seeded or committed.

---

## 5. JWT and authentication model

### 5.1 Required claims

Tenant JWT:

```json
{
  "sub": "<tenant-user-guid>",
  "scope": "tenant",
  "tenant_id": "<tenant-guid>",
  "role": "owner|admin|kasir",
  "username": "...",
  "nama": "..."
}
```

Platform JWT:

```json
{
  "sub": "<platform-user-guid>",
  "scope": "platform",
  "role": "superadmin",
  "username": "...",
  "nama": "..."
}
```

The JSON examples are internal claim models, not new public endpoint contracts.

### 5.2 Cryptographic and routing separation

The proposed implementation uses two explicit authentication schemes:

- `TenantBearer` for existing tenant endpoints;
- `PlatformBearer` for `/api/platform/...`.

Platform tokens use a separate authentication scheme, signing key, audience, and issuer from tenant tokens. The platform scheme validates its platform-specific issuer, audience, signing key, `scope=platform`, absence of `tenant_id`, and `role=superadmin`. Tenant authentication validates the existing tenant issuer/audience/key plus `scope=tenant`, a valid `tenant_id`, and a tenant role.

This prevents a correctly signed tenant token from becoming a platform token merely by carrying a role string, and prevents a platform token from entering tenant endpoints.

No secret values belong in source or migration files.

### 5.3 Compatibility at cutover

Existing endpoint routes and response bodies remain unchanged. Adding the mandatory `scope=tenant` claim affects issued credentials, not endpoint payloads.

Existing tenant tokens issued before the cutover lack `scope` and are not supported after Phase 2B deployment. Mandatory re-login is required. There is no missing-scope compatibility mode.

---

## 6. Request context separation

The current all-purpose `CurrentUser` should evolve into explicitly typed contexts.

### 6.1 TenantCurrentUser / ITenantRequestContext

Exposes only validated tenant identity data:

```text
UserId
TenantId (non-null for an authorized tenant request)
Role
Username
Nama
IsAuthenticatedTenant
```

Construction/validation must reject invalid combinations rather than expose a nullable TenantId that callers can accidentally interpret as unrestricted access.

### 6.2 PlatformCurrentUser / IPlatformRequestContext

Exposes only validated platform identity data:

```text
PlatformUserId
Role = superadmin
Username
Nama
IsAuthenticatedPlatform
```

It never exposes a TenantId.

### 6.3 ITenantExecutionContext

Database tenant scoping must not depend solely on raw HTTP claims. A scoped execution context supplies one of these explicit states:

```text
None
AuthenticatedTenant(targetTenantId)
TrustedSystem(targetTenantId, operationName)
```

There is deliberately no `PlatformUnrestricted` state.

- Tenant requests create `AuthenticatedTenant` only after authentication and claim validation.
- Platform listing/lifecycle operations remain in `None` because they query control-plane entities such as `Tenant` and `PlatformUser`, not tenant business entities.
- Provisioning and approved future system work create `TrustedSystem` with one explicit target TenantId.

The context is scoped and immutable after target assignment. Nested or conflicting target assignments fail.

---

## 7. Authorization policies

Proposed named policies:

| Policy | Authentication scheme | Required claims | Intended endpoints |
|---|---|---|---|
| `TenantUser` | `TenantBearer` | `scope=tenant`, valid `tenant_id`, role in tenant roles | existing authenticated tenant APIs |
| `TenantOwnerOrAdmin` | `TenantBearer` | tenant identity and role `owner` or `admin` | existing restricted APIs |
| `TenantOwner` | `TenantBearer` | tenant identity and role `owner` | future owner-only functions |
| `PlatformSuperAdmin` | `PlatformBearer` | `scope=platform`, no `tenant_id`, role `superadmin` | `/api/platform/...` |

Controllers must select the correct scheme/policy explicitly. A role check alone is insufficient.

`superadmin` must not be added to existing `[Authorize(Roles = "owner,admin")]` attributes. Platform policies must not be applied to existing tenant business controllers.

---

## 8. Fail-closed query-filter design

All entities inheriting tenant-scoped `BaseEntity` remain subject to a global query filter. The conceptual predicate is:

```text
executionContext.HasExplicitTenant
AND entity.TenantId == executionContext.TargetTenantId
```

Expected results:

| Context | Tenant business query result |
|---|---|
| Valid tenant request | Only authenticated tenant rows |
| Trusted system scope with target tenant | Only explicit target tenant rows |
| Platform request without target scope | No rows |
| Anonymous request | No rows |
| Missing/malformed tenant claim | No rows / request rejected |
| DbContext created without tenant execution context | No rows |

Missing tenant context must never translate into an unfiltered predicate.

The `Tenant` and `PlatformUser` control-plane entities are not tenant-scoped `BaseEntity` records and are not governed by the tenant business filter. Their access is restricted through platform services and authorization policies.

`IgnoreQueryFilters()` must not be spread through platform controllers or services. Existing frozen authentication usage is reviewed separately during implementation. Any exceptional unfiltered lookup must be centralized, narrowly scoped, security-reviewed, and covered by negative tests; it must never return arbitrary tenant business collections.

---

## 9. Write-side TenantId guard

Query filters do not protect inserts or all update scenarios. A centralized SaveChanges guard is required for every Added, Modified, or Deleted tenant-scoped entity.

### 9.1 Normal tenant request

For every tracked tenant entity:

- execution mode must be `AuthenticatedTenant`;
- entity `TenantId` must be non-empty;
- entity `TenantId` must exactly equal authenticated `TenantId`;
- changing `TenantId` on an existing entity is forbidden;
- a mismatched original/current TenantId fails before SQL is issued.

The guard may stamp a missing TenantId on a newly added entity only from the validated authenticated target, but the safer recommended rule is to require or deterministically stamp it centrally and then validate it. It must never use a client DTO tenant identifier.

### 9.2 Trusted provisioning/system operation

For `TrustedSystem(targetTenantId, operationName)`:

- target TenantId must be explicit and non-empty;
- every tenant entity written must equal that one target;
- changing TenantId remains forbidden;
- the operation name and target tenant are available for audit logging;
- the scope is created only by an internal trusted executor, never from request body/query/header values.

### 9.3 No execution tenant

Any attempted tenant-entity insert, update, or delete with execution state `None` fails before database access. A platform token alone never satisfies the guard.

The guard can be implemented in `AppDbContext.SaveChanges/SaveChangesAsync` or an EF Core interceptor. One authoritative implementation should cover all save paths and database transactions.

Bulk SQL, raw SQL, and future bulk-update APIs bypassing change tracking are prohibited for tenant business writes unless a separate equivalent guard and security review exist.

---

## 10. Trusted tenant execution scope

A trusted scope is a narrow capability, not a filter bypass.

Conceptual API:

```text
ITenantExecutionScopeFactory.RunAsync(
    targetTenantId,
    operationName,
    scopedOperation)
```

Required behavior:

1. validate target TenantId and operation name;
2. establish immutable `TrustedSystem(targetTenantId)` context;
3. create/use a scoped DbContext whose query filter resolves only that tenant;
4. enforce the same write guard against the explicit target;
5. record security-relevant operation metadata;
6. dispose the scope after the operation;
7. reject nested scopes targeting another tenant.

The scope factory is internal application infrastructure. Controllers cannot accept an arbitrary TenantId and obtain a trusted scope directly. Platform controllers pass an authorized provisioning command to `TenantProvisioningService`; that service is the approved boundary.

---

## 11. TenantProvisioningService

`TenantProvisioningService` owns the initial atomic business workflow:

```text
Authorized superadmin
→ validate tenant and owner input
→ create Tenant control-plane row
→ enter trusted scope for the new TenantId
→ create initial owner with explicit TenantId
→ initialize tenant Settings with the same TenantId
→ commit
```

Responsibilities:

- require a validated `PlatformSuperAdmin` caller at its application boundary;
- enforce tenant slug and globally unique username rules required by the current contract;
- generate the Tenant Id server-side;
- hash the initial owner credential using the existing approved password mechanism;
- force initial role to `owner`; never accept `superadmin` as a tenant-user role;
- assign the generated TenantId to owner and settings through trusted scope;
- ensure owner/settings cannot be created for a different tenant;
- execute tenant creation, owner creation, and settings initialization atomically where practical;
- return a sanitized result without password hashes or secrets;
- produce an auditable provisioning event without logging credentials;
- leave payment onboarding to a later Phase 2C boundary.

The authenticated superadmin supplies the initial owner password in the provisioning request. The backend validates it, hashes it immediately with the existing approved BCrypt mechanism, never persists or logs plaintext, and never returns it. Phase 2B does not introduce `MustChangePassword` or mandatory reset behavior.

Failure must not leave an operational tenant with a missing owner/settings silently. The implementation must define transaction/compensation behavior before coding.

---

## 12. Platform API boundaries

All new platform endpoints are conceptually under `/api/platform/...` and require `PlatformSuperAdmin`.

Candidate Phase 2B surface, subject to a separately approved API contract:

```text
POST   /api/platform/auth/login
GET    /api/platform/auth/me

GET    /api/platform/tenants
GET    /api/platform/tenants/{tenantId}
POST   /api/platform/tenants
POST   /api/platform/tenants/{tenantId}/activate
POST   /api/platform/tenants/{tenantId}/suspend
```

Platform tenant responses may include lifecycle metadata and sanitized initial-owner identity/status. They must not include tenant business datasets, password hashes, credentials, full financial details, or general-purpose “execute as tenant” functionality.

There is no platform endpoint for arbitrary Products, Customers, Transactions, or future Payments/Payouts in Phase 2B.

Hard delete is not part of the normal MVP lifecycle.

---

## 13. Tenant API boundaries

Existing `/api/auth`, `/api/products`, `/api/customers`, `/api/transactions`, `/api/users`, and other frozen routes remain tenant APIs.

They must:

- accept only `TenantBearer` identities;
- require valid `scope=tenant` and `tenant_id` after cutover;
- resolve TenantId from validated identity, never client input;
- remain subject to fail-closed query filters and the write guard;
- preserve current DTOs, JSON casing, authorization behavior, and endpoint semantics unless a later approved contract changes them.

Tenant owners cannot create, activate, suspend, or inspect other tenants.

---

## 14. Tenant lifecycle behavior

Phase 2B lifecycle is exactly `active` or `suspended`; `inactive` is excluded. New tenants start `active`.

- `active`: tenant login and tenant business APIs operate normally. Cash POS is available. Gateway/non-cash and payout remain unavailable until separate Phase 2C onboarding/capability becomes active.
- `suspended`: tenant login is rejected, existing tenant JWTs lose data-plane access, all tenant business APIs are rejected, and no read-only mode exists.

The centralized tenant-access validation must load/check current tenant status for every tenant authentication/session path, not trust status captured at token issuance. Rejection uses stable code `TENANT_SUSPENDED`, distinct from invalid credentials and generic authorization failure. Superadmin can activate or suspend; repeated same-state operations return defined conflicts and do not duplicate audit events. Hard delete is absent.

---

## 15. Safe future background and webhook operations

Background processing has no end-user tenant JWT, so it must never depend on “missing TenantId means all tenants.”

Required pattern:

```text
receive system event
→ authenticate/verify event using the future provider adapter
→ derive an immutable external routing reference
→ resolve exactly one TenantId through a narrow trusted resolver
→ reject zero or multiple matches
→ open TrustedSystem(targetTenantId, operationName)
→ load and mutate only that tenant's resources
→ commit with idempotency and audit controls
```

The future tenant resolver must be narrowly scoped. A possible provider-neutral control-plane concept is an external-account routing record containing only a provider discriminator, a non-secret external account reference, and TenantId with a uniqueness constraint. Exact fields cannot be frozen until provider capability audit confirms which verified webhook reference is always present.

Important invariants:

- raw `tenantId` supplied by a webhook body, frontend, queue message, or header is never trusted by itself;
- signature/authenticity verification occurs before event data can select a tenant;
- resolution uses an immutable provider reference previously bound during trusted onboarding;
- ambiguous or unknown routes fail and are quarantined/logged without tenant writes;
- after resolution, ordinary fail-closed filters and the same SaveChanges guard remain active;
- no background job receives a global tenant-business DbContext;
- cross-tenant batch work enumerates authorized Tenant IDs from the control plane and opens a fresh explicit scope per tenant;
- idempotency and provider-specific verification remain Phase 2C design work.

This pattern allows future webhooks and scheduled reconciliation to target one tenant safely without weakening isolation or adding general `IgnoreQueryFilters()` calls.

---

## 16. Security invariants

The following are release-blocking invariants:

1. Missing tenant context returns no tenant-scoped rows.
2. Missing tenant context cannot write tenant-scoped rows.
3. Tenant identity always has `scope=tenant`, one valid TenantId, and a tenant role.
4. Platform identity always has `scope=platform`, no TenantId, and role `superadmin`.
5. Tenant tokens cannot authorize platform endpoints.
6. Platform tokens cannot authorize tenant business endpoints.
7. Superadmin has no implicit tenant-business filter bypass.
8. Tenant A cannot read, update, or delete Tenant B data, including by guessed Guid.
9. TenantId cannot be changed after entity creation.
10. Client-provided TenantId never establishes request or execution scope.
11. Trusted operations target exactly one explicit tenant.
12. Provisioned owner and settings always use the newly created TenantId.
13. No password hash, JWT key, temporary credential, or future provider secret is logged or returned unintentionally.
14. Platform-user credentials are separate from tenant-user credentials and storage.
15. Any exceptional unfiltered control-plane lookup is narrow, centralized, reviewed, and cannot return arbitrary business data.

---

## 17. Migration impact assessment

Phase 2B is expected to require database migration work, but no migration is created by this design.

Expected schema changes:

1. create `platform_users` with globally unique username, password hash, role, active flag, and timestamps;
2. add `status` constrained to `active|suspended` and `updated_at` to `tenants`;
3. create `platform_audit_events` for `TENANT_PROVISIONED`, `TENANT_ACTIVATED`, and `TENANT_SUSPENDED` events;
4. add database constraints/indexes supporting platform role, tenant status, audit actor/tenant lookup, and event ordering.

No change is currently required to make existing tenant `users.TenantId` nullable; it should remain required. `superadmin` belongs in `platform_users`, not `users`.

Data impact:

- existing tenant/user/business rows must be preserved;
- existing tenant receives non-destructive lifecycle state `active`;
- no production seed/reset/recreate is permitted;
- initial PlatformUser uses an explicit one-time environment-controlled bootstrap: an enable flag is required, it runs only when no PlatformUser exists, contains no default/demo credential, logs no secret, and cannot create another administrator after the first exists;
- migrations must be reviewed for non-destructive behavior and compared with production migration history before execution.

Payment account, Payment, Payout, and webhook routing migrations are not part of Phase 2B.

---

## 18. Compatibility with existing flows

Expected compatibility:

- current owner/admin/kasir records stay in `users` with required TenantId;
- current login response and `/api/auth/me` response remain unchanged;
- current business endpoint paths and DTOs remain unchanged;
- current role permissions remain unchanged;
- current server-side transaction amount validation remains unchanged;
- the global filter becomes stricter without changing correct requests;
- the write guard formalizes the TenantId behavior existing services already intend.

Expected operational effects:

- users must re-login when mandatory tenant token scope is deployed; old missing-scope tokens are rejected;
- tests or internal tools constructing `AppDbContext` without a tenant context will see no tenant data and must use an explicit trusted test/system scope;
- code paths that accidentally omit or mismatch TenantId will begin failing, which is intended;
- development seed logic must use an explicitly approved trusted initialization scope or be redesigned, without changing production bootstrap protections.

The frozen contract prevents silently changing current endpoint behavior while preparing this architecture.

---

## 19. Targeted tests required

### 19.1 Authentication and token-shape tests

- tenant login issues `scope=tenant`, valid `tenant_id`, and allowed tenant role;
- platform login issues `scope=platform`, `superadmin`, and no `tenant_id`;
- tenant token rejected by platform endpoint;
- platform token rejected by tenant endpoint;
- platform token containing `tenant_id` rejected;
- tenant token missing/malformed `tenant_id` rejected;
- tenant token with `superadmin` rejected;
- platform token with owner/admin/kasir rejected;
- inactive PlatformUser rejected;
- expired/invalid-signature/wrong-audience tokens rejected by their schemes.

### 19.2 Fail-closed read tests

- valid Tenant A context only returns Tenant A rows for every tenant entity type;
- guessed Tenant B IDs return not found/forbidden according to existing endpoint semantics;
- missing context returns zero tenant rows;
- platform context returns zero tenant business rows;
- anonymous context returns zero tenant business rows;
- trusted scope for Tenant A cannot see Tenant B;
- model-level test confirms every `BaseEntity` subtype has the filter.

### 19.3 Write-guard tests

- Tenant A can add/update/delete Tenant A entity through allowed flow;
- Tenant A cannot add entity stamped Tenant B;
- Tenant A cannot modify/delete a tracked Tenant B entity;
- empty TenantId fails;
- TenantId mutation fails;
- platform/no-context tenant write fails;
- trusted scope succeeds only for its explicit target;
- nested/conflicting trusted scopes fail;
- sync and async SaveChanges paths enforce identical rules;
- transaction rollback leaves no partial rows after guard failure.

### 19.4 Provisioning tests

- authorized superadmin creates tenant, exactly one initial owner, and settings with one TenantId;
- tenant ID is server-generated;
- initial role is always owner;
- duplicate slug fails atomically;
- duplicate globally unique username fails atomically;
- invalid input leaves no partial tenant/owner/settings;
- password is hashed and never returned/logged;
- tenant admin/owner/kasir cannot call provisioning;
- platform response does not expose tenant business data;
- payment onboarding is not fabricated during Phase 2B.

### 19.5 Lifecycle tests

- only platform superadmin can activate/suspend;
- tenant roles cannot alter lifecycle;
- hard-delete endpoint is absent;
- newly provisioned tenant starts active and can use cash POS before Phase 2C;
- suspended tenant login is rejected with `TENANT_SUSPENDED`;
- existing tenant JWT is rejected with `TENANT_SUSPENDED` after suspension;
- every tenant business API, including reads, is rejected for suspended tenant;
- same-state lifecycle conflicts create no duplicate audit event.

### 19.6 Existing regression

- all existing backend regression remains green;
- existing owner/admin/kasir authorization remains green;
- transaction monetary-integrity tests remain green;
- Docker/runtime smoke remains green;
- migration against a production-like existing schema is non-destructive;
- dependency vulnerability gate remains green.

---

## 20. Proposed implementation order

Implementation must wait for approval and proceed in small reviewable units:

1. freeze the Phase 2B endpoint/domain contract and resolve lifecycle/bootstrap open decisions;
2. add typed identity-scope constants and authentication-policy tests;
3. introduce tenant/platform request-context separation;
4. make global tenant filters fail-closed and add negative isolation tests;
5. add the centralized write guard and write-isolation tests;
6. introduce PlatformUser model/configuration and reviewed migration;
7. add separate platform JWT service/authentication scheme and platform auth endpoints;
8. add tenant lifecycle fields and reviewed migration, if not combined with step 6;
9. add trusted tenant execution scope with unit/integration tests;
10. implement TenantProvisioningService and atomic provisioning tests;
11. implement minimal `/api/platform/tenants` lifecycle endpoints;
12. apply the approved suspended-tenant behavior;
13. run complete regression, isolation, migration, Docker, and security gates;
14. independently review that no generalized filter bypass was introduced.

Each unit must build and pass targeted tests before the next unit. No payment-provider work belongs in this sequence.

---

## 21. Risks and mitigations

| Risk | Impact | Mitigation |
|---|---|---|
| Fail-closed filter breaks tests/internal jobs relying on no context | Intended code paths fail | Require explicit test or trusted system scope; never restore fail-open behavior |
| Authentication scheme confusion | Privilege escalation | Separate schemes, audiences/keys, scope validation, negative cross-scheme tests |
| Role-string collision (`superadmin` in tenant user) | Platform privilege escalation | Separate table, scheme, scope, and policy; reject invalid role/scope combinations |
| Platform API grows into tenant-data browser | Cross-tenant exposure | Keep control-plane DTOs/services narrow; no generic impersonation/filter bypass |
| Provisioning leaves partial data | Broken tenant | Transactional workflow and failure tests; define compensation before external side effects |
| SaveChanges guard is bypassed | Cross-tenant writes | Cover every save path; prohibit unguarded raw/bulk writes; integration tests |
| Existing tokens lack scope | User sessions rejected at cutover | Mandatory re-login is part of the approved deployment plan; no compatibility mode |
| Tenant lifecycle rule is inconsistently enforced | Suspended tenant remains operational | Central tenant-status validation on login and every authenticated tenant request |
| Background job targets wrong tenant | Financial/data breach | Verified immutable routing reference, unique resolution, one explicit scope per tenant |
| Seed/bootstrap creates insecure platform admin | Platform compromise | No demo platform user; approved one-time secret/bootstrap procedure |

---

## 22. Unresolved product and operational decisions

Phase 2B still requires decisions on:

1. PlatformUser credential recovery/rotation process after initial bootstrap;
2. whether additional platform audit event types or safe metadata are required beyond the frozen minimum;
3. operational owner-password delivery procedure outside the API after superadmin submits it;
4. whether provisioning evolves into a persisted workflow once external provider onboarding is introduced in Phase 2C.

The platform endpoint, DTO, error, persistence, and acceptance-test contract is frozen separately in `docs/PHASE_2B_PLATFORM_API_CONTRACT.md`.

Provider capability, payment onboarding mapping, webhook identifiers, KYC, payout, fees, and payment methods remain Phase 2C+ blockers and must not be resolved in Phase 2B by assumption.

---

## 23. Proposed implementation inventory

### 23.1 New files/classes expected

Names are proposed and may be adjusted to existing conventions during approved implementation:

```text
Auth/IdentityScopes.cs
Auth/TenantCurrentUser.cs (or ITenantRequestContext + implementation)
Auth/PlatformCurrentUser.cs (or IPlatformRequestContext + implementation)
Auth/ITenantExecutionContext.cs
Auth/TenantExecutionContext.cs
Auth/AuthorizationPolicies.cs
Auth/IPlatformJwtService.cs
Auth/PlatformJwtService.cs

Entities/PlatformUser.cs
Data/Configurations/PlatformUserConfiguration.cs

Services/PlatformAuth/IPlatformAuthService.cs
Services/PlatformAuth/PlatformAuthService.cs
Services/TenantProvisioning/ITenantProvisioningService.cs
Services/TenantProvisioning/TenantProvisioningService.cs
Services/TenantExecution/ITenantExecutionScopeFactory.cs
Services/TenantExecution/TenantExecutionScopeFactory.cs
Services/PlatformTenants/IPlatformTenantService.cs
Services/PlatformTenants/PlatformTenantService.cs

Controllers/Platform/PlatformAuthController.cs
Controllers/Platform/PlatformTenantsController.cs

DTOs/PlatformAuth/...
DTOs/PlatformTenants/...

Data/TenantWriteGuard.cs or Data/Interceptors/TenantWriteGuardInterceptor.cs
Entities/PlatformAuditEvent.cs
Data/Configurations/PlatformAuditEventConfiguration.cs
Services/PlatformBootstrap/PlatformUserBootstrapService.cs
```

Test projects/files are expected for authentication separation, filters, write guard, provisioning, lifecycle, and API authorization.

### 23.2 Existing files expected to change

```text
NeverfadePos.Api/Auth/CurrentUser.cs
NeverfadePos.Api/Auth/JwtService.cs
NeverfadePos.Api/Data/AppDbContext.cs
NeverfadePos.Api/Entities/Tenant.cs
NeverfadePos.Api/Data/Configurations/TenantConfiguration.cs
NeverfadePos.Api/Program.cs
NeverfadePos.Api/Data/Seed/SeedData.cs
NeverfadePos.Api/Migrations/AppDbContextModelSnapshot.cs (generated by approved migration)
```

Existing controllers using role attributes may later move to named tenant policies without changing behavior. Existing services may require typed tenant-context injection, but their public endpoint contracts should remain unchanged.

### 23.3 Expected migrations

At least one reviewed, non-destructive Phase 2B migration is expected to:

- create `platform_users`;
- add approved lifecycle fields to `tenants`;
- add required indexes/constraints.

It may be split into two migrations—platform identity and tenant lifecycle—to reduce review/rollback risk. No payment tables are expected in Phase 2B.

### 23.4 Required test suites

- tenant/platform JWT claim and scheme tests;
- authorization-policy matrix tests;
- fail-closed global-filter tests for all tenant entities;
- cross-tenant endpoint regression;
- SaveChanges write-guard tests;
- trusted execution-scope tests;
- tenant provisioning atomicity/security tests;
- tenant lifecycle authorization tests;
- existing 222-test regression or its current successor;
- migration, Docker/runtime, and dependency/security gates.

### 23.5 Remaining blockers before implementation

- operational PlatformUser credential recovery/rotation is not yet defined;
- operational handling/delivery of the initial owner credential after provisioning needs a runbook, although the API behavior is frozen;
- no payment provider/capability information exists, which blocks Phase 2C but not the tenant-control-plane core once Phase 2B decisions above are resolved.

---

## 24. Decision summary

Phase 2B must implement a separate, explicit platform control plane. Tenant data access becomes fail-closed on both reads and writes. `superadmin` is a separate `PlatformUser`, uses a distinguishable platform JWT, and receives no implicit access to tenant business data. Provisioning and future system operations target exactly one tenant through an auditable trusted execution scope while retaining the same tenant query filter and write guard.

No source code, migration, database, configuration, or provider behavior is changed or defined by this document.
