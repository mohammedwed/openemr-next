using Microsoft.OpenApi;
using OpenEmrNext.AuthFacade.Api.Contracts.Requests;
using OpenEmrNext.AuthFacade.Domain.ValueObjects;

namespace OpenEmrNext.AuthFacade.Api.OpenApi;

/// <summary>
/// Describes the form fields that bind to value objects as the strings they are on the wire.
/// The OpenAPI generator reflects the C# type, so without this a field such as <c>username</c> is shown as an
/// object with a <c>value</c> property, and clients built from the document, including the Scalar UI, send
/// something other than the plain text the endpoint parses.
/// </summary>
public static class FormFieldSchemas
{
    /// <summary>Adds the schema transformer to the OpenAPI document options.</summary>
    /// <param name="options">The document options.</param>
    /// <returns>The same options, for chaining.</returns>
    public static Microsoft.AspNetCore.OpenApi.OpenApiOptions DescribeFormFieldsAsStrings(
        this Microsoft.AspNetCore.OpenApi.OpenApiOptions options) =>
        options.AddSchemaTransformer((schema, context, _) =>
        {
            Apply(schema, context.JsonTypeInfo.Type);
            return Task.CompletedTask;
        });

    /// <summary>Rewrites <paramref name="schema"/> as a string when <paramref name="type"/> is a form-field value object.</summary>
    /// <param name="schema">The generated schema.</param>
    /// <param name="type">The C# type the schema describes.</param>
    internal static void Apply(OpenApiSchema schema, Type type)
    {
        if (type == typeof(PasswordGrant))
        {
            AsString(schema, "The grant type. Only `password` is accepted.");
        }
        else if (type == typeof(Username))
        {
            AsString(schema, "The OpenEMR login name, 1 to 255 characters.");
            schema.MinLength = 1;
            schema.MaxLength = 255;
        }
        else if (type == typeof(Password))
        {
            AsString(schema, "The OpenEMR password, 1 to 1024 characters.");
            schema.Format = "password";
            schema.MinLength = 1;
            schema.MaxLength = Password.MaxLength;
        }
    }

    private static void AsString(OpenApiSchema schema, string description)
    {
        schema.Type = JsonSchemaType.String;
        schema.Properties = null;
        schema.Required = null;
        schema.Description = description;
    }
}
