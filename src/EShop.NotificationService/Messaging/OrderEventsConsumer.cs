using System.Text.Json;
using EShop.Data;
using EShop.Data.Entities;
using EShop.Messaging.RabbitMq;
using EShop.NotificationService.Email;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;

namespace EShop.NotificationService.Messaging;

// Consumes the order events: one email per event, each event once (inbox), retried with delays, then the DLQ.
// The queues are declared by TopologyInitializer, which runs before this service starts.
public sealed class OrderEventsConsumer : BackgroundService
{
    // The key of this consumer in inbox_messages.
    private const string ConsumerName = "notifications";

    // The number of the retry a message is on; absent on the first delivery.
    private const string RetryAttemptHeader = "x-retry-attempt";

    // At most this many unacknowledged messages are delivered to us at once (BasicQos).
    private const ushort Prefetch = 10;

    private const string InboxPrimaryKey = "pk_inbox_messages";

    // Confirms for the copies published to the retry queues: the original is acked only after the broker
    // has stored its copy, so a failed email is never lost between the two.
    private static readonly CreateChannelOptions ConfirmedChannel = new(
        publisherConfirmationsEnabled: true,
        publisherConfirmationTrackingEnabled: true
    );

    private readonly RabbitMqConnection _connection;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IEmailSender _emailSender;
    private readonly ILogger<OrderEventsConsumer> _logger;

    public OrderEventsConsumer(
        RabbitMqConnection connection,
        IServiceScopeFactory scopeFactory,
        IEmailSender emailSender,
        ILogger<OrderEventsConsumer> logger
    )
    {
        _connection = connection;
        _scopeFactory = scopeFactory;
        _emailSender = emailSender;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var channel = await _connection.CreateChannelAsync(stoppingToken, ConfirmedChannel);
        try
        {
            await channel.BasicQosAsync(
                prefetchSize: 0,
                prefetchCount: Prefetch,
                global: false,
                stoppingToken
            );

            // Handlers run one after another on this channel, so acks and publishes never overlap.
            var consumer = new AsyncEventingBasicConsumer(channel);
            consumer.ReceivedAsync += (_, ea) => HandleAsync(channel, ea, stoppingToken);

            // autoAck false: a message stays in the queue (unacked) until HandleAsync acks or rejects it;
            // if this process dies first, RabbitMQ delivers it again (redelivered = true).
            await channel.BasicConsumeAsync(
                OrderEventsQueue.Name,
                autoAck: false,
                consumer,
                stoppingToken
            );
            _logger.LogInformation("Consuming {Queue}", OrderEventsQueue.Name);

            // The messages arrive in HandleAsync; this only keeps the service alive until shutdown.
            // A broker restart is handled by the client: automatic recovery reopens the channel and the consumer.
            await Task.Delay(Timeout.Infinite, stoppingToken);
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { }
        finally
        {
            // stoppingToken is already cancelled here: close without it. Unacked messages go back to the queue.
            if (channel.IsOpen)
            {
                await channel.CloseAsync(cancellationToken: CancellationToken.None);
            }
            await channel.DisposeAsync();
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
            await using var scope = _scopeFactory.CreateAsyncScope();
            var db = scope.ServiceProvider.GetRequiredService<EShopDbContext>();

            // 1. Seen before (a redelivery, or the outbox published twice): done, only ack.
            if (
                await db.InboxMessages.AnyAsync(
                    m => m.Consumer == ConsumerName && m.MessageId == messageId,
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

            // 2. Unknown type or a body that does not match the contract: retrying cannot fix it.
            OrderEmail? email;
            try
            {
                if (!OrderEmails.TryCompose(properties.Type, ea.Body, out email))
                {
                    await DeadLetterAsync(
                        channel,
                        ea,
                        $"its type '{properties.Type}' is unknown",
                        ct
                    );
                    return;
                }
            }
            catch (JsonException ex)
            {
                await DeadLetterAsync(channel, ea, $"its body is invalid: {ex.Message}", ct);
                return;
            }

            // 3. The email first, the inbox row after: SMTP cannot be rolled back. A crash between the two
            //    sends the email again on redelivery; the other order would lose it. A duplicate beats a loss.
            if (email != null)
            {
                await _emailSender.SendAsync(email.To, email.Subject, email.Body, ct);
            }

            // 4. Remember it, then ack.
            db.InboxMessages.Add(
                new InboxMessage
                {
                    Consumer = ConsumerName,
                    MessageId = messageId,
                    ProcessedAt = DateTime.UtcNow,
                }
            );
            try
            {
                await db.SaveChangesAsync(ct);
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
                // Another delivery of the same message was processed at the same time: already recorded.
            }
            await channel.BasicAckAsync(ea.DeliveryTag, multiple: false, ct);

            _logger.LogInformation(
                "Message {MessageId} ({Type}) processed, email {Email}",
                messageId,
                properties.Type,
                email == null ? "not needed" : "sent"
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

    // A failure that may pass (SMTP or the database down): retry after a delay, give up after the last one.
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
            var delays = OrderEventsQueue.RetryDelays;
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
                routingKey: Topology.RetryQueue(OrderEventsQueue.Name, next),
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
}
