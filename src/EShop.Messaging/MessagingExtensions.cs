using EShop.Messaging.Outbox;
using EShop.Messaging.RabbitMq;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace EShop.Messaging;

public static class MessagingExtensions
{
    public static IServiceCollection AddEShopMessaging(
        this IServiceCollection services,
        IConfiguration configuration
    )
    {
        services.Configure<RabbitMqOptions>(configuration.GetSection(RabbitMqOptions.SectionName));
        // One connection per process; channels are opened from it by each publisher and consumer.
        services.AddSingleton<RabbitMqConnection>();
        return services;
    }

    // For producers: publishes the events their transactions wrote to the outbox.
    public static IServiceCollection AddOutboxPublisher(this IServiceCollection services) =>
        services.AddHostedService<OutboxPublisher>();
}
