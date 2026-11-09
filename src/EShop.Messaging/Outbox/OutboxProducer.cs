namespace EShop.Messaging.Outbox;

// The producer name of this service's outbox rows: its OutboxPublisher publishes only these (AddOutboxPublisher).
public sealed record OutboxProducer(string Name);
