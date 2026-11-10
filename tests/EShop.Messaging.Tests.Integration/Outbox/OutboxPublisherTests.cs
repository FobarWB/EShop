using System.Text.Json;
using EShop.Data;
using EShop.Data.Entities;
using EShop.Messaging.Outbox;
using EShop.Messaging.RabbitMq;
using EShop.Tests.Common;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using RabbitMQ.Client;

namespace EShop.Messaging.Tests.Integration.Outbox;

// The publisher as a running background service: rows in the test database, messages in the test broker.
public class OutboxPublisherTests : IAsyncLifetime
{
    // The producer whose publisher runs in these tests.
    private const string Producer = "testapi";

    private readonly MessagingDatabase _database;
    private readonly RabbitMqFixture _broker;
    private readonly CancellationToken _cancellationToken = TestContext.Current.CancellationToken;

    // An event type of its own per test: it is the routing key, and the exchange is shared by all tests.
    private readonly string _type = $"test.{Guid.NewGuid():N}";
    private ServiceProvider? _services;
    private IChannel? _probeChannel;
    private string _probeQueue = "";

    public OutboxPublisherTests(MessagingDatabase database, RabbitMqFixture broker)
    {
        _database = database;
        _broker = broker;
    }

    public async ValueTask InitializeAsync()
    {
        await _database.ResetAsync(_cancellationToken);

        // The scope factory the publisher gets in a service: one DbContext per pass, on the test database.
        _services = new ServiceCollection()
            .AddDbContext<EShopDbContext>(options =>
                options.UseNpgsql(_database.ConnectionString).UseSnakeCaseNamingConvention()
            )
            .BuildServiceProvider();

        // A temporary queue bound to this test's type: it receives what the publisher sends.
        _probeChannel = await _broker.CreateChannelAsync(_cancellationToken);
        await Topology.DeclareEventsExchangeAsync(_probeChannel, _cancellationToken);
        _probeQueue = (
            await _probeChannel.QueueDeclareAsync(cancellationToken: _cancellationToken)
        ).QueueName;
        await _probeChannel.QueueBindAsync(
            _probeQueue,
            Topology.EventsExchange,
            _type,
            cancellationToken: _cancellationToken
        );
    }

    public async ValueTask DisposeAsync()
    {
        if (_probeChannel != null)
        {
            await _probeChannel.DisposeAsync();
        }
        if (_services != null)
        {
            await _services.DisposeAsync();
        }
    }

    [Fact]
    public async Task Run_PendingMessages_PublishesThemInOrderAndMarksThemProcessed()
    {
        var first = await SeedAsync(new DateTime(2026, 11, 1, 10, 0, 0, DateTimeKind.Utc), 1);
        var second = await SeedAsync(new DateTime(2026, 11, 1, 10, 0, 1, DateTimeKind.Utc), 2);

        await RunPublisherUntilProcessedAsync(first.Id, second.Id);

        var one = (await NextMessageAsync()).ShouldNotBeNull();
        var two = (await NextMessageAsync()).ShouldNotBeNull();
        (one.BasicProperties.MessageId, NumberOf(one)).ShouldBe((first.Id.ToString(), 1));
        (two.BasicProperties.MessageId, NumberOf(two)).ShouldBe((second.Id.ToString(), 2));
        one.BasicProperties.Type.ShouldBe(_type);
        one.BasicProperties.ContentType.ShouldBe("application/json");
        one.BasicProperties.DeliveryMode.ShouldBe(DeliveryModes.Persistent);
    }

    [Fact]
    public async Task Run_OtherProducersRows_AreNotPublished()
    {
        // Another service's row, older than ours: its own publisher sends it, in its own order.
        var other = await SeedAsync(DateTime.UtcNow.AddMinutes(-1), 1, producer: "otherapi");
        var ours = await SeedAsync(DateTime.UtcNow, 2);

        await RunPublisherUntilProcessedAsync(ours.Id);

        (await NextMessageAsync())
            .ShouldNotBeNull()
            .BasicProperties.MessageId.ShouldBe(ours.Id.ToString());
        (await _broker.MessageCountAsync(_probeQueue, _cancellationToken)).ShouldBe(0u);
        await using var db = _database.CreateContext();
        (
            await db.OutboxMessages.SingleAsync(m => m.Id == other.Id, _cancellationToken)
        ).ProcessedAt.ShouldBeNull();
    }

    [Fact]
    public async Task Run_ProcessedMessage_IsNotPublishedAgain()
    {
        var processed = await SeedAsync(DateTime.UtcNow.AddMinutes(-1), 1, processed: true);
        var pending = await SeedAsync(DateTime.UtcNow, 2);

        await RunPublisherUntilProcessedAsync(pending.Id);

        (await NextMessageAsync())
            .ShouldNotBeNull()
            .BasicProperties.MessageId.ShouldBe(pending.Id.ToString());
        (await _broker.MessageCountAsync(_probeQueue, _cancellationToken)).ShouldBe(0u);
        processed.Id.ShouldNotBe(pending.Id);
    }

    private Task<OutboxMessage> SeedAsync(
        DateTime occurredAt,
        int number,
        bool processed = false,
        string producer = Producer
    ) =>
        _database.SeedAsync(
            new OutboxMessage
            {
                Id = Guid.CreateVersion7(),
                Producer = producer,
                Type = _type,
                Payload = $$"""{"number": {{number}}}""",
                OccurredAt = occurredAt,
                ProcessedAt = processed ? occurredAt : null,
            },
            _cancellationToken
        );

    // Starts the publisher, waits until the rows are marked processed, stops it.
    private async Task RunPublisherUntilProcessedAsync(params Guid[] ids)
    {
        var publisher = new OutboxPublisher(
            _services.ShouldNotBeNull().GetRequiredService<IServiceScopeFactory>(),
            _broker.Connection,
            new OutboxProducer(Producer),
            NullLogger<OutboxPublisher>.Instance
        );
        await publisher.StartAsync(_cancellationToken);
        try
        {
            (
                await Eventually.HoldsAsync(
                    async () =>
                    {
                        await using var db = _database.CreateContext();
                        return await db.OutboxMessages.CountAsync(
                                m => ids.Contains(m.Id) && m.ProcessedAt != null,
                                _cancellationToken
                            ) == ids.Length;
                    },
                    _cancellationToken
                )
            ).ShouldBeTrue("the publisher did not mark the rows processed");
        }
        finally
        {
            await publisher.StopAsync(CancellationToken.None);
        }
    }

    private Task<BasicGetResult?> NextMessageAsync() =>
        _broker.WaitForMessageAsync(_probeQueue, _cancellationToken, TimeSpan.FromSeconds(2));

    private static int NumberOf(BasicGetResult message) =>
        JsonDocument.Parse(message.Body).RootElement.GetProperty("number").GetInt32();
}
