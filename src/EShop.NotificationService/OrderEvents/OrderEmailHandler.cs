using EShop.Messaging.Consumers;
using EShop.NotificationService.Email;

namespace EShop.NotificationService.OrderEvents;

// Turns an order event into the buyer's email. The consumer records the message in the inbox after this returns:
// the email first, SMTP cannot be rolled back. A crash between the two sends it again; a duplicate beats a loss.
public sealed class OrderEmailHandler : IMessageHandler
{
    private readonly IEmailSender _emailSender;
    private readonly ILogger<OrderEmailHandler> _logger;

    public OrderEmailHandler(IEmailSender emailSender, ILogger<OrderEmailHandler> logger)
    {
        _emailSender = emailSender;
        _logger = logger;
    }

    public async Task<HandleResult> HandleAsync(
        ReceivedMessage message,
        CancellationToken cancellationToken
    )
    {
        // Not an order event: retrying cannot help. A body that does not match throws JsonException (also the DLQ).
        if (!OrderEmails.TryCompose(message.Type, message.Body, out var email))
        {
            return HandleResult.Reject($"its type '{message.Type}' is unknown");
        }

        // No email address: nothing to send, the message is still recorded as processed.
        if (email != null)
        {
            await _emailSender.SendAsync(email.To, email.Subject, email.Body, cancellationToken);
        }

        _logger.LogInformation(
            "Message {MessageId} ({Type}): email {Email}",
            message.MessageId,
            message.Type,
            email == null ? "not needed" : "sent"
        );
        return HandleResult.Processed;
    }
}
