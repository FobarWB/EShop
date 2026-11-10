---
type: decision
status: accepted
verified: 2026-11-10
issue: 50
---

# MessageContractsOwnedByProducer

## Context

Producer and consumers run in different processes and only share bytes through RabbitMQ: the JSON shape of an event is
a contract. Messages wait in the outbox, in queues and in the DLQ, so old and new shapes exist at the same time.
Practices differ: Microsoft's eShop copies each event class into every service; NServiceBus recommends one contracts
assembly per owning service; one shared library with all events of all services is advised against (every service
depends on every event, and no one owns them).

## Decision

- Each producer owns a contracts project: `EShop.OrderApi.Contracts` with `OrderPlaced`, `OrderCancelled` and
  `OrderEventTypes` (the type strings, which are also the routing keys). Plain positional `sealed record`s, no dependencies.
- Consumers reference the producer's contracts project (NotificationService now; in #51 ProductApi gets its own).
- Since #45 a second producer: `EShop.CustomerApi.Contracts` with `GuestAccountReady` and `CustomerEventTypes`.
  OrderApi and CustomerApi reference each other's contracts project, never each other's service project.
- Events carry the data their consumers need (event-carried state transfer): the email is built from `OrderPlaced`
  alone, without calling OrderApi back.
- Fields are only added (nullable), never renamed, removed or retyped.
- Serialized with `JsonSerializerOptions.Web` (camelCase) on both sides.

## Consequences

- A breaking rename fails the build of producer and consumer; the compiler does not protect messages already queued,
  hence the add-only rule.
- The infrastructure (`EShop.Messaging`) is shared, the contracts are not: changing an order event does not touch other
  producers' consumers.
- If services move to separate repositories, the contracts project becomes a NuGet package.
- Properties, not fields: System.Text.Json skips fields by default, and an event of fields serialized as `{}` during
  development.
