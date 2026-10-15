---
type: service
verified: 2026-10-15
---

# CategoryApi

REST API for product categories (CRUD).

- Project: [Northwind.CategoryApi](../../Northwind.CategoryApi/)
- Local URL: https://localhost:5003 (Swagger: `/swagger`)

## Endpoints (`api/categories`)

| Method | Route | Action |
| --- | --- | --- |
| GET | `/` | List all categories |
| GET | `/{id}` | Get a category |
| POST | `/` | Create a category |
| PUT | `/{id}` | Update a category |
| DELETE | `/{id}` | Delete a category |

## Data

`Category` in the shared Northwind database.

## Dependencies

Shared DbContext (`Northwind.Common.DataContext.SqlServer`). Called by [ClientApp](ClientApp.md) and the archived `Northwind.Mvc`.
Kept separate from ProductApi for now: see [SeparateProductAndCategoryApis](../Decisions/SeparateProductAndCategoryApis.md).
