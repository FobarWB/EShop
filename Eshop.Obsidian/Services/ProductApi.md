---
type: service
verified: 2026-10-20
---

# ProductApi

REST API for products (CRUD).

- Project: [EShop.ProductApi](../../src/EShop.ProductApi/)
- Local URL: https://localhost:5002 (API reference: `/scalar`, OpenAPI document: `/openapi/v1.json`)

## Endpoints (`api/products`)

| Method | Route | Action |
| --- | --- | --- |
| GET | `/` | List all products |
| GET | `/{id}` | Get a product |
| POST | `/` | Create a product |
| PUT | `/{id}` | Update a product |
| DELETE | `/{id}` | Delete a product |

DELETE returns 409 ProblemDetails while cart items or order details reference the product (#32).

## Data

`Product` in the shared PostgreSQL database `eshop` (Northwind data), accessed through `IProductRepository`.
Reads go straight to PostgreSQL (no cache); a distributed cache is planned in #30.
`UnitsInStock` is required (`smallint NOT NULL DEFAULT 0`); a PUT with `"unitsInStock": null` returns 400,
an omitted value is stored as 0 (#31).

## Dependencies

Shared DbContext (`EShopDbContext` in `EShop.Data`). Called by [ClientApp](ClientApp.md).

## Tests

[Unit](../../tests/EShop.ProductApi.Tests.Unit/) (controller) and [integration](../../tests/EShop.ProductApi.Tests.Integration/)
(API) tests; see [Testing](../Learning/Testing.md).

## Known issues

- The list endpoint returns all products without paging.
- A product added to a cart between the reference check and the `DELETE` still fails with 500 (foreign key, race).
- EF entities are returned directly; filtering by category runs in memory (#33).
