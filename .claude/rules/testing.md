---
paths:
  - "tests/**"
  - "**/*.spec.ts"
---

# Tests

Architecture and reasons: `Eshop.Obsidian/Learning/Testing.md`.

## Choosing the level

- Controller logic (status codes from repository results): unit test in `tests/EShop.<Service>.Tests.Unit`,
  repository mocked with NSubstitute.
- Repository behavior (queries, `Include`, constraints, cascades): integration test against PostgreSQL.
  Never mock `EShopDbContext`, never use the EF Core InMemory provider.
- HTTP behavior (routing, `[ApiController]` validation, JSON, headers): API test with `EShopApiFactory<Program>`
  in `tests/EShop.<Service>.Tests.Integration/Api`.
- Mock our own interfaces, not infrastructure (`DbContext`, `HttpClient`).

## .NET

- xUnit v3 + Shouldly. Names `Method_Scenario_Expected`; Arrange, Act and Assert separated by blank lines.
- Assert status codes and stored data, not response texts.
- Integration tests: seed, act and assert on separate DbContexts (`SeedAsync`, `FindAsync`, `CreateContext()`),
  seed only what the test needs. The fixture empties the database before each test.
- Each integration project has its own database (`<EShopTests database>_<suffix>`) and runs its tests sequentially.
- Pass `TestContext.Current.CancellationToken` to async calls.
- Shared helpers live in `tests/EShop.Tests.Common` (a library, not a test project).

## Angular

- Vitest (`npm test`). HTTP goes through `provideHttpClient()` + `provideHttpClientTesting()`;
  call `httpTesting.verify()` in `afterEach`. A test never reaches a real API.
- Component tests assert on the rendered DOM after user actions, not only on the requests sent.

## Trusting a test

- A new test must be seen failing once (break the code or the expectation) before it is trusted.
- A test for a known bug keeps the correct expectation and is skipped with the issue number:
  `[Fact(Skip = "#N")]` or `it.skip(...) // #N`. The issue that fixes the bug enables it.
