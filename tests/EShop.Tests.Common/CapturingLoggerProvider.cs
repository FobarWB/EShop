using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;

namespace EShop.Tests.Common;

/// <summary>
/// Collects the formatted log messages of an in-memory API, so tests can assert what reaches the logs.
/// The API's logging configuration (levels from appsettings) still applies.
/// </summary>
public sealed class CapturingLoggerProvider : ILoggerProvider
{
    private readonly ConcurrentQueue<string> _messages = new();

    public IReadOnlyCollection<string> Messages => _messages;

    public ILogger CreateLogger(string categoryName) => new CapturingLogger(_messages);

    public void Dispose() { }

    private sealed class CapturingLogger(ConcurrentQueue<string> messages) : ILogger
    {
        public IDisposable? BeginScope<TState>(TState state)
            where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter
        ) => messages.Enqueue(formatter(state, exception));
    }
}
