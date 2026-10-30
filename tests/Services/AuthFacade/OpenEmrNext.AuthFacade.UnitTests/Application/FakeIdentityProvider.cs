using OpenEmrNext.AuthFacade.Application.Ports;
using OpenEmrNext.AuthFacade.Domain.ValueObjects;

namespace OpenEmrNext.AuthFacade.UnitTests.Application;

/// <summary>
/// Test double for <see cref="IIdentityProvider"/>. Tests configure it with either a
/// <see cref="Result"/> or an <see cref="Exception"/>. The exception takes precedence and is
/// thrown from the call.
/// </summary>
internal sealed class FakeIdentityProvider : IIdentityProvider
{
    /// <summary>The value returned from <see cref="PasswordGrantAsync"/>. Must be set when no exception is.</summary>
    public PasswordGrantResult? Result { get; set; }

    /// <summary>When set, thrown from <see cref="PasswordGrantAsync"/> instead of returning <see cref="Result"/>.</summary>
    public Exception? Exception { get; set; }

    /// <summary>The username passed on the last call, for assertions.</summary>
    public Username? ReceivedUsername { get; private set; }

    /// <summary>The password passed on the last call, for assertions. Its plaintext is not consumed by the fake.</summary>
    public Password? ReceivedPassword { get; private set; }

    /// <summary>The refresh token passed on the last call, for assertions. Its plaintext is not consumed by the fake.</summary>
    public RefreshToken? ReceivedRefreshToken { get; private set; }

    /// <summary>The authorization code passed on the last call, for assertions.</summary>
    public string? ReceivedCode { get; private set; }

    /// <summary>The PKCE verifier passed on the last call, for assertions.</summary>
    public string? ReceivedCodeVerifier { get; private set; }

    /// <summary>The redirect URI passed on the last call, for assertions.</summary>
    public string? ReceivedRedirectUri { get; private set; }

    /// <summary>Records the arguments, then throws <see cref="Exception"/> or returns <see cref="Result"/>.</summary>
    public Task<PasswordGrantResult> PasswordGrantAsync(Username username, Password password, CancellationToken ct)
    {
        ReceivedUsername = username;
        ReceivedPassword = password;

        return RespondAsync();
    }

    /// <summary>Records the argument, then throws <see cref="Exception"/> or returns <see cref="Result"/>.</summary>
    public Task<PasswordGrantResult> RefreshGrantAsync(RefreshToken refreshToken, CancellationToken ct)
    {
        ReceivedRefreshToken = refreshToken;

        return RespondAsync();
    }

    /// <summary>Records the arguments, then throws <see cref="Exception"/> or returns <see cref="Result"/>.</summary>
    public Task<PasswordGrantResult> AuthorizationCodeGrantAsync(
        string code, string codeVerifier, string redirectUri, CancellationToken ct)
    {
        ReceivedCode = code;
        ReceivedCodeVerifier = codeVerifier;
        ReceivedRedirectUri = redirectUri;

        return RespondAsync();
    }

    private Task<PasswordGrantResult> RespondAsync() =>
        Exception is not null ? throw Exception : Task.FromResult(Result!);
}
