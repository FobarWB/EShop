using System.Text;
using EShop.Messaging.RabbitMq;
using EShop.Tests.Common;
using RabbitMQ.Client;
using RabbitMQ.Client.Exceptions;

namespace EShop.Messaging.Tests.Integration.RabbitMq;

// The broker silently ignores misspelled queue arguments, so these tests check the behavior
// (where a message ends up), not only that the declaration did not throw.
public class TopologyTests
{
    private static readonly TimeSpan ShortDelay = TimeSpan.FromMilliseconds(200);

    private readonly RabbitMqFixture _broker;
    private readonly CancellationToken _cancellationToken = TestContext.Current.CancellationToken;

    // A queue and a routing key of their own per test: the events exchange is shared by all tests.
    private readonly string _queue = $"test.{Guid.NewGuid():N}";
    private readonly string _routingKey = $"test.{Guid.NewGuid():N}";

    public TopologyTests(RabbitMqFixture broker)
    {
        _broker = broker;
    }

    [Fact]
    public async Task DeclareConsumerQueue_DeclaresWorkRetryAndDeadLetterQueues()
    {
        await using var channel = await DeclareAsync([ShortDelay, ShortDelay]);

        // Passive: only checks that the queue exists; a missing one closes the channel with NOT_FOUND.
        await channel.QueueDeclarePassiveAsync(_queue, _cancellationToken);
        await channel.QueueDeclarePassiveAsync(Topology.RetryQueue(_queue, 1), _cancellationToken);
        await channel.QueueDeclarePassiveAsync(Topology.RetryQueue(_queue, 2), _cancellationToken);
        await channel.QueueDeclarePassiveAsync(
            Topology.DeadLetterQueue(_queue),
            _cancellationToken
        );
    }

    [Fact]
    public async Task DeclareConsumerQueue_SameArgumentsAgain_DoesNotThrow()
    {
        await using var first = await DeclareAsync([ShortDelay]);

        await using var second = await DeclareAsync([ShortDelay]);

        second.IsOpen.ShouldBeTrue();
    }

    [Fact]
    public async Task DeclareConsumerQueue_OtherArgumentsAgain_FailsWithPreconditionFailed()
    {
        await using var first = await DeclareAsync([ShortDelay]);

        // The lesson of the lab: arguments are set once; a different TTL is refused, not applied.
        var error = await Should.ThrowAsync<OperationInterruptedException>(() =>
            DeclareAsync([TimeSpan.FromSeconds(1)])
        );

        ((int)error.ShutdownReason.ShouldNotBeNull().ReplyCode).ShouldBe(
            Constants.PreconditionFailed
        );
    }

    [Fact]
    public async Task EventsExchange_BoundRoutingKey_RoutesToWorkQueue()
    {
        await using var channel = await DeclareAsync([ShortDelay]);

        await PublishAsync(channel, Topology.EventsExchange, _routingKey, "event");

        var message = await _broker.WaitForMessageAsync(_queue, _cancellationToken);
        Encoding.UTF8.GetString(message.ShouldNotBeNull().Body.Span).ShouldBe("event");
    }

    [Fact]
    public async Task RetryQueue_TtlExpired_ReturnsMessageToWorkQueue()
    {
        await using var channel = await DeclareAsync([ShortDelay]);

        // As the consumer does: straight into the wait queue through the default exchange.
        await PublishAsync(channel, "", Topology.RetryQueue(_queue, 1), "retry me");

        var message = await _broker.WaitForMessageAsync(_queue, _cancellationToken);
        Encoding.UTF8.GetString(message.ShouldNotBeNull().Body.Span).ShouldBe("retry me");
        (
            await _broker.MessageCountAsync(Topology.RetryQueue(_queue, 1), _cancellationToken)
        ).ShouldBe(0u);
    }

    [Fact]
    public async Task WorkQueue_RejectedWithoutRequeue_MovesMessageToDeadLetterQueue()
    {
        await using var channel = await DeclareAsync([ShortDelay]);
        await PublishAsync(channel, Topology.EventsExchange, _routingKey, "poison");
        var delivered = (await WaitForUnackedAsync(channel, _queue)).ShouldNotBeNull();

        await channel.BasicRejectAsync(delivered.DeliveryTag, requeue: false, _cancellationToken);

        var message = await _broker.WaitForMessageAsync(
            Topology.DeadLetterQueue(_queue),
            _cancellationToken
        );
        Encoding.UTF8.GetString(message.ShouldNotBeNull().Body.Span).ShouldBe("poison");
    }

    // A channel with this test's exchange, queues and binding declared; the caller disposes it.
    private async Task<IChannel> DeclareAsync(IReadOnlyList<TimeSpan> retryDelays)
    {
        var channel = await _broker.CreateChannelAsync(_cancellationToken);
        try
        {
            await Topology.DeclareEventsExchangeAsync(channel, _cancellationToken);
            await Topology.DeclareConsumerQueueAsync(
                channel,
                _queue,
                [_routingKey],
                retryDelays,
                _cancellationToken
            );
            return channel;
        }
        catch
        {
            await channel.DisposeAsync();
            throw;
        }
    }

    private async Task PublishAsync(
        IChannel channel,
        string exchange,
        string routingKey,
        string body
    ) =>
        await channel.BasicPublishAsync(
            exchange,
            routingKey,
            Encoding.UTF8.GetBytes(body),
            _cancellationToken
        );

    // Takes a message without acknowledging it, so the test can reject it.
    private async Task<BasicGetResult?> WaitForUnackedAsync(IChannel channel, string queue)
    {
        var deadline = DateTime.UtcNow + TimeSpan.FromSeconds(10);
        while (DateTime.UtcNow < deadline)
        {
            var message = await channel.BasicGetAsync(queue, autoAck: false, _cancellationToken);
            if (message != null)
            {
                return message;
            }
            await Task.Delay(50, _cancellationToken);
        }
        return null;
    }
}
