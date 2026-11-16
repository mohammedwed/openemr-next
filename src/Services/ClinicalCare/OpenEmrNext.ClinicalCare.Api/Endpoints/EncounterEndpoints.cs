using OpenEmrNext.ClinicalCare.Api.Contracts.Requests;
using OpenEmrNext.ClinicalCare.Api.Contracts.Responses;
using OpenEmrNext.ClinicalCare.Application;
using OpenEmrNext.ClinicalCare.Domain.ReadModels;
using OpenEmrNext.ClinicalCare.Domain.ValueObjects;

namespace OpenEmrNext.ClinicalCare.Api.Endpoints;

/// <summary>Endpoints for encounters.</summary>
public static class EncounterEndpoints
{
    public static IEndpointRouteBuilder MapEncounterEndpoints(this IEndpointRouteBuilder app)
    {
        // A flat /encounters route, not /patients/{uuid}/encounters: the gateway already routes everything under
        // /api/v1/patients/ to PatientManagement, and one prefix must belong to one service (ADR 0064).
        app.MapPost("/encounters", CreateEncounter)
            .WithName("CreateEncounter")
            .WithTags("Encounters")
            .WithSummary("Create an encounter for a registered patient")
            .Produces<EncounterCreatedResponse>(StatusCodes.Status201Created)
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status404NotFound);

        app.MapGet("/encounters/{uuid:guid}", GetEncounter)
            .WithName("GetEncounter")
            .WithTags("Encounters")
            .WithSummary("Get one encounter by its uuid")
            .Produces<EncounterResponse>()
            .ProducesProblem(StatusCodes.Status404NotFound);

        app.MapGet("/encounters", ListEncounters)
            .WithName("ListEncounters")
            .WithTags("Encounters")
            .WithSummary("List one patient's encounters, newest first, one bounded page at a time")
            .Produces<EncounterListResponse>()
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status404NotFound);

        return app;
    }

    private static async Task<IResult> GetEncounter(Guid uuid, GetEncounterUseCase useCase, CancellationToken ct)
    {
        var view = await useCase.ExecuteAsync(uuid, ct);

        // Also 404 for an encounter legacy has deleted (CC-9): from here it does not exist.
        return view is null
            ? Results.Problem(
                title: "Encounter not found.", detail: $"No encounter with uuid {uuid} exists.",
                statusCode: StatusCodes.Status404NotFound)
            : Results.Ok(EncounterResponse.From(view));
    }

    private static async Task<IResult> ListEncounters(
        Guid? patientUuid, int? limit, int? offset, ListPatientEncountersUseCase useCase, CancellationToken ct)
    {
        // A list is always one patient's: there is no "all encounters" read, which would be unbounded (API-6).
        if (patientUuid is null || patientUuid == Guid.Empty)
            return ValidationProblems.Single("patientUuid is required.");

        try
        {
            var page = await useCase.ExecuteAsync(patientUuid.Value, limit, offset, ct);

            return Results.Ok(new EncounterListResponse(
                page.Items.Select(EncounterResponse.From).ToList(), page.Total,
                limit ?? PageRequest.DefaultLimit, offset ?? 0));
        }
        catch (EncounterValidationException ex)
        {
            return ValidationProblems.Many(ex.Errors);
        }
        catch (PatientNotFoundException ex)
        {
            return Results.Problem(
                title: "Patient not found.", detail: ex.Message, statusCode: StatusCodes.Status404NotFound);
        }
    }

    private static async Task<IResult> CreateEncounter(
        CreateEncounterRequest request, CreateEncounterUseCase useCase, CancellationToken ct)
    {
        try
        {
            var encounter = await useCase.ExecuteAsync(
                new CreateEncounterCommand(
                    request.PatientUuid, request.ClassCode, request.CalendarCategoryId, request.Date, request.Reason,
                    request.FacilityId, request.PosCode, request.ProviderId, request.OnsetDate, request.Sensitivity),
                ct);

            return Results.Created(
                $"/api/v1/encounters/{encounter.Uuid}",
                new EncounterCreatedResponse(encounter.Uuid, encounter.Number, encounter.Patient.Uuid));
        }
        catch (EncounterValidationException ex)
        {
            return ValidationProblems.Many(ex.Errors);
        }
        catch (PatientNotFoundException ex)
        {
            return Results.Problem(
                title: "Patient not found.", detail: ex.Message, statusCode: StatusCodes.Status404NotFound);
        }
    }
}
