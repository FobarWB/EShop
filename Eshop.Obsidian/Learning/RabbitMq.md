---
type: topic
verified: 2026-11-10
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

## In code (#50)

- **Connection and channels.** A connection is one TCP connection (handshake, authentication): one per process,
  `RabbitMqConnection`, a singleton opened on first use under a `SemaphoreSlim`. Not `Lazy<T>`: it would cache a failed
  first attempt (broker still starting) forever. A channel is a light session inside it, not thread-safe: one per
  publisher and per consumer. `ClientProvidedName` (the entry assembly) names the connection in the management UI.
- **Automatic recovery** (on by default in `RabbitMQ.Client` 7) reconnects an established connection every 5 s and
  restores its channels and consumers; it does not retry the first connect. Seen live: after `docker compose stop
  rabbitmq` the publisher logged `CONNECTION_FORCED … 'shutdown'` (the broker closed the connections itself), then
  `AlreadyClosed` from `AutorecoveringConnection` until the broker was back; then it carried on without a restart.
- **Publisher confirms** (`CreateChannelOptions(publisherConfirmationsEnabled: true, publisherConfirmationTrackingEnabled: true)`):
  `BasicPublishAsync` waits for the broker's ack and throws on a nack. The client adds `x-dotnet-pub-seq-no` to match them.
- **Message properties**: `MessageId` (idempotency), `Type` (which contract), `ContentType`, `Persistent = true`
  (delivery mode 2). Properties are standard AMQP fields, headers are free key-values: `message.id` typed as a property
  key in the UI is simply unknown, and the message arrived without an id.
- **`mandatory: false`** for events: no subscriber is not an error in publish/subscribe; `true` would stall the outbox
  until a consumer exists.
- **Consumer**: `AsyncEventingBasicConsumer` + `BasicConsumeAsync(queue, autoAck: false)`; ack or reject only when done;
  `BasicQosAsync(prefetchCount: 10)` limits the unacked messages one consumer holds. Handlers on one channel run one
  after another, so acks and publishes never overlap. `ea.Body` is valid only inside the handler.
- **Declaring is idempotent**: producer (`OutboxPublisher`) declares the exchange, the consumer side declares the exchange
  and its own queues; the topology exists whoever starts first. Binding to a missing exchange fails with `NOT_FOUND`.
- **`QueueDeclareAsync` defaults are for temporary queues** (`exclusive: true`, `autoDelete: true`): durable queues need
  both set to false explicitly (`DeclareDurableQueueAsync`).
- **Two retry bugs caught in review**: `x-dead-letter-key` (should be `-routing-key`) would have been ignored; DLX
  `eshop.events` for the wait queues would have dropped every retried message (no binding for the queue name). Both are
  now integration tests ([DelayedRetryWithTtlQueues](../Decisions/DelayedRetryWithTtlQueues.md)).
- **Erlang shows an empty string as `[]`** in `rabbitmqctl list_queues arguments`: `{"x-dead-letter-exchange",[]}` is `""`.
- In containers a stopped service disappears from the compose DNS: the consumer got `Name or service not known` for
  `mailpit`, not `Connection refused`.

## Applied in #50

Outbox and polling publisher in OrderApi, consumer with inbox, retries 5 s / 30 s / 2 min and a DLQ in
[NotificationService](../Services/NotificationService.md); decisions [TransactionalOutbox](../Decisions/TransactionalOutbox.md),
[UseRabbitMqClient](../Decisions/UseRabbitMqClient.md), [MessageContractsOwnedByProducer](../Decisions/MessageContractsOwnedByProducer.md),
[DelayedRetryWithTtlQueues](../Decisions/DelayedRetryWithTtlQueues.md). Checked by hand in containers: an order with
the broker stopped is published later; a redelivered `MessageId` is acknowledged as a duplicate; a message without
`MessageId` goes to the DLQ; with Mailpit stopped a message went retry.1 → retry.2 → retry.3 → DLQ (5 s and 30 s
apart, plus ~4 s per failed SMTP attempt). Tested against a RabbitMQ container (Testcontainers), see [Testing](Testing.md).

## Applied in #45

Guest checkout ([GuestCheckout](../Decisions/GuestCheckout.md)) as a choreography of three services, without a
coordinator: OrderApi → `order.guest-placed` → CustomerApi (Keycloak user, set-password email) →
`customer.guest-account-ready` → OrderApi (attach the order). Lessons:

- **One consumer, many handlers.** The mechanics (inbox, ack, retries, DLQ) moved from NotificationService into
  `MessageConsumer<THandler>` of `EShop.Messaging`; a service writes only an `IMessageHandler`. The consumer runs the
  handler and the inbox row in **one database transaction**, so a handler's `ExecuteUpdate` (OrderApi's attach) or
  outbox row (CustomerApi's next event) is stored exactly once per message, or not at all.
- **Side effects outside the database stay at least once.** Keycloak calls and emails cannot join the transaction:
  a crash before the commit repeats them. Keycloak's find-or-create is idempotent; a second email is accepted.
- **Two publishers on one outbox table break the order.** With `FOR UPDATE SKIP LOCKED` no row is published twice,
  but two services' publishers take different batches, and a later event can overtake an earlier one. Each publisher
  now takes only its own rows (`outbox_messages.producer`).
- **Declare in the consumer, not before the host starts.** NotificationService's `TopologyInitializer` stopped the
  process without a broker; an API must keep serving HTTP. The consumer declares its queues in a loop that connects
  again every 5 s.
- **Bind exact routing keys.** NotificationService binds `order.placed` and `order.cancelled`, not `order.*`: a new
  event type (`order.guest-placed`) would otherwise land in its queue and go to the DLQ as unknown.
- Checked on compose: a guest order went through all three queues in about a second (three outbox rows published,
  one inbox row per consumer, all DLQs empty).

## Open questions

- Topology from code (queue arguments) or from policies: arguments for now, they are visible next to the code.
- Quorum queues instead of classic ones once there is more than one node (#59).
- MassTransit: decided against for now ([UseRabbitMqClient](../Decisions/UseRabbitMqClient.md)); revisit with #51.
- Moving messages back from a DLQ: the shovel plugin (`rabbitmq_shovel_management`) or a small tool.
- Cleanup of `outbox_messages` and `inbox_messages` (e.g. delete rows older than 30 days).
