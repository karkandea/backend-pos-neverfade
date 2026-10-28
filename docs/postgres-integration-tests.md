# PostgreSQL integration tests

The existing `NeverfadePos.Api.Tests` suite covers API behavior using an in-memory EF Core provider. The separate `NeverfadePos.PostgresIntegrationTests` project verifies database guarantees that an in-memory provider cannot enforce.

## Scope

- Tenant-specific receipt-number uniqueness and query isolation.
- Rejection of attempts to write a record for another tenant.
- Concurrent requests for the same receipt number: one insert succeeds.
- Concurrent payments with the same external provider reference: one insert succeeds.

These tests exercise EF Core persistence and PostgreSQL constraints, **not** full payment provider or webhook end-to-end flows.

## Run locally

Start a **disposable** PostgreSQL instance that is separate from any application database. For example, with Docker:

```bash
docker run --rm --name neverfade-pos-test-db \
  -e POSTGRES_USER=postgres \
  -e POSTGRES_PASSWORD=disposable-local-test-only \
  -e POSTGRES_DB=postgres \
  -p 127.0.0.1:54329:5432 postgres:16-alpine
```

In a separate terminal:

```bash
dotnet restore NeverfadePos.slnx
dotnet build NeverfadePos.slnx --configuration Release --no-restore
dotnet test NeverfadePos.Api.Tests/NeverfadePos.Api.Tests.csproj --configuration Release --no-build

POS_TEST_POSTGRES='Host=127.0.0.1;Port=54329;Database=postgres;Username=postgres;Password=disposable-local-test-only;Pooling=false' \
  dotnet test NeverfadePos.PostgresIntegrationTests/NeverfadePos.PostgresIntegrationTests.csproj --configuration Release --no-build
```

The integration fixture refuses non-loopback PostgreSQL hosts and requires `Database=postgres`. Every test creates a randomized `neverfade_it_...` database and drops only databases with that prefix. **Never point these tests at a real server via an SSH port forward or any other localhost proxy.** They need database creation/deletion rights, so use only a disposable local container.

GitHub Actions provisions its own disposable PostgreSQL service and sets `POS_TEST_POSTGRES` for the integration test step. The GitHub-hosted runner itself must be available before CI can execute; a job failing before checkout is not evidence of a failing test.
