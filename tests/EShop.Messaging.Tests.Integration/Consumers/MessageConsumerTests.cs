using System.Text.Json;
using EShop.Data;
using EShop.Data.Entities;
using EShop.Messaging.Consumers;
using EShop.Messaging.Outbox;
using EShop.Messaging.RabbitMq;
using EShop.Tests.Common;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using RabbitMQ.Client;

namespace EShop.Messaging.Tests.Integration.Consumers;

// The shared consumer with a test handler: what the consumer guarantees to every handler, above all that the
// handler's database changes and the inbox row are stored in one transaction.
public class MessageConsumerTests : IAsyncLifetime
{
    private const string Queue = "tests.message-consumer";
    private const string Added = "test.added";
    private const string UpdatedThenFailed = "test.updated-then-failed";
    private const string Rejected = "test.rejected";

    private static readonly ConsumerQueue Definition = new(
        Queue,
        "tests",
        [Added, UpdatedThenFailed, Rejected],
        [TimeSpan.FromSeconds(30)]
    );

    private readonly MessagingDatabase _database;
    private readonly RabbitMqFixture _broker;
    private readonly CancellationToken _cancellationToken = TestContext.Current.CancellationToken;
    private ServiceProvider? _services;
    private IHostedService? _consumer;

    public MessageConsumerTests(MessagingDatabase database, RabbitMqFixture broker)
    {
        _database = database;
        _broker = broker;
    }

    public async ValueTask InitializeAsync()
    {
        await _database.ResetAsync(_cancellationToken);
        await PurgeQueuesAsync();

        _services = new ServiceCollection()
            .AddLogging()
            .AddDbContext<EShopDbContext>(options =>
                options.UseNpgsql(_database.ConnectionString).UseSnakeCaseNamingConvention()
            )
            .AddSingleton(_broker.Connection)
            .AddMessageConsumer<TestHandler>(Definition)
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
        await PurgeQueuesAsync();
    }

    [Fact]
    public async Task HandlerChanges_AreStoredWithTheInboxRow()
    {
        var messageId = Guid.CreateVersion7();

        await PublishAsync(Added, new { number = 7 }, messageId);

        (
            await Eventually.HoldsAsync(() => InboxContainsAsync(messageId), _cancellationToken)
        ).ShouldBeTrue("the message was not recorded in the inbox");
        await using var db = _database.CreateContext();
        (await db.OutboxMessages.SingleAsync(_cancellationToken)).Payload.ShouldContain("7");
    }

    [Fact]
    public async Task HandlerThrowsAfterAnUpdate_RollsBackTheUpdateAndRetries()
    {
        var product = await _database.SeedProductAsync(10, _cancellationToken);
        var messageId = Guid.CreateVersion7();

        await PublishAsync(UpdatedThenFailed, new { number = product }, messageId);

        // The copy waits in retry.1 (30 s): taken out before it comes back.
        var copy = (
            await _broker.WaitForMessageAsync(Topology.RetryQueue(Queue, 1), _cancellationToken)
        ).ShouldNotBeNull("no copy in the retry queue");
        copy.BasicProperties.MessageId.ShouldBe(messageId.ToString());
        (await _database.FindAsync<Product>(product, _cancellationToken))
            .ShouldNotBeNull()
            .UnitsInStock.ShouldBe((short)10);
        (await InboxContainsAsync(messageId)).ShouldBeFalse();
    }

    [Fact]
    public async Task HandlerRejects_MovesTheMessageToTheDeadLetterQueueWithoutInbox()
    {
        var messageId = Guid.CreateVersion7();

        await PublishAsync(Rejected, new { number = 1 }, messageId);

        (await DeadLetteredAsync()).MessageId.ShouldBe(messageId.ToString());
        (await InboxContainsAsync(messageId)).ShouldBeFalse();
    }

    [Fact]
    public async Task BodyNotMatchingTheContract_MovesTheMessageToTheDeadLetterQueue()
    {
        var messageId = Guid.CreateVersion7();

        await PublishAsync(Added, new { number = "not a number" }, messageId);

        (await DeadLetteredAsync()).MessageId.ShouldBe(messageId.ToString());
        await using var db = _database.CreateContext();
        (await db.OutboxMessages.AnyAsync(_cancellationToken)).ShouldBeFalse();
    }

    [Fact]
    public async Task SameMessageIdAgain_IsHandledOnce()
    {
        var messageId = Guid.CreateVersion7();
        await PublishAsync(Added, new { number = 1 }, messageId);
        (
            await Eventually.HoldsAsync(() => InboxContainsAsync(messageId), _cancellationToken)
        ).ShouldBeTrue();

        // The redelivery, then another message: handled in order, so once the second is stored the first was seen.
        var second = Guid.CreateVersion7();
        await PublishAsync(Added, new { number = 1 }, messageId);
        await PublishAsync(Added, new { number = 2 }, second);

        (
            await Eventually.HoldsAsync(() => InboxContainsAsync(second), _cancellationToken)
        ).ShouldBeTrue();
        await using var db = _database.CreateContext();
        (await db.OutboxMessages.CountAsync(_cancellationToken)).ShouldBe(2);
    }

    private async Task PublishAsync(string type, object body, Guid messageId)
    {
        await using var channel = await _broker.CreateChannelAsync(_cancellationToken);
        await channel.BasicPublishAsync(
            Topology.EventsExchange,
            type,
            mandatory: false,
            new BasicProperties
            {
                MessageId = messageId.ToString(),
                Type = type,
                ContentType = "application/json",
                Persistent = true,
            },
            JsonSerializer.SerializeToUtf8Bytes(body, JsonSerializerOptions.Web),
            _cancellationToken
        );
    }

    private async Task<bool> InboxContainsAsync(Guid messageId)
    {
        await using var db = _database.CreateContext();
        return await db.InboxMessages.AnyAsync(
            m => m.Consumer == Definition.Consumer && m.MessageId == messageId,
            _cancellationToken
        );
    }

    private async Task<IReadOnlyBasicProperties> DeadLetteredAsync() =>
        (await _broker.WaitForMessageAsync(Topology.DeadLetterQueue(Queue), _cancellationToken))
            .ShouldNotBeNull("no message in the dead letter queue")
            .BasicProperties;

    // The queues survive the tests (durable): declared and emptied, so every test starts clean.
    private async Task PurgeQueuesAsync()
    {
        await using var channel = await _broker.CreateChannelAsync(_cancellationToken);
        await Topology.DeclareEventsExchangeAsync(channel, _cancellationToken);
        await Topology.DeclareConsumerQueueAsync(
            channel,
            Queue,
            Definition.RoutingKeys,
            Definition.RetryDelays,
            _cancellationToken
        );
        foreach (
            var queue in new[]
            {
                Queue,
                Topology.RetryQueue(Queue, 1),
                Topology.DeadLetterQueue(Queue),
            }
        )
        {
            await channel.QueuePurgeAsync(queue, _cancellationToken);
        }
    }

    private sealed record TestPayload(int Number);

    // What a service's handler does, by message type: a tracked change, a statement and a failure, a rejection.
    private sealed class TestHandler(EShopDbContext db) : IMessageHandler
    {
        public async Task<HandleResult> HandleAsync(
            ReceivedMessage message,
            CancellationToken cancellationToken
        )
        {
            var payload = message.Deserialize<TestPayload>();
            switch (message.Type)
            {
                case Added:
                    // Only tracked: the consumer saves it with the inbox row.
                    db.OutboxMessages.Add(
                        OutboxMessageFactory.Create(
                            "tests",
                            "test.result",
                            payload,
                            DateTime.UtcNow
                        )
                    );
                    return HandleResult.Processed;
                case UpdatedThenFailed:
                    // Runs at once, inside the consumer's transaction; the exception must undo it.
                    await db
                        .Products.Where(p => p.ProductId == payload.Number)
                        .ExecuteUpdateAsync(
                            s => s.SetProperty(p => p.UnitsInStock, (short)0),
                            cancellationToken
                        );
                    throw new InvalidOperationException("The handler failed after its update.");
                default:
                    return HandleResult.Reject("rejected by the test handler");
            }
        }
    }
}
