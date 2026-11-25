using System.Text.Json;
using EShop.Messaging;
using EShop.Messaging.RabbitMq;
using EShop.NotificationService.Data;
using EShop.NotificationService.Email;
using EShop.NotificationService.OrderEvents;
using EShop.OrderApi.Contracts;
using EShop.Persistence;
using EShop.Tests.Common;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using RabbitMQ.Client;

namespace EShop.NotificationService.Tests.Integration.OrderEvents;

// The shared MessageConsumer with this service's OrderEmailHandler as a running background service on the real
// topology (the production queue names and delays), with the email sender replaced. Retry delays are not waited for:
// a test publishes a message as it arrives on its last attempt instead.
public class OrderEventsConsumerTests : IAsyncLifetime
{
    private const string Email = "jane@example.com";
    private const string RetryAttemptHeader = "x-retry-attempt";

    private static readonly string[] AllQueues =
    [
        OrderEventsQueue.Name,
        Topology.RetryQueue(OrderEventsQueue.Name, 1),
        Topology.RetryQueue(OrderEventsQueue.Name, 2),
        Topology.RetryQueue(OrderEventsQueue.Name, 3),
        Topology.DeadLetterQueue(OrderEventsQueue.Name),
    ];

    private readonly NotificationServiceDatabase _database;
    private readonly RabbitMqFixture _broker;
    private readonly FakeEmailSender _sender = new();
    private readonly CancellationToken _cancellationToken = TestContext.Current.CancellationToken;
    private ServiceProvider? _services;
    private IHostedService? _consumer;

    public OrderEventsConsumerTests(NotificationServiceDatabase database, RabbitMqFixture broker)
    {
        _database = database;
        _broker = broker;
    }

    public async ValueTask InitializeAsync()
    {
        await _database.ResetAsync(_cancellationToken);
        await DeclareAndPurgeQueuesAsync();

        // The registration of Program.cs, with the test database, the test broker and the fake sender.
        _services = new ServiceCollection()
            .AddLogging()
            .AddDbContext<NotificationDbContext>(options =>
                options.UseServiceNpgsql<NotificationDbContext>(_database.ConnectionString)
            )
            .AddSingleton(_broker.Connection)
            .AddSingleton<IEmailSender>(_sender)
            .AddMessageConsumer<OrderEmailHandler, NotificationDbContext>(
                OrderEventsQueue.Definition
            )
            .BuildServiceProvider();
        _consumer = _services.GetServices<IHostedService>().ShouldHaveSingleItem();
        await _consumer.StartAsync(_cancellationToken);
    }

    public async ValueTask DisposeAsync()
    {
        if (_consumer != null)
        {
            await _consumer.StopAsync(CancellationToken.None);
        }
        if (_services != null)
        {
            await _services.DisposeAsync();
        }
        // Copies may still wait in the retry queues: the next test starts empty.
        await DeclareAndPurgeQueuesAsync();
    }

    [Fact]
    public async Task OrderConfirmed_SendsEmailAndRecordsInbox()
    {
        var messageId = Guid.CreateVersion7();

        await PublishAsync(OrderEventTypes.OrderConfirmed, Confirmed(11085, Email), messageId);

        (await SentCountReachesAsync(1)).ShouldBeTrue();
        var email = _sender.Sent.ShouldHaveSingleItem();
        (email.To, email.Subject).ShouldBe((Email, "Order #11085 accepted"));
        // The inbox row is written after the email: wait for it too.
        (
            await Eventually.HoldsAsync(() => InboxContainsAsync(messageId), _cancellationToken)
        ).ShouldBeTrue();
    }

    [Fact]
    public async Task OrderRejected_SendsRejectionEmail()
    {
        await PublishAsync(
            OrderEventTypes.OrderRejected,
            new OrderRejected(
                11085,
                Email,
                "Jane Doe",
                DateTime.UtcNow,
                [new OrderRejectedItem("Chai", 2, 0)]
            ),
            Guid.CreateVersion7()
        );

        (await SentCountReachesAsync(1)).ShouldBeTrue();
        _sender.Sent.ShouldHaveSingleItem().Subject.ShouldBe("Order #11085 could not be accepted");
    }

    [Fact]
    public async Task OrderCancelled_SendsCancellationEmail()
    {
        await PublishAsync(
            OrderEventTypes.OrderCancelled,
            new OrderCancelled(11085, Email, DateTime.UtcNow),
            Guid.CreateVersion7()
        );

        (await SentCountReachesAsync(1)).ShouldBeTrue();
        _sender.Sent.ShouldHaveSingleItem().Subject.ShouldBe("Order #11085 cancelled");
    }

    [Fact]
    public async Task OrderPlaced_BoundByAnEarlierVersion_NoLongerArrives()
    {
        // The queue as the version before #76 left it, then declared again as this one does on start.
        await using (var channel = await _broker.CreateChannelAsync(_cancellationToken))
        {
            await channel.QueueBindAsync(
                OrderEventsQueue.Name,
                Topology.EventsExchange,
                OrderEventTypes.OrderPlaced,
                cancellationToken: _cancellationToken
            );
        }
        await DeclareAndPurgeQueuesAsync();

        await PublishAsync(
            OrderEventTypes.OrderPlaced,
            Confirmed(1, Email),
            Guid.CreateVersion7(),
            routingKey: OrderEventTypes.OrderPlaced
        );
        await PublishAsync(
            OrderEventTypes.OrderCancelled,
            new OrderCancelled(2, Email, DateTime.UtcNow),
            Guid.CreateVersion7()
        );

        // Handled in order: once the cancellation's email is sent, order.placed would have reached the DLQ already.
        (await SentCountReachesAsync(1)).ShouldBeTrue();
        _sender.Sent.ShouldHaveSingleItem().Subject.ShouldBe("Order #2 cancelled");
        (
            await _broker.MessageCountAsync(
                Topology.DeadLetterQueue(OrderEventsQueue.Name),
                _cancellationToken
            )
        ).ShouldBe(0u);
    }

    [Fact]
    public async Task SameMessageIdAgain_SendsOneEmail()
    {
        var messageId = Guid.CreateVersion7();
        await PublishAsync(OrderEventTypes.OrderConfirmed, Confirmed(1, Email), messageId);
        (await SentCountReachesAsync(1)).ShouldBeTrue();

        // A redelivery, then another event: handled in order, so once the second email is sent,
        // the duplicate has been handled too.
        await PublishAsync(OrderEventTypes.OrderConfirmed, Confirmed(1, Email), messageId);
        await PublishAsync(
            OrderEventTypes.OrderConfirmed,
            Confirmed(2, Email),
            Guid.CreateVersion7()
        );

        (await SentCountReachesAsync(2)).ShouldBeTrue();
        _sender.Sent.Select(e => e.Subject).ShouldBe(["Order #1 accepted", "Order #2 accepted"]);
    }

    [Fact]
    public async Task NoEmailAddress_RecordsInboxWithoutEmail()
    {
        var messageId = Guid.CreateVersion7();

        await PublishAsync(OrderEventTypes.OrderConfirmed, Confirmed(1, email: null), messageId);

        (
            await Eventually.HoldsAsync(() => InboxContainsAsync(messageId), _cancellationToken)
        ).ShouldBeTrue();
        _sender.Sent.ShouldBeEmpty();
    }

    [Fact]
    public async Task SenderFails_PublishesCopyToFirstRetryQueue()
    {
        _sender.Fail = true;
        var messageId = Guid.CreateVersion7();

        await PublishAsync(OrderEventTypes.OrderConfirmed, Confirmed(1, Email), messageId);

        // Taken out of retry.1 before its 5 s TTL sends it back.
        var copy = (
            await _broker.WaitForMessageAsync(
                Topology.RetryQueue(OrderEventsQueue.Name, 1),
                _cancellationToken
            )
        ).ShouldNotBeNull();
        copy.BasicProperties.MessageId.ShouldBe(messageId.ToString());
        copy.BasicProperties.Headers.ShouldNotBeNull()[RetryAttemptHeader].ShouldBe(1);
        (await InboxContainsAsync(messageId)).ShouldBeFalse();
    }

    [Fact]
    public async Task SenderFailsOnLastRetry_MovesMessageToDeadLetterQueue()
    {
        _sender.Fail = true;
        var messageId = Guid.CreateVersion7();

        // As the copy from retry.3 arrives back in the work queue: through the default exchange, attempt 3.
        await PublishAsync(
            OrderEventTypes.OrderConfirmed,
            Confirmed(1, Email),
            messageId,
            retryAttempt: OrderEventsQueue.RetryDelays.Count
        );

        (await DeadLetteredAsync()).MessageId.ShouldBe(messageId.ToString());
        (await InboxContainsAsync(messageId)).ShouldBeFalse();
    }

    [Fact]
    public async Task UnknownType_MovesMessageToDeadLetterQueueWithoutSending()
    {
        await PublishAsync("order.shipped", Confirmed(1, Email), Guid.CreateVersion7());

        (await DeadLetteredAsync()).Type.ShouldBe("order.shipped");
        _sender.Sent.ShouldBeEmpty();
    }

    [Fact]
    public async Task MissingMessageId_MovesMessageToDeadLetterQueue()
    {
        await PublishAsync(OrderEventTypes.OrderConfirmed, Confirmed(1, Email), messageId: null);

        (await DeadLetteredAsync()).MessageId.ShouldBeNull();
        _sender.Sent.ShouldBeEmpty();
    }

    private static OrderConfirmed Confirmed(int orderId, string? email) =>
        new(
            orderId,
            "user-1",
            email,
            "Jane Doe",
            41m,
            PlacedAsGuest: false,
            DateTime.UtcNow,
            [new OrderConfirmedItem(1, "Chai", 2, 18m)]
        );

    // Like the outbox publisher: to the events exchange, routed by the order event key. retryAttempt set:
    // straight into the work queue with the attempt header, as a copy coming back from a retry queue.
    private async Task PublishAsync<TEvent>(
        string type,
        TEvent @event,
        Guid? messageId,
        int? retryAttempt = null,
        string? routingKey = null
    )
    {
        await using var channel = await _broker.CreateChannelAsync(_cancellationToken);
        var properties = new BasicProperties
        {
            MessageId = messageId?.ToString(),
            Type = type,
            ContentType = "application/json",
            Persistent = true,
        };
        if (retryAttempt != null)
        {
            properties.Headers = new Dictionary<string, object?>
            {
                [RetryAttemptHeader] = retryAttempt.Value,
            };
        }
        var body = JsonSerializer.SerializeToUtf8Bytes(@event, JsonSerializerOptions.Web);

        // An unknown type still reaches the queue: routed by a bound order key, only its Type property differs.
        routingKey ??= type switch
        {
            OrderEventTypes.OrderCancelled => OrderEventTypes.OrderCancelled,
            OrderEventTypes.OrderRejected => OrderEventTypes.OrderRejected,
            _ => OrderEventTypes.OrderConfirmed,
        };
        var (exchange, destination) = retryAttempt is null
            ? (Topology.EventsExchange, routingKey)
            : ("", OrderEventsQueue.Name);
        await channel.BasicPublishAsync(
            exchange,
            destination,
            mandatory: false,
            properties,
            body,
            _cancellationToken
        );
    }

    private Task<bool> SentCountReachesAsync(int count) =>
        Eventually.HoldsAsync(
            () => Task.FromResult(_sender.Sent.Count >= count),
            _cancellationToken
        );

    private async Task<bool> InboxContainsAsync(Guid messageId)
    {
        await using var db = _database.CreateContext();
        return await db.InboxMessages.AnyAsync(m => m.MessageId == messageId, _cancellationToken);
    }

    private async Task<IReadOnlyBasicProperties> DeadLetteredAsync() =>
        (
            await _broker.WaitForMessageAsync(
                Topology.DeadLetterQueue(OrderEventsQueue.Name),
                _cancellationToken
            )
        )
            .ShouldNotBeNull("no message in the dead letter queue")
            .BasicProperties;

    // As the consumer declares it on start, retired keys included.
    private async Task DeclareAndPurgeQueuesAsync()
    {
        await using var channel = await _broker.CreateChannelAsync(_cancellationToken);
        await Topology.DeclareEventsExchangeAsync(channel, _cancellationToken);
        await Topology.DeclareConsumerQueueAsync(
            channel,
            OrderEventsQueue.Name,
            OrderEventsQueue.RoutingKeys,
            OrderEventsQueue.RetryDelays,
            _cancellationToken,
            OrderEventsQueue.RetiredRoutingKeys
        );
        foreach (var queue in AllQueues)
        {
            await channel.QueuePurgeAsync(queue, _cancellationToken);
        }
    }
}
