---
type: service
verified: 2026-10-27
issue: 42
---

# OrderApi

REST API for orders: turns the signed-in user's cart into an order and returns the user's orders.

- Project: [EShop.OrderApi](../../src/EShop.OrderApi/)
- Local URL: https://localhost:5005 (API reference: `/scalar`, OpenAPI document: `/openapi/v1.json`)
- Container: service `orderapi`, through nginx at http://localhost:8080/api/orders ([Dockerfile](../../src/EShop.OrderApi/Dockerfile))

## Endpoints (`api/orders`)

Every endpoint needs a signed-in user and works on that user's orders: the id is the token's `sub`, never a request
value. 401 without a token. Errors are ProblemDetails.

| Method | Route | Action |
| --- | --- | --- |
| POST | `/` | Place an order from the user's cart: header `Idempotency-Key: <uuid>` (required), body `PlaceOrderRequest` (shipping address); 201 with `Location` and the order |
| GET | `/{id}` | Get one of the user's orders; 404 for a missing order and for another user's order (the response does not reveal that it exists) |

POST answers:

- 201: the order. A retry with the same key returns the same order, again 201 ([IdempotentCheckout](../Decisions/IdempotentCheckout.md)).
- 400: missing or malformed `Idempotency-Key`, invalid address, no cart or an empty cart.
- 409 with `productIds`: discontinued products or products without a price ("Some products are no longer available"),
  or quantities above the stock ("Not enough units in stock"). The client marks these items.
- 409 without `productIds`: a product changed between reading and writing (another buyer, an admin edit), see
  [OptimisticConcurrencyWithXmin](../Decisions/OptimisticConcurrencyWithXmin.md). Nothing is stored.

Orders are documents, not editable records: there is no PUT or DELETE. The order list, statuses and cancellation
(which returns the stock) come with [#43](https://github.com/FobarWB/EShop/issues/43).

## Checkout

`OrderRepository.PlaceOrderAsync` writes everything in one `SaveChangesAsync`, so in one transaction:

1. An order of this user with this key exists: return it (the cart is already empty after the first success).
2. Load the cart with its items and their products, tracked; each product's `xmin` is read here.
3. Check availability and stock; a failed check returns a result and writes nothing.
4. Insert `orders` and `order_details` with the current prices copied into `order_details.unit_price`, freight 5
   (`OrderRepository.StandardFreight`, a flat fee); decrement `products.units_in_stock`; delete the cart items.
5. A unique violation (23505) on `ix_orders_user_id_idempotency_key`: a concurrent request with the same key committed
   first; return its order.

## Contract

- `PlaceOrderRequest`: `shipName` (40), `shipAddress` (60), `shipCity` (15), `shipRegion` (optional, 15),
  `shipPostalCode` (10), `shipCountry` (15): the lengths of the Northwind columns. Items and prices never come from the client.
- `OrderResponse`: `orderId`, `orderDate` (UTC, with `Z`), the address, `freight`, `total` (items + freight, summed in SQL),
  `items` (`productId`, `productName`, `unitPrice` as copied at checkout, `quantity`).

## Data

Writes `orders`, `order_details`, `products.units_in_stock` and `cart_items`; reads `carts` and `products`.

- `orders.user_id` (`varchar(50)`, null for the Northwind orders) and `orders.idempotency_key` (`uuid`), with a unique
  index on both. PostgreSQL treats NULLs as distinct in a unique index, so the 830 Northwind rows do not collide.
  The index also serves lookups by `user_id` (its leading column).
- `order_date` is `timestamp without time zone`: the API stores UTC as `DateTimeKind.Unspecified` (Npgsql refuses
  `Utc` for this type) and marks it as UTC in the response.
- `customer_id` (Northwind `char(5)`) stays null for new orders.

## Dependencies

Shared DbContext (`EShopDbContext` in `EShop.Data`); reads the cart of [ShoppingCart](ShoppingCart.md) and the products
of [ProductApi](ProductApi.md) directly. With a database per service ([#51](https://github.com/FobarWB/EShop/issues/51))
the checkout would get the cart by request and publish `OrderPlaced` to change the stock ([#50](https://github.com/FobarWB/EShop/issues/50)).
JWT validation, `User.GetUserId()` and error handling (`ConcurrencyConflictHandler`) from `EShop.ServiceDefaults`.
Called by [ClientApp](ClientApp.md).

## Tests

[Unit](../../tests/EShop.OrderApi.Tests.Unit/) (controller) and [integration](../../tests/EShop.OrderApi.Tests.Integration/)
(repository and API, database `eshop_test_orderapi`) tests. Races are played with `BeforeSaveChangesInterceptor`: in the
API test a second buyer runs a whole checkout inside the first buyer's `SaveChanges`; the first gets 409, one order
is stored and the stock is 0.

## Known issues

- Freight is a fixed 5; delivery options are not modelled.
- `order_date` should be `timestamptz`; change it with #43, which changes `orders` anyway.
- `total` keeps the scale of the `numeric` product (`23.00000000`); the JSON number is the same.
