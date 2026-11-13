using OpenEmrNext.PatientManagement.Api.Contracts.Requests;
using OpenEmrNext.PatientManagement.Api.Contracts.Responses;
using OpenEmrNext.PatientManagement.Application;
using OpenEmrNext.PatientManagement.Domain;

namespace OpenEmrNext.PatientManagement.Api.Endpoints;

/// <summary>Endpoints for registering patients.</summary>
public static class PatientEndpoints
{
    public static IEndpointRouteBuilder MapPatientEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapPost("/patients", RegisterPatient)
            .WithName("RegisterPatient")
            .WithTags("Patients")
            .WithSummary("Register a new patient")
            .Produces<PatientRegisteredResponse>(StatusCodes.Status201Created)
            .ProducesValidationProblem();

        return app;
    }

    private static async Task<IResult> RegisterPatient(
        RegisterPatientRequest request, RegisterPatientUseCase useCase, CancellationToken ct)
    {
        Patient patient;
        try
        {
            patient = await useCase.ExecuteAsync(
                new PatientRegistration(
                    request.FirstName, request.LastName, request.Sex, request.DateOfBirth,
                    request.Email, request.TrustedEmail),
                request.RegisteredBy,
                DateTime.UtcNow,
                ct);
        }
        catch (PatientValidationException ex)
        {
            return Results.ValidationProblem(new Dictionary<string, string[]> { ["errors"] = [.. ex.Errors] });
        }

        return Results.Created(
            $"/api/v1/patients/{patient.Uuid}",
            new PatientRegisteredResponse(patient.Uuid, patient.Pid, patient.Pubpid));
    }
}
