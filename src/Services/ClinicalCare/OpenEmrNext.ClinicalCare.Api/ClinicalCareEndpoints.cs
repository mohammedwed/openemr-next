using OpenEmrNext.ClinicalCare.Api.Endpoints;

namespace OpenEmrNext.ClinicalCare.Api;

/// <summary>Maps every Clinical Care endpoint group in one place.</summary>
public static class ClinicalCareEndpoints
{
    /// <param name="app">The endpoint route builder.</param>
    /// <returns>The same builder, for chaining.</returns>
    public static IEndpointRouteBuilder MapClinicalCareEndpoints(this IEndpointRouteBuilder app)
    {
        // Deny by default (SEC-1): authorization is required on the whole group, so a route added later
        // cannot forget it. Endpoint mappers for each slice are added to `v1` as they are built.
        var v1 = app.MapGroup("/api/v1").RequireAuthorization();
        v1.MapEncounterEndpoints();
        v1.MapVitalsEndpoints();
        v1.MapProblemEndpoints();

        // Event delivery: a separate path, a separate scheme, never routed by the gateway (ADR 0066).
        app.MapInternalEventEndpoints();
        return app;
    }
}
