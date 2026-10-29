using OpenEmrNext.AuthFacade.Domain.ValueObjects;

namespace OpenEmrNext.AuthFacade.UnitTests.Domain;

/// <summary>
/// Unit tests for the <see cref="RefreshToken"/> value object. They cover the presence and length
/// rules, the one-time <c>TakeValue</c> contract, and that <c>ToString</c> never reveals the plaintext.
/// </summary>
public class RefreshTokenTests
{
    [Theory]
    [InlineData("x")]
    [InlineData("opaque-token-123")]
    [InlineData("eyJhbGciOiJSUzI1NiJ9.e30.sig")]
    public void Accepts_any_refresh_token_OpenEmr_could_have_issued(string value)
    {
        Assert.Equal(value, new RefreshToken(value).TakeValue());
    }

    [Fact]
    public void Accepts_the_maximum_length()
    {
        var value = new string('a', RefreshToken.MaxLength);
        Assert.Equal(value, new RefreshToken(value).TakeValue());
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public void Constructor_rejects_null_and_empty_input(string? value)
    {
        Assert.Throws<ArgumentException>(() => new RefreshToken(value!));
    }

    [Fact]
    public void Rejects_more_than_the_maximum_length()
    {
        var value = new string('a', RefreshToken.MaxLength + 1);
        Assert.Throws<ArgumentException>(() => new RefreshToken(value));
    }

    [Fact]
    public void ToString_never_reveals_the_refresh_token()
    {
        var token = new RefreshToken("opaque-token-123");
        Assert.DoesNotContain("opaque-token", token.ToString());
        Assert.Equal("********", token.ToString());
    }

    [Fact]
    public void TakeValue_returns_the_refresh_token_once()
    {
        var token = new RefreshToken("opaque-token-123");

        Assert.Equal("opaque-token-123", token.TakeValue());
    }

    [Fact]
    public void TakeValue_throws_on_second_call()
    {
        var token = new RefreshToken("opaque-token-123");
        token.TakeValue();

        var ex = Assert.Throws<InvalidOperationException>(() => token.TakeValue());
        Assert.Contains("already been retrieved", ex.Message);
    }
}
