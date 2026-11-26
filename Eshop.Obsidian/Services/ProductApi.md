---
type: service
verified: 2026-11-26
issue: 77
---

# ProductApi

REST API for products (CRUD); owns the stock: reserves it for placed orders and returns it on cancel (#76).

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
| POST | `/events/republish` | `ProductChanged` of every product to the outbox; 202 `{ products: N }` |

GET is public; POST, PUT and DELETE need the `admin` role (401 without a token, 403 without the role),
see [Authentication](../Learning/Authentication.md).

The list is paged (`{ items, page, pageSize, totalCount }`, see [PagedListResponses](../Decisions/PagedListResponses.md)).
DELETE no longer checks carts and orders (#75): orders keep a copy of name and price, carts drop the product on
`ProductDeleted`. Before #75 it returned 409 while cart items or order details referenced the product (#32, #33).
PUT and DELETE also return 409 when the product changed since it was read (its `xmin` no longer matches, e.g. a
reservation changed the stock): the change is not stored, see [OptimisticConcurrencyWithXmin](../Decisions/OptimisticConcurrencyWithXmin.md).

## Contract

- `ProductResponse`: the scalar columns only, no navigations (`Category`, `Supplier`, `CartItems`, `OrderDetails`).
- `ProductRequest`: the same fields without the id, validated by data annotations (400 ProblemDetails).
  Binding the entity allowed overposting: a PUT with a nested `category` inserted a category, nested items with
  keys would have updated other rows (#33).
- Mapping in [ProductMappings](../../src/EShop.ProductApi/Dtos/ProductMappings.cs): `ToResponse()` extends
  `IQueryable<Product>`, so EF Core selects only the response columns; `ApplyTo()` copies a request onto the tracked
  entity, so an update writes only the changed columns.

## Data

`Product`, `stock_reservations`, `outbox_messages` and `inbox_messages` in the database and schema `catalog` (`CatalogDbContext` in
[EShop.Catalog.Data](../../src/EShop.Catalog.Data/), shared with CategoryApi, #77, #78; Northwind data), accessed through
`IProductRepository`.
Filtering by category runs in SQL. Reads go straight to PostgreSQL (no cache); a distributed cache is planned in #30.
`UnitsInStock` is required (`smallint NOT NULL DEFAULT 0`); a PUT with `"unitsInStock": null` returns 400,
an omitted value is stored as 0 (#31).

## Events (#75)

Published through the outbox (producer `productapi`, `ProductApiOutbox.Producer`) to `eshop.events`; contracts in
[EShop.ProductApi.Contracts](../../src/EShop.ProductApi.Contracts/).

| Type | Contract | When |
| --- | --- | --- |
| `product.changed` | `ProductChanged(ProductId, ProductName, CategoryId, UnitPrice, UnitsInStock, Discontinued, Revision, ChangedAt)` | create, a PUT that changes something, a reservation or its return (#76), republish |
| `product.deleted` | `ProductDeleted(ProductId, Revision, DeletedAt)` | delete |
| `stock.reserved` | `StockReserved(OrderId, ReservedAt)` | the stock of every line of an order is taken (#76) |
| `stock.rejected` | `StockRejected(OrderId, Shortages[ProductId, Requested, Available], RejectedAt)` | a line was short, nothing taken (#76) |

- A snapshot, not a diff: consumers replace their copy and ignore a revision not newer than theirs.
- `products.revision` (`bigint`, 0 for Northwind) grows by one with every change, in `ProductEvents` only: the one place
  that changes the revision and adds the event, saved with the change in one `SaveChanges`. Read + 1 in C# is safe: the
  `UPDATE` checks `xmin`, so of two concurrent PUTs the second fails with 409. Not `xmin` itself: it changes with every
  `UPDATE` of the row (a checkout's stock change too) and wraps around at 2^32; an event time is no order either (it is
  taken before the commit).
- Create saves twice in one transaction: the event needs the generated id. A PUT without changes publishes nothing.
- Republish writes the current state without a new revision: safe to repeat. Call it only after the consumers have
  declared their queues (ShoppingCart started once): an event without a bound queue is dropped (`mandatory: false`).

## Stock reservation (#76)

`Reservations/OrderEventsHandler` on the queue `products.orders` (`order.placed`, `order.cancelled` from
[OrderApi](OrderApi.md)), see [DatabasePerService](../Decisions/DatabasePerService.md):

- `order.placed`: a reservation of the order exists → nothing (a redelivery, or the tombstone of an order cancelled
  first). Otherwise the lines summed per product, then
  `SELECT p.*, p.xmin FROM products p WHERE p.product_id = ANY(@ids) ORDER BY p.product_id FOR UPDATE`. A missing
  product, `ProductId` 0, discontinued or no price counts as 0 available. All lines available → the stock decremented
  through `ProductEvents.AddChanged` (revision + 1, `ProductChanged`), `Reserved`, `stock.reserved`; any line short →
  nothing taken, `Rejected`, `stock.rejected` with every shortage.
- `order.cancelled`: the reservation locked `FOR UPDATE`; none → the tombstone `Cancelled`; `Reserved` → the stock
  returned (`AddChanged`; a product deleted meanwhile is skipped), `Released`; otherwise nothing.
- Inside the consumer's transaction: stock, events, answer and inbox row commit together. `FOR UPDATE` makes a second
  order for the same product wait and read the stock the first one left; the same lock order (`ORDER BY product_id`)
  rules out a deadlock. `xmin` stays as a safety net.
- `stock_reservations` (`order_id` from the event, `status` text with CHECK, `created_at`, `released_at`) and
  `stock_reservation_lines` (`order_id`, `product_id`, `quantity`; cascade from the reservation, no foreign key to
  `products`: the lines are a record).

## Dependencies

`CatalogDbContext`, registered with `AddServiceDbContext` from `EShop.Persistence`; outbox and RabbitMQ from `EShop.Messaging` (`RabbitMq:Password`
from user-secrets; in compose it waits for a healthy RabbitMQ); contracts of `EShop.OrderApi.Contracts`.
Called by [ClientApp](ClientApp.md).
Paging and error handling from `EShop.ServiceDefaults` (see [Services](Services.md)).
No longer the startup project of the EF Core tools (#77): each context has a design-time factory.

## Tests

[Unit](../../tests/EShop.ProductApi.Tests.Unit/) (controller) and [integration](../../tests/EShop.ProductApi.Tests.Integration/)
(API) tests; see [Testing](../Learning/Testing.md). The API tests read `outbox_messages`: one event per change with
the right type, fields and `Revision`, none for a PUT without changes or a 409, one per product for the republish.
`OrderEventsHandlerTests`: all or nothing, every shortage, products not for sale, a redelivery, the stock returned once,
cancel before place, cancel after a rejection, two orders for the last unit (the second waits for the first one's lock,
then is rejected).

## Known issues

- `Revision` could replace `xmin` as the concurrency token now that ProductApi is the only writer of `products`.

- The category page asks for one page of 100 and filters and sorts it in the browser; server-side filters, sorting
  and paging: [#61](https://github.com/FobarWB/EShop/issues/61).
