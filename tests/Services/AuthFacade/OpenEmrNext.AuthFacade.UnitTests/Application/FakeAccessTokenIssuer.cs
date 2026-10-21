using OpenEmrNext.AuthFacade.Application.Abstractions;

namespace OpenEmrNext.AuthFacade.UnitTests.Application;

/// <summary>
/// Test double for <see cref="IAccessTokenIssuer"/>. It records the subject and name it was
/// asked to issue for, and always returns the token <c>fake-token</c> with a 1800-second lifetime.
/// </summary>
internal sealed class FakeAccessTokenIssuer : IAccessTokenIssuer
{
    /// <summary>The subject from the last <see cref="Issue"/> call, or null if none was made.</summary>
    public string? IssuedForSubject { get; private set; }

    /// <summary>The name from the last <see cref="Issue"/> call, or null if none was made.</summary>
    public string? IssuedForName { get; private set; }

    /// <summary>Records the subject and name, then returns the fixed fake token.</summary>
    public IssuedAccessToken Issue(string subject, string name)
    {
        IssuedForSubject = subject;
        IssuedForName = name;
        return new IssuedAccessToken("fake-token", 1800);
    }
}
