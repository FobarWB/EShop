---
type: service
verified: 2026-11-23
issue: 76
---

# NotificationService

Worker without HTTP endpoints: consumes the order events of [OrderApi](OrderApi.md) from RabbitMQ and emails the buyer.

- Project: [EShop.NotificationService](../../src/EShop.NotificationService/)
- Container: service `notificationservice` (profile `app`), no published port ([Dockerfile](../../src/EShop.NotificationService/Dockerfile))
- Emails: Mailpit, http://localhost:8025 (SMTP `localhost:1025`, in containers `mailpit:1025`)

## Messages

Queue `notifications.order-events`, bound to the topic exchange `eshop.events` with the routing keys `order.confirmed`,
`order.rejected` and `order.cancelled` (`OrderEvents/OrderEventsQueue.cs`; not to `order.*`, so `order.guest-placed`
does not come here). `order.placed` is retired (#76): `OrderEventsQueue.RetiredRoutingKeys` unbinds it on start, because
declaring only adds bindings. Contracts: `OrderConfirmed`, `OrderRejected`, `OrderCancelled` from
[EShop.OrderApi.Contracts](../../src/EShop.OrderApi.Contracts/), see
[MessageContractsOwnedByProducer](../Decisions/MessageContractsOwnedByProducer.md).

| Event | Email |
| --- | --- |
| `order.confirmed` | "Order #N accepted" (the stock is reserved, #76): the recipient name, the items (name × quantity, line total), the total with shipping; for a guest order (`PlacedAsGuest`) also that an account was created and which email sets its password (#45) |
| `order.rejected` | "Order #N could not be accepted" (#76): each short line as "name: ordered N, available M"; nothing was reserved, the buyer may order again with what is available |
| `order.cancelled` | "Order #N cancelled" with the time of the cancellation (UTC) |

An event without `email` (the buyer has no address) sends nothing and is still recorded as processed.

## Processing

Since #45 the mechanics are the shared `MessageConsumer<OrderEmailHandler>` of `EShop.Messaging` (moved from this
service's `OrderEventsConsumer`; the same consumer runs in OrderApi and CustomerApi). This service writes only
`OrderEvents/OrderEmailHandler` (compose and send the email) and registers it with `AddMessageConsumer`.
One channel with publisher confirms, `BasicQos` prefetch 10, `autoAck: false`. For each message:

1. No valid `MessageId`: reject without requeue → dead letter queue.
2. `(consumer 'notifications', MessageId)` in `inbox_messages`: a redelivery or a second publish; ack, nothing else.
3. In a transaction: the handler. Unknown `Type` (`Reject`) or a body that does not match the contract
   (`JsonException`): dead letter queue at once (a retry cannot fix it).
4. The handler sends the email (`IEmailSender`, MailKit).
5. Insert the inbox row (with the handler's changes; this handler has none), commit, then ack. A unique violation on
   `pk_inbox_messages` means a parallel delivery recorded it: rollback, ack.
6. Any exception in 2–5 (SMTP or the database down): retry or give up, see
   [DelayedRetryWithTtlQueues](../Decisions/DelayedRetryWithTtlQueues.md):
   attempt < 3 → a copy with `x-retry-attempt = attempt + 1` into `notifications.order-events.retry.{n}`
   (5 s, 30 s, 2 min), then ack the original; attempt 3 → reject → `notifications.order-events.dlq`.
   If the channel itself is broken, nothing is acked: the broker delivers the message again.

The email is sent before the inbox row is written: SMTP cannot be rolled back, and the other order would lose the email
when the process dies in between. A duplicate beats a loss ([TransactionalOutbox](../Decisions/TransactionalOutbox.md)).

The consumer declares the exchange and the five queues itself and unbinds the retired keys, in a loop that connects again every 5 s while RabbitMQ
is unavailable (until #45 a `TopologyInitializer` did it at start and stopped the process without a broker).

## Data

`inbox_messages` (`consumer`, `message_id` primary key, `processed_at`) in the shared database; reads nothing else.

## Configuration

`ConnectionStrings:EShop`, `RabbitMq:Password` (user-secrets `eshop` locally; in compose from `.env`), `RabbitMq:Host`,
`Smtp:Host`/`Port`/`FromAddress`/`FromName` (`appsettings.json`: `localhost:1025`, `shop@eshop.local`). The container
runs as `Production`; SQL commands are logged at `Warning` only.

## Dependencies

RabbitMQ (connection, topology and the consumer from [EShop.Messaging](../../src/EShop.Messaging/)), PostgreSQL
(inbox), Mailpit.
In compose it waits for the migrator, a healthy RabbitMQ and a healthy Mailpit.

## Tests

- [Unit](../../tests/EShop.NotificationService.Tests.Unit/): `OrderEmails` (subjects, texts, the guest paragraph only
  for guest orders, the short lines of a rejection, `order.placed` no longer an email, no address, unknown type,
  invalid JSON).
- [Integration](../../tests/EShop.NotificationService.Tests.Integration/): `MessageConsumer<OrderEmailHandler>`, registered
  as in `Program.cs`, against a RabbitMQ container
  (Testcontainers) and the database `eshop_test_notificationservice`, with a fake `IEmailSender`: email and inbox row,
  a duplicate `MessageId` sends one email, a failing sender puts a copy into `retry.1` with attempt 1, the last attempt
  goes to the DLQ, unknown type and missing `MessageId` go to the DLQ without an email; an `order.placed` binding left by
  an earlier version is removed, so such an event no longer arrives.

## Known issues

- A crash between sending the email and writing the inbox row sends the email again on redelivery.
- No health check: there is no endpoint; a crashed process is restarted by `restart: unless-stopped`.
- Messages in the DLQ wait for a person: moving them back needs the shovel plugin (not enabled) or a small tool.
