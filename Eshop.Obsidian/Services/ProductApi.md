---
type: service
verified: 2026-10-27
---

# ProductApi

REST API for products (CRUD).

- Project: [EShop.ProductApi](../../src/EShop.ProductApi/)
- Local URL: https://localhost:5002 (API reference: `/scalar`, OpenAPI document: `/openapi/v1.json`)
- Container: service `productapi`, through nginx at http://localhost:8080/api/products ([Dockerfile](../../src/EShop.ProductApi/Dockerfile))

## Endpoints (`api/products`)

| Method | Route | Action |
| --- | --- | --- |
| GET | `/?categoryId=1&page=1&pageSize=20` | One page of products, optionally of one category, ordered by id |
| GET | `/{id}` | Get a product |
| POST | `/` | Create a product (body `ProductRequest`); 201 with `Location` |
| PUT | `/{id}` | Update a product (body `ProductRequest`); the id comes from the route only |
| DELETE | `/{id}` | Delete a product |

GET is public; POST, PUT and DELETE need the `admin` role (401 without a token, 403 without the role),
see [Authentication](../Learning/Authentication.md).

The list is paged (`{ items, page, pageSize, totalCount }`, see [PagedListResponses](../Decisions/PagedListResponses.md)).
DELETE returns 409 ProblemDetails while cart items or order details reference the product (#32); a reference added
between that check and the `DELETE` is rejected by PostgreSQL and also ends as 409 (#33).
PUT and DELETE also return 409 when the product changed since it was read (its `xmin` no longer matches, e.g. a
checkout decremented the stock): the change is not stored, see [OptimisticConcurrencyWithXmin](../Decisions/OptimisticConcurrencyWithXmin.md).

## Contract

- `ProductResponse`: the scalar columns only, no navigations (`Category`, `Supplier`, `CartItems`, `OrderDetails`).
- `ProductRequest`: the same fields without the id, validated by data annotations (400 ProblemDetails).
  Binding the entity allowed overposting: a PUT with a nested `category` inserted a category, nested items with
  keys would have updated other rows (#33).
- Mapping in [ProductMappings](../../src/EShop.ProductApi/Dtos/ProductMappings.cs): `ToResponse()` extends
  `IQueryable<Product>`, so EF Core selects only the response columns; `ApplyTo()` copies a request onto the tracked
  entity, so an update writes only the changed columns.

## Data

`Product` in the shared PostgreSQL database `eshop` (Northwind data), accessed through `IProductRepository`.
Filtering by category runs in SQL. Reads go straight to PostgreSQL (no cache); a distributed cache is planned in #30.
`UnitsInStock` is required (`smallint NOT NULL DEFAULT 0`); a PUT with `"unitsInStock": null` returns 400,
an omitted value is stored as 0 (#31).

## Dependencies

Shared DbContext (`EShopDbContext` in `EShop.Data`). Called by [ClientApp](ClientApp.md).
Paging and error handling from `EShop.ServiceDefaults` (see [Services](Services.md)).
Startup project of the EF Core tools and of the migrations bundle: building the migrator runs its `Program.cs`.

## Tests

[Unit](../../tests/EShop.ProductApi.Tests.Unit/) (controller) and [integration](../../tests/EShop.ProductApi.Tests.Integration/)
(API) tests; see [Testing](../Learning/Testing.md).

## Known issues

- The category page asks for one page of 100 and filters and sorts it in the browser; server-side filters, sorting
  and paging: [#61](https://github.com/FobarWB/EShop/issues/61).
