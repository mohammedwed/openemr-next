using OpenEmrNext.AuthFacade.Domain.ValueObjects;

namespace OpenEmrNext.AuthFacade.UnitTests.Domain;

/// <summary>
/// Unit tests for the <see cref="GrantType"/> value object. Only <c>password</c> is supported,
/// matched case-sensitively with no surrounding whitespace.
/// </summary>
public class GrantTypeTests
{
    [Fact]
    public void Constructor_accepts_password()
    {
        Assert.Equal("password", new GrantType("password").Value);
    }

    [Theory]
    [InlineData("Password")]           // case-sensitive
    [InlineData("password ")]          // no trailing whitespace
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
