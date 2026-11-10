---
type: topic
verified: 2026-11-10
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

## Messaging tests (#50)

- **RabbitMQ from Testcontainers**: `RabbitMqFixture` (in `EShop.Tests.Common`) starts a `rabbitmq:4` container per test
  project (assembly fixture) and removes it afterwards. The tests never touch the developer's broker; Docker is needed
  locally and is available on the GitHub runners. PostgreSQL stays the configured test server
  ([TestAgainstRealPostgreSql](../Decisions/TestAgainstRealPostgreSql.md)); moving it to Testcontainers is #49.
- **Background services are started for real** (`StartAsync`/`StopAsync`) with a scope factory on the test database,
  and the test waits for the effect (`Eventually.HoldsAsync`, `RabbitMqFixture.WaitForMessageAsync`) instead of sleeping.
- **Behavior, not declarations**: the broker ignores misspelled arguments, so topology tests check where a message ends
  up (a wait queue sends it back after the TTL, a rejected one lands in the DLQ). Shared exchange, unique queue names
  and routing keys per test.
- **No waiting for real delays**: the consumer tests use the production queues (5 s / 30 s / 2 min) and publish a
  message as it arrives on its last attempt (`x-retry-attempt: 3`); the TTL mechanics are tested with 200 ms delays.
- **Ordering as a signal**: handlers on one channel run in order, so after a duplicate the test publishes another event;
  when that email arrives, the duplicate has been handled.
- **A first failure showed the design**: the test checked the inbox row right after the email and failed, because the
  consumer writes the inbox after sending. It now waits for the row too.
- **The API tests remove `OutboxPublisher`** from the services: it would read the developer's user-secrets and publish
  test events to the local broker.
- Each new test was seen failing: retry DLX pointed at the events exchange, publisher without `Persistent`, newest first,
  consumer without the inbox check, one retry too many, unknown type treated as known.

## Keycloak and guest checkout tests (#45)

- **Keycloak from Testcontainers**: `KeycloakFixture` starts `quay.io/keycloak/keycloak:26.8` (the compose image) per
  test project and imports **the repository's realm file** with test secrets. So the tests check the file too: a
  client, a service account or a role missing there fails them. About 30 s per run, so only the projects that call
  Keycloak have the fixture (OrderApi, CustomerApi).
- **Real HTTP for the Keycloak clients, fakes elsewhere**: `KeycloakAccountDirectory` and `KeycloakAccountProvisioner`
  run against the container through the same registration as `Program.cs`; API and handler tests use fakes of the
  interfaces (`FakeAccountDirectory`, a fake provisioner). Keycloak's emails need SMTP: checked by hand on compose
  (Mailpit), where the check found that `UPDATE_PASSWORD` alone left the email unverified.
- **The consumer's transaction is tested once**, in `EShop.Messaging.Tests.Integration` with a test handler (tracked
  change, `ExecuteUpdate` then an exception, rejection); the services' handler tests call the handler and save as the
  consumer would.
- **Mutation checks by test class**: a whole test project starts its containers again for every broken change; running
  only the affected project (or class) keeps a round of breakages at a few minutes. Watch the parser: a breakage that
  does not compile shows no test count at all and must not be read as "passed".

## Open questions

- Move the test databases to Testcontainers in the Docker phase (one throwaway PostgreSQL container per run).
