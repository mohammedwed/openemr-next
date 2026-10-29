using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Mvc;
using OpenEmrNext.AuthFacade.Domain.ValueObjects;
using DomainGrantType = OpenEmrNext.AuthFacade.Domain.ValueObjects.GrantType;

namespace OpenEmrNext.AuthFacade.Api.Contracts.Requests;

/// <summary>
/// The form fields of a password or refresh grant request to <c>POST /api/v1/connect/token</c>. The endpoint
/// binds it with <c>[AsParameters]</c>, so each field maps to its snake case name. <see cref="GrantType"/> is
/// always required; which of <see cref="Username"/>/<see cref="Password"/> or <see cref="RefreshToken"/> is
/// required depends on it, enforced by <see cref="Validate"/> since that cannot be expressed as a fixed
/// attribute. Every field's shape (length, pattern) is still a fixed attribute, which the endpoint's validation
/// filter checks. The rules match the value objects in Domain, which still enforce them when the endpoint
/// builds the login arguments.
/// </summary>
/// <param name="GrantType">The grant type. Must be <c>password</c> or <c>refresh_token</c>.</param>
/// <param name="Username">The OpenEMR login name, 1 to 255 characters with no control characters. Required for the password grant.</param>
/// <param name="Password">The OpenEMR password, 1 to 1024 characters. Required for the password grant.</param>
/// <param name="RefreshToken">The refresh token to redeem, 1 to 4096 characters. Required for the refresh grant.</param>
public sealed record TokenRequest(
    [property: JsonPropertyName(TokenRequestFields.GrantType), Required, AllowedValues(DomainGrantType.Password, DomainGrantType.RefreshToken)]
    [param: FromForm(Name = TokenRequestFields.GrantType)]
    string? GrantType,

    [property: JsonPropertyName(TokenRequestFields.Username), StringLength(255, MinimumLength = 1), RegularExpression(@"\A[^\p{Cc}]{1,255}\z")]
    [param: FromForm(Name = TokenRequestFields.Username)]
    string? Username,

    [property: JsonPropertyName(TokenRequestFields.Password), StringLength(Password.MaxLength, MinimumLength = 1), DataType(DataType.Password)]
    [param: FromForm(Name = TokenRequestFields.Password)]
    string? Password,

    [property: JsonPropertyName(TokenRequestFields.RefreshToken), StringLength(RefreshToken.MaxLength, MinimumLength = 1), DataType(DataType.Password)]
    [param: FromForm(Name = TokenRequestFields.RefreshToken)]
    string? RefreshToken) : IValidatableObject
{
    /// <summary>
    /// Requires <see cref="Username"/> and <see cref="Password"/> for the password grant, or
    /// <see cref="RefreshToken"/> for the refresh grant. An unsupported <see cref="GrantType"/> fails its own
    /// <see cref="AllowedValuesAttribute"/> and is not re-reported here.
    /// </summary>
    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (GrantType == DomainGrantType.Password)
        {
            if (string.IsNullOrEmpty(Username))
            {
                yield return new ValidationResult("The username field is required.", [nameof(Username)]);
            }

            if (string.IsNullOrEmpty(Password))
            {
                yield return new ValidationResult("The password field is required.", [nameof(Password)]);
            }
        }
        else if (GrantType == DomainGrantType.RefreshToken)
        {
            if (string.IsNullOrEmpty(RefreshToken))
            {
                yield return new ValidationResult("The refresh_token field is required.", [nameof(RefreshToken)]);
            }
        }
    }

    /// <summary>
    /// Shows the password and refresh token as a fixed mask, so logging or string interpolation of the
    /// request never reveals them. The record's generated <c>ToString</c> would print every field.
    /// </summary>
    public override string ToString() =>
        $"{nameof(TokenRequest)} {{ GrantType = {GrantType}, Username = {Username}, " +
        "Password = ********, RefreshToken = ******** }";
}
