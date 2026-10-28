---
type: decision
status: accepted
verified: 2026-10-28
issue: 43
---

# OrderLifecycle

## Context

Orders had no state: a placed order could not be paid, shipped or cancelled, and nothing recorded who changed what.
A real shop also needs the order to stay a document: what the buyer saw at checkout must not change later.

## Decision

- **Statuses** `Pending → Paid → Shipped → Delivered`, `Pending, Paid → Cancelled`. The transitions live in one place,
  `Order.TryChangeStatus` ([Order.Lifecycle.cs](../../src/EShop.Data.Entities/Order.Lifecycle.cs)); `Status` has a
  private setter. A forbidden transition returns `false` (the API answers 409), not an exception.
- **Stored as text** (`orders.status varchar(20)`) with `CHECK (status IN (...))`, the list built from the enum
  (`EShopDbContext.OrderStatusValues`), so a new status shows up in the next migration. Considered:
  an `int` (unreadable in psql and pgAdmin) and a PostgreSQL enum type (`CREATE TYPE`: values cannot be removed or
  renamed without recreating the type, and Npgsql needs it mapped).
- **Journal** `order_status_changes`: one row per change (`from_status`, `to_status`, `changed_at`, `changed_by` = token
  `sub`), the creation included (`from_status` NULL). Append-only; `ON DELETE RESTRICT` from `orders`.
- **Snapshots**: `order_details.product_name` is copied at checkout like `unit_price`; renaming a product does not
  change old orders.
- **Cancel returns the stock** in the same transaction as the status: an explicit transaction, the order saved first
  (its `xmin` is checked), then `units_in_stock = units_in_stock + quantity` with `ExecuteUpdateAsync`.
  A second concurrent cancel waits for the order row lock, then fails the `xmin` check: 409, and the stock comes back once.
- **Northwind rows** (`user_id` NULL) are `Delivered` and have no journal rows: there is no real history to record.

## Consequences

- Buyers cancel their own `Pending` or `Paid` orders (`POST api/orders/{id}/cancel`); admins move any order
  (`PUT api/orders/{id}/status`). `Paid` is set by hand until payments exist.
- The cancel's relative stock update cannot be made wrong by a concurrent checkout; it changes `products.xmin`, so
  that checkout gets 409 instead of writing a stale stock.
- Deleting an order with a journal fails; orders are never deleted.
- The client repeats the "cancellable" rule only to show the button; the API decides.
- Not covered yet: stock movements per product (why the stock changed), payments and refunds, sales reports.
