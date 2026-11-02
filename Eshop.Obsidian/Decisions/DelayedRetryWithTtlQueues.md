---
type: decision
status: accepted
verified: 2026-11-02
issue: 50
---

# DelayedRetryWithTtlQueues

## Context

A consumer fails for a while (SMTP server or database down). `nack` with requeue returns the message at once and turns
the failure into a hot loop; dropping it loses the email. RabbitMQ has no built-in delayed redelivery without the
delayed-message plugin, and a queue has exactly one dead letter exchange.

## Decision

Per consumer queue, `Topology.DeclareConsumerQueueAsync` declares:

- the work queue, dead-lettering to `{queue}.dlq` (DLX `""`, DLK the dlq name);
- one wait queue per delay, `{queue}.retry.{n}`: no consumers, `x-message-ttl`, dead-lettering back to the work queue
  through the default exchange (`""` + the work queue name). Not the events exchange: no binding matches the queue name
  there, and the message would be dropped;
- `{queue}.dlq`, inspected by a person.

The consumer, on a failure on attempt `a` (header `x-retry-attempt`, absent = 0): for `a < delays` it publishes a copy
to `retry.{a+1}` with the header `a+1`, waits for the broker's confirm, then acks the original; for the last attempt it
rejects without requeue, and the work queue's DLX moves it to the DLQ. Messages that can never succeed (no `MessageId`,
unknown type, invalid JSON) go to the DLQ at once. NotificationService uses 5 s, 30 s, 2 min.

## Consequences

- Increasing delays without a plugin; each step is visible in the management UI.
- Our own attempt header, not `x-death`: the broker maintains `x-death` per queue and reason; the attempt counts per message.
- The copy is published before the original is acked: a crash in between gives a duplicate (caught by the inbox),
  never a loss.
- The delays are queue arguments: changing them means deleting the wait queues (`PRECONDITION_FAILED` otherwise).
  RabbitMQ recommends policies for TTL and DLX, which can change on a live queue; arguments keep the topology next to the code.
- One TTL per wait queue avoids the head-of-line problem of per-message TTLs (an expired message behind a longer one waits).
