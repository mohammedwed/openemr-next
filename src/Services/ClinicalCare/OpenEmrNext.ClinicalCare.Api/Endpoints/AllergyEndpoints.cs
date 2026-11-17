using OpenEmrNext.ClinicalCare.Api.Contracts.Requests;
using OpenEmrNext.ClinicalCare.Api.Contracts.Responses;
using OpenEmrNext.ClinicalCare.Application;
using OpenEmrNext.ClinicalCare.Domain.ReadModels;
using OpenEmrNext.ClinicalCare.Domain.ValueObjects;

namespace OpenEmrNext.ClinicalCare.Api.Endpoints;

/// <summary>Endpoints for a patient's allergy list (M5c, ADR 0070).</summary>
public static class AllergyEndpoints
{
    public static IEndpointRouteBuilder MapAllergyEndpoints(this IEndpointRouteBuilder app)
    {
        // A flat /allergies route, for the same reason as /problems and /encounters: one gateway prefix belongs to one service (ADR 0064).
        app.MapPost("/allergies", RecordAllergy)
            .WithName("RecordAllergy")
            .WithTags("Allergies")
            .WithSummary("Record an allergy on a registered patient's list")
            .Produces<AllergyRecordedResponse>(StatusCodes.Status201Created)
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status404NotFound);

        app.MapGet("/allergies/{uuid:guid}", GetAllergy)
            .WithName("GetAllergy")
            .WithTags("Allergies")
            .WithSummary("Get one allergy by its uuid, with an ETag")
            .Produces<AllergyResponse>()
            .ProducesProblem(StatusCodes.Status404NotFound);

        app.MapGet("/allergies", ListAllergies)
            .WithName("ListAllergies")
            .WithTags("Allergies")
            .WithSummary("List one patient's allergies, newest first, one bounded page at a time")
            .Produces<AllergyListResponse>()
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status404NotFound);

        return app;
    }

    private static async Task<IResult> RecordAllergy(
        RecordAllergyRequest request, RecordAllergyUseCase useCase, HttpContext http, CancellationToken ct)
    {
        try
        {
            var allergy = await useCase.ExecuteAsync(
                new RecordAllergyCommand(
                    request.PatientUuid, request.Title, ClientTime.ToServerLocal(request.BeginDate), ClientTime.ToServerLocal(request.EndDate),
                    request.Diagnosis, request.Reaction, request.Severity, request.Verification, request.Comments, request.ProviderId,
                    RequestOrigins.From(http)),
                ct);

            return Results.Created($"/api/v1/allergies/{allergy.Uuid}", new AllergyRecordedResponse(allergy.Uuid, allergy.Patient.Uuid));
        }
        catch (ClinicalCareValidationException ex)
        {
            return ValidationProblems.Many(ex.Errors);
        }
        catch (PatientNotFoundException ex)
        {
            return Results.Problem(title: "Patient not found.", detail: ex.Message, statusCode: StatusCodes.Status404NotFound);
        }
    }

    private static async Task<IResult> GetAllergy(Guid uuid, GetAllergyUseCase useCase, CancellationToken ct)
    {
        var view = await useCase.ExecuteAsync(uuid, ct);

        // Also 404 for a row of another issue type (ADR 0070 #2): from here a problem is not an allergy.
        return view is null
            ? Results.Problem(
                title: "Allergy not found.", detail: $"No allergy with uuid {uuid} exists.", statusCode: StatusCodes.Status404NotFound)
            : Preconditions.Ok(AllergyResponse.From(view), view.LastModified);
    }

    private static async Task<IResult> ListAllergies(
        Guid? patientUuid, int? limit, int? offset, ListPatientAllergiesUseCase useCase, CancellationToken ct)
    {
        // A list is always one patient's: there is no "all allergies" read, which would be unbounded (API-6).
        if (patientUuid is null || patientUuid == Guid.Empty)
            return ValidationProblems.Single("patientUuid is required.");

        try
        {
            var page = await useCase.ExecuteAsync(patientUuid.Value, limit, offset, ct);

            return Results.Ok(new AllergyListResponse(
                page.Items.Select(AllergyResponse.From).ToList(), page.Total, limit ?? PageRequest.DefaultLimit, offset ?? 0));
        }
        catch (ClinicalCareValidationException ex)
        {
            return ValidationProblems.Many(ex.Errors);
        }
        catch (PatientNotFoundException ex)
        {
            return Results.Problem(title: "Patient not found.", detail: ex.Message, statusCode: StatusCodes.Status404NotFound);
        }
    }
}
