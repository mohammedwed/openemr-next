using OpenEmrNext.PatientManagement.Api.Contracts.Requests;
using OpenEmrNext.PatientManagement.Api.Contracts.Responses;
using OpenEmrNext.PatientManagement.Application;
using OpenEmrNext.PatientManagement.Domain;
using OpenEmrNext.PatientManagement.Domain.ValueObjects;

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
        RegisterPatientRequest request, RegisterPatientUseCase useCase,
        ScorePatientForDuplicatesUseCase scoreUseCase, CancellationToken ct)
    {
        Patient patient;
        try
        {
            patient = await useCase.ExecuteAsync(
                PatientRegistration.Create(
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

        // Matches legacy OpenEMR's own behavior: scored synchronously right after insert, cached for
        // a later report, never surfaced in the registration response itself (legacy doesn't either --
        // there is no report endpoint yet to make this actionable to a caller).
        await scoreUseCase.ExecuteAsync(patient.Pid, ct);

        return Results.Created(
            $"/api/v1/patients/{patient.Uuid}",
            new PatientRegisteredResponse(patient.Uuid, patient.Pid, patient.Pubpid));
    }
}
