---
type: index
verified: 2026-10-27
---

# Services

[Home](../Home.md)

All backend services share one PostgreSQL database (`eshop`) through the common DbContext.
Ports and database setup are in [AGENTS.md](../../AGENTS.md).

- [ProductApi](ProductApi.md): products CRUD.
- [CategoryApi](CategoryApi.md): categories CRUD.
- [ShoppingCart](ShoppingCart.md): carts and cart items per user.
- [OrderApi](OrderApi.md): checkout (cart → order) and the user's orders.
- [ClientApp](ClientApp.md): Angular SPA that calls the four APIs.

## Common to all APIs

- CORS: one named policy, origins from `Cors:AllowedOrigins` in `appsettings.json` (`http://localhost:4200`);
  an empty or missing list allows no cross-origin requests. In containers: `Cors__AllowedOrigins__0=<origin>`.
- Logging: HttpLogging writes request and response metadata only (method, path, status, headers with
  non-allowlisted ones redacted, duration), never bodies: bodies will carry passwords and tokens (#40).
  It is enabled by the log level in `appsettings.Development.json`.
- Health: `GET /health` checks the database connection (200 Healthy, 503 Unhealthy); not logged by HttpLogging.
- Contracts (#33): request and response DTOs in `Dtos/` of each API, never EF entities; mappings are extension methods
  (`ToResponse()` on `IQueryable<T>`, translated to SQL; `ApplyTo()` onto a tracked entity). JSON only: the XML
  formatters are removed (they could not serialize the entities and do not fit positional records).
- Errors: `AddEShopProblemDetails()` + `app.UseExceptionHandler()` (first in the pipeline) from `EShop.ServiceDefaults`.
  Every error is `application/problem+json`; an unhandled exception is 500 without a stack trace.
  `ForeignKeyViolationHandler` maps PostgreSQL `23503` and `23001` to 409 (see [PostgreSql](../Learning/PostgreSql.md)):
  the repositories check references before a `DELETE`, the constraint catches a concurrent request between check and write.
  `ConcurrencyConflictHandler` maps `DbUpdateConcurrencyException` to 409: a row changed between reading and writing
  (products carry `xmin`, see [OptimisticConcurrencyWithXmin](../Decisions/OptimisticConcurrencyWithXmin.md)).
- Lists: `[FromQuery] PageRequest paging` (`page` ≥ 1, `pageSize` 1–100, default 20) and `PagedResponse<T>`
  from `EShop.ServiceDefaults`; see [PagedListResponses](../Decisions/PagedListResponses.md).
- Every action takes a `CancellationToken` (bound to `HttpContext.RequestAborted`) and passes it to the repository and
  EF Core, so an aborted request cancels its query. Repository parameters are required, so a forgotten token does not compile.
- Containers: `src/EShop.<Service>/Dockerfile`, services of the compose profile `app` with `ASPNETCORE_ENVIRONMENT=Development`.
  Inside the network the APIs listen on HTTP 8080 and are reachable from the host only through nginx
  (http://localhost:8080/api/...); see [RelativeApiUrlsBehindProxy](../Decisions/RelativeApiUrlsBehindProxy.md).
  No `UseHttpsRedirection()`: HTTPS ends at the entry point. nginx passes the original `Host` header
  (`proxy_set_header Host $http_host`), so `Location` headers point to http://localhost:8080, not to the container name.

The archived `Northwind.Mvc` lives in `archive/` and is not documented here.
