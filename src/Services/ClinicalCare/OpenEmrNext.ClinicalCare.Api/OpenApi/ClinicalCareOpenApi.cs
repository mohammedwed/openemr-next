using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.OpenApi;
using Microsoft.OpenApi;
using OpenEmrNext.ClinicalCare.Api.Authentication;

namespace OpenEmrNext.ClinicalCare.Api.OpenApi;

/// <summary>Configures the generated OpenAPI document: its description, the gateway-secret scheme, and operation security (ADR 0058 pattern).</summary>
public static class ClinicalCareOpenApi
{
    /// <summary>The name of the shared-secret security scheme in the document.</summary>
    public const string GatewaySecretScheme = InternalGatewayAuthenticationHandler.SchemeName;

    /// <param name="options">The document options.</param>
    /// <returns>The same options, for chaining.</returns>
    public static OpenApiOptions ConfigureClinicalCare(this OpenApiOptions options)
    {
        options.AddDocumentTransformer((document, _, _) =>
        {
            document.Info.Title = "ClinicalCare API";
            document.Info.Description =
                "Clinical encounters (and, later, vitals). Every endpoint requires the shared " +
                "X-Internal-Gateway-Secret header (ADR 0055, ADR 0064) -- there is no per-user authentication yet, " +
                "so the acting provider is supplied by the caller.";

            document.Components ??= new OpenApiComponents();
            document.Components.SecuritySchemes ??= new Dictionary<string, IOpenApiSecurityScheme>();
            document.Components.SecuritySchemes[GatewaySecretScheme] = new OpenApiSecurityScheme
            {
                Type = SecuritySchemeType.ApiKey,
                In = ParameterLocation.Header,
                Name = "X-Internal-Gateway-Secret",
                Description = "The shared secret configured by InternalGatewayOptions.Secret. Gates the " +
                    "service shut to anyone else; it does not identify which end user is behind the call."
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
                    [new OpenApiSecuritySchemeReference(GatewaySecretScheme, context.Document)] = []
                });
            }

            return Task.CompletedTask;
        });

        options.AddSchemaTransformer<ValidationAttributesSchemaTransformer>();

        return options;
    }
}
