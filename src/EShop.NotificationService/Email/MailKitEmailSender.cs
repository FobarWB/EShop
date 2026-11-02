using MailKit.Net.Smtp;
using MailKit.Security;
using Microsoft.Extensions.Options;
using MimeKit;
using MimeKit.Text;

namespace EShop.NotificationService.Email;

public sealed class MailKitEmailSender : IEmailSender
{
    private readonly SmtpOptions _options;

    public MailKitEmailSender(IOptions<SmtpOptions> options)
    {
        _options = options.Value;
    }

    public async Task SendAsync(
        string to,
        string subject,
        string body,
        CancellationToken cancellationToken
    )
    {
        var message = new MimeMessage();
        message.From.Add(new MailboxAddress(_options.FromName, _options.FromAddress));
        message.To.Add(MailboxAddress.Parse(to));
        message.Subject = subject;
        message.Body = new TextPart(TextFormat.Plain) { Text = body };

        // One connection per email: a few emails per order do not need a pool. Mailpit offers no TLS;
        // a real server that offers STARTTLS gets an encrypted connection.
        using var client = new SmtpClient();
        await client.ConnectAsync(
            _options.Host,
            _options.Port,
            SecureSocketOptions.StartTlsWhenAvailable,
            cancellationToken
        );
        await client.SendAsync(message, cancellationToken);
        await client.DisconnectAsync(quit: true, cancellationToken);
    }
}
