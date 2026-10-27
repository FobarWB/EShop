---
type: service
verified: 2026-10-27
---

# CategoryApi

REST API for product categories (CRUD).

- Project: [EShop.CategoryApi](../../src/EShop.CategoryApi/)
- Local URL: https://localhost:5003 (API reference: `/scalar`, OpenAPI document: `/openapi/v1.json`)
- Container: service `categoryapi`, through nginx at http://localhost:8080/api/categories ([Dockerfile](../../src/EShop.CategoryApi/Dockerfile))

## Endpoints (`api/categories`)

| Method | Route | Action |
| --- | --- | --- |
| GET | `/?categoryName=bev&page=1&pageSize=20` | One page of categories, optionally filtered by name, ordered by id |
| GET | `/{id}` | Get a category |
| POST | `/` | Create a category (body `CategoryRequest`); 201 with `Location` |
| PUT | `/{id}` | Update a category (body `CategoryRequest`); the id comes from the route only |
| DELETE | `/{id}` | Delete a category |

GET is public; POST, PUT and DELETE need the `admin` role (401 without a token, 403 without the role),
see [Authentication](../Learning/Authentication.md).

The list is paged (`{ items, page, pageSize, totalCount }`, see [PagedListResponses](../Decisions/PagedListResponses.md)).
`categoryName` is a case-insensitive "contains" in SQL (`ILIKE`); `%`, `_` and `\` are escaped, so `%` matches only
a literal percent sign. A blank name means no filter.
DELETE returns 409 ProblemDetails while products still belong to the category (#32); a product added between that check
and the `DELETE` is rejected by PostgreSQL and also ends as 409 (#33).

## Contract

- `CategoryResponse(CategoryId, CategoryName, Description)`, `CategoryRequest(CategoryName, Description)`.
- No `Picture`: Northwind stores OLE objects (an Access header before a bitmap) that a browser cannot show, about
  14 KB of base64 each; the client shows its own images (`public/images/category{id}.jpeg`). The column and its
  data stay; an update copies the request onto the tracked entity, so it no longer overwrites the picture with `NULL`
  (before #33 every edit from the client erased it). Serving pictures would be a separate endpoint with `image/*`.
- Mapping in [CategoryMappings](../../src/EShop.CategoryApi/Dtos/CategoryMappings.cs), as in
  [ProductApi](ProductApi.md#contract): the projection never reads the `picture` column.

## Data

`Category` in the shared PostgreSQL database `eshop` (Northwind data).
Reads go straight to PostgreSQL (no cache); a distributed cache is planned in #30.

## Dependencies

Shared DbContext (`EShopDbContext` in `EShop.Data`). Called by [ClientApp](ClientApp.md).
Kept separate from ProductApi for now: see [SeparateProductAndCategoryApis](../Decisions/SeparateProductAndCategoryApis.md).

## Tests

[Unit](../../tests/EShop.CategoryApi.Tests.Unit/) (controller) and [integration](../../tests/EShop.CategoryApi.Tests.Integration/)
(API) tests; see [Testing](../Learning/Testing.md).
