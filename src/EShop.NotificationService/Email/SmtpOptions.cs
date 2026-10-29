namespace EShop.NotificationService.Email;

public sealed class SmtpOptions
{
    public const string SectionName = "Smtp";

    public string Host { get; set; } = "localhost";
    public int Port { get; set; } = 1025;
    public string FromAddress { get; set; } = "shop@eshop.local";
    public string FromName { get; set; } = "EShop";
}
