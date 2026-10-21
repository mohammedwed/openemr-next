using OpenEmrNext.AuthFacade.Application.Abstractions;

namespace OpenEmrNext.AuthFacade.UnitTests.Application;

internal sealed class FakeAccessTokenIssuer : IAccessTokenIssuer
{
    public string? IssuedForSubject { get; private set; }
    public string? IssuedForName { get; private set; }

    public IssuedAccessToken Issue(string subject, string name)
    {
        IssuedForSubject = subject;
        IssuedForName = name;
        return new IssuedAccessToken("fake-token", 1800);
    }
}
