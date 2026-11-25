---
type: decision
status: accepted
verified: 2026-11-25
issue: 43
---

# OrderLifecycle

## Context

Orders had no state: a placed order could not be paid, shipped or cancelled, and nothing recorded who changed what.
A real shop also needs the order to stay a document: what the buyer saw at checkout must not change later.

## Decision

- **Statuses** `Pending → Confirmed → Paid → Shipped → Delivered`, `Pending → Rejected` (#76: ProductApi could not
  reserve the stock), `Pending, Confirmed, Paid → Cancelled`. The transitions live in one place,
  `Order.TryChangeStatus` ([Order.Lifecycle.cs](../../src/EShop.OrderApi/Data/Entities/Order.Lifecycle.cs)); `Status` has a
  private setter. A forbidden transition returns `false` (the API answers 409), not an exception.
- **Stored as text** (`orders.status varchar(20)`) with `CHECK (status IN (...))`, the list built from the enum
  (`OrderingDbContext.OrderStatusValues`), so a new status shows up in the next migration. Considered:
  an `int` (unreadable in psql and pgAdmin) and a PostgreSQL enum type (`CREATE TYPE`: values cannot be removed or
  renamed without recreating the type, and Npgsql needs it mapped).
- **Journal** `order_status_changes`: one row per change (`from_status`, `to_status`, `changed_at`, `changed_by` = token
  `sub`), the creation included (`from_status` NULL). Append-only; `ON DELETE RESTRICT` from `orders`.
- **Snapshots**: `order_details.product_name` is copied at checkout like `unit_price`; renaming a product does not
  change old orders.
- **Cancel returned the stock** in the same transaction as the status (#43 to #76). Since #76 the stock is ProductApi's:
  the cancel stores the status, its journal row and `order.cancelled` in one `SaveChanges`, and ProductApi returns what
  it reserved. A second concurrent cancel still fails the order's `xmin` check: 409, one event, the stock comes back once.
- **Northwind rows** (`user_id` NULL) are `Delivered` and have no journal rows: there is no real history to record.

## Consequences

- Buyers cancel their own `Pending`, `Confirmed` or `Paid` orders (`POST api/orders/{id}/cancel`); admins move any order
  (`PUT api/orders/{id}/status`). `Paid` is set by hand until payments exist.
- `Confirmed` and `Rejected` come only from the stock answer (journal `changed_by = productapi`); the admin endpoint
  refuses them with 400: set by hand, they would confirm an order whose stock was never taken.
- Deleting an order with a journal fails; orders are never deleted.
- The client repeats the "cancellable" rule only to show the button; the API decides.
- Not covered yet: stock movements per product (why the stock changed), payments and refunds, sales reports.
