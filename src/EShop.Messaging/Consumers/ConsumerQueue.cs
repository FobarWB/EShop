namespace EShop.Messaging.Consumers;

// A consumer's queue: the events it binds (routing keys), its key in inbox_messages and its retry delays;
// after the last retry a message goes to the dead letter queue.
public sealed record ConsumerQueue(
    string Name,
    string Consumer,
    IReadOnlyList<string> RoutingKeys,
    IReadOnlyList<TimeSpan> RetryDelays
);
