using OpenEmrNext.AuthFacade.Application.Ports;
using OpenEmrNext.AuthFacade.Domain.ValueObjects;

namespace OpenEmrNext.AuthFacade.UnitTests.Application;

/// <summary>
/// Test double for <see cref="IOpenEmrIdentityProvider"/>. Tests configure it with either a
/// <see cref="Result"/> or an <see cref="Exception"/>. The exception takes precedence and is
/// thrown from the call.
/// </summary>
internal sealed class FakeOpenEmrIdentityProvider : IOpenEmrIdentityProvider
{
    /// <summary>The value returned from <see cref="PasswordGrantAsync"/>. Must be set when no exception is.</summary>
    public PasswordGrantResult? Result { get; set; }

    /// <summary>When set, thrown from <see cref="PasswordGrantAsync"/> instead of returning <see cref="Result"/>.</summary>
    public Exception? Exception { get; set; }

    /// <summary>The username passed on the last call, for assertions.</summary>
    public Username? ReceivedUsername { get; private set; }

    /// <summary>The password passed on the last call, for assertions. Its plaintext is not consumed by the fake.</summary>
    public Password? ReceivedPassword { get; private set; }

    /// <summary>Records the arguments, then throws <see cref="Exception"/> or returns <see cref="Result"/>.</summary>
    public Task<PasswordGrantResult> PasswordGrantAsync(Username username, Password password, CancellationToken ct)
    {
        ReceivedUsername = username;
        ReceivedPassword = password;

        if (Exception is not null)
        {
            throw Exception;
        }

        return Task.FromResult(Result!);
    }
}
