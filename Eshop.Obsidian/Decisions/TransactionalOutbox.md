---
type: decision
status: accepted
verified: 2026-11-02
issue: 50
---

# TransactionalOutbox

## Context

OrderApi stores an order in PostgreSQL and has to tell other services about it through RabbitMQ. The two have no common
transaction (dual write): publishing after the commit loses the event when the broker is down or the process dies in
between; publishing before the commit announces orders that are then rolled back (409, failed stock check).
Checkout must not wait for, or fail because of, the broker.

## Decision

- **Transactional outbox.** The repository adds an `outbox_messages` row (`OutboxMessageFactory`: UUIDv7 id, type,
  JSON payload, `occurred_at`) to the same DbContext as the change; one commit stores both or neither. Checkout needs two
  `SaveChanges` in one explicit transaction: the payload needs the order id the database generates.
- **Polling publisher** (message relay): `OutboxPublisher`, a `BackgroundService` in the producer, every 2 s takes up to
  20 pending rows `ORDER BY occurred_at FOR UPDATE SKIP LOCKED`, publishes them to `eshop.events` (routing key = type,
  `MessageId` = row id, persistent) with publisher confirms, sets `processed_at`, commits. A failure records
  `attempts`/`last_error` and stops the batch, so a later event never overtakes an earlier one.
- **At least once, made exactly-once in effect by the consumer.** A crash after the broker's confirm and before the
  commit publishes again; consumers keep an **inbox** (`inbox_messages`, key `(consumer, message_id)`) and skip
  what they have processed.
- A partial index on `occurred_at WHERE processed_at IS NULL` keeps the poll cheap however many rows were sent.

## Consequences

- Checkout answers in the same time with RabbitMQ stopped; the events are published when it is back (checked by hand:
  order 11084 placed with the broker stopped, `attempts = 1`, published after the restart).
- Events reach the broker up to ~2 s after the commit; consumers must expect duplicates.
- `outbox_messages` and `inbox_messages` grow: no cleanup job yet.
- Compared: Microsoft's eShop (`IntegrationEventLogEF`) publishes right after the commit and marks failures, with no
  background retry; MassTransit's bus outbox uses the same table + polling delivery service + inbox design, inside the
  library. Ours is the explicit version of the latter (see [UseRabbitMqClient](UseRabbitMqClient.md)).
- References: microservices.io "Transactional outbox", "Polling publisher", "Idempotent consumer".
