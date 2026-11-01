using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.OpenApi;
using Microsoft.OpenApi;
using OpenEmrNext.AuthFacade.Api.OpenIddictClient;

namespace OpenEmrNext.AuthFacade.Api.OpenApi;

/// <summary>Configures the generated OpenAPI document: its description, cookie authentication and operation security.</summary>
public static class AuthFacadeOpenApi
{
    /// <summary>
    /// The name of the session-cookie security scheme in the document. Named after the cookie
    /// <see cref="OpenIddictClientServiceCollectionExtensions.SessionScheme"/> actually sets, not the
    /// scheme name itself: ASP.NET Core's cookie handler prefixes it by default
    /// (<c>.AspNetCore.AuthFacadeSession</c>, confirmed in <c>OpenIddictClientCallbackTests</c>).
    /// </summary>
    public const string CookieScheme = $".AspNetCore.{OpenIddictClientServiceCollectionExtensions.SessionScheme}";

    /// <summary>
    /// Describes the API, declares the session-cookie scheme and marks the operations that require authorization.
    /// </summary>
    /// <param name="options">The document options.</param>
    /// <returns>The same options, for chaining.</returns>
    public static OpenApiOptions ConfigureAuthFacade(this OpenApiOptions options)
    {
        options.AddDocumentTransformer((document, _, _) =>
        {
            document.Info.Title = "AuthFacade API";
            document.Info.Description =
                "A BFF in front of OpenEMR. Log in through GET /oidc/login, and the session cookie it " +
                "establishes authorizes the other endpoints.";

            document.Components ??= new OpenApiComponents();
            document.Components.SecuritySchemes ??= new Dictionary<string, IOpenApiSecurityScheme>();
            document.Components.SecuritySchemes[CookieScheme] = new OpenApiSecurityScheme
            {
                Type = SecuritySchemeType.ApiKey,
                In = ParameterLocation.Cookie,
                Name = CookieScheme,
                Description = "The session cookie /callback sets after a successful browser login through " +
                    "/oidc/login (ADR 0026). Only usable by a caller sharing that cookie jar, such as the " +
                    "Login UI."
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
                    [new OpenApiSecuritySchemeReference(CookieScheme, context.Document)] = []
                });
            }

            return Task.CompletedTask;
        });

        return options;
    }
}
