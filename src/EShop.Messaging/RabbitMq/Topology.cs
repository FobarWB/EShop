using RabbitMQ.Client;

namespace EShop.Messaging.RabbitMq;

// RabbitMQ objects shared by the services: the events exchange and the queues of each consumer.
// Declaring is idempotent: the same arguments again are a no-op, different ones fail with PRECONDITION_FAILED.
public static class Topology
{
    public const string EventsExchange = "eshop.events";

    // Queue arguments: the broker silently ignores an unknown (misspelled) key, so they are spelled once, here.
    public const string MessageTtl = "x-message-ttl";
    public const string DeadLetterExchange = "x-dead-letter-exchange";
    public const string DeadLetterRoutingKey = "x-dead-letter-routing-key";

    public static Task DeclareEventsExchangeAsync(IChannel channel, CancellationToken ct) =>
        channel.ExchangeDeclareAsync(
            EventsExchange,
            ExchangeType.Topic,
            durable: true,
            cancellationToken: ct
        );

    public static async Task DeclareConsumerQueueAsync(
        IChannel channel,
        string queue,
        IReadOnlyList<string> routingKeys,
        IReadOnlyList<TimeSpan> retryDelays,
        CancellationToken ct
    )
    {
        // 1. The final dead letter queue: no arguments, a person inspects what lands here.
        await DeclareDurableQueueAsync(channel, DeadLetterQueue(queue), arguments: null, ct);

        // 2. One wait queue per delay: no consumers; when the TTL expires the message goes back
        //    to the work queue through the default exchange ("" + the queue name as routing key).
        //    Not the events exchange: no binding matches the queue name there, the message would be lost.
        for (var i = 0; i < retryDelays.Count; i++)
        {
            await DeclareDurableQueueAsync(
                channel,
                RetryQueue(queue, i + 1),
                new Dictionary<string, object?>
                {
                    // A number: the broker rejects or ignores a string TTL.
                    [MessageTtl] = (int)retryDelays[i].TotalMilliseconds,
                    [DeadLetterExchange] = "",
                    [DeadLetterRoutingKey] = queue,
                },
                ct
            );
        }

        // 3. The work queue: a message rejected without requeue goes to the dead letter queue.
        await DeclareDurableQueueAsync(
            channel,
            queue,
            new Dictionary<string, object?>
            {
                [DeadLetterExchange] = "",
                [DeadLetterRoutingKey] = DeadLetterQueue(queue),
            },
            ct
        );

        // 4. Last: only now can events arrive, everything they may need exists.
        foreach (var routingKey in routingKeys)
        {
            await channel.QueueBindAsync(queue, EventsExchange, routingKey, cancellationToken: ct);
        }
    }

    public static string RetryQueue(string queue, int attempt) => $"{queue}.retry.{attempt}";

    public static string DeadLetterQueue(string queue) => $"{queue}.dlq";

    // exclusive and autoDelete default to true (temporary queues): ours must survive the connection.
    private static Task DeclareDurableQueueAsync(
        IChannel channel,
        string name,
        IDictionary<string, object?>? arguments,
        CancellationToken ct
    ) =>
        channel.QueueDeclareAsync(
            name,
            durable: true,
            exclusive: false,
            autoDelete: false,
            arguments: arguments,
            cancellationToken: ct
        );
}
