using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Mvc;
using OpenEmrNext.AuthFacade.Domain.ValueObjects;

namespace OpenEmrNext.AuthFacade.Api.Contracts.Requests;

/// <summary>
/// The form fields of a password-grant request to <c>POST /api/v1/connect/token</c>. The endpoint binds it with
/// <c>[AsParameters]</c>, so each field maps to its snake case name, and each property states its own rules as
/// attributes, which the endpoint's validation filter checks. The rules match the value objects in Domain, which still
/// enforce them when the endpoint builds the login arguments.
/// </summary>
/// <param name="GrantType">The grant type. Must be <c>password</c>.</param>
/// <param name="Username">The OpenEMR login name, 1 to 255 characters with no control characters.</param>
/// <param name="Password">The OpenEMR password, 1 to 1024 characters.</param>
public sealed record PasswordLoginRequest(
    [property: JsonPropertyName(TokenRequestFields.GrantType), Required, AllowedValues(GrantType.Password)]
    [param: FromForm(Name = TokenRequestFields.GrantType)]
    string? GrantType,

    [property: JsonPropertyName(TokenRequestFields.Username), Required(AllowEmptyStrings = true), StringLength(255, MinimumLength = 1), RegularExpression(@"\A[^\p{Cc}]{1,255}\z")]
    [param: FromForm(Name = TokenRequestFields.Username)]
    string? Username,

    [property: JsonPropertyName(TokenRequestFields.Password), Required(AllowEmptyStrings = true), StringLength(Password.MaxLength, MinimumLength = 1), DataType(DataType.Password)]
    [param: FromForm(Name = TokenRequestFields.Password)]
    string? Password)
{
    /// <summary>
    /// Shows the password as a fixed mask, the same as <see cref="Password"/>, so logging or string interpolation
    /// of the request never reveals it. The record's generated <c>ToString</c> would print every field.
    /// </summary>
    public override string ToString() =>
        $"{nameof(PasswordLoginRequest)} {{ GrantType = {GrantType}, Username = {Username}, Password = ******** }}";
}
