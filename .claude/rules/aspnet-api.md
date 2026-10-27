---
paths:
  - "**/Controllers/**/*.cs"
  - "**/Repositories/**/*.cs"
  - "**/Program.cs"
---

# ASP.NET Core Web API

Layering: controller → repository → DbContext. Controllers contain no EF queries.

- Return `ActionResult<T>` / `IActionResult` with correct status codes: 200, 201 + location for create, 204 for delete, 400, 404.
- Endpoints take and return DTOs (`Dtos/` of the API), never EF entities; mappings are extension methods
  (`ToResponse()` on `IQueryable<T>`, `ApplyTo()` onto a tracked entity).
- Collection endpoints are paged (page/size or `Take` limit); never return a whole table.
- Materialize queries with `ToListAsync()` before returning; returning a deferred `IEnumerable` makes the serializer enumerate synchronously.
- Request validation: `[ApiController]` + data annotations on request DTOs → automatic 400 ProblemDetails; no manual null checks of the body.
- Errors: `AddProblemDetails()` + `UseExceptionHandler()`; actions do not wrap everything in `try/catch` or build 500 responses themselves.
- Pass the action `CancellationToken` down to repositories.
- Structured logging with message templates: `_logger.LogInformation("Cart {UserId} created", userId)`, never string interpolation.
- Never log raw request or response bodies (passwords, tokens); `AddHttpLogging` without body fields.
- Service-to-service HTTP uses `IHttpClientFactory`, never `new HttpClient()`.
- Background work (hosted services, message consumers) never captures `HttpContext` or a request-scoped `DbContext`; create a scope with `IServiceScopeFactory`.
- API ports are duplicated in `src/EShop.ClientApp/proxy.conf.json` (see AGENTS.md); change both sides together.
- Keep `MapHealthChecks("/health")`: the container health checks depend on it.
- CORS: one named policy per API, origins from `Cors:AllowedOrigins` in configuration; never `AllowAnyOrigin()`.
