namespace EShop.Messaging.RabbitMq;

public sealed class RabbitMqOptions
{
    public const string SectionName = "RabbitMq";

    public string Host { get; set; } = "localhost";
    public int Port { get; set; } = 5672;
    public string VirtualHost { get; set; } = "/";
    public string UserName { get; set; } = "eshop";

    // Never in appsettings: user-secrets locally, RabbitMq__Password in containers.
    public string Password { get; set; } = "";
}
