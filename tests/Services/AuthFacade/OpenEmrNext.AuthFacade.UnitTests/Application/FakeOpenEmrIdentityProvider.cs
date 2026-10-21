using OpenEmrNext.AuthFacade.Application.Abstractions;
using OpenEmrNext.AuthFacade.Domain.ValueObjects;

namespace OpenEmrNext.AuthFacade.UnitTests.Application;

internal sealed class FakeOpenEmrIdentityProvider : IOpenEmrIdentityProvider
{
    public PasswordGrantResult? Result { get; set; }
    public Exception? Exception { get; set; }

    public Username? ReceivedUsername { get; private set; }
    public Password? ReceivedPassword { get; private set; }

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
