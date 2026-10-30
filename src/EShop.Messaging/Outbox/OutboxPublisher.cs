using System.Runtime.ExceptionServices;
using System.Text;
using EShop.Data;
using EShop.Data.Entities;
using EShop.Messaging.RabbitMq;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using RabbitMQ.Client;

namespace EShop.Messaging.Outbox;

// Polls outbox_messages and publishes the pending events to RabbitMQ (polling publisher / message relay).
// At least once: a crash between the broker's confirm and the commit publishes the event again,
// so consumers recognize a redelivery by its MessageId.
public sealed class OutboxPublisher : BackgroundService
{
    private const int BatchSize = 20;
    private static readonly TimeSpan PollingInterval = TimeSpan.FromSeconds(2);

    // Publisher confirms: BasicPublishAsync waits until the broker has stored the message, and throws if it refused.
    private static readonly CreateChannelOptions ConfirmedChannel = new(
        publisherConfirmationsEnabled: true,
        publisherConfirmationTrackingEnabled: true
    );

    private readonly IServiceScopeFactory _serviceScopeFactory;
    private readonly RabbitMqConnection _rabbitMqConnection;
    private readonly ILogger<OutboxPublisher> _logger;

    public OutboxPublisher(
        IServiceScopeFactory serviceScopeFactory,
        RabbitMqConnection rabbitMqConnection,
        ILogger<OutboxPublisher> logger
    )
    {
        _serviceScopeFactory = serviceScopeFactory;
        _rabbitMqConnection = rabbitMqConnection;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        IChannel? channel = null;
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                if (channel == null)
                {
                    channel = await _rabbitMqConnection.CreateChannelAsync(
                        stoppingToken,
                        ConfirmedChannel
                    );
                    // Publishing to a missing exchange closes the channel (NOT_FOUND): declare it, the consumers
                    // may not have started yet.
                    await Topology.DeclareEventsExchangeAsync(channel, stoppingToken);
                }

                var published = await PublishBatchAsync(channel, stoppingToken);
                // A full batch: more may be waiting, go on at once. Otherwise wait for new events.
                if (published < BatchSize)
                {
                    await Task.Delay(PollingInterval, stoppingToken);
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                // The broker or the database is down: the API keeps working, the events wait in the outbox.
                // An unhandled exception here would stop the whole host.
                _logger.LogWarning(
                    ex,
                    "Publishing the outbox failed; retrying in {Delay}",
                    PollingInterval
                );
                if (channel != null)
                {
                    // Without Close: the channel may already be broken. The next pass opens a new one.
                    await channel.DisposeAsync();
                    channel = null;
                }
                try
                {
                    await Task.Delay(PollingInterval, stoppingToken);
                }
                catch (OperationCanceledException)
                {
                    break;
                }
            }
        }

        if (channel != null)
        {
            // stoppingToken is already cancelled here: close without it.
            await channel.CloseAsync(cancellationToken: CancellationToken.None);
            await channel.DisposeAsync();
        }
    }

    // Publishes the oldest pending events in their order; returns how many were published.
    private async Task<int> PublishBatchAsync(IChannel channel, CancellationToken ct)
    {
        // A scope per pass: the DbContext is scoped (one per request in the API), this service lives forever.
        await using var scope = _serviceScopeFactory.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<EShopDbContext>();

        // FOR UPDATE locks the rows until the commit; SKIP LOCKED lets another instance of the service take
        // the next rows instead of waiting, so no event is published by two instances at once.
        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        var messages = await db
            .OutboxMessages.FromSql(
                $"""
                SELECT * FROM outbox_messages
                WHERE processed_at IS NULL
                ORDER BY occurred_at
                LIMIT {BatchSize}
                FOR UPDATE SKIP LOCKED
                """
            )
            .ToListAsync(ct);

        var published = 0;
        Exception? failure = null;
        foreach (var message in messages)
        {
            try
            {
                await PublishAsync(channel, message, ct);
                message.ProcessedAt = DateTime.UtcNow;
                published++;
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                // Stop here: a later event must not overtake this one (a cancellation before its placement).
                message.Attempts++;
                message.LastError = ex.Message;
                failure = ex;
                break;
            }
        }

        // Stores the published ones and the failed attempt together.
        await db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);

        if (failure != null)
        {
            // The caller logs it and opens a new channel: this one may be broken.
            ExceptionDispatchInfo.Throw(failure);
        }
        return published;
    }

    private static async Task PublishAsync(
        IChannel channel,
        OutboxMessage message,
        CancellationToken ct
    )
    {
        var properties = new BasicProperties
        {
            // The outbox id: consumers recognize a redelivery by it (inbox, step 6).
            MessageId = message.Id.ToString(),
            // The event type: consumers pick the contract to deserialize.
            Type = message.Type,
            ContentType = "application/json",
            // Delivery mode 2: stored on disk, survives a broker restart in a durable queue.
            Persistent = true,
        };

        // mandatory: false: an event nobody subscribes to yet is not an error in publish/subscribe.
        await channel.BasicPublishAsync(
            Topology.EventsExchange,
            message.Type,
            mandatory: false,
            properties,
            Encoding.UTF8.GetBytes(message.Payload),
            ct
        );
    }
}
