using System.Text.Json;
using EShop.Data;
using EShop.Messaging.RabbitMq;
using EShop.NotificationService.Messaging;
using EShop.OrderApi.Contracts;
using EShop.Tests.Common;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using RabbitMQ.Client;

namespace EShop.NotificationService.Tests.Integration.Messaging;

// The consumer as a running background service on the real topology (the production queue names and delays),
// with the email sender replaced. Retry delays are not waited for: a test publishes a message as it arrives
// on its last attempt instead.
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
    private OrderEventsConsumer? _consumer;

    public OrderEventsConsumerTests(NotificationServiceDatabase database, RabbitMqFixture broker)
    {
        _database = database;
        _broker = broker;
    }

    public async ValueTask InitializeAsync()
    {
        await _database.ResetAsync(_cancellationToken);
        await DeclareAndPurgeQueuesAsync();

        _services = new ServiceCollection()
            .AddDbContext<EShopDbContext>(options =>
                options.UseNpgsql(_database.ConnectionString).UseSnakeCaseNamingConvention()
            )
            .BuildServiceProvider();
        _consumer = new OrderEventsConsumer(
            _broker.Connection,
            _services.GetRequiredService<IServiceScopeFactory>(),
            _sender,
            NullLogger<OrderEventsConsumer>.Instance
        );
        await _consumer.StartAsync(_cancellationToken);
    }

    public async ValueTask DisposeAsync()
    {
        if (_consumer != null)
        {
            await _consumer.StopAsync(CancellationToken.None);
            _consumer.Dispose();
        }
        if (_services != null)
        {
            await _services.DisposeAsync();
        }
        // Copies may still wait in the retry queues: the next test starts empty.
        await DeclareAndPurgeQueuesAsync();
    }

    [Fact]
    public async Task OrderPlaced_SendsEmailAndRecordsInbox()
    {
        var messageId = Guid.CreateVersion7();

        await PublishAsync(OrderEventTypes.OrderPlaced, Placed(11085, Email), messageId);

        (await SentCountReachesAsync(1)).ShouldBeTrue();
        var email = _sender.Sent.ShouldHaveSingleItem();
        (email.To, email.Subject).ShouldBe((Email, "Order #11085 placed"));
        // The inbox row is written after the email: wait for it too.
        (
            await Eventually.HoldsAsync(() => InboxContainsAsync(messageId), _cancellationToken)
        ).ShouldBeTrue();
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
    public async Task SameMessageIdAgain_SendsOneEmail()
    {
        var messageId = Guid.CreateVersion7();
        await PublishAsync(OrderEventTypes.OrderPlaced, Placed(1, Email), messageId);
        (await SentCountReachesAsync(1)).ShouldBeTrue();

        // A redelivery, then another event: handled in order, so once the second email is sent,
        // the duplicate has been handled too.
        await PublishAsync(OrderEventTypes.OrderPlaced, Placed(1, Email), messageId);
        await PublishAsync(OrderEventTypes.OrderPlaced, Placed(2, Email), Guid.CreateVersion7());

        (await SentCountReachesAsync(2)).ShouldBeTrue();
        _sender.Sent.Select(e => e.Subject).ShouldBe(["Order #1 placed", "Order #2 placed"]);
    }

    [Fact]
    public async Task NoEmailAddress_RecordsInboxWithoutEmail()
    {
        var messageId = Guid.CreateVersion7();

        await PublishAsync(OrderEventTypes.OrderPlaced, Placed(1, email: null), messageId);

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

        await PublishAsync(OrderEventTypes.OrderPlaced, Placed(1, Email), messageId);

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
            OrderEventTypes.OrderPlaced,
            Placed(1, Email),
            messageId,
            retryAttempt: OrderEventsQueue.RetryDelays.Count
        );

        (await DeadLetteredAsync()).MessageId.ShouldBe(messageId.ToString());
        (await InboxContainsAsync(messageId)).ShouldBeFalse();
    }

    [Fact]
    public async Task UnknownType_MovesMessageToDeadLetterQueueWithoutSending()
    {
        await PublishAsync("order.shipped", Placed(1, Email), Guid.CreateVersion7());

        (await DeadLetteredAsync()).Type.ShouldBe("order.shipped");
        _sender.Sent.ShouldBeEmpty();
    }

    [Fact]
    public async Task MissingMessageId_MovesMessageToDeadLetterQueue()
    {
        await PublishAsync(OrderEventTypes.OrderPlaced, Placed(1, Email), messageId: null);

        (await DeadLetteredAsync()).MessageId.ShouldBeNull();
        _sender.Sent.ShouldBeEmpty();
    }

    private static OrderPlaced Placed(int orderId, string? email) =>
        new(
            orderId,
            email,
            "Jane Doe",
            41m,
            DateTime.UtcNow,
            [new OrderPlacedItem("Chai", 2, 18m)]
        );

    // Like the outbox publisher: to the events exchange, routed by the order event key. retryAttempt set:
    // straight into the work queue with the attempt header, as a copy coming back from a retry queue.
    private async Task PublishAsync<TEvent>(
        string type,
        TEvent @event,
        Guid? messageId,
        int? retryAttempt = null
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

        // An unknown type still reaches the queue: routed by an order key, only its Type property differs.
        var routingKey =
            type == OrderEventTypes.OrderCancelled
                ? OrderEventTypes.OrderCancelled
                : OrderEventTypes.OrderPlaced;
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

    private async Task DeclareAndPurgeQueuesAsync()
    {
        await using var channel = await _broker.CreateChannelAsync(_cancellationToken);
        await Topology.DeclareEventsExchangeAsync(channel, _cancellationToken);
        await Topology.DeclareConsumerQueueAsync(
            channel,
            OrderEventsQueue.Name,
            OrderEventsQueue.RoutingKeys,
            OrderEventsQueue.RetryDelays,
            _cancellationToken
        );
        foreach (var queue in AllQueues)
        {
            await channel.QueuePurgeAsync(queue, _cancellationToken);
        }
    }
}
