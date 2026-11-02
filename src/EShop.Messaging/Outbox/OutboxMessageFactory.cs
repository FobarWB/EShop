using System.Text.Json;
using EShop.Data.Entities;

namespace EShop.Messaging.Outbox;

// Packs an event into an outbox row; the caller adds the row to the same DbContext as the change
// the event describes, so both are saved in one transaction.
public static class OutboxMessageFactory
{
    public static OutboxMessage Create<TEvent>(string type, TEvent @event, DateTime occurredAt)
        where TEvent : notnull =>
        new()
        {
            // UUIDv7 starts with a timestamp: new keys go to the end of the primary key index.
            // The id becomes the RabbitMQ MessageId, so consumers can recognize a redelivery.
            Id = Guid.CreateVersion7(),
            Type = type,
            // Web defaults: camelCase, the same JSON style as the APIs.
            Payload = JsonSerializer.Serialize(@event, JsonSerializerOptions.Web),
            OccurredAt = occurredAt,
        };
}
