using OpenEmrNext.AuthFacade.Domain.ValueObjects;

namespace OpenEmrNext.AuthFacade.UnitTests.Domain;

/// <summary>
/// Unit tests for the <see cref="Password"/> value object. They cover the presence and length
/// rules, the one-time <c>TakeValue</c> contract, and that <c>ToString</c> never reveals the plaintext.
/// </summary>
public class PasswordTests
{
    [Theory]
    [InlineData("x")]                 // legacy passwords may be short
    [InlineData("password")]          // no complexity rules at login
    [InlineData("ALLUPPER")]
    [InlineData("1234")]
    [InlineData("with spaces  ")]
    [InlineData("Passw0rd!")]
    public void Accepts_any_password_OpenEmr_could_have_been_set_to(string value)
    {
        Assert.Equal(value, new Password(value).TakeValue());
    }

    [Fact]
    public void Accepts_the_maximum_length()
    {
        var value = new string('a', Password.MaxLength);
        Assert.Equal(value, new Password(value).TakeValue());
    }

    [Fact]
    public void Rejects_empty_input()
    {
        Assert.Throws<ArgumentException>(() => new Password(""));
    }

    [Fact]
    public void Rejects_more_than_the_maximum_length()
    {
        var value = new string('a', Password.MaxLength + 1);
        Assert.Throws<ArgumentException>(() => new Password(value));
    }

    [Fact]
    public void ToString_never_reveals_the_password()
    {
        var password = new Password("Passw0rd!");
        Assert.DoesNotContain("Passw0rd", password.ToString());
        Assert.Equal("********", password.ToString());
    }

    [Fact]
    public void TakeValue_returns_the_password_once()
    {
        var password = new Password("Passw0rd!");

        Assert.Equal("Passw0rd!", password.TakeValue());
    }

    [Fact]
    public void TakeValue_throws_on_second_call()
    {
        var password = new Password("Passw0rd!");
        password.TakeValue();

        var ex = Assert.Throws<InvalidOperationException>(() => password.TakeValue());
        Assert.Contains("already been retrieved", ex.Message);
    }

    [Fact]
    public void TryParse_returns_false_for_null_or_empty_input()
    {
        Assert.False(Password.TryParse(null, out _));
        Assert.False(Password.TryParse("", out _));
    }

    [Fact]
    public void TryParse_accepts_short_passwords()
    {
        Assert.True(Password.TryParse("x", out var password));
        Assert.Equal("x", password!.TakeValue());
    }
}
