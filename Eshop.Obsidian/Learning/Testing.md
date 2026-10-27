---
type: topic
verified: 2026-10-27
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
  the cart bug #26 sent a correct request and still showed stale data; only the "page updated" tests failed.
- **Coverage shows what ran, not what was checked.** Its value is the list of uncovered lines: here it found dead
  code and an untested `UnitsInStock == null` branch (#31). Test quality is checked by breaking the code on purpose
  (mutation testing): restoring the old static cache failed 5 API tests, tracking in `RetrieveAsync` failed exactly 1.
- **Reproduce a race for real.** A check-then-write race (#32/#33) is reproduced with an EF Core interceptor:
  `BeforeSaveChangesInterceptor` runs a write on another connection right before `SaveChanges`, after the repository's
  check. PostgreSQL then rejects the `DELETE` with a real error. A faked `PostgresException("23503")` would have passed,
  but the real one was `23001` (a `RESTRICT` key): only the real database showed that the handler missed a code.
- **A real database finds what mocks hide.** The overposting test (#33) showed that a PUT with a nested `category`
  inserted a row; the picture test showed that every category edit erased the stored picture.

## Applied in this project

- `tests/EShop.<Service>.Tests.Unit` and `tests/EShop.<Service>.Tests.Integration`; shared helpers in
  `tests/EShop.Tests.Common`: `TestDatabase` (connection string, `_test` guard, migrations, reset, seeding)
  and `EShopApiFactory` (in-memory API that is checked to use the test database).
- An API test changes a service of the in-memory API with `factory.WithWebHostBuilder(... ConfigureTestServices ...)`,
  e.g. `services.ConfigureDbContext<EShopDbContext>(o => o.AddInterceptors(...))` for the race tests.
- API tests sign in without Keycloak: `EShopApiFactory` makes `TestAuthHandler` the default scheme; a test client
  becomes a user with `client.AsUser(id, roles)` or `client.AsAdmin()` (headers `X-Test-User` / `X-Test-Roles`),
  a client without them stays anonymous (#41).
- Lifetimes: database fixture once per test project (assembly fixture, runs `MigrateAsync`), API factory once
  per test class (class fixture), `TRUNCATE` of all tables before each test. Test projects run in parallel on
  separate databases; tests inside a project run sequentially.
- xUnit v3 and Vitest run on their current platforms: Microsoft.Testing.Platform (`global.json`) and
  `@angular/build:unit-test`. Coverage: `Microsoft.Testing.Extensions.CodeCoverage` + ReportGenerator (`tests/coverage.ps1`).
- Known bugs are tests with the correct expectation, skipped with the issue number; the fix enables them
  (done in #26, which also enabled the #24 test for the anonymous cart).
- CI (`.github/workflows/ci.yml`, #10) runs `dotnet test` against a throwaway `postgres:18` service container and
  `ng test` on every pull request and push to `master`. Coverage goes to the job summary and the `backend-test-results` artifact.

## Open questions

- Move the test databases to Testcontainers in the Docker phase (one throwaway PostgreSQL container per run).
