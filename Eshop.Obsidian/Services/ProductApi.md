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

## Data

`Product` in the shared PostgreSQL database `eshop` (Northwind data), accessed through `IProductRepository`.
Reads go straight to PostgreSQL (no cache); a distributed cache is planned in #30.

## Dependencies

Shared DbContext (`EShopDbContext` in `EShop.Data`). Called by [ClientApp](ClientApp.md).

## Tests

[Unit](../../tests/EShop.ProductApi.Tests.Unit/) (controller) and [integration](../../tests/EShop.ProductApi.Tests.Integration/)
(API) tests; see [Testing](../Learning/Testing.md).

## Known issues

- The list endpoint returns all products without paging.
- Deleting a product that is still referenced (cart items, order details) returns 500 instead of 409 (#32).
- EF entities are returned directly; filtering by category runs in memory (#33).
