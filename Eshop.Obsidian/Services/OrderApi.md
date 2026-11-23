---
type: service
verified: 2026-11-23
issue: 76
---

# OrderApi

REST API for orders: places orders (signed in or as a guest, #45) priced from its own product replica, confirms or
rejects them on ProductApi's stock answer (#76), lists the user's orders, cancels them, and lets admins move orders
through their lifecycle.

- Project: [EShop.OrderApi](../../src/EShop.OrderApi/)
- Local URL: https://localhost:5005 (API reference: `/scalar`, OpenAPI document: `/openapi/v1.json`)
- Container: service `orderapi`, through nginx at http://localhost:8080/api/orders ([Dockerfile](../../src/EShop.OrderApi/Dockerfile))

## Endpoints (`api/orders`)

Every endpoint except `POST /guest` needs a signed-in user and works on that user's orders: the id is the token's
`sub`, never a request value. 401 without a token. Errors are ProblemDetails.

| Method | Route | Action |
| --- | --- | --- |
| POST | `/` | Place an order: header `Idempotency-Key: <uuid>` (required), body `PlaceOrderRequest` (address and items); 201 with `Location` and the `Pending` order |
| POST | `/guest` | Anonymous, see [Guest checkout](#guest-checkout): `Idempotency-Key`, body `GuestOrderRequest`; 201, 409 `code: account-exists`, 409 with `productIds`, 400, 429, 503 |
| GET | `/?page=1&pageSize=20` | The user's orders, newest first: `PagedResponse<OrderSummaryResponse>` |
| GET | `/{id}` | Get one of the user's orders; 404 for a missing order and for another user's order (the response does not reveal that it exists) |
| POST | `/{id}/cancel` | Cancel the user's `Pending`, `Confirmed` or `Paid` order; ProductApi returns the stock (`order.cancelled`); 200 with the order, 404, 409 when it can no longer be cancelled |
| PUT | `/{id}/status` | Admin (realm role `admin`): body `{ "status": "Shipped" }`, any order; 204, 404, 409 for a forbidden transition, 400 for a missing or unknown status and for `Confirmed` and `Rejected` (set by the stock answer only), 403 without the role |

POST answers:

- 201: the order, `Pending`: ProductApi decides within seconds, the client polls `GET /{id}`. A retry with the same key
  returns the same order, again 201 ([IdempotentCheckout](../Decisions/IdempotentCheckout.md)).
- 400: missing or malformed `Idempotency-Key`, invalid address, no items, a quantity below 1.
- 409 with `productIds`: products the replica does not have (unknown, deleted, not arrived yet), discontinued or without
  a price ("Some products are no longer available"), or quantities above the replica's stock ("Not enough units in
  stock", a hint: the reservation decides). The client marks these items.

Orders are documents: no PUT of the order or DELETE. Status changes follow
[OrderLifecycle](../Decisions/OrderLifecycle.md).

## Checkout

`OrderRepository.CreateOrderAsync` serves both checkouts in one explicit transaction with two `SaveChangesAsync`;
since #76 it reads no cart and no `products` and writes no stock:

1. Signed in: an order of this user with this key exists → return it (a guest's retry is checked by the controller).
2. The request's lines summed per product (as `long`); the copies loaded from `order_products` (`AsNoTracking`, without
   tombstones).
3. A missing copy, discontinued or no price → 409 `ProductsUnavailable`; a quantity above the copy's stock → 409
   `InsufficientStock`. Nothing is written.
4. Insert `orders` (`Pending`) and `order_details` with the replica's name and price copied into
   `order_details.product_name` and `unit_price`, freight 5 (`OrderRepository.StandardFreight`, a flat fee),
   `order_date` = `DateTime.UtcNow`, `customer_email` from the token's `email` claim; record the creation in the journal
   (`null → Pending`). First `SaveChanges`: the order gets its id.
5. A unique violation (23505) on `ix_orders_user_id_idempotency_key`: a concurrent request with the same key committed
   first; return its order. Inside a transaction `SaveChanges` runs under a savepoint and rolls back to it, so the
   transaction is still usable for this read.
6. Add `order.placed` (with `ProductId` per line) and, for a guest, `order.guest-placed` to `outbox_messages`; second
   `SaveChanges`; commit. The order and its events are stored together or not at all
   ([TransactionalOutbox](../Decisions/TransactionalOutbox.md)).

ProductApi reserves the stock on `order.placed`; the order becomes `Confirmed` or `Rejected` when its answer arrives
(see Consumed events).

## Guest checkout

See [GuestCheckout](../Decisions/GuestCheckout.md). `OrdersController.PlaceGuestOrder`:

1. The email is trimmed and lower-cased (as Keycloak stores it; the account later finds its orders by it). Not logged.
2. An earlier try with this key and email (`RetrieveGuestOrderAsync`): 201 with that order, before anything else, so a
   retry after a lost response still gets its order once the account exists.
3. `IAccountDirectory.AccountExistsAsync` (Keycloak Admin API, `GET users?email=…&exact=true` as `eshop-orderapi`):
   an account → 409 `code: account-exists`. Keycloak down → `IdentityProviderUnavailableException` → 503.
4. `OrderRepository.PlaceGuestOrderAsync`: the same `CreateOrderAsync` as the signed-in checkout (replica, checks,
   prices, journal with `ChangedBy = guest`), with `user_id` null and `placed_as_guest`; the outbox gets `order.placed`
   (`PlacedAsGuest: true`) and `order.guest-placed`.

The endpoint has the rate limit `guest-checkout`: 5 requests per minute and client address (`GuestCheckout:PermitsPerMinute`),
429 above it. Behind nginx the address comes from `X-Forwarded-For` (`UseForwardedHeaders`, every proxy trusted: the
API ports are not published in compose; revisit for a public server, #59).

## Consumed events

`GuestAccountReadyHandler` on the queue `orders.guest-accounts` (consumer `orders`, `customer.guest-account-ready` from
[CustomerApi](CustomerApi.md), retries 5 s / 30 s / 2 min, then the DLQ), through the shared `MessageConsumer` of
`EShop.Messaging`: one `UPDATE orders SET user_id = … WHERE placed_as_guest AND user_id IS NULL AND customer_email = …`
inside the consumer's transaction with the inbox row. Orders of the email that have an owner are not touched; a
redelivery updates 0 rows.

- `ProductEventsHandler` on `orders.products` (#76): the upsert of ShoppingCart's replica, into `order_products`
  (no category). A deletion only writes the tombstone: placed orders keep their copies. Initial data: the republish.
- `StockEventsHandler` on `orders.stock` (#76): `stock.reserved` → `Confirmed` and `order.confirmed`; `stock.rejected` →
  `Rejected` and `order.rejected` (the short lines named from `order_details`). Journal `changed_by = productapi`.
  An order no longer `Pending` (cancelled meanwhile, a redelivery) stays as it is; an unknown order goes to the DLQ.
  The order's `xmin` orders the answer against a concurrent cancel: the later of the two fails (409 or a retry).
- The new queues use the queue name as the inbox key (`orders` stays for guest accounts): one event bound to two queues
  of one service must be processed by each.

## Status changes

`OrderRepository.ChangeStatusAsync(orderId, ownerId, next, changedBy)` serves both endpoints (`ownerId` null for an admin):

1. Load the order, tracked (`ownerId` filters to the user's orders); none: `NotFound`.
2. `Order.TryChangeStatus`: a forbidden transition is `NotAllowed` and writes nothing. An allowed one adds a journal row;
   `Shipped` also sets `shipped_date`. For `Cancelled` an `OrderCancelled` event (id, the buyer's `customer_email`,
   the same time as the journal row) is added to the outbox: the buyer is emailed also when an admin cancels.
3. One `SaveChanges`, one transaction: the journal row, the outbox row and `UPDATE orders … WHERE xmin = …`. The stock is
   ProductApi's (#76): it returns a reserved order's stock on `order.cancelled`.
4. A concurrent change of the same order fails the `xmin` check: `DbUpdateConcurrencyException`, 409, nothing stored.

## Contract

- `PlaceOrderRequest`: `address` (`ShippingAddressRequest`: `shipName` (40), `shipAddress` (60), `shipCity` (15),
  `shipRegion` (optional, 15), `shipPostalCode` (10), `shipCountry` (15): the lengths of the Northwind columns) and
  `items` (1–100 `OrderItemRequest`: `productId` ≥ 1, `quantity` 1–32767). The guest body uses the same two types.
  Names and prices never come from the client.
- `OrderResponse`: `orderId`, `orderDate` (UTC, with `Z`), `status`, `customerEmail` (null without an `email` claim), the address, `freight`, `total` (items + freight,
  summed in SQL), `items` (`productId`, `productName` and `unitPrice` as copied at checkout, `quantity`).
- `OrderSummaryResponse`: `orderId`, `orderDate`, `status`, `total`, `itemCount`.
- `ChangeOrderStatusRequest`: `status` (required, not `Confirmed` or `Rejected`).
- Enums are sent as names (`JsonStringEnumConverter` in `Program.cs`): `"status": "Cancelled"`.

## Events

Published to the topic exchange `eshop.events` by `OutboxPublisher` (a `BackgroundService` from `EShop.Messaging`,
registered with `AddOutboxPublisher<EShopDbContext>()`): routing key = `Type`, `MessageId` = outbox row id, persistent, publisher
confirms, up to ~2 s after the commit, in the order they occurred. Contracts in
[EShop.OrderApi.Contracts](../../src/EShop.OrderApi.Contracts/), see
[MessageContractsOwnedByProducer](../Decisions/MessageContractsOwnedByProducer.md).

| Type | Contract | When |
| --- | --- | --- |
| `order.placed` | `OrderPlaced(OrderId, Email, ShipName, Total, PlacedAt, Items[ProductName, Quantity, UnitPrice, ProductId], PlacedAsGuest)` | checkout (`PlacedAsGuest` since #45, `ProductId` since #76; older messages read false and 0) |
| `order.confirmed` | `OrderConfirmed(OrderId, UserId, Email, ShipName, Total, PlacedAsGuest, ConfirmedAt, Items[ProductId, ProductName, Quantity, UnitPrice])` | the stock is reserved (#76); `UserId` null for a guest order |
| `order.rejected` | `OrderRejected(OrderId, Email, ShipName, RejectedAt, Items[ProductName, Quantity, Available])` | the stock could not be reserved (#76): the short lines |
| `order.cancelled` | `OrderCancelled(OrderId, Email, CancelledAt)` | cancel by the buyer or an admin |
| `order.guest-placed` | `GuestOrderPlaced(OrderId, Email, PlacedAt)` | guest checkout: CustomerApi creates the account |

The outbox rows have the producer `orderapi` (`OrderApiOutbox.Producer`): this service's publisher sends only these.

With RabbitMQ down the API keeps working: the publisher logs a warning every 2 s, the rows wait, and the client's
automatic recovery reconnects when the broker is back. Consumers: [NotificationService](NotificationService.md)
(emails), [ProductApi](ProductApi.md) (`order.placed`, `order.cancelled`), [ShoppingCart](ShoppingCart.md)
(`order.confirmed`), [CustomerApi](CustomerApi.md) (`order.guest-placed`).

## Data

Writes `orders`, `order_details`, `order_status_changes`, `outbox_messages` and its replica `order_products`; reads no
other service's table (#76).

- `order_products` (#76): `product_id` (the key from the events), name, price, stock (a hint for checkout),
  `discontinued`, `deleted` (tombstone), `revision`; no category. No foreign key to `products`.

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
- `orders.placed_as_guest` (#45, migration `AddGuestOrders`): a guest order; `user_id` is null until the account is
  attached. `ix_orders_guest_idempotency_key` (unique, `WHERE placed_as_guest`) makes a guest's retry return its order,
  also after `user_id` was set; `ix_orders_guest_customer_email` (`WHERE placed_as_guest AND user_id IS NULL`) serves
  the attach.
- `outbox_messages.producer` (migration `AddOutboxProducer`; existing rows got `orderapi`, the column has no default).
- Migration `AddOrderLifecycle`: the Northwind rows got `Delivered`, orders placed through the API before it `Pending`;
  `product_name` was copied from `products`.
- `customer_id` (Northwind `char(5)`) stays null for new orders.
- `order_details.product_id` has no foreign key since #75: a product can be deleted, the order keeps its copy of name
  and price.

## Dependencies

Shared DbContext (`EShopDbContext` in `EShop.Data`); reads no other service's table: the products of
[ProductApi](ProductApi.md) through `order_products` (events), the cart's lines from the client (#76).
JWT validation, `User.GetUserId()`, `User.GetUserEmail()`, error handling (`ConcurrencyConflictHandler`) and the
Keycloak Admin API client (`AddKeycloakAdminClient`, section `KeycloakAdmin:OrderApi`, secret in user-secrets and
`ORDERAPI_KEYCLOAK_SECRET`) from `EShop.ServiceDefaults`; contracts of `EShop.CustomerApi.Contracts` and
`EShop.ProductApi.Contracts`; outbox and RabbitMQ from `EShop.Messaging` (`RabbitMq:Password` from user-secrets, in compose
`RabbitMq__Host=rabbitmq` and the password from `.env`; the container waits for a healthy RabbitMQ).
Called by [ClientApp](ClientApp.md).

## Tests

[Unit](../../tests/EShop.OrderApi.Tests.Unit/) (controller) and [integration](../../tests/EShop.OrderApi.Tests.Integration/)
(repository and API, database `eshop_test_orderapi`) tests. Races are played with `BeforeSaveChangesInterceptor`: in
`ChangeStatus_CancelledConcurrently_ThrowsAndStoresOneCancellation` a second cancel commits inside the first one's
`SaveChanges`: the first throws, one `order.cancelled` is stored. The race of two buyers moved to ProductApi (#76);
`PlaceOrder_Items_TakesNoStockAndLeavesTheCart` guards that the checkout writes neither `products` nor `carts`.
`OrderLifecycleTests` checks all 49 pairs of statuses. `ProductEventsHandlerTests` (the replica) and
`StockEventsHandlerTests` (confirm, reject with the lines' names, an order no longer `Pending`, a guest's confirmation
without a user id, an unknown order) call the handlers in a transaction, as the consumer does.
Outbox: checkout and cancel store exactly one event with the right payload (also an admin's cancel: the buyer's email),
a retry with the same key and every failed or concurrent checkout store none, other status changes store none.
The API tests remove `OutboxPublisher` and the message consumers from the services (`EShopApiFactory`): they would
publish test events to the developer's broker and take its messages.
Guest checkout (#45): API, repository and controller tests with `FakeAccountDirectory` (existing emails, Keycloak
down); the rate limit with its own factory (2 per minute); `KeycloakAccountDirectory` against a Keycloak container
(`KeycloakFixture`) that imports the repository's realm file; `GuestAccountReadyHandler` on the test database. The publisher itself is tested in [EShop.Messaging.Tests.Integration](../../tests/EShop.Messaging.Tests.Integration/).

## Known issues

- Freight is a fixed 5; delivery options are not modelled.
- Stock changes are not journaled per product (why the stock changed): [#64](https://github.com/FobarWB/EShop/issues/64).
- `total` keeps the scale of the `numeric` product (`23.00000000`); the JSON number is the same.
- A guest does not see the order's status: the result comes by email (sign-in for guest accounts: a separate issue).
- The page of a rejected order does not name the short lines: they are only in `order.rejected` (the email).
