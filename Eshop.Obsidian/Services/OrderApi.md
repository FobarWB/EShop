---
type: service
verified: 2026-10-28
issue: 43
---

# OrderApi

REST API for orders: turns the signed-in user's cart into an order, lists the user's orders, cancels them, and lets
admins move orders through their lifecycle.

- Project: [EShop.OrderApi](../../src/EShop.OrderApi/)
- Local URL: https://localhost:5005 (API reference: `/scalar`, OpenAPI document: `/openapi/v1.json`)
- Container: service `orderapi`, through nginx at http://localhost:8080/api/orders ([Dockerfile](../../src/EShop.OrderApi/Dockerfile))

## Endpoints (`api/orders`)

Every endpoint needs a signed-in user and works on that user's orders: the id is the token's `sub`, never a request
value. 401 without a token. Errors are ProblemDetails.

| Method | Route | Action |
| --- | --- | --- |
| POST | `/` | Place an order from the user's cart: header `Idempotency-Key: <uuid>` (required), body `PlaceOrderRequest` (shipping address); 201 with `Location` and the order |
| GET | `/?page=1&pageSize=20` | The user's orders, newest first: `PagedResponse<OrderSummaryResponse>` |
| GET | `/{id}` | Get one of the user's orders; 404 for a missing order and for another user's order (the response does not reveal that it exists) |
| POST | `/{id}/cancel` | Cancel the user's `Pending` or `Paid` order and return its stock; 200 with the order, 404, 409 when it can no longer be cancelled |
| PUT | `/{id}/status` | Admin (realm role `admin`): body `{ "status": "Shipped" }`, any order; 204, 404, 409 for a forbidden transition, 400 for a missing or unknown status, 403 without the role |

POST answers:

- 201: the order. A retry with the same key returns the same order, again 201 ([IdempotentCheckout](../Decisions/IdempotentCheckout.md)).
- 400: missing or malformed `Idempotency-Key`, invalid address, no cart or an empty cart.
- 409 with `productIds`: discontinued products or products without a price ("Some products are no longer available"),
  or quantities above the stock ("Not enough units in stock"). The client marks these items.
- 409 without `productIds`: a product changed between reading and writing (another buyer, an admin edit), see
  [OptimisticConcurrencyWithXmin](../Decisions/OptimisticConcurrencyWithXmin.md). Nothing is stored.

Orders are documents: no PUT of the order or DELETE. Status changes follow
[OrderLifecycle](../Decisions/OrderLifecycle.md).

## Checkout

`OrderRepository.PlaceOrderAsync` writes everything in one `SaveChangesAsync`, so in one transaction:

1. An order of this user with this key exists: return it (the cart is already empty after the first success).
2. Load the cart with its items and their products, tracked; each product's `xmin` is read here.
3. Check availability and stock; a failed check returns a result and writes nothing.
4. Insert `orders` and `order_details` with the current prices and names copied into `order_details.unit_price` and
   `order_details.product_name`, freight 5 (`OrderRepository.StandardFreight`, a flat fee), `order_date` =
   `DateTime.UtcNow`; record the creation in the journal (`null → Pending`); decrement `products.units_in_stock`;
   delete the cart items.
5. A unique violation (23505) on `ix_orders_user_id_idempotency_key`: a concurrent request with the same key committed
   first; return its order.

## Status changes

`OrderRepository.ChangeStatusAsync(orderId, ownerId, next, changedBy)` serves both endpoints (`ownerId` null for an admin):

1. Load the order, tracked (`ownerId` filters to the user's orders); none: `NotFound`.
2. `Order.TryChangeStatus`: a forbidden transition is `NotAllowed` and writes nothing. An allowed one adds a journal row;
   `Shipped` also sets `shipped_date`.
3. In an explicit transaction: `SaveChanges` (journal row, `UPDATE orders … WHERE xmin = …`), then for `Cancelled`
   one `UPDATE products SET units_in_stock = units_in_stock + quantity` for the order's products; commit.
4. A concurrent change of the same order fails the `xmin` check: `DbUpdateConcurrencyException`, 409, nothing stored.

## Contract

- `PlaceOrderRequest`: `shipName` (40), `shipAddress` (60), `shipCity` (15), `shipRegion` (optional, 15),
  `shipPostalCode` (10), `shipCountry` (15): the lengths of the Northwind columns. Items and prices never come from the client.
- `OrderResponse`: `orderId`, `orderDate` (UTC, with `Z`), `status`, the address, `freight`, `total` (items + freight,
  summed in SQL), `items` (`productId`, `productName` and `unitPrice` as copied at checkout, `quantity`).
- `OrderSummaryResponse`: `orderId`, `orderDate`, `status`, `total`, `itemCount`.
- `ChangeOrderStatusRequest`: `status` (required).
- Enums are sent as names (`JsonStringEnumConverter` in `Program.cs`): `"status": "Cancelled"`.

## Data

Writes `orders`, `order_details`, `order_status_changes`, `products.units_in_stock` and `cart_items`; reads `carts`
and `products`.

- `orders.user_id` (`varchar(50)`, null for the Northwind orders) and `orders.idempotency_key` (`uuid`), with a unique
  index on both. PostgreSQL treats NULLs as distinct in a unique index, so the 830 Northwind rows do not collide.
  The index also serves lookups by `user_id` (its leading column); `(user_id, order_date)` serves the order list.
- `orders.status` (`varchar(20)`, CHECK `ck_orders_status`) and `orders.xmin` as the concurrency token.
- `order_date`, `required_date` and `shipped_date` are `timestamptz`: Npgsql reads and writes `DateTime` of kind `Utc`.
- `order_status_changes`: the journal, see [OrderLifecycle](../Decisions/OrderLifecycle.md).
- Migration `AddOrderLifecycle`: the Northwind rows got `Delivered`, orders placed through the API before it `Pending`;
  `product_name` was copied from `products`.
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
is stored and the stock is 0. In the repository test `ChangeStatus_CancelledConcurrently_ThrowsAndReturnsStockOnce`
a second cancel commits inside the first one's `SaveChanges`: the first throws, the stock comes back once.
`OrderLifecycleTests` checks all 25 pairs of statuses.

## Known issues

- Freight is a fixed 5; delivery options are not modelled.
- Stock changes are not journaled per product (why the stock changed): [#64](https://github.com/FobarWB/EShop/issues/64).
- `total` keeps the scale of the `numeric` product (`23.00000000`); the JSON number is the same.
