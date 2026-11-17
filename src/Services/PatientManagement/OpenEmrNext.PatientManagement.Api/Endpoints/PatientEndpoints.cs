using OpenEmrNext.PatientManagement.Api.Authentication;
using OpenEmrNext.PatientManagement.Api.Contracts.Requests;
using OpenEmrNext.PatientManagement.Api.Contracts.Responses;
using OpenEmrNext.PatientManagement.Application;
using OpenEmrNext.PatientManagement.Domain;
using OpenEmrNext.PatientManagement.Domain.Fhir.ReadModels;
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

        app.MapGet("/patients", GetPatients)
            .WithName("GetPatients")
            .WithTags("Patients")
            .WithSummary("List patients, optionally filtered by name")
            .Produces<IReadOnlyList<PatientSummaryResponse>>();

        app.MapGet("/patients/{uuid:guid}", GetPatient)
            .WithName("GetPatient")
            .WithTags("Patients")
            .WithSummary("Get a single patient by uuid")
            .Produces<PatientSummaryResponse>()
            .Produces(StatusCodes.Status404NotFound);

        return app;
    }

    private static async Task<IResult> RegisterPatient(
        RegisterPatientRequest request, RegisterPatientUseCase useCase,
        ScorePatientForDuplicatesUseCase scoreUseCase, HttpContext http, CancellationToken ct)
    {
        Patient patient;
        try
        {
            patient = await useCase.ExecuteAsync(
                request.FirstName, request.LastName, request.Sex, request.DateOfBirth,
                request.Email, request.TrustedEmail,
                http.ActingUserId(request.RegisteredBy), DateTime.UtcNow, ct);
        }
        catch (PatientValidationException ex)
        {
            return ValidationProblems.Many(ex.Errors);
        }

        // Matches legacy OpenEMR's own behavior: scored synchronously right after insert, cached for
        // a later report, never surfaced in the registration response itself (legacy doesn't either --
        // there is no report endpoint yet to make this actionable to a caller).
        await scoreUseCase.ExecuteAsync(patient.Pid, ct);

        return Results.Created(
            $"/api/v1/patients/{patient.Uuid}",
            new PatientRegisteredResponse(patient.Uuid, patient.Pid, patient.Pubpid));
    }

    /// <summary>
    /// A plain list, for a caller that doesn't want a FHIR Bundle -- reuses the same search this
    /// service's own FHIR API already does (<see cref="SearchPatientFhirRecordsUseCase"/>), just
    /// projected to a smaller shape. No new Domain or persistence logic.
    /// </summary>
    private static async Task<IResult> GetPatients(
        string? name, SearchPatientFhirRecordsUseCase useCase, CancellationToken ct)
    {
        var views = await useCase.ExecuteAsync(new PatientFhirSearchCriteria(Name: name), ct);

        return Results.Ok(views.Select(ToSummary).ToList());
    }

    /// <summary>
    /// A plain single-patient lookup by uuid -- reuses the same read this service's own FHIR API
    /// already does (<see cref="GetPatientFhirRecordUseCase"/>) for <c>GET /fhir/Patient/{uuid}</c>,
    /// just projected to the same smaller shape <see cref="GetPatients"/> uses. No new Domain or
    /// persistence logic.
    /// </summary>
    private static async Task<IResult> GetPatient(Guid uuid, GetPatientFhirRecordUseCase useCase, CancellationToken ct)
    {
        var view = await useCase.ExecuteAsync(uuid, ct);
        return view is null ? Results.NotFound() : Results.Ok(ToSummary(view));
    }

    private static PatientSummaryResponse ToSummary(PatientFhirView v) =>
        new(v.Uuid, v.Pid, v.FirstName, v.LastName, v.DateOfBirth, v.Email, v.PublicPatientId);
}
