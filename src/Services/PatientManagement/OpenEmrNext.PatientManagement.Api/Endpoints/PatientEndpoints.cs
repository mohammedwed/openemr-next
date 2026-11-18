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

        app.MapPut("/patients/{uuid:guid}", UpdatePatient)
            .WithName("UpdatePatient")
            .WithTags("Patients")
            .WithSummary("Correct a patient's name, date of birth, sex and emails, conditional on the ETag the caller read")
            .Produces<PatientSummaryResponse>()
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status412PreconditionFailed)
            .ProducesProblem(StatusCodes.Status428PreconditionRequired);

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
                http.ActingUserId(request.RegisteredBy), DateTime.UtcNow, ct, AuditOrigins.From(http));
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
        string? name, int? limit, int? offset, HttpContext http, SearchPatientFhirRecordsUseCase useCase, CancellationToken ct)
    {
        // Bounded like every search: at most 100 patients a request (100 when no limit is named), and the total that matched
        // travels in X-Total-Count so a caller can tell a complete list from a page of it.
        if (limit is < 1 or > PatientFhirSearchCriteria.MaxLimit)
            return Results.ValidationProblem(new Dictionary<string, string[]> { ["limit"] = [$"Limit must be between 1 and {PatientFhirSearchCriteria.MaxLimit}."] });
        if (offset is < 0)
            return Results.ValidationProblem(new Dictionary<string, string[]> { ["offset"] = ["Offset cannot be negative."] });

        var result = await useCase.ExecuteAsync(
            new PatientFhirSearchCriteria(Name: name, Limit: limit ?? PatientFhirSearchCriteria.MaxLimit, Offset: offset ?? 0), ct);

        http.Response.Headers["X-Total-Count"] = result.Total.ToString(System.Globalization.CultureInfo.InvariantCulture);
        return Results.Ok(result.Items.Select(ToSummary).ToList());
    }

    /// <summary>
    /// A plain single-patient lookup by uuid -- reuses the same read this service's own FHIR API
    /// already does (<see cref="GetPatientFhirRecordUseCase"/>) for <c>GET /fhir/Patient/{uuid}</c>,
    /// just projected to the same smaller shape <see cref="GetPatients"/> uses. No new Domain or
    /// persistence logic.
    /// </summary>
    private static async Task<IResult> GetPatient(Guid uuid, GetPatientFhirRecordUseCase useCase, GetPatientDemographicsUseCase demographics, CancellationToken ct)
    {
        var view = await useCase.ExecuteAsync(uuid, ct);
        if (view is null)
            return Results.NotFound();

        // The version of the part of the patient a correction can change (ADR 0081): the same token a PUT must be based on.
        // The same read supplies the two editable fields the summary does not otherwise carry (sex and trusted email), so a form built on this response can send back every field it owns: a correction replaces all six,
        // and one built without them would clear them. The list endpoint leaves them out: it is not what an edit starts from.
        var current = await demographics.ExecuteAsync(uuid, ct);
        if (current is null)
            return Results.Ok(ToSummary(view));

        var version = current.Version().ToETag();
        return Preconditions.Ok(ToSummary(view) with { ETag = version, Sex = current.Sex, Email = current.Email, TrustedEmail = current.TrustedEmail }, version);
    }

    /// <summary>
    /// Corrects a patient's demographics (M5r, ADR 0081). The order of answers is the one every write here uses: not signed in (401, middleware), no <c>If-Match</c> (428), no such patient (404), changed since read (412),
    /// then the fields themselves (400). Matching the patient is by their uuid, the public identity (API-7). The duplicate score is worked out again afterwards, as it is after registration.
    /// </summary>
    private static async Task<IResult> UpdatePatient(
        Guid uuid, UpdatePatientRequest request, UpdatePatientDemographicsUseCase useCase, ScorePatientForDuplicatesUseCase scoreUseCase, HttpContext http,
        [Microsoft.AspNetCore.Mvc.FromHeader(Name = "If-Match")] string? ifMatch, CancellationToken ct)
    {
        if (!Preconditions.TryRead(ifMatch, out var offered))
            return Preconditions.Required();

        DemographicsCorrectionResult result;
        try
        {
            result = await useCase.ExecuteAsync(
                uuid, offered, request.FirstName, request.LastName, request.Sex, request.DateOfBirth, request.Email, request.TrustedEmail, ct, AuditOrigins.From(http));
        }
        catch (PatientNotFoundException)
        {
            return Results.NotFound();
        }
        catch (PatientVersionConflictException ex)
        {
            return Preconditions.Failed(ex.CurrentVersion);
        }
        catch (PatientValidationException ex)
        {
            return ValidationProblems.Many(ex.Errors);
        }

        if (result.Changed.Count > 0)
            await scoreUseCase.ExecuteAsync(result.Patient.Pid, ct);

        var p = result.Patient;
        var version = p.Version().ToETag();
        return Preconditions.Ok(new PatientSummaryResponse(p.Uuid, p.Pid, p.FirstName, p.LastName, p.DateOfBirth, p.Email, p.PublicPatientId, version, p.Sex, p.TrustedEmail), version);
    }

    private static PatientSummaryResponse ToSummary(PatientFhirView v) =>
        new(v.Uuid, v.Pid, v.FirstName, v.LastName, v.DateOfBirth, v.Email, v.PublicPatientId);
}
