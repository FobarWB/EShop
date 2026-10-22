---
type: index
verified: 2026-10-22
---

# Services

[Home](../Home.md)

All backend services share one PostgreSQL database (`eshop`) through the common DbContext.
Ports and database setup are in [AGENTS.md](../../AGENTS.md).

- [ProductApi](ProductApi.md): products CRUD.
- [CategoryApi](CategoryApi.md): categories CRUD.
- [ShoppingCart](ShoppingCart.md): carts and cart items per user.
- [ClientApp](ClientApp.md): Angular SPA that calls the three APIs.

## Common to all APIs

- CORS: one named policy, origins from `Cors:AllowedOrigins` in `appsettings.json` (`http://localhost:4200`);
  an empty or missing list allows no cross-origin requests. In containers: `Cors__AllowedOrigins__0=<origin>`.
- Logging: HttpLogging writes request and response metadata only (method, path, status, headers with
  non-allowlisted ones redacted, duration), never bodies: bodies will carry passwords and tokens (#40).
  It is enabled by the log level in `appsettings.Development.json`.
- Health: `GET /health` checks the database connection (200 Healthy, 503 Unhealthy); not logged by HttpLogging.
- Containers: `src/EShop.<Service>/Dockerfile`, services of the compose profile `app` with `ASPNETCORE_ENVIRONMENT=Development`.
  Inside the network the APIs listen on HTTP 8080 and are reachable from the host only through nginx
  (http://localhost:8080/api/...); see [RelativeApiUrlsBehindProxy](../Decisions/RelativeApiUrlsBehindProxy.md).
  No `UseHttpsRedirection()`: HTTPS ends at the entry point.

The archived `Northwind.Mvc` lives in `archive/` and is not documented here.
