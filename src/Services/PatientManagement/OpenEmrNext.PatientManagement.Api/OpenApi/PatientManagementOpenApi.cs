using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.OpenApi;
using Microsoft.OpenApi;
using OpenEmrNext.PatientManagement.Api.Authentication;

namespace OpenEmrNext.PatientManagement.Api.OpenApi;

/// <summary>Configures the generated OpenAPI document: its description, the gateway-secret scheme, and operation security.</summary>
public static class PatientManagementOpenApi
{
    /// <summary>The name of the shared-secret security scheme in the document.</summary>
    public const string GatewaySecretScheme = InternalGatewayAuthenticationHandler.SchemeName;

    /// <summary>
    /// Describes the API, declares the <c>X-Internal-Gateway-Secret</c> scheme, and marks every
    /// operation that requires it -- the same pattern AuthFacade's own <c>AuthFacadeOpenApi</c> uses
    /// for its session-cookie scheme.
    /// </summary>
    /// <param name="options">The document options.</param>
    /// <returns>The same options, for chaining.</returns>
    public static OpenApiOptions ConfigurePatientManagement(this OpenApiOptions options)
    {
        options.AddDocumentTransformer((document, _, _) =>
        {
            document.Info.Title = "PatientManagement API";
            document.Info.Description =
                "Patient registration, insurance, advance directives, and the FHIR Patient API. Every " +
                "endpoint requires the shared X-Internal-Gateway-Secret header (ADR 0055) -- there is no " +
                "per-user authentication yet; see that ADR for why.";

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
