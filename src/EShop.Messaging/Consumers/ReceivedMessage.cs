using System.Text.Json;

namespace EShop.Messaging.Consumers;

// One delivered message, as a handler sees it: the outbox id, the event type and the JSON body.
public sealed record ReceivedMessage(Guid MessageId, string? Type, ReadOnlyMemory<byte> Body)
{
    // Web defaults, as OutboxMessageFactory serializes. A body that does not match throws JsonException,
    // which the consumer treats as a message that can never be processed.
    public T Deserialize<T>() =>
        JsonSerializer.Deserialize<T>(Body.Span, JsonSerializerOptions.Web)
        ?? throw new JsonException("The message body is null.");
}
