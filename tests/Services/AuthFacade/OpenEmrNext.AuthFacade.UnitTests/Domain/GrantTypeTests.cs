using OpenEmrNext.AuthFacade.Domain.ValueObjects;

namespace OpenEmrNext.AuthFacade.UnitTests.Domain;

/// <summary>
/// Unit tests for the <see cref="GrantType"/> value object. <c>password</c> and <c>refresh_token</c> are
/// supported, matched case-sensitively with no surrounding whitespace.
/// </summary>
public class GrantTypeTests
{
    [Fact]
    public void Constructor_accepts_password()
    {
        Assert.Equal("password", new GrantType("password").Value);
    }

    [Fact]
    public void Constructor_accepts_refresh_token()
    {
        Assert.Equal("refresh_token", new GrantType("refresh_token").Value);
    }

    [Theory]
    [InlineData("Password")]           // case-sensitive
    [InlineData("password ")]          // no trailing whitespace
    [InlineData("Refresh_Token")]      // case-sensitive
    [InlineData("refresh_token ")]     // no trailing whitespace
    [InlineData("client_credentials")]
    [InlineData("authorization_code")]
    [InlineData("")]
    [InlineData(null)]
    public void Constructor_rejects_unsupported_grant_types(string? value)
    {
        Assert.Throws<ArgumentException>(() => new GrantType(value!));
    }

    [Fact]
    public void Constructor_throws_for_unsupported_grant_type()
    {
        Assert.Throws<ArgumentException>(() => new GrantType("client_credentials"));
    }
}
