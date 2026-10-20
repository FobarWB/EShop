---
type: topic
verified: 2026-10-20
issue: 9
---

# Testing

Related: [TestAgainstRealPostgreSql](../Decisions/TestAgainstRealPostgreSql.md), [Services](../Services/Services.md).
Rules for writing tests: [.claude/rules/testing.md](../../.claude/rules/testing.md).

## Key ideas

- **Test where the bug can be.** Controller code decides status codes, so it is unit-tested with a mocked repository.
  Repository code is mostly instructions for EF Core and PostgreSQL, so it is tested against a real database:
  a mocked DbContext returns what the test put into it and hides missing `Include`s, broken constraints and cascades.
- **Three backend levels.** Unit (controller + NSubstitute mock, no HTTP, no database); repository integration
  (repository + EF Core + PostgreSQL); API integration (`WebApplicationFactory`: the whole `Program.cs` in memory,
  real HTTP through `TestServer`, real PostgreSQL).
- **Frontend.** Services are tested against `HttpTestingController` (URL, method, body, error handling);
  components are rendered in jsdom and checked after user actions. "The request was sent" is not enough:
  the cart bug #26 sends a correct request and still shows stale data.
- **Coverage shows what ran, not what was checked.** Its value is the list of uncovered lines: here it found dead
  code and an untested `UnitsInStock == null` branch (#31). Test quality is checked by breaking the code on purpose
  (mutation testing): restoring the old static cache failed 5 API tests, tracking in `RetrieveAsync` failed exactly 1.

## Applied in this project

- `tests/EShop.<Service>.Tests.Unit` and `tests/EShop.<Service>.Tests.Integration`; shared helpers in
  `tests/EShop.Tests.Common`: `TestDatabase` (connection string, `_test` guard, migrations, reset, seeding)
  and `EShopApiFactory` (in-memory API that is checked to use the test database).
- Lifetimes: database fixture once per test project (assembly fixture, runs `MigrateAsync`), API factory once
  per test class (class fixture), `TRUNCATE` of all tables before each test. Test projects run in parallel on
  separate databases; tests inside a project run sequentially.
- xUnit v3 and Vitest run on their current platforms: Microsoft.Testing.Platform (`global.json`) and
  `@angular/build:unit-test`. Coverage: `Microsoft.Testing.Extensions.CodeCoverage` + ReportGenerator (`tests/coverage.ps1`).
- Known bugs are tests with the correct expectation, skipped with the issue number (#24, #26).

## Open questions

- Move the test databases to Testcontainers in the Docker phase (one throwaway PostgreSQL container per run).
- Run `dotnet test` and `npm test` in CI (#10), with a PostgreSQL service container.
