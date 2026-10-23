using OpenEmrNext.AuthFacade.Application.Ports;

namespace OpenEmrNext.AuthFacade.UnitTests.Application;

/// <summary>
/// Test double for <see cref="IExternalTokenVerifier"/>. Tests configure it with either a
/// <see cref="Result"/> or an <see cref="Exception"/>. The exception takes precedence and is
/// thrown from the call.
/// </summary>
internal sealed class FakeExternalTokenVerifier : IExternalTokenVerifier
{
    /// <summary>The value returned from <see cref="VerifyAsync"/>. Null means the token is not valid.</summary>
    public VerifiedToken? Result { get; set; }

    /// <summary>When set, thrown from <see cref="VerifyAsync"/> instead of returning <see cref="Result"/>.</summary>
    public Exception? Exception { get; set; }

    /// <summary>The token passed on the last call, for assertions.</summary>
    public string? ReceivedToken { get; private set; }

    /// <summary>Records the token, then throws <see cref="Exception"/> or returns <see cref="Result"/>.</summary>
    public Task<VerifiedToken?> VerifyAsync(string token, CancellationToken ct)
    {
        ReceivedToken = token;

        if (Exception is not null)
        {
            throw Exception;
        }

        return Task.FromResult(Result);
    }
}
