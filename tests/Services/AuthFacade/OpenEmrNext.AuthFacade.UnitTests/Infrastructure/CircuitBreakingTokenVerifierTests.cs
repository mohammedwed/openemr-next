using OpenEmrNext.AuthFacade.Application.Ports;
using OpenEmrNext.AuthFacade.Infrastructure.OpenEmr;
using Polly.CircuitBreaker;

namespace OpenEmrNext.AuthFacade.UnitTests.Infrastructure;

/// <summary>
/// Unit tests for <see cref="CircuitBreakingTokenVerifier"/> over a counting fake of the verifier port. They
/// check that only an unreachable key set counts as a failure, and that an open circuit stops calls from
/// reaching the inner verifier.
/// </summary>
public class CircuitBreakingTokenVerifierTests
{
    /// <summary>The number of failing calls that opens the circuit. Every call in the failure tests fails.</summary>
    private const int MinimumThroughput = 5;

    private readonly CountingTokenVerifier _inner = new();

    private CircuitBreakingTokenVerifier CreateBreaker(TimeSpan breakDuration)
    {
        var pipeline = CircuitBreakingTokenVerifier.CreatePipeline(new CircuitBreakerOptions
        {
            MinimumThroughput = MinimumThroughput,
            BreakDuration = breakDuration
        });

        return new CircuitBreakingTokenVerifier(_inner, pipeline);
    }

    private static Task<VerifiedToken?> CallAsync(CircuitBreakingTokenVerifier breaker) =>
        breaker.VerifyAsync("token", CancellationToken.None);

    /// <summary>An inner verifier that counts calls and returns or throws what the test sets.</summary>
    private sealed class CountingTokenVerifier : IExternalTokenVerifier
    {
        public int Calls { get; private set; }
        public VerifiedToken? Result { get; set; }
        public Exception? Exception { get; set; }

        public Task<VerifiedToken?> VerifyAsync(string token, CancellationToken ct)
        {
            Calls++;
            if (Exception is not null)
            {
                throw Exception;
            }

            return Task.FromResult(Result);
        }
    }

    [Fact]
    public async Task Unreachable_keys_open_the_circuit_and_stop_reaching_the_inner_verifier()
    {
        var breaker = CreateBreaker(TimeSpan.FromSeconds(30));
        _inner.Exception = new TokenVerifierUnavailableException(new HttpRequestException("refused"));

        for (var i = 0; i < MinimumThroughput; i++)
        {
            await Assert.ThrowsAsync<TokenVerifierUnavailableException>(() => CallAsync(breaker));
        }

        var callsBeforeOpen = _inner.Calls;
        _inner.Exception = null;
        _inner.Result = new VerifiedToken("subject-1", null);

        var ex = await Assert.ThrowsAsync<TokenVerifierUnavailableException>(() => CallAsync(breaker));

        Assert.IsType<BrokenCircuitException>(ex.InnerException);
        Assert.Equal(callsBeforeOpen, _inner.Calls);
    }

    [Fact]
    public async Task Tokens_that_fail_verification_do_not_open_the_circuit()
    {
        var breaker = CreateBreaker(TimeSpan.FromSeconds(30));
        _inner.Result = null;

        for (var i = 0; i < MinimumThroughput * 2; i++)
        {
            Assert.Null(await CallAsync(breaker));
        }

        Assert.Equal(MinimumThroughput * 2, _inner.Calls);
    }

    [Fact]
    public async Task Circuit_closes_after_the_break_when_the_trial_call_succeeds()
    {
        var breaker = CreateBreaker(TimeSpan.FromMilliseconds(500));
        _inner.Exception = new TokenVerifierUnavailableException(new HttpRequestException("refused"));
        for (var i = 0; i < MinimumThroughput; i++)
        {
            await Assert.ThrowsAsync<TokenVerifierUnavailableException>(() => CallAsync(breaker));
        }

        _inner.Exception = null;
        _inner.Result = new VerifiedToken("subject-1", null);
        await Task.Delay(TimeSpan.FromMilliseconds(800));

        var trial = await CallAsync(breaker);
        var afterClose = await CallAsync(breaker);

        Assert.Equal("subject-1", trial!.Subject);
        Assert.Equal("subject-1", afterClose!.Subject);
    }
}
