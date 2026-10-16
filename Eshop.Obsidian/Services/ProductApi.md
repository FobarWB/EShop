---
type: service
verified: 2026-10-16
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

## Dependencies

Shared DbContext (`EShopDbContext` in `EShop.Data`). Called by [ClientApp](ClientApp.md).

## Known issues

- The list endpoint returns all products without paging.
