using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;

namespace OpenEmrNext.AuthFacade.UnitTests.Api;

/// <summary>
/// Captures every formatted log message and exception text written through any <see cref="ILogger"/> in
/// the host, across every category, so a test can assert that none of them ever contain a given value —
/// the only way to prove a negative about logging without reading production code for every place it
/// could call into a logger.
/// </summary>
internal sealed class CapturingLoggerProvider : ILoggerProvider
{
    private readonly ConcurrentQueue<string> _entries = new();

    /// <summary>Every formatted message and exception message captured so far.</summary>
    public IReadOnlyCollection<string> Entries => _entries;

    public ILogger CreateLogger(string categoryName) => new CapturingLogger(_entries);

    public void Dispose() { }

    private sealed class CapturingLogger(ConcurrentQueue<string> entries) : ILogger
    {
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(
            LogLevel logLevel, EventId eventId, TState state, Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
            entries.Enqueue(formatter(state, exception));
            if (exception is not null)
            {
                entries.Enqueue(exception.ToString());
            }
        }
    }
}
