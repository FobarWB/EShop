---
type: decision
status: accepted
verified: 2026-11-19
issue: 51
---

# DatabasePerService

## Context

All services share one PostgreSQL database and one `EShopDbContext`. Besides foreign keys across services
(`cart_items → products`, `order_details → products`), services read and write each other's tables: ShoppingCart
reads `products` and `categories` for the cart; OrderApi reads `carts`, decrements `products.units_in_stock` and
empties the cart in its checkout transaction, and returns the stock on cancel; ProductApi checks `cart_items` and
`order_details` before a delete. A schema change of one service can break another, and the checkout's consistency
rests on one transaction over three services' data.

## Decision

- **A database per bounded context**, not per deployable: catalog (shared by ProductApi and CategoryApi: their tables
  are tied by a foreign key, and CategoryApi checks products before a delete; the client uses the two APIs separately,
  so they stay two services, see [SeparateProductAndCategoryApis](SeparateProductAndCategoryApis.md)), cart, ordering
  (with the Northwind back-office tables that only orders reference), customer, notification.
- **Data another service needs is copied, not read**: ProductApi publishes snapshot events (`ProductChanged` with a
  version, `ProductDeleted`); ShoppingCart and OrderApi keep replicas and ignore an event not newer than the stored
  version. The cart returns `categoryId`; the client resolves the name, as it does for the anonymous cart.
- **Checkout as a saga (choreography) over RabbitMQ**: OrderApi stores a `Pending` order priced from its replica and
  publishes `OrderPlaced`; ProductApi reserves the stock of all lines in one transaction (conditional update,
  `stock_reservations`) and answers `StockReserved` or `StockRejected`; the order becomes `Confirmed` or `Rejected`.
  Cancelling publishes `OrderCancelled`, and ProductApi returns the reserved stock once (compensation). ShoppingCart
  empties the cart on `OrderPlaced`. Chosen over a synchronous reservation call: it builds on the messaging of #50,
  and OrderApi does not need ProductApi to be up to accept an order.
- **Steps**, each keeping the shop working, merged into the integration branch of #51: messaging independent of
  `EShopDbContext` (#74), events and replicas (#75), checkout saga (#76), a DbContext and schema per service (#77),
  separate databases (#78).
- **No data migration**: nothing is deployed yet. Each context starts with its own `InitialCreate` (#77), the Northwind
  data is split by service, and the local volume is recreated.

## Consequences

- Eventual consistency: a price or stock change reaches the carts after the event; the cart's stock is a hint, the
  reservation decides.
- A placed order can still be rejected for stock: the client polls the order until `Confirmed` or `Rejected`, and the
  confirmation email is sent on `OrderConfirmed`.
- Overselling is prevented by ProductApi alone (conditional update), no longer by `xmin` checks in OrderApi.
- Deleting a product no longer checks carts and orders: orders keep a snapshot, carts get `ProductDeleted`.
- [ShoppingCartReadsCatalogData](ShoppingCartReadsCatalogData.md) is superseded by #75, the stock return of
  [OrderLifecycle](OrderLifecycle.md) by #76.

## Refined in #75

- `products.revision` orders the snapshots, not `xmin` (changes with every UPDATE, wraps around) and not the event time
  (taken before the commit). Two counters for now: `Version` = `xmin` (concurrency), `Revision` = events; once
  ProductApi is the only writer of `products` (#76), `Revision` can replace `xmin` as the concurrency token.
- A deleted product stays in a replica as a tombstone, so a late `ProductChanged` cannot bring it back.
- Initial replica data: `POST api/products/events/republish`, not SQL copied between tables.
- The OrderApi replica moved to #76, with its first reader; stock changed by OrderApi does not reach the replicas
  until #76.
- No foreign key `cart_items → cart_products` until the cart has its own DbContext (#77): now it would need a backfill
  and replica rows in OrderApi's tests.
- Validation that a product exists moves from the foreign keys to the services: the cart checks its copy, the checkout
  reads `products` by id (#75), then the replica and the reservation (#76).
