using EShop.Messaging.Consumers;
using EShop.Messaging.Outbox;
using EShop.Messaging.RabbitMq;
using Microsoft.EntityFrameworkCore;
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

    // For producers: publishes the events their transactions wrote to the outbox of TContext, only their own rows.
    public static IServiceCollection AddOutboxPublisher<TContext>(
        this IServiceCollection services,
        string producer
    )
        where TContext : DbContext =>
        services
            .AddSingleton(new OutboxProducer(producer))
            .AddHostedService<OutboxPublisher<TContext>>();

    // For consumers: a background service that delivers the messages of this queue to THandler,
    // with the inbox in TContext.
    public static IServiceCollection AddMessageConsumer<THandler, TContext>(
        this IServiceCollection services,
        ConsumerQueue queue
    )
        where THandler : class, IMessageHandler
        where TContext : DbContext =>
        services
            .AddScoped<THandler>()
            .AddSingleton(new ConsumerRegistration<THandler>(queue))
            .AddHostedService<MessageConsumer<THandler, TContext>>();
}
