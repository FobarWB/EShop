---
type: decision
status: accepted
verified: 2026-10-20
issue: 9
---

# Run integration tests against real PostgreSQL

## Context

Repositories and API endpoints depend on EF Core and PostgreSQL behavior: `Include`, the unique index on
`carts.user_id`, cascade and restrict foreign keys, identity columns. A mocked DbContext or the EF Core InMemory
provider does not enforce any of it. Docker is not installed yet.

## Decision

Integration tests use the local PostgreSQL. Every test project gets its own database, derived from the
`ConnectionStrings:EShopTests` secret (`eshop_test` → `eshop_test_shoppingcart`, …), created and migrated by the
tests and emptied with `TRUNCATE … RESTART IDENTITY CASCADE` before each test.

## Consequences

- The `eshop` role needs `CREATEDB`; the tests refuse a database whose name does not end with `_test`,
  and the API factory checks that the API really uses the test database.
- Separate databases let test projects run in parallel (Microsoft.Testing.Platform runs them concurrently);
  tests inside one project run sequentially.
- Migrations are exercised on an empty database on every new machine.
- In the Docker phase the connection string can come from Testcontainers instead; the fixtures stay the same.
