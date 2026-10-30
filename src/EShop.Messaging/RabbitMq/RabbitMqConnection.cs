using System.Reflection;
using Microsoft.Extensions.Options;
using RabbitMQ.Client;

namespace EShop.Messaging.RabbitMq;

public sealed class RabbitMqConnection : IAsyncDisposable
{
    private readonly ConnectionFactory _factory;
    private readonly SemaphoreSlim _semaphore = new(1, 1);

    // One TCP connection per process, opened on first use; channels are cheap and opened per publisher and consumer.
    // Not Lazy<T>: it would cache a failed first attempt (broker still starting), and this retries on the next call.
    private IConnection? _connection;

    public RabbitMqConnection(IOptions<RabbitMqOptions> options)
    {
        _factory = new ConnectionFactory
        {
            HostName = options.Value.Host,
            Port = options.Value.Port,
            VirtualHost = options.Value.VirtualHost,
            UserName = options.Value.UserName,
            Password = options.Value.Password,
            ClientProvidedName = Assembly.GetEntryAssembly()?.GetName().Name,
        };
    }

    public async Task<IChannel> CreateChannelAsync(
        CancellationToken cancellationToken,
        CreateChannelOptions? options = null
    )
    {
        if (_connection == null)
        {
            await _semaphore.WaitAsync(cancellationToken);
            try
            {
                _connection ??= await _factory.CreateConnectionAsync(cancellationToken);
            }
            finally
            {
                _semaphore.Release();
            }
        }

        return await _connection.CreateChannelAsync(options, cancellationToken);
    }

    public async ValueTask DisposeAsync()
    {
        if (_connection != null)
        {
            // A clean connection.close: the broker releases the channels at once instead of waiting for a timeout.
            await _connection.CloseAsync();
            await _connection.DisposeAsync();
        }
        _semaphore.Dispose();
    }
}
