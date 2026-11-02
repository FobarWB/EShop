---
type: service
verified: 2026-11-02
issue: 50
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

`OrderRepository.PlaceOrderAsync` writes in one explicit transaction with two `SaveChangesAsync`:

1. An order of this user with this key exists: return it (the cart is already empty after the first success).
2. Load the cart with its items and their products, tracked; each product's `xmin` is read here.
3. Check availability and stock; a failed check returns a result and writes nothing.
4. Insert `orders` and `order_details` with the current prices and names copied into `order_details.unit_price` and
   `order_details.product_name`, freight 5 (`OrderRepository.StandardFreight`, a flat fee), `order_date` =
   `DateTime.UtcNow`, `customer_email` from the token's `email` claim; record the creation in the journal
   (`null → Pending`); decrement `products.units_in_stock`; delete the cart items. First `SaveChanges`: the order gets its id.
5. A unique violation (23505) on `ix_orders_user_id_idempotency_key`: a concurrent request with the same key committed
   first; return its order. Inside a transaction `SaveChanges` runs under a savepoint and rolls back to it, so the
   transaction is still usable for this read.
6. Add the `OrderPlaced` event (id, email, recipient, total, items) to `outbox_messages`; second `SaveChanges`; commit.
   The order and its event are stored together or not at all ([TransactionalOutbox](../Decisions/TransactionalOutbox.md)).

## Status changes

`OrderRepository.ChangeStatusAsync(orderId, ownerId, next, changedBy)` serves both endpoints (`ownerId` null for an admin):

1. Load the order, tracked (`ownerId` filters to the user's orders); none: `NotFound`.
2. `Order.TryChangeStatus`: a forbidden transition is `NotAllowed` and writes nothing. An allowed one adds a journal row;
   `Shipped` also sets `shipped_date`. For `Cancelled` an `OrderCancelled` event (id, the buyer's `customer_email`,
   the same time as the journal row) is added to the outbox: the buyer is emailed also when an admin cancels.
3. In an explicit transaction: `SaveChanges` (journal row, outbox row, `UPDATE orders … WHERE xmin = …`), then for
   `Cancelled` one `UPDATE products SET units_in_stock = units_in_stock + quantity` for the order's products; commit.
   The outbox row is added before `SaveChanges`: `ExecuteUpdate` after it writes SQL directly and saves nothing tracked.
4. A concurrent change of the same order fails the `xmin` check: `DbUpdateConcurrencyException`, 409, nothing stored.

## Contract

- `PlaceOrderRequest`: `shipName` (40), `shipAddress` (60), `shipCity` (15), `shipRegion` (optional, 15),
  `shipPostalCode` (10), `shipCountry` (15): the lengths of the Northwind columns. Items and prices never come from the client.
- `OrderResponse`: `orderId`, `orderDate` (UTC, with `Z`), `status`, `customerEmail` (null without an `email` claim), the address, `freight`, `total` (items + freight,
  summed in SQL), `items` (`productId`, `productName` and `unitPrice` as copied at checkout, `quantity`).
- `OrderSummaryResponse`: `orderId`, `orderDate`, `status`, `total`, `itemCount`.
- `ChangeOrderStatusRequest`: `status` (required).
- Enums are sent as names (`JsonStringEnumConverter` in `Program.cs`): `"status": "Cancelled"`.

## Events

Published to the topic exchange `eshop.events` by `OutboxPublisher` (a `BackgroundService` from `EShop.Messaging`,
registered with `AddOutboxPublisher()`): routing key = `Type`, `MessageId` = outbox row id, persistent, publisher
confirms, up to ~2 s after the commit, in the order they occurred. Contracts in
[EShop.OrderApi.Contracts](../../src/EShop.OrderApi.Contracts/), see
[MessageContractsOwnedByProducer](../Decisions/MessageContractsOwnedByProducer.md).

| Type | Contract | When |
| --- | --- | --- |
| `order.placed` | `OrderPlaced(OrderId, Email, ShipName, Total, PlacedAt, Items[ProductName, Quantity, UnitPrice])` | checkout |
| `order.cancelled` | `OrderCancelled(OrderId, Email, CancelledAt)` | cancel by the buyer or an admin |

With RabbitMQ down the API keeps working: the publisher logs a warning every 2 s, the rows wait, and the client's
automatic recovery reconnects when the broker is back. Consumer: [NotificationService](NotificationService.md).

## Data

Writes `orders`, `order_details`, `order_status_changes`, `outbox_messages`, `products.units_in_stock` and
`cart_items`; reads `carts` and `products`.

- `orders.customer_email` (`varchar(254)`, the maximum length of an address; null for Northwind and users without an
  address): a copy of the buyer's address at checkout, like the prices.
- `outbox_messages`: `id` (UUIDv7 from code: the `MessageId`, known before the insert, appended at the end of the
  key index), `type`, `payload` (`jsonb`), `occurred_at`, `processed_at` (null = pending), `attempts`, `last_error`;
  partial index `ix_outbox_messages_occurred_at … WHERE processed_at IS NULL`.

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
the checkout would get the cart by request and change the stock through events.
JWT validation, `User.GetUserId()`, `User.GetUserEmail()` and error handling (`ConcurrencyConflictHandler`) from
`EShop.ServiceDefaults`; outbox and RabbitMQ from `EShop.Messaging` (`RabbitMq:Password` from user-secrets, in compose
`RabbitMq__Host=rabbitmq` and the password from `.env`; the container waits for a healthy RabbitMQ).
Called by [ClientApp](ClientApp.md).

## Tests

[Unit](../../tests/EShop.OrderApi.Tests.Unit/) (controller) and [integration](../../tests/EShop.OrderApi.Tests.Integration/)
(repository and API, database `eshop_test_orderapi`) tests. Races are played with `BeforeSaveChangesInterceptor`: in the
API test a second buyer runs a whole checkout inside the first buyer's `SaveChanges`; the first gets 409, one order
is stored and the stock is 0. In the repository test `ChangeStatus_CancelledConcurrently_ThrowsAndReturnsStockOnce`
a second cancel commits inside the first one's `SaveChanges`: the first throws, the stock comes back once.
`OrderLifecycleTests` checks all 25 pairs of statuses.
Outbox: checkout and cancel store exactly one event with the right payload (also an admin's cancel: the buyer's email),
a retry with the same key and every failed or concurrent checkout store none, other status changes store none.
The API tests remove `OutboxPublisher` from the services (`EShopApiFactory`): it would publish test events to the
developer's broker. The publisher itself is tested in [EShop.Messaging.Tests.Integration](../../tests/EShop.Messaging.Tests.Integration/).

## Known issues

- Freight is a fixed 5; delivery options are not modelled.
- Stock changes are not journaled per product (why the stock changed): [#64](https://github.com/FobarWB/EShop/issues/64).
- `total` keeps the scale of the `numeric` product (`23.00000000`); the JSON number is the same.
