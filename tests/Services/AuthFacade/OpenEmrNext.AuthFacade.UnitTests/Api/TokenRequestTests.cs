using OpenEmrNext.AuthFacade.Api.Contracts.Requests;
using OpenEmrNext.AuthFacade.Api.Endpoints;
using OpenEmrNext.AuthFacade.Domain.ValueObjects;

namespace OpenEmrNext.AuthFacade.UnitTests.Api;

/// <summary>
/// Unit tests for <see cref="TokenRequest"/>, whose attributes state the form's shape and whose
/// <see cref="TokenRequest.Validate"/> states which fields each grant type requires. The username, password and
/// refresh token rules must agree with the value objects, which still enforce them when the endpoint builds the
/// login arguments.
/// </summary>
public class TokenRequestTests
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

    /// <summary>Whether the <see cref="RefreshToken"/> value object accepts the value.</summary>
    private static bool RefreshTokenAccepted(string value)
    {
        try
        {
            _ = new RefreshToken(value);
            return true;
        }
        catch (ArgumentException)
        {
            return false;
        }
    }

    private static TokenRequest PasswordRequest(
        string? grantType = "password",
        string? username = "alice_01",
        string? password = "Passw0rd!") => new(grantType, username, password, RefreshToken: null);

    private static TokenRequest RefreshRequest(
        string? grantType = "refresh_token",
        string? refreshToken = "opaque-token-123") => new(grantType, Username: null, Password: null, refreshToken);

    [Fact]
    public void A_complete_password_grant_is_valid()
    {
        Assert.True(RequestValidationExtensions.IsValid(PasswordRequest()));
    }

    [Fact]
    public void A_complete_refresh_grant_is_valid()
    {
        Assert.True(RequestValidationExtensions.IsValid(RefreshRequest()));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("Password")]
    [InlineData("password ")]
    [InlineData("client_credentials")]
    public void Grant_type_must_be_password_or_refresh_token(string? grantType)
    {
        Assert.False(RequestValidationExtensions.IsValid(PasswordRequest(grantType: grantType)));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("alice\u0001")]
    [InlineData("a\u0007b")]
    public void Username_rejects_missing_empty_and_control_characters_for_the_password_grant(string? username)
    {
        Assert.False(RequestValidationExtensions.IsValid(PasswordRequest(username: username)));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public void Password_rejects_missing_and_empty(string? password)
    {
        Assert.False(RequestValidationExtensions.IsValid(PasswordRequest(password: password)));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public void Refresh_token_rejects_missing_and_empty_for_the_refresh_grant(string? refreshToken)
    {
        Assert.False(RequestValidationExtensions.IsValid(RefreshRequest(refreshToken: refreshToken)));
    }

    [Fact]
    public void Refresh_grant_does_not_require_username_or_password()
    {
        Assert.True(RequestValidationExtensions.IsValid(RefreshRequest()));
    }

    [Fact]
    public void Password_grant_does_not_require_a_refresh_token()
    {
        Assert.True(RequestValidationExtensions.IsValid(PasswordRequest()));
    }

    [Theory]
    [InlineData("a")]
    [InlineData("alice 01")]
    public void Short_legacy_usernames_are_valid(string username)
    {
        Assert.True(RequestValidationExtensions.IsValid(PasswordRequest(username: username)));
    }

    [Theory]
    [InlineData(255, true)]
    [InlineData(256, false)]
    public void Username_length_limit_is_255(int length, bool expected)
    {
        Assert.Equal(expected, RequestValidationExtensions.IsValid(PasswordRequest(username: new string('a', length))));
    }

    [Theory]
    [InlineData(1, true)]
    [InlineData(Password.MaxLength, true)]
    [InlineData(Password.MaxLength + 1, false)]
    public void Password_length_limit_is_1024(int length, bool expected)
    {
        Assert.Equal(expected, RequestValidationExtensions.IsValid(PasswordRequest(password: new string('x', length))));
    }

    [Theory]
    [InlineData(1, true)]
    [InlineData(RefreshToken.MaxLength, true)]
    [InlineData(RefreshToken.MaxLength + 1, false)]
    public void Refresh_token_length_limit_is_4096(int length, bool expected)
    {
        Assert.Equal(expected, RequestValidationExtensions.IsValid(RefreshRequest(refreshToken: new string('x', length))));
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
        Assert.Equal(UsernameAccepted(username), RequestValidationExtensions.IsValid(PasswordRequest(username: username)));
    }

    [Theory]
    [InlineData("x")]
    [InlineData("")]
    [InlineData("pass word")]
    public void Password_rule_matches_the_value_object(string password)
    {
        Assert.Equal(PasswordAccepted(password), RequestValidationExtensions.IsValid(PasswordRequest(password: password)));
    }

    [Fact]
    public void Password_rule_matches_the_value_object_at_the_length_limit()
    {
        var atLimit = new string('x', Password.MaxLength + 1);

        Assert.Equal(PasswordAccepted(atLimit), RequestValidationExtensions.IsValid(PasswordRequest(password: atLimit)));
    }

    [Theory]
    [InlineData("opaque-token-123")]
    [InlineData("")]
    public void Refresh_token_rule_matches_the_value_object(string refreshToken)
    {
        Assert.Equal(
            RefreshTokenAccepted(refreshToken),
            RequestValidationExtensions.IsValid(RefreshRequest(refreshToken: refreshToken)));
    }

    [Fact]
    public void Refresh_token_rule_matches_the_value_object_at_the_length_limit()
    {
        var atLimit = new string('x', RefreshToken.MaxLength + 1);

        Assert.Equal(
            RefreshTokenAccepted(atLimit),
            RequestValidationExtensions.IsValid(RefreshRequest(refreshToken: atLimit)));
    }

    [Fact]
    public void ToString_never_shows_the_password_or_refresh_token()
    {
        var text = new TokenRequest("password", "alice_01", "S3cret-Passw0rd!", "r-S3cret-123").ToString();

        Assert.DoesNotContain("S3cret", text);
        Assert.Contains("********", text);
        Assert.Contains("alice_01", text);
    }
}
