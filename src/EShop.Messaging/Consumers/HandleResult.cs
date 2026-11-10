namespace EShop.Messaging.Consumers;

// What a handler decided: processed, or rejected for good (the reason is logged, the message goes to the DLQ).
public readonly record struct HandleResult(string? RejectReason)
{
    public static HandleResult Processed { get; } = new(null);

    public static HandleResult Reject(string reason) => new(reason);
}
