using OpenEmrNext.PatientManagement.Api.Endpoints;

namespace OpenEmrNext.PatientManagement.Api;

/// <summary>Maps every PatientManagement endpoint group in one place.</summary>
public static class PatientManagementEndpoints
{
    /// <param name="app">The endpoint route builder.</param>
    /// <returns>The same builder, for chaining.</returns>
    public static IEndpointRouteBuilder MapPatientManagementEndpoints(this IEndpointRouteBuilder app)
    {
        var v1 = app.MapGroup("/api/v1");
        v1.MapPatientEndpoints();
        v1.MapInsuranceEndpoints();
        return app;
    }
}
