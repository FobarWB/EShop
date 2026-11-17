---
type: decision
status: accepted
verified: 2026-11-17
issue: 49
---

# Run integration tests against real PostgreSQL

## Context

Repositories and API endpoints depend on EF Core and PostgreSQL behavior: `Include`, the unique index on
`carts.user_id`, cascade and restrict foreign keys, identity columns. A mocked DbContext or the EF Core InMemory
provider does not enforce any of it. Docker is not installed yet.
Since #49 Docker is installed: the server comes from Testcontainers.

## Decision

Integration tests use a real PostgreSQL. Every test project starts its own throwaway `postgres:18` container
(Testcontainers) and its own database there (`eshop_test` → `eshop_test_shoppingcart`, …), created and migrated by the
tests and emptied with `TRUNCATE … RESTART IDENTITY CASCADE` before each test. The environment variable
`ConnectionStrings__EShopTests` replaces the container with another server (#49; until then it was the only source,
as a user-secret).

## Consequences

- The tests refuse a database whose name does not end with `_test`, and the API factory checks that the API really
  uses the test database. On an override server the role needs `CREATEDB`.
- Separate containers let test projects run in parallel (Microsoft.Testing.Platform runs them concurrently);
  tests inside one project run sequentially.
- Migrations are exercised on an empty database on every run, not only on a new machine.
- Integration tests need Docker; without it, the override is the fallback.
