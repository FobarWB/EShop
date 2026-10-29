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
        // RabbitMqConnection (step 4) is registered here.
        return services;
    }
}
