namespace EShop.Messaging.Consumers;

// The part of a consumer a service writes: what to do with one message. MessageConsumer does the rest (inbox,
// transaction, acknowledgement, retries, DLQ).
// Resolved from the message's scope, so it may take scoped services such as EShopDbContext. It does not call
// SaveChanges: the consumer saves its changes together with the inbox row in one transaction, which statements
// such as ExecuteUpdate join too.
// Return Reject for a message that can never be processed (an unknown type) → DLQ at once.
// Throw for a failure that may pass (a service down) → retried with delays, then the DLQ.
public interface IMessageHandler
{
    Task<HandleResult> HandleAsync(ReceivedMessage message, CancellationToken cancellationToken);
}
