using OpenEmrNext.AuthFacade.Api.Contracts.Requests;

namespace OpenEmrNext.AuthFacade.UnitTests.Api;

/// <summary>Unit tests for <see cref="PasswordGrant"/>, which lets the token endpoint accept only the password grant.</summary>
public class PasswordGrantTests
{
    [Fact]
    public void TryParse_accepts_password()
    {
        Assert.True(PasswordGrant.TryParse("password", out var grant));
        Assert.NotNull(grant);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("Password")]
    [InlineData("password ")]
    [InlineData("client_credentials")]
    [InlineData("refresh_token")]
    public void TryParse_rejects_everything_else(string? value)
    {
        Assert.False(PasswordGrant.TryParse(value, out var grant));
        Assert.Null(grant);
    }
}
