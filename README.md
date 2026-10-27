# Neverfade POS — Backend

**Multi-tenant point-of-sale API** built with C#, ASP.NET Core 10, Entity
Framework Core, and PostgreSQL. It covers retail transactions and inventory,
merchant/outlet boundaries, and specialized restaurant and laundry workflows.

**Status:** An evolving application with extensive feature and API test code.
The repository contains historical QA findings and open release gates; it is
**not presented as production-certified**. See the
[architecture and transaction design](docs/architecture.md).

## Engineering snapshot

| Area | Implemented evidence |
| --- | --- |
| API and persistence | ASP.NET Core controllers/services, EF Core/Npgsql, migrations |
| Authentication | Tenant JWT and separate platform authentication, BCrypt password verification |
| Isolation | Tenant execution context, EF global filters, role/capability and outlet checks |
| Checkout | Server-recomputed totals, inventory mutation and transaction commit |
| Business modules | Products, customers, employee attendance, reports, restaurant, laundry, retail pricing/returns |
| Payments | Xendit provider and sandbox components, payment-mode gates, webhook controller |
| Tests | xUnit API test project and [GitHub Actions test workflow](.github/workflows/phase-3-ci.yml) |

## What problem it solves

A single backend must serve more than one merchant without exposing another
merchant's records. Each merchant can have outlets, employees and configurable
business capabilities. Core POS workflows also have to preserve consistent
prices, payments, inventory and financial reporting.

The code is organized into `Controllers`, `Services`, `DTOs`, `Entities`,
`Data`, `Auth`, `Middleware`, and `Payments`. It is a layered ASP.NET Core
application with direct EF Core access in services, **not** a microservice
platform or repository-pattern implementation.

### Representative API groups

- `/api/auth` — tenant login and account context.
- `/api/transactions` — checkout and transaction lookup.
- `/api/payments` — payment operations.
- `/api/restaurant/orders` — restaurant orders.
- `/api/laundry/work-orders` — laundry orders and lifecycle.
- `/api/retail/returns` — retail returns.
- `/api/platform/tenants` — separate platform control-plane endpoints.

These are route groups, not a complete API reference. Development OpenAPI and
Scalar endpoints are configured in `NeverfadePos.Api/Program.cs`.

## Architecture and decisions

[Read the architecture document](docs/architecture.md) for the dependency
diagram, tenant/outlet isolation flow, and transactional checkout. Key choices:

1. **Tenant context originates server-side.** Tenant identity is derived from
   authenticated context and applied to EF Core access; outlet and object-level
   authorization are additional checks, not substitutes for tenant filters.
2. **Server is the authority for money.** `TransactionService` resolves a
   product/variant and recomputes price, discounts, tax and change before
   writing a cash transaction.
3. **One database transaction for cash checkout.** Sale items, stock history,
   inventory changes and customer loyalty points are committed together.
4. **Platform and merchant identities are separated.** The platform control
   plane has a different JWT configuration and authorization policy.
5. **External payment behavior is gated.** Payment-provider adapters and the
   sandbox simulator exist; their presence is not evidence of live settlement.

## Reproduce locally

Prerequisites: .NET 10 SDK and an **isolated PostgreSQL database**. Do not
reuse production credentials or run migrations against a live client database.

```bash
dotnet restore NeverfadePos.slnx
dotnet build NeverfadePos.slnx --configuration Release --no-restore
dotnet test NeverfadePos.Api.Tests/NeverfadePos.Api.Tests.csproj --configuration Release --no-build
```

For an API runtime, set connection and signing values through local .NET User
Secrets (the API project already has a `UserSecretsId`) or environment
variables. The following are **configuration names**, not real values:

```text
ConnectionStrings__DefaultConnection
Jwt__Key                    # 32+ characters
Jwt__Issuer
Jwt__Audience
PlatformJwt__Key            # independent, 32+ characters
PlatformJwt__Issuer         # different from tenant issuer
PlatformJwt__Audience       # different from tenant audience
```

Then, after inspecting the EF migrations, apply them **only to a disposable
database** using an appropriately installed `dotnet ef` tool. Start with:

```bash
dotnet run --project NeverfadePos.Api
```

The application initializes seed/bootstrap logic on startup. Review
[`SeedData.cs`](NeverfadePos.Api/Data/Seed/SeedData.cs) and the runtime
configuration before starting in an environment containing meaningful data.

## Tests and quality evidence

- [`NeverfadePos.Api.Tests`](NeverfadePos.Api.Tests/) contains xUnit tests for
  tenant separation, API authorization, transaction/payment boundaries,
  restaurant, laundry, and retail features. The test command above is also
  expressed in the [Phase 3 CI workflow](.github/workflows/phase-3-ci.yml).
- [`qa/`](qa/) contains additional integration/smoke scripts with stronger
  infrastructure requirements.
- [`qa/QA_REPORT.md`](qa/QA_REPORT.md) is a dated historical report with
  confirmed defects at that revision; do **not** interpret it as a current
  passing release assessment. [`docs/sprint1/IMPLEMENTATION_STATUS.md`](docs/sprint1/IMPLEMENTATION_STATUS.md)
  records later branch-specific checks and outstanding release gates.

No local build/test result is claimed by this README. Run the commands on an
SDK-equipped environment or inspect the checks for the exact PR commit.

## Known limitations and boundaries

- Full PostgreSQL concurrency, payment-provider certification, formal UAT,
  production release readiness and some outlet authorization matrices remain
  separately gated; historical progress files are not a substitute for a
  current same-commit verification.
- The repository includes migration and seed logic. Never execute either
  against a shared database without an explicit change plan and backup.
- This README documents implemented controller/service surfaces; future
  specifications under `docs/` are not automatically completed features.

Individual contributions should be verified from commits and PRs; repository
ownership alone does not establish authorship of every line.
