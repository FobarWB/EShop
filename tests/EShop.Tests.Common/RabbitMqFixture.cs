using EShop.Messaging.RabbitMq;
using Microsoft.Extensions.Options;
using RabbitMQ.Client;
using Testcontainers.RabbitMq;
using Xunit;

namespace EShop.Tests.Common;

/// <summary>
/// A throwaway RabbitMQ broker for one test project (Testcontainers): started once per test run, removed afterwards.
/// The tests never reach the developer's broker or its queues.
/// </summary>
public sealed class RabbitMqFixture : IAsyncLifetime
{
    private const string UserName = "eshop";
    private const string Password = "eshop";

    // The same major version as docker-compose.yml; without the management plugin, which the tests do not need.
    private readonly RabbitMqContainer _container = new RabbitMqBuilder("rabbitmq:4")
        .WithUsername(UserName)
        .WithPassword(Password)
        .Build();

    private RabbitMqConnection? _connection;

    // The connection the services under test use, pointed at the container.
    public RabbitMqConnection Connection =>
        _connection ?? throw new InvalidOperationException("The broker has not started.");

    public async ValueTask InitializeAsync()
    {
        await _container.StartAsync(TestContext.Current.CancellationToken);

        var address = new Uri(_container.GetConnectionString());
        _connection = new RabbitMqConnection(
            Options.Create(
                new RabbitMqOptions
                {
                    Host = address.Host,
                    Port = address.Port,
                    UserName = UserName,
                    Password = Password,
                }
            )
        );
    }

    public Task<IChannel> CreateChannelAsync(CancellationToken cancellationToken) =>
        Connection.CreateChannelAsync(cancellationToken);

    /// <summary>
    /// Waits for the next message of a queue: returns it (acknowledged) or null after the timeout.
    /// </summary>
    public async Task<BasicGetResult?> WaitForMessageAsync(
        string queue,
        CancellationToken cancellationToken,
        TimeSpan? timeout = null
    )
    {
        await using var channel = await CreateChannelAsync(cancellationToken);
        var deadline = DateTime.UtcNow + (timeout ?? TimeSpan.FromSeconds(10));
        while (DateTime.UtcNow < deadline)
        {
            var message = await channel.BasicGetAsync(queue, autoAck: true, cancellationToken);
            if (message != null)
            {
                return message;
            }
            await Task.Delay(50, cancellationToken);
        }
        return null;
    }

    public async Task<uint> MessageCountAsync(string queue, CancellationToken cancellationToken)
    {
        await using var channel = await CreateChannelAsync(cancellationToken);
        return await channel.MessageCountAsync(queue, cancellationToken);
    }

    public async ValueTask DisposeAsync()
    {
        if (_connection != null)
        {
            await _connection.DisposeAsync();
        }
        await _container.DisposeAsync();
    }
}
