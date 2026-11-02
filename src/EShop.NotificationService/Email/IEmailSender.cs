namespace EShop.NotificationService.Email;

// Plain-text emails; a failure throws, so the consumer does not ack the message and RabbitMQ retries it.
public interface IEmailSender
{
    Task SendAsync(string to, string subject, string body, CancellationToken cancellationToken);
}
