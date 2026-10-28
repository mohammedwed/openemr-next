using OpenEmrNext.AuthFacade.Api.Contracts.Requests;
using OpenEmrNext.AuthFacade.Api.Endpoints;
using OpenEmrNext.AuthFacade.Domain.ValueObjects;

namespace OpenEmrNext.AuthFacade.UnitTests.Api;

/// <summary>
/// Unit tests for <see cref="PasswordLoginRequest"/>, whose attributes state the form rules. The username and password
/// rules must agree with the value objects, which still enforce them when the endpoint builds the login arguments.
/// </summary>
public class PasswordLoginRequestTests
{
    /// <summary>Whether the <see cref="Username"/> value object accepts the value.</summary>
    private static bool UsernameAccepted(string value)
    {
        try
        {
            _ = new Username(value);
            return true;
        }
        catch (ArgumentException)
        {
            return false;
        }
    }

    /// <summary>Whether the <see cref="Password"/> value object accepts the value.</summary>
    private static bool PasswordAccepted(string value)
    {
        try
        {
            _ = new Password(value);
            return true;
        }
        catch (ArgumentException)
        {
            return false;
        }
    }

    private static PasswordLoginRequest Request(
        string? grantType = "password",
        string? username = "alice_01",
        string? password = "Passw0rd!") => new(grantType, username, password);

    [Fact]
    public void A_complete_password_grant_is_valid()
    {
        Assert.True(RequestValidationExtensions.IsValid(Request()));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("Password")]
    [InlineData("password ")]
    [InlineData("client_credentials")]
    [InlineData("refresh_token")]
    public void Grant_type_must_be_password(string? grantType)
    {
        Assert.False(RequestValidationExtensions.IsValid(Request(grantType: grantType)));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("alice\u0001")]
    [InlineData("a\u0007b")]
    public void Username_rejects_missing_empty_and_control_characters(string? username)
    {
        Assert.False(RequestValidationExtensions.IsValid(Request(username: username)));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public void Password_rejects_missing_and_empty(string? password)
    {
        Assert.False(RequestValidationExtensions.IsValid(Request(password: password)));
    }

    [Theory]
    [InlineData("a")]
    [InlineData("alice 01")]
    public void Short_legacy_usernames_are_valid(string username)
    {
        Assert.True(RequestValidationExtensions.IsValid(Request(username: username)));
    }

    [Theory]
    [InlineData(255, true)]
    [InlineData(256, false)]
    public void Username_length_limit_is_255(int length, bool expected)
    {
        Assert.Equal(expected, RequestValidationExtensions.IsValid(Request(username: new string('a', length))));
    }

    [Theory]
    [InlineData(1, true)]
    [InlineData(Password.MaxLength, true)]
    [InlineData(Password.MaxLength + 1, false)]
    public void Password_length_limit_is_1024(int length, bool expected)
    {
        Assert.Equal(expected, RequestValidationExtensions.IsValid(Request(password: new string('x', length))));
    }

    [Theory]
    [InlineData("alice_01")]
    [InlineData("a")]
    [InlineData("alice\u0001")]
    [InlineData("a\u0009b")]
    [InlineData("")]
    [InlineData("  ")]
    public void Username_rule_matches_the_value_object(string username)
    {
        Assert.Equal(UsernameAccepted(username), RequestValidationExtensions.IsValid(Request(username: username)));
    }

    [Theory]
    [InlineData("x")]
    [InlineData("")]
    [InlineData("pass word")]
    public void Password_rule_matches_the_value_object(string password)
    {
        Assert.Equal(PasswordAccepted(password), RequestValidationExtensions.IsValid(Request(password: password)));
    }

    [Fact]
    public void Password_rule_matches_the_value_object_at_the_length_limit()
    {
        var atLimit = new string('x', Password.MaxLength + 1);

        Assert.Equal(PasswordAccepted(atLimit), RequestValidationExtensions.IsValid(Request(password: atLimit)));
    }

    [Fact]
    public void ToString_never_shows_the_password()
    {
        var text = Request(password: "S3cret-Passw0rd!").ToString();

        Assert.DoesNotContain("S3cret", text);
        Assert.Contains("********", text);
        Assert.Contains("alice_01", text);
    }
}
