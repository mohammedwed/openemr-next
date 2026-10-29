using OpenEmrNext.AuthFacade.Application.Ports;
using OpenEmrNext.AuthFacade.Domain.ValueObjects;
using OpenEmrNext.AuthFacade.Infrastructure.OpenEmr;
using Polly.CircuitBreaker;

namespace OpenEmrNext.AuthFacade.UnitTests.Infrastructure;

/// <summary>
/// Unit tests for <see cref="CircuitBreakingIdentityProvider"/> over a counting fake of the port. They
/// check which outcomes count as failures and that an open circuit stops calls from reaching the inner provider.
/// </summary>
public class CircuitBreakingIdentityProviderTests
{
    /// <summary>The number of failing calls that opens the circuit. Every call in these tests fails.</summary>
    private const int MinimumThroughput = 5;

    private readonly CountingIdentityProvider _inner = new();
    private readonly CircuitBreakingIdentityProvider _breaker;

    public CircuitBreakingIdentityProviderTests()
    {
        _breaker = CreateBreaker(TimeSpan.FromSeconds(30));
    }

    /// <summary>Builds a breaker over the counting fake with the given break duration.</summary>
    private CircuitBreakingIdentityProvider CreateBreaker(TimeSpan breakDuration)
    {
        var pipeline = CircuitBreakingIdentityProvider.CreatePipeline(new CircuitBreakerOptions
        {
            MinimumThroughput = MinimumThroughput,
            BreakDuration = breakDuration
        });

        return new CircuitBreakingIdentityProvider(_inner, pipeline);
    }

    private static Username Username() => new("alice_01");
    private static Password Password() => new("Passw0rd!");

    private Task<PasswordGrantResult> CallAsync() =>
        _breaker.PasswordGrantAsync(Username(), Password(), CancellationToken.None);

    /// <summary>An inner provider that counts calls and returns or throws what the test sets.</summary>
    private sealed class CountingIdentityProvider : IIdentityProvider
    {
        public int Calls { get; private set; }
        public PasswordGrantResult? Result { get; set; }
        public Exception? Exception { get; set; }

        public Task<PasswordGrantResult> PasswordGrantAsync(Username username, Password password, CancellationToken ct) =>
            RespondAsync();

        public Task<PasswordGrantResult> RefreshGrantAsync(RefreshToken refreshToken, CancellationToken ct) =>
            RespondAsync();

        private Task<PasswordGrantResult> RespondAsync()
        {
            Calls++;
            if (Exception is not null)
            {
                throw Exception;
            }

            return Task.FromResult(Result!);
        }
    }

    [Fact]
    public async Task Unavailable_calls_open_the_circuit_and_stop_reaching_the_inner_provider()
    {
        _inner.Exception = new IdentityProviderUnavailableException(new HttpRequestException("refused"));

        for (var i = 0; i < MinimumThroughput; i++)
        {
            await Assert.ThrowsAsync<IdentityProviderUnavailableException>(CallAsync);
        }

        var callsBeforeOpen = _inner.Calls;
        _inner.Exception = null;
        _inner.Result = new PasswordGrantResult(200, new OpenEmrToken("t", "Bearer", null, null));

        var ex = await Assert.ThrowsAsync<IdentityProviderUnavailableException>(CallAsync);

        Assert.IsType<BrokenCircuitException>(ex.InnerException);
        Assert.Equal(callsBeforeOpen, _inner.Calls);
    }

    [Theory]
    [InlineData(500)]
    [InlineData(503)]
    [InlineData(408)]
    public async Task Upstream_failure_statuses_count_as_failures(int status)
    {
        _inner.Result = new PasswordGrantResult(status);

        for (var i = 0; i < MinimumThroughput; i++)
        {
            await CallAsync();
        }

        var ex = await Assert.ThrowsAsync<IdentityProviderUnavailableException>(CallAsync);
        Assert.IsType<BrokenCircuitException>(ex.InnerException);
    }

    [Fact]
    public async Task Rejected_credentials_do_not_open_the_circuit()
    {
        _inner.Result = new PasswordGrantResult(400);

        for (var i = 0; i < MinimumThroughput * 2; i++)
        {
            var result = await CallAsync();
            Assert.Equal(400, result.StatusCode);
        }

        Assert.Equal(MinimumThroughput * 2, _inner.Calls);
    }

    [Fact]
    public async Task Circuit_closes_after_the_break_when_the_trial_call_succeeds()
    {
        var breaker = CreateBreaker(TimeSpan.FromMilliseconds(500));
        await OpenCircuitAsync(breaker);

        _inner.Exception = null;
        _inner.Result = new PasswordGrantResult(200, new OpenEmrToken("t", "Bearer", null, null));
        await Task.Delay(TimeSpan.FromMilliseconds(800));

        var trial = await breaker.PasswordGrantAsync(Username(), Password(), CancellationToken.None);
        var afterClose = await breaker.PasswordGrantAsync(Username(), Password(), CancellationToken.None);

        Assert.Equal(200, trial.StatusCode);
        Assert.Equal(200, afterClose.StatusCode);
    }

    [Fact]
    public async Task Failed_trial_call_reopens_the_circuit()
    {
        var breaker = CreateBreaker(TimeSpan.FromMilliseconds(500));
        await OpenCircuitAsync(breaker);
        await Task.Delay(TimeSpan.FromMilliseconds(800));

        // The trial call goes through and fails, so the circuit opens again.
        await Assert.ThrowsAsync<IdentityProviderUnavailableException>(() =>
            breaker.PasswordGrantAsync(Username(), Password(), CancellationToken.None));

        var callsBefore = _inner.Calls;
        var ex = await Assert.ThrowsAsync<IdentityProviderUnavailableException>(() =>
            breaker.PasswordGrantAsync(Username(), Password(), CancellationToken.None));

        Assert.IsType<BrokenCircuitException>(ex.InnerException);
        Assert.Equal(callsBefore, _inner.Calls);
    }

    /// <summary>Makes enough failing calls to open the circuit of <paramref name="breaker"/>.</summary>
    private async Task OpenCircuitAsync(CircuitBreakingIdentityProvider breaker)
    {
        _inner.Exception = new IdentityProviderUnavailableException(new HttpRequestException("refused"));

        for (var i = 0; i < MinimumThroughput; i++)
        {
            await Assert.ThrowsAsync<IdentityProviderUnavailableException>(() =>
                breaker.PasswordGrantAsync(Username(), Password(), CancellationToken.None));
        }
    }

    [Fact]
    public async Task Unusable_success_responses_open_the_circuit()
    {
        _inner.Exception = new IdentityProviderResponseException("OpenEMR response has no access_token.");

        for (var i = 0; i < MinimumThroughput; i++)
        {
            await Assert.ThrowsAsync<IdentityProviderResponseException>(CallAsync);
        }

        var ex = await Assert.ThrowsAsync<IdentityProviderUnavailableException>(CallAsync);
        Assert.IsType<BrokenCircuitException>(ex.InnerException);
    }
}
