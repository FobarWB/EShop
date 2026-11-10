using System.Text.Json;
using EShop.Data;
using EShop.Data.Entities;
using EShop.Messaging.RabbitMq;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Npgsql;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;

namespace EShop.Messaging.Consumers;

// The shared mechanics of a consumer: one queue, each message once (inbox), retried with delays, then the DLQ.
// What happens with a message is the handler's job (THandler, one per service); the handler's database changes and
// the inbox row are stored in one transaction. Moved from NotificationService's OrderEventsConsumer in #45.
public sealed class MessageConsumer<THandler> : BackgroundService
    where THandler : IMessageHandler
{
    // The number of the retry a message is on; absent on the first delivery.
    private const string RetryAttemptHeader = "x-retry-attempt";

    // At most this many unacknowledged messages are delivered to us at once (BasicQos).
    private const ushort Prefetch = 10;

    private const string InboxPrimaryKey = "pk_inbox_messages";

    // How long to wait before connecting again when the broker is unavailable at start.
    private static readonly TimeSpan ReconnectDelay = TimeSpan.FromSeconds(5);

    // Confirms for the copies published to the retry queues: the original is acked only after the broker
    // has stored its copy, so a failed message is never lost between the two.
    private static readonly CreateChannelOptions ConfirmedChannel = new(
        publisherConfirmationsEnabled: true,
        publisherConfirmationTrackingEnabled: true
    );

    private readonly ConsumerQueue _queue;
    private readonly RabbitMqConnection _connection;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<MessageConsumer<THandler>> _logger;

    public MessageConsumer(
        ConsumerRegistration<THandler> registration,
        RabbitMqConnection connection,
        IServiceScopeFactory scopeFactory,
        ILogger<MessageConsumer<THandler>> logger
    )
    {
        _queue = registration.Queue;
        _connection = connection;
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            IChannel? channel = null;
            try
            {
                channel = await _connection.CreateChannelAsync(stoppingToken, ConfirmedChannel);

                // Declared here, not before the host starts: an API keeps serving requests while the broker is
                // down, and this loop connects as soon as it is back. Once consuming, the client's automatic
                // recovery handles broker restarts. The exchange first: binding to a missing one fails.
                await Topology.DeclareEventsExchangeAsync(channel, stoppingToken);
                await Topology.DeclareConsumerQueueAsync(
                    channel,
                    _queue.Name,
                    _queue.RoutingKeys,
                    _queue.RetryDelays,
                    stoppingToken
                );
                await channel.BasicQosAsync(
                    prefetchSize: 0,
                    prefetchCount: Prefetch,
                    global: false,
                    stoppingToken
                );

                // Handlers run one after another on this channel, so acks and publishes never overlap.
                var consumer = new AsyncEventingBasicConsumer(channel);
                var consuming = channel;
                consumer.ReceivedAsync += (_, ea) => HandleAsync(consuming, ea, stoppingToken);

                // autoAck false: a message stays in the queue (unacked) until HandleAsync acks or rejects it;
                // if this process dies first, RabbitMQ delivers it again (redelivered = true).
                await channel.BasicConsumeAsync(
                    _queue.Name,
                    autoAck: false,
                    consumer,
                    stoppingToken
                );
                _logger.LogInformation("Consuming {Queue}", _queue.Name);

                // The messages arrive in HandleAsync; this only keeps the consumer alive until shutdown.
                await Task.Delay(Timeout.Infinite, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { }
            catch (Exception ex)
            {
                _logger.LogWarning(
                    ex,
                    "Consuming {Queue} could not start; retrying in {Delay}",
                    _queue.Name,
                    ReconnectDelay
                );
                await DelayUnlessStoppingAsync(ReconnectDelay, stoppingToken);
            }
            finally
            {
                await CloseAsync(channel);
            }
        }
    }

    private async Task HandleAsync(IChannel channel, BasicDeliverEventArgs ea, CancellationToken ct)
    {
        var properties = ea.BasicProperties;
        if (!Guid.TryParse(properties.MessageId, out var messageId))
        {
            await DeadLetterAsync(channel, ea, "it has no valid MessageId", ct);
            return;
        }
        var attempt = RetryAttemptOf(properties);

        try
        {
            // One scope per message: the handler and this DbContext are the same scoped instances.
            await using var scope = _scopeFactory.CreateAsyncScope();
            var db = scope.ServiceProvider.GetRequiredService<EShopDbContext>();

            // 1. Seen before (a redelivery, or the outbox published twice): done, only ack.
            if (
                await db.InboxMessages.AnyAsync(
                    m => m.Consumer == _queue.Consumer && m.MessageId == messageId,
                    ct
                )
            )
            {
                _logger.LogInformation(
                    "Message {MessageId} ({Type}) already processed; acknowledged as a duplicate",
                    messageId,
                    properties.Type
                );
                await channel.BasicAckAsync(ea.DeliveryTag, multiple: false, ct);
                return;
            }

            // 2. One transaction for the handler's changes and the inbox row: both are stored, or neither.
            await using var transaction = await db.Database.BeginTransactionAsync(ct);
            HandleResult result;
            try
            {
                result = await scope
                    .ServiceProvider.GetRequiredService<THandler>()
                    .HandleAsync(new ReceivedMessage(messageId, properties.Type, ea.Body), ct);
            }
            catch (JsonException ex)
            {
                // A body that does not match the contract: retrying cannot fix it.
                result = HandleResult.Reject($"its body is invalid: {ex.Message}");
            }

            if (result.RejectReason is { } reason)
            {
                // The transaction is disposed without a commit: the handler's changes are rolled back.
                await DeadLetterAsync(channel, ea, reason, ct);
                return;
            }

            // 3. Remember it with the handler's changes, then ack.
            db.InboxMessages.Add(
                new InboxMessage
                {
                    Consumer = _queue.Consumer,
                    MessageId = messageId,
                    ProcessedAt = DateTime.UtcNow,
                }
            );
            try
            {
                await db.SaveChangesAsync(ct);
                await transaction.CommitAsync(ct);
            }
            catch (DbUpdateException ex)
                when (ex.InnerException
                        is PostgresException
                        {
                            SqlState: PostgresErrorCodes.UniqueViolation,
                            ConstraintName: InboxPrimaryKey,
                        }
                )
            {
                // A parallel delivery of the same message committed first: its changes are stored, ours are not.
                await transaction.RollbackAsync(ct);
            }
            await channel.BasicAckAsync(ea.DeliveryTag, multiple: false, ct);

            _logger.LogInformation(
                "Message {MessageId} ({Type}) processed by {Consumer}",
                messageId,
                properties.Type,
                _queue.Consumer
            );
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            // Shutting down: no ack, the message goes back to the queue and is delivered again later.
        }
        catch (Exception ex)
        {
            await RetryOrDeadLetterAsync(channel, ea, messageId, attempt, ex, ct);
        }
    }

    // A failure that may pass (a service or the database down): retry after a delay, give up after the last one.
    private async Task RetryOrDeadLetterAsync(
        IChannel channel,
        BasicDeliverEventArgs ea,
        Guid messageId,
        int attempt,
        Exception error,
        CancellationToken ct
    )
    {
        try
        {
            var delays = _queue.RetryDelays;
            if (attempt >= delays.Count)
            {
                _logger.LogError(
                    error,
                    "Message {MessageId} ({Type}) failed after {Retries} retries; moved to the dead letter queue",
                    messageId,
                    ea.BasicProperties.Type,
                    attempt
                );
                // The work queue's dead letter exchange routes it to the .dlq.
                await channel.BasicRejectAsync(ea.DeliveryTag, requeue: false, ct);
                return;
            }

            var next = attempt + 1;
            // A copy with the same MessageId, Type and body; only the attempt number changes. Our own header,
            // not x-death: the broker manages x-death and counts per queue, the attempt counts per message.
            var properties = new BasicProperties(ea.BasicProperties)
            {
                Headers = new Dictionary<string, object?> { [RetryAttemptHeader] = next },
            };
            // The default exchange ("") routes by queue name: straight into the wait queue. When its TTL expires,
            // the wait queue dead-letters the copy back to the work queue.
            await channel.BasicPublishAsync(
                exchange: "",
                routingKey: Topology.RetryQueue(_queue.Name, next),
                mandatory: false,
                properties,
                ea.Body,
                ct
            );
            await channel.BasicAckAsync(ea.DeliveryTag, multiple: false, ct);

            _logger.LogWarning(
                error,
                "Message {MessageId} ({Type}) failed; retry {Retry} of {Retries} in {Delay}",
                messageId,
                ea.BasicProperties.Type,
                next,
                delays.Count,
                delays[attempt]
            );
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            // The channel itself failed (broker down): no ack, so RabbitMQ delivers the message again.
            _logger.LogError(
                ex,
                "Message {MessageId} could not be retried; it stays unacknowledged",
                messageId
            );
        }
    }

    // A message that can never be processed: straight to the dead letter queue, for a person to inspect.
    private async Task DeadLetterAsync(
        IChannel channel,
        BasicDeliverEventArgs ea,
        string reason,
        CancellationToken ct
    )
    {
        _logger.LogError(
            "Message {MessageId} ({Type}) moved to the dead letter queue: {Reason}",
            ea.BasicProperties.MessageId,
            ea.BasicProperties.Type,
            reason
        );
        await channel.BasicRejectAsync(ea.DeliveryTag, requeue: false, ct);
    }

    private static int RetryAttemptOf(IReadOnlyBasicProperties properties) =>
        properties.Headers?.TryGetValue(RetryAttemptHeader, out var value) == true
            ? value switch
            {
                int number => number,
                long number => (int)number,
                _ => 0,
            }
            : 0;

    private static async Task DelayUnlessStoppingAsync(TimeSpan delay, CancellationToken ct)
    {
        try
        {
            await Task.Delay(delay, ct);
        }
        catch (OperationCanceledException) { }
    }

    // stoppingToken may be cancelled already: close without it. Unacked messages go back to the queue.
    private static async Task CloseAsync(IChannel? channel)
    {
        if (channel == null)
        {
            return;
        }
        try
        {
            if (channel.IsOpen)
            {
                await channel.CloseAsync(cancellationToken: CancellationToken.None);
            }
        }
        catch (Exception) when (!channel.IsOpen)
        {
            // The broker closed it meanwhile: nothing left to close.
        }
        await channel.DisposeAsync();
    }
}
