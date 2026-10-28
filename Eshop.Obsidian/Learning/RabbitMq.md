---
type: topic
verified: 2026-10-28
issue: 50
---

# RabbitMq

Related: [Docker](Docker.md), [OrderApi](../Services/OrderApi.md).

## Key ideas

- **Broker, AMQP 0-9-1.** A producer publishes to an **exchange**; the exchange copies the message into the
  **queues** whose **bindings** match; a consumer reads from a queue. Exchanges route and store nothing, queues
  store messages, bindings (exchange → queue + key) are separate objects. Exchanges and queues are many-to-many.
- **The producer does not know the queues.** It publishes a fact (`order.placed`); every consumer declares its own
  queue and binding. A new consumer needs no change in the producer: that is the decoupling.
- **Exchange types.**

  | Type | Routing | Use |
  | --- | --- | --- |
  | `direct` | routing key equals the binding key | addressed work: this task to that queue |
  | `topic` | pattern on dot-separated words: `*` one word, `#` zero or more | events by category: `order.*` |
  | `fanout` | key ignored, a copy to every bound queue | broadcast: "clear your cache" |

- **Default exchange.** Every queue is bound to the nameless default exchange with its own name as the key:
  "publish to a queue" is publishing there. The `amq.*` exchanges are built in and cannot be deleted.
- **Unroutable messages are dropped.** A message that matches no binding disappears ("published, but not routed"):
  declare queues and bindings before publishing.
- **Acknowledgements.** A consumer confirms a message with `ack`; `nack`/`reject` with `requeue=true` returns it at
  once, `requeue=false` drops it or sends it to the dead letter exchange. Unacked messages of a consumer whose
  connection closes return to the queue.
- **`redelivered`** is a flag the broker sets on a message it has delivered before (requeue, lost connection),
  not a retry mechanism.
- **At-least-once delivery → idempotent consumers.** A consumer that did the work and crashed before `ack` gets the
  message again; a publisher that crashed after publishing publishes again. Duplicates are normal operation,
  so a consumer remembers the message ids it has processed.
- **Ready vs unacked.** Both are counted by the broker: `messages_ready` wait in the queue, `messages_unacknowledged`
  were delivered and wait for the consumer's ack. Growing unacked = a hung or slow consumer, or a missing ack;
  the prefetch (`BasicQos`) limits how many one consumer may hold.
- **Delayed retry = TTL + DLX.** A failed message goes to a wait queue with `x-message-ttl` and no consumers; when the
  TTL expires the queue dead-letters it (`x-dead-letter-exchange`, `x-dead-letter-routing-key`) back to the work queue.
  The broker adds the header `x-death` (queue, reason `expired`/`rejected`, `count`): the consumer reads the attempt
  number from it. After the last attempt the message goes to a final dead letter queue, which a person inspects.
- **Surviving a broker restart** needs three things: a durable exchange and queue, a persistent message
  (delivery mode 2; a transient one is lost even in a durable queue, checked by a restart), and publisher confirms,
  so the producer knows the broker has stored it. A cluster also needs quorum queues (replication).
- **Queue arguments are an open dictionary, set once.** The broker stores unknown keys and ignores them: plugins and
  later versions add their own. Declaring an existing queue with other arguments fails with `PRECONDITION_FAILED`;
  changing them means deleting the queue (and its messages). RabbitMQ recommends policies for TTL and DLX:
  a policy can change on a live queue.

## Applied in this project

- [docker-compose.yml](../../docker-compose.yml): `rabbitmq:4-management` (the suffix enables the management plugin)
  with `hostname: rabbitmq` (the node is `rabbit@<hostname>` and keeps its data under that name: a random container
  hostname would not find the queues in the volume `eshop_rabbitmq-data`). User `eshop` from `RABBITMQ_PASSWORD`:
  `guest` may connect from localhost only, and the port mapping does not count as local. Health check
  `rabbitmq-diagnostics -q ping`.
- Ports: 5672 AMQP (registered with IANA), 15672 management UI and HTTP API (5672 + 10000), both on `127.0.0.1`.
  Not published: 25672 (clustering and the CLI, 5672 + 20000; the CLI runs inside the container), 5671 AMQPS,
  15692 Prometheus metrics.
- `axllent/mailpit:v1`: a fake SMTP server for development, SMTP on 1025, inbox on http://localhost:8025.
- Lab in the management UI before writing code: topic exchange `lab.events`, bindings `order.*` and `#`, default exchange,
  Get messages with ack/requeue modes, a TTL queue dead-lettering through `amq.direct`, persistent vs transient after
  `docker compose restart rabbitmq`.
- **Lesson from the lab:** the TTL queue never expired its message. `list_queues name arguments` showed the key
  `"x-message-ttl    "` with trailing spaces: unknown to the broker, silently ignored. The UI showed no TTL badge in
  Features. In code: names of queues, exchanges and arguments as constants; integration tests check the behavior
  (a failed message comes back after the delay), not only that the declaration did not throw.
- Commands (`docker compose exec rabbitmq rabbitmqctl ...`; `-q` hides the header, `--formatter pretty_table`):
  `list_queues name messages_ready messages_unacknowledged consumers arguments`, `list_bindings source_name
  destination_name routing_key` (an empty source is the default exchange), `list_exchanges name type durable`,
  `list_connections`, `list_channels`, `list_consumers`, `purge_queue <name>`, `delete_queue <name>`,
  `help <command>`. Exchanges are deleted in the UI or the HTTP API, not with `rabbitmqctl`.
- Never `docker compose down -v` to clean RabbitMQ: it deletes the PostgreSQL volume too.

## Open questions

- Topology from code (queue arguments) or from policies: arguments for now, they are visible next to the code.
- Quorum queues instead of classic ones once there is more than one node (#59).
- MassTransit after the first flow on `RabbitMQ.Client` (v9 is commercial, v8 Apache 2.0): to be decided in this issue.
