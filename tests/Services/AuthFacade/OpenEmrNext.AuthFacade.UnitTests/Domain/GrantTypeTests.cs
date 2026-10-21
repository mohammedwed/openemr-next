using OpenEmrNext.AuthFacade.Domain.ValueObjects;

namespace OpenEmrNext.AuthFacade.UnitTests.Domain;

public class GrantTypeTests
{
    [Fact]
    public void TryParse_accepts_password()
    {
        Assert.True(GrantType.TryParse("password", out var grantType));
        Assert.Equal("password", grantType!.Value);
    }

    [Theory]
    [InlineData("Password")]           // case-sensitive
    [InlineData("password ")]          // no trailing whitespace
    [InlineData("client_credentials")]
    [InlineData("authorization_code")]
    [InlineData("")]
    public void TryParse_rejects_unsupported_grant_types(string value)
    {
        Assert.False(GrantType.TryParse(value, out var grantType));
        Assert.Null(grantType);
    }

    [Fact]
    public void TryParse_rejects_null()
    {
        Assert.False(GrantType.TryParse(null, out _));
    }

    [Fact]
    public void Constructor_throws_for_unsupported_grant_type()
    {
        Assert.Throws<ArgumentException>(() => new GrantType("client_credentials"));
    }
}
