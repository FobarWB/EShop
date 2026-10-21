---
type: service
verified: 2026-10-20
---

# CategoryApi

REST API for product categories (CRUD).

- Project: [EShop.CategoryApi](../../src/EShop.CategoryApi/)
- Local URL: https://localhost:5003 (API reference: `/scalar`, OpenAPI document: `/openapi/v1.json`)

## Endpoints (`api/categories`)

| Method | Route | Action |
| --- | --- | --- |
| GET | `/` | List all categories |
| GET | `/{id}` | Get a category |
| POST | `/` | Create a category |
| PUT | `/{id}` | Update a category |
| DELETE | `/{id}` | Delete a category |

DELETE returns 409 ProblemDetails while products still belong to the category (#32).

## Data

`Category` in the shared PostgreSQL database `eshop` (Northwind data).
Reads go straight to PostgreSQL (no cache); a distributed cache is planned in #30.

## Dependencies

Shared DbContext (`EShopDbContext` in `EShop.Data`). Called by [ClientApp](ClientApp.md).
Kept separate from ProductApi for now: see [SeparateProductAndCategoryApis](../Decisions/SeparateProductAndCategoryApis.md).

## Tests

[Unit](../../tests/EShop.CategoryApi.Tests.Unit/) (controller) and [integration](../../tests/EShop.CategoryApi.Tests.Integration/)
(API) tests; see [Testing](../Learning/Testing.md).

## Known issues

- A product added to a category between the reference check and the `DELETE` still fails with 500 (foreign key, race).
- EF entities are returned directly; filtering by name runs in memory (#33).

- The XML output formatter cannot serialize entities with navigation collections (`Category.Products`);
  it logs warnings at startup. JSON responses are not affected.
