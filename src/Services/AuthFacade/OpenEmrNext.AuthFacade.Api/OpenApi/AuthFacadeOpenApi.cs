using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.OpenApi;
using Microsoft.OpenApi;
using OpenEmrNext.AuthFacade.Api.Contracts.Requests;

namespace OpenEmrNext.AuthFacade.Api.OpenApi;

/// <summary>Configures the generated OpenAPI document: its description, bearer authentication and form content types.</summary>
public static class AuthFacadeOpenApi
{
    /// <summary>The name of the bearer security scheme in the document.</summary>
    public const string BearerScheme = "Bearer";

    private const string FormUrlEncoded = "application/x-www-form-urlencoded";

    /// <summary>
    /// Describes the API, declares the bearer scheme and marks the operations that require authorization,
    /// offers the token endpoint's form only as <c>application/x-www-form-urlencoded</c>, and describes
    /// the value-object form fields as strings.
    /// </summary>
    /// <param name="options">The document options.</param>
    /// <returns>The same options, for chaining.</returns>
    public static OpenApiOptions ConfigureAuthFacade(this OpenApiOptions options)
    {
        options.AddDocumentTransformer((document, _, _) =>
        {
            document.Info.Title = "AuthFacade API";
            document.Info.Description =
                "A token broker in front of OpenEMR. Log in with an OpenEMR username and password to get an " +
                "OpenEMR access token, then send it as a bearer token to the other endpoints.";

            document.Components ??= new OpenApiComponents();
            document.Components.SecuritySchemes ??= new Dictionary<string, IOpenApiSecurityScheme>();
            document.Components.SecuritySchemes[BearerScheme] = new OpenApiSecurityScheme
            {
                Type = SecuritySchemeType.Http,
                Scheme = "bearer",
                BearerFormat = "JWT",
                Description = "The access_token returned by POST /api/v1/connect/token."
            };
            return Task.CompletedTask;
        });

        options.AddOperationTransformer((operation, context, _) =>
        {
            var requiresAuthorization = context.Description.ActionDescriptor.EndpointMetadata
                .OfType<IAuthorizeData>().Any();
            if (requiresAuthorization)
            {
                operation.Security ??= [];
                operation.Security.Add(new OpenApiSecurityRequirement
                {
                    [new OpenApiSecuritySchemeReference(BearerScheme, context.Document)] = []
                });
            }

            // OAuth token endpoints take application/x-www-form-urlencoded. The framework also accepts
            // multipart, and clients such as Scalar send the first content type listed.
            if (operation.RequestBody?.Content is { } content && content.ContainsKey(FormUrlEncoded))
            {
                foreach (var contentType in content.Keys.Where(key => key != FormUrlEncoded).ToList())
                {
                    content.Remove(contentType);
                }

                if (content.TryGetValue(FormUrlEncoded, out var form))
                {
                    FlattenForm(form);
                }
            }

            return Task.CompletedTask;
        });

        return options;
    }

    /// <summary>
    /// Merges the form described by <c>[AsParameters]</c>, which the generator emits as one schema per field inside
    /// <c>allOf</c>, into a single object. It lists the required fields and marks the password as a password, so
    /// clients built from the document send the form the endpoint expects.
    /// </summary>
    /// <param name="form">The form media type. Its schema is replaced when it has the per-field <c>allOf</c> shape.</param>
    private static void FlattenForm(OpenApiMediaType form)
    {
        if (form.Schema is not OpenApiSchema { AllOf.Count: > 0 } schema)
        {
            return;
        }

        var flat = new OpenApiSchema
        {
            Type = JsonSchemaType.Object,
            Properties = new Dictionary<string, IOpenApiSchema>(),
            Required = new HashSet<string>
            {
                TokenRequestFields.GrantType,
                TokenRequestFields.Username,
                TokenRequestFields.Password
            }
        };

        foreach (var part in schema.AllOf.OfType<OpenApiSchema>())
        {
            foreach (var (name, property) in part.Properties ?? new Dictionary<string, IOpenApiSchema>())
            {
                flat.Properties[name] = property;
            }
        }

        if (flat.Properties.TryGetValue(TokenRequestFields.Password, out var password) && password is OpenApiSchema passwordSchema)
        {
            passwordSchema.Format = "password";
        }

        form.Schema = flat;
    }
}
