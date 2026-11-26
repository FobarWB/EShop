---
type: decision
status: accepted
verified: 2026-11-26
issue: 77
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
  publishes `OrderPlaced`; ProductApi reserves the stock of all lines in one transaction (rows locked with
  `SELECT … FOR UPDATE`, `stock_reservations`) and answers `StockReserved` or `StockRejected`; the order becomes
  `Confirmed` or `Rejected`. Cancelling publishes `OrderCancelled`, and ProductApi returns the reserved stock once
  (compensation). ShoppingCart removes the ordered quantities from the cart on `OrderConfirmed`. Chosen over a synchronous reservation call: it builds on the messaging of #50,
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
- Overselling is prevented by ProductApi alone (row locks in the reservation, `xmin` as a safety net), no longer by
  `xmin` checks in OrderApi.
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

## Refined in #76

- **Lines from the client**: `POST /api/orders` takes `{ address, items }`, as the guest checkout; names and prices come
  from OrderApi's replica `order_products`, so the client cannot set a price. Considered: OrderApi asking ShoppingCart
  for the cart synchronously (the cart service would have to be up to accept an order).
- **Early check from the replica**: an unknown, deleted, discontinued or unpriced product, or a quantity above the
  replica's stock, is a 409 at checkout. A hint only: the reservation decides.
- **Reservation**: `SELECT … FOR UPDATE` of the order's products in product id order (no deadlock between two orders
  sharing products), every stock change through `ProductEvents.AddChanged` (a revision and a `ProductChanged`).
  Considered: a conditional `UPDATE … WHERE units_in_stock >= @q`, which would build the revision and the snapshot in a
  second, SQL-only path.
- **`stock_reservations` records the decision per order** (`Reserved`, `Rejected`, `Released`, `Cancelled`): a redelivered
  event changes nothing, the stock is returned once, and an `order.cancelled` overtaking its `order.placed` (a retry
  reorders them) leaves the tombstone `Cancelled`, so the late `order.placed` takes nothing.
- **The cart is emptied on `OrderConfirmed`**, not on `OrderPlaced`: a rejected order keeps the cart. Only the ordered
  quantities go; what was added after the checkout stays. A guest order's `OrderConfirmed` carries no user id, also when
  CustomerApi attached the account before the answer.
- **Lifecycle**: `Pending → Confirmed | Rejected`, only a confirmed order can be paid; admins cannot set `Confirmed` or
  `Rejected` ([OrderLifecycle](OrderLifecycle.md)).
- **Emails** on `order.confirmed` and `order.rejected`; NotificationService unbinds `order.placed` on start.
- **Client**: the order page polls `GET /api/orders/{id}` every 2 s, at most 15 times; the guest page says the result
  comes by email. The cart counter is read again 3 s after `Confirmed`, once `order.confirmed` has left the outbox.
- Not in #76: `Revision` replacing `xmin` as the products' concurrency token.
- Checked on compose: an order confirmed 4.3 s after the checkout (two outbox polls); a stock lowered behind the replica's
  back rejected the order and kept the cart; two buyers of the last unit got one `Confirmed` and one `Rejected`, stock 0,
  decided within 14 ms; a cancel returned the stock once (`Released`); a guest order was confirmed with `userId: null`;
  all DLQs empty.
- Open: sign-in for the account created at a guest checkout, and whether such an order waits for the email confirmation
  ([#83](https://github.com/FobarWB/EShop/issues/83)).

## Refined in #77

- **A schema per bounded context, one database** (`catalog`, `cart`, `ordering`, `customer`, `notification`), each with
  its own DbContext, entities and migrations; separate databases and roles follow in #78. `CatalogDbContext` is a
  project of its own, `EShop.Catalog.Data`, used by ProductApi and CategoryApi: one bounded context, two deployables,
  as dotnet/eShop's `orderingdb` is used by `Ordering.API` and `OrderProcessor`. The other contexts live in `Data/` of
  their service, as dotnet/eShop keeps them in `Catalog.API` and `Ordering.Infrastructure`.
- **`EShop.Persistence`** (`AddServiceDbContext<TContext>`, `UseServiceNpgsql<TContext>`): the static abstract
  `IServiceDbContext.Schema` names the schema; the model gets it through `HasDefaultSchema`, the migrations history
  through `MigrationsHistoryTable(..., schema)` (the default schema does not apply to it), and the connection string a
  `Search Path`, so the services' raw SQL (the replicas' `INSERT … ON CONFLICT`, the reservation's `FOR UPDATE`) needs
  no schema names. Named after its area, as `EShop.Messaging`; first called `EShop.DataService`, which read as a
  deployable next to `EShop.ServiceDefaults`.
- **A design-time factory per context** (`IDesignTimeDbContextFactory`): `dotnet ef` and the migrations bundle create the
  context without starting a service: no `--startup-project`, no placeholder connection string.
- **A fresh `InitialCreate` per context**, no data migration (nothing is deployed); `EShopDbContext`, its entities and
  its 18 migrations are removed.
- **Foreign key `cart_items → cart_products`**, now that both are in one schema: replica rows are never deleted
  (tombstones), so `Restrict` only stops an item of an unknown product. **No key `order_details → order_products`**:
  Northwind's order lines are loaded before the replica is filled (republish), and a line keeps its own name and price.
- **Tests**: `TestDatabase<TContext>` migrates its context and empties only its schema. The tests that seeded another
  service's tables (ProductApi: carts and orders kept on a delete; OrderApi: checkout leaves stock and cart) are
  removed: that data is out of the service's model.
- **Migrator** `db/migrator/`: one bundle per context; `migrate.sh` runs them in turn and stops at the first failure.
- **Northwind** split into `db/northwind-catalog.sql` and `db/northwind-ordering.sql`, with schema-qualified names. The
  single file no longer loaded into a database created from the current model: `products.revision`, `orders.status`,
  `orders.placed_as_guest` and `order_details.product_name` had been filled by the old migrations. The generator now
  writes the same values.
- **Open for #78** (moved to [#85](https://github.com/FobarWB/EShop/issues/85)): `outbox_messages.producer`. Each schema has its own outbox table and one publisher, so the column
  separates nothing; removing it is a model change in three contexts.
- Checked: `has-pending-model-changes` for every context, 457 tests, the migrator twice on an empty database (the
  second run applies nothing), both Northwind files loaded with the sequences past the loaded ids.
- Checked on compose after `down -v`: `docker compose run --rm --build migrator` created the five schemas and nothing in
  `public`; Northwind loaded; republish filled both replicas (77 rows each); an order of 2 × Chai was confirmed 3.7 s
  after the checkout, the stock went 39 → 37, the ordered line left the cart and the other line stayed, the email
  arrived; the cancel returned the stock once (`Released`, 39 again); all dead letter queues empty.
- Sources: [dotnet/eShop](https://github.com/dotnet/eShop),
  [Database per service](https://microservices.io/patterns/data/database-per-service.html) (schema per service as a
  step towards a database per service).

## Refined in #78

- **A database and a role per bounded context**, created by `db/init/01-create-service-databases.sh`: the role owns its
  database (and `public`, owned by `pg_database_owner` since PostgreSQL 15), `REVOKE CONNECT … FROM PUBLIC` keeps every
  other role out. No role has `CREATEDB` or superuser; only `eshop_test` (fallback test server) has `CREATEDB`.
- **The schemas stay inside the databases** (`catalog.catalog.products`): no model change and no new migration; the
  Northwind files keep their schema-qualified names. The database is the boundary: PostgreSQL has no queries across
  databases without `dblink`/`postgres_fdw`. Considered: tables in `public` (five new `InitialCreate`, regenerated data).
- **A connection string per database**, not per service: `IServiceDbContext.ConnectionStringName`; `catalog` is shared by
  ProductApi and CategoryApi. One password per role: with a shared password a service's connection string would open
  every database by changing the user name.
- **Compose**: `x-databases` anchors; each container gets only its string, the migrator all five, and each bundle runs as
  its database's owner.
- Not in #78: the services connect as the owner of their database (DDL and data with one role); a role with data rights
  only, the migrator as the owner, follows in [#86](https://github.com/FobarWB/EShop/issues/86).
  `outbox_messages.producer` moved to [#85](https://github.com/FobarWB/EShop/issues/85).
- Checked: init script on a throwaway `postgres:18` (`cart` → `catalog`: `permission denied … CONNECT`, a password with a
  quote); 457 tests; on compose after `down -v`: five databases, no table in `public`, the migrator's second run applied
  nothing, each database held connections of its own role only, republish filled both replicas (77 rows each); order
  11078 of 2 × Chai confirmed, the email arrived, the cancel returned the stock once (`Released`, 39); outboxes empty,
  all eight DLQs empty.
