---
type: decision
status: accepted
verified: 2026-10-28
issue: 42
---

# OptimisticConcurrencyWithXmin

## Context

The checkout reads the stock, checks it and writes the decremented value. Two buyers of the last unit both read 1,
both pass the check and both write 0: two orders for one unit, and the second write silently replaces the first
(lost update). PostgreSQL's default isolation level, Read Committed, lets both UPDATEs through.

## Decision

- `Product.Version` (`uint`, `[Timestamp]`) maps to PostgreSQL's system column `xmin`: the id of the transaction that
  wrote the current row version, new on every UPDATE. There is no own column or trigger; Npgsql leaves the generated
  `AddColumn("xmin")` out of the SQL, so the migration adds nothing to `products`.
- EF Core adds `WHERE xmin = <value read>` to every UPDATE and DELETE of a product, reads the new value back with
  `RETURNING xmin`, and throws `DbUpdateConcurrencyException` when no row matched. The transaction is rolled back.
- `ConcurrencyConflictHandler` in `EShop.ServiceDefaults` turns that exception into 409 ProblemDetails in every API.

Considered:

- An atomic conditional UPDATE, `SET units_in_stock = units_in_stock - @q WHERE product_id = @id AND units_in_stock >= @q`
  (`ExecuteUpdateAsync`): no exception and no false conflicts, but it guards only the stock, needs one statement per
  product and a check of each affected row count.
- Pessimistic locking (`SELECT … FOR UPDATE`): buyers of the same product wait for each other.

## Consequences

- Of two concurrent buyers of the last unit exactly one succeeds; the other gets 409 and sees the current stock.
- Any change of the product row conflicts, not only the stock: an admin edit during a checkout makes the checkout 409
  (the client reloads and retries), and a checkout during an admin edit makes the admin's PUT or DELETE 409 instead of
  overwriting the stock with an old value (a lost update in [ProductApi](../Services/ProductApi.md) before #42).
- Removing the token changes the model: a test run then fails on `PendingModelChangesWarning` before any test runs,
  so seeing the concurrency tests fail needs that warning ignored for the experiment.
- Orders carry the same token since #43: two concurrent status changes of one order cannot both be saved
  ([OrderLifecycle](OrderLifecycle.md)).
- Carts have no token: two concurrent changes of one cart still end as "last write wins" (see [EfCore](../Learning/EfCore.md)).
