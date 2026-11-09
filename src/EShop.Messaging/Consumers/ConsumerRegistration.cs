namespace EShop.Messaging.Consumers;

// The queue of one handler type: several consumers in one process each get their own (AddMessageConsumer).
public sealed record ConsumerRegistration<THandler>(ConsumerQueue Queue)
    where THandler : IMessageHandler;
