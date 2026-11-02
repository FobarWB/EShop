---
type: decision
status: accepted
verified: 2026-11-02
issue: 50
---

# UseRabbitMqClient

## Context

The services need a message broker client. Options: the official `RabbitMQ.Client` (7.x, fully async), or a framework
on top of it: MassTransit (topology, retries, outbox and inbox built in; version 9 is commercial, version 8 Apache 2.0
and in maintenance), NServiceBus (commercial), Wolverine, CAP. The project exists to learn RabbitMQ.

## Decision

Use `RabbitMQ.Client` directly, with a thin shared layer in [EShop.Messaging](../../src/EShop.Messaging/):
`RabbitMqConnection` (one connection per process, channels per user), `Topology` (exchange, consumer queues, retry and
dead letter queues), `OutboxPublisher`, `OutboxMessageFactory`, and the inbox table.

## Consequences

- Every concept is visible in our code: exchanges, bindings, acks, prefetch, publisher confirms, TTL and DLX.
- We own what a framework would give: retries, idempotency, recovery handling, tests. The code is small (a few hundred
  lines) but has to be maintained.
- Revisit when there are many message types or services (#51): MassTransit 8 (free) maps onto the same ideas
  (its outbox, inbox and redelivery replace ours); the contracts stay.
