using EShop.Messaging.RabbitMq;

namespace EShop.NotificationService.Messaging;

// Declares the exchange and this service's queues at start, before anything is consumed (step 6).
public sealed class TopologyInitializer : IHostedService
{
    private readonly RabbitMqConnection _connection;
    private readonly ILogger<TopologyInitializer> _logger;

    public TopologyInitializer(RabbitMqConnection connection, ILogger<TopologyInitializer> logger)
    {
        _connection = connection;
        _logger = logger;
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        // A channel only for the declarations; the connection stays open for the consumers.
        await using var channel = await _connection.CreateChannelAsync(cancellationToken);

        // The exchange first: binding a queue to a missing exchange fails with NOT_FOUND.
        await Topology.DeclareEventsExchangeAsync(channel, cancellationToken);
        await Topology.DeclareConsumerQueueAsync(
            channel,
            OrderEventsQueue.Name,
            OrderEventsQueue.RoutingKeys,
            OrderEventsQueue.RetryDelays,
            cancellationToken
        );

        _logger.LogInformation(
            "Declared queue {Queue} and its retry queues",
            OrderEventsQueue.Name
        );
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
