namespace EShop.Messaging.Consumers;

// A consumer's queue: the events it binds (routing keys), its key in inbox_messages and its retry delays;
// after the last retry a message goes to the dead letter queue.
// RetiredRoutingKeys: keys an earlier version bound. Declaring only adds bindings, so a key dropped from RoutingKeys
// would still route its events here (and to the DLQ, as an unknown type) until it is unbound.
public sealed record ConsumerQueue(
    string Name,
    string Consumer,
    IReadOnlyList<string> RoutingKeys,
    IReadOnlyList<TimeSpan> RetryDelays,
    IReadOnlyList<string>? RetiredRoutingKeys = null
);
