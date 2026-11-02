using System.Collections.Concurrent;
using EShop.NotificationService.Email;

namespace EShop.NotificationService.Tests.Integration;

// Records the emails instead of sending them; Fail makes every send throw, as when the SMTP server is down.
public sealed class FakeEmailSender : IEmailSender
{
    public ConcurrentQueue<OrderEmail> Sent { get; } = new();

    public bool Fail { get; set; }

    public Task SendAsync(
        string to,
        string subject,
        string body,
        CancellationToken cancellationToken
    )
    {
        if (Fail)
        {
            throw new IOException("SMTP server unavailable (test)");
        }
        Sent.Enqueue(new OrderEmail(to, subject, body));
        return Task.CompletedTask;
    }
}
