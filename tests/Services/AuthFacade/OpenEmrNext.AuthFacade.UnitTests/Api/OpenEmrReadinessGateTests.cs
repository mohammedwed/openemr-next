using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;
using OpenEmrNext.AuthFacade.Api.Startup;

namespace OpenEmrNext.AuthFacade.UnitTests.Api;

/// <summary>
/// Unit tests for <see cref="OpenEmrReadinessGate"/>'s polling logic, isolated from the real network and
/// the full host (<see cref="TestApi"/> never calls this gate, since most of its configs point
/// <c>OpenEmr:BaseUrl</c> at placeholder hosts that would otherwise make every test wait out the timeout).
/// Uses small thresholds so the retry and timeout paths run in milliseconds, not the real 60s/2s ones.
/// </summary>
public class OpenEmrReadinessGateTests
{
    private static readonly Uri ReadyzUri = new("https://openemr.test/meta/health/readyz");
    private static readonly TimeSpan ShortMaxWait = TimeSpan.FromMilliseconds(200);
    private static readonly TimeSpan ShortPollInterval = TimeSpan.FromMilliseconds(20);

    /// <summary>
    /// Returns a fresh <see cref="HttpResponseMessage"/> on every call, unlike the shared
    /// <c>Infrastructure.FakeHttpMessageHandler</c>: that one deliberately returns the same instance every
    /// time, which a retry loop's own <c>using var response = ...</c> disposes after the first call,
    /// throwing <see cref="ObjectDisposedException"/> on the second. No content is ever needed here — only
    /// the status code (or an exception) matters to the gate.
    /// </summary>
    private sealed class RespondingHandler : HttpMessageHandler
    {
        public System.Net.HttpStatusCode? StatusCode { get; set; }
        public Exception? Exception { get; set; }
        public int CallCount { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            CallCount++;
            return Exception is not null
                ? Task.FromException<HttpResponseMessage>(Exception)
                : Task.FromResult(new HttpResponseMessage(StatusCode!.Value));
        }
    }

    /// <summary>Captures every formatted message logged through it, so a test can assert on the text.</summary>
    private sealed class CapturingLogger : ILogger
    {
        public ConcurrentQueue<string> Entries { get; } = new();

        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(
            LogLevel logLevel, EventId eventId, TState state, Exception? exception,
            Func<TState, Exception?, string> formatter) =>
            Entries.Enqueue(formatter(state, exception));
    }

    [Fact]
    public async Task Returns_immediately_once_openemr_answers_successfully()
    {
        using var handler = new RespondingHandler { StatusCode = System.Net.HttpStatusCode.OK };
        using var http = new HttpClient(handler);
        var logger = new CapturingLogger();

        await OpenEmrReadinessGate.PollUntilReadyAsync(http, ReadyzUri, ShortMaxWait, ShortPollInterval, logger);

        Assert.Equal(1, handler.CallCount);
        Assert.DoesNotContain(logger.Entries, entry => entry.Contains("did not report ready", StringComparison.Ordinal));
    }

    [Fact]
    public async Task Gives_up_after_the_deadline_and_logs_a_warning_instead_of_throwing()
    {
        using var handler = new RespondingHandler { StatusCode = System.Net.HttpStatusCode.ServiceUnavailable };
        using var http = new HttpClient(handler);
        var logger = new CapturingLogger();

        await OpenEmrReadinessGate.PollUntilReadyAsync(http, ReadyzUri, ShortMaxWait, ShortPollInterval, logger);

        Assert.True(handler.CallCount > 1, "Expected more than one attempt before giving up.");
        Assert.Contains(logger.Entries, entry => entry.Contains("did not report ready", StringComparison.Ordinal));
    }

    [Fact]
    public async Task An_unreachable_openemr_counts_as_not_ready_rather_than_throwing()
    {
        using var handler = new RespondingHandler { Exception = new HttpRequestException("connection refused") };
        using var http = new HttpClient(handler);
        var logger = new CapturingLogger();

        await OpenEmrReadinessGate.PollUntilReadyAsync(http, ReadyzUri, ShortMaxWait, ShortPollInterval, logger);

        Assert.Contains(logger.Entries, entry => entry.Contains("did not report ready", StringComparison.Ordinal));
    }
}
