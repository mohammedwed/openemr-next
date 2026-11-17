using Microsoft.AspNetCore.Mvc;
using OpenEmrNext.ClinicalCare.Api.Contracts.Requests;
using OpenEmrNext.ClinicalCare.Api.Contracts.Responses;
using OpenEmrNext.ClinicalCare.Application;
using OpenEmrNext.ClinicalCare.Domain.ReadModels;
using OpenEmrNext.ClinicalCare.Domain.ValueObjects;
using OpenEmrNext.ClinicalCare.Domain.VitalSigns;

namespace OpenEmrNext.ClinicalCare.Api.Endpoints;

/// <summary>
/// Endpoints for an encounter's vitals. Nested under <c>/api/v1/encounters/{uuid}/vitals</c>, so the gateway's existing
/// <c>/api/v1/encounters/**</c> route already covers them: no new route, no new secret (ADR 0064).
/// </summary>
public static class VitalsEndpoints
{
    public static IEndpointRouteBuilder MapVitalsEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapPost("/encounters/{encounterUuid:guid}/vitals", RecordVitals)
            .WithName("RecordVitals")
            .WithTags("Vitals")
            .WithSummary("Record a set of vital signs for an encounter")
            .Produces<VitalsRecordedResponse>(StatusCodes.Status201Created)
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status404NotFound);

        app.MapGet("/encounters/{encounterUuid:guid}/vitals", ListVitals)
            .WithName("ListVitals")
            .WithTags("Vitals")
            .WithSummary("List an encounter's vitals, newest first, one bounded page at a time")
            .Produces<VitalsListResponse>()
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status404NotFound);

        app.MapPut("/encounters/{encounterUuid:guid}/vitals/{vitalsUuid:guid}", CorrectVitals)
            .WithName("CorrectVitals")
            .WithTags("Vitals")
            .WithSummary("Replace a set of vital signs, conditional on the ETag the caller read")
            .Produces<VitalsResponse>()
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status412PreconditionFailed)
            .ProducesProblem(StatusCodes.Status428PreconditionRequired);

        app.MapGet("/encounters/{encounterUuid:guid}/vitals/{vitalsUuid:guid}", GetVitals)
            .WithName("GetVitals")
            .WithTags("Vitals")
            .WithSummary("Get one set of vitals")
            .Produces<VitalsResponse>()
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status404NotFound);

        return app;
    }

    private static async Task<IResult> RecordVitals(
        Guid encounterUuid, RecordVitalsRequest request, RecordVitalsUseCase useCase, HttpContext http, CancellationToken ct)
    {
        // A unit that is not one of ours is a 400 with the same shape as every other validation failure, listed
        // together with whatever else is wrong (DOM-3), not a binding exception.
        var errors = new List<string>();
        var weight = ToQuantity(request.Weight, "Weight", errors);
        var height = ToQuantity(request.Height, "Height", errors);
        var temperature = ToQuantity(request.Temperature, "Temperature", errors);
        var waist = ToQuantity(request.WaistCircumference, "Waist circumference", errors);
        var head = ToQuantity(request.HeadCircumference, "Head circumference", errors);

        if (errors.Count > 0)
            return ValidationProblems.Many(errors);

        try
        {
            var vitals = await useCase.ExecuteAsync(
                new RecordVitalsCommand(
                    encounterUuid,
                    new VitalsInput(
                        weight, height, temperature, request.TemperatureMethod, request.BloodPressureSystolic,
                        request.BloodPressureDiastolic, request.Pulse, request.Respiration, request.OxygenSaturation,
                        waist, head, request.Note),
                    RequestOrigins.ProviderOf(http, request.ProviderId),
                    RequestOrigins.From(http)),
                ct);

            return Results.Created(
                $"/api/v1/encounters/{encounterUuid}/vitals/{vitals.Uuid}",
                new VitalsRecordedResponse(vitals.Uuid, encounterUuid, vitals.Bmi, vitals.BmiStatus));
        }
        catch (VitalsValidationException ex)
        {
            return ValidationProblems.Many(ex.Errors);
        }
        catch (EncounterNotFoundException ex)
        {
            return NotFound("Encounter not found.", ex.Message);
        }
    }

    private static async Task<IResult> ListVitals(
        Guid encounterUuid, int? limit, int? offset, string? units, ListEncounterVitalsUseCase useCase, CancellationToken ct)
    {
        if (!UnitWire.TryParseSystem(units, out var system))
            return ValidationProblems.Single("units must be 'us' or 'metric'.");

        try
        {
            var page = await useCase.ExecuteAsync(encounterUuid, limit, offset, ct);

            return Results.Ok(new VitalsListResponse(
                page.Items.Select(v => VitalsResponse.From(v, system)).ToList(), page.Total,
                limit ?? PageRequest.DefaultLimit, offset ?? 0));
        }
        catch (ClinicalCareValidationException ex)
        {
            return ValidationProblems.Many(ex.Errors);
        }
        catch (EncounterNotFoundException ex)
        {
            return NotFound("Encounter not found.", ex.Message);
        }
    }

    private static async Task<IResult> CorrectVitals(
        Guid encounterUuid, Guid vitalsUuid, CorrectVitalsRequest request, string? units, CorrectVitalsUseCase useCase, HttpContext http,
        [FromHeader(Name = "If-Match")] string? ifMatch, CancellationToken ct)
    {
        // Before anything else: a correction must say which version it is based on (ADR 0068 #3).
        if (!Preconditions.TryRead(ifMatch, out var offered))
            return Preconditions.Required();

        if (!UnitWire.TryParseSystem(units, out var system))
            return ValidationProblems.Single("units must be 'us' or 'metric'.");

        // A unit that is not one of ours is a 400 in the same shape as every other validation failure, decided (like a malformed body) before the record is read.
        var errors = new List<string>();
        var weight = ToQuantity(request.Weight, "Weight", errors);
        var height = ToQuantity(request.Height, "Height", errors);
        var temperature = ToQuantity(request.Temperature, "Temperature", errors);
        var waist = ToQuantity(request.WaistCircumference, "Waist circumference", errors);
        var head = ToQuantity(request.HeadCircumference, "Head circumference", errors);

        if (errors.Count > 0)
            return ValidationProblems.Many(errors);

        try
        {
            var result = await useCase.ExecuteAsync(
                new CorrectVitalsCommand(
                    encounterUuid, vitalsUuid, offered,
                    new VitalsInput(
                        weight, height, temperature, request.TemperatureMethod, request.BloodPressureSystolic, request.BloodPressureDiastolic, request.Pulse,
                        request.Respiration, request.OxygenSaturation, waist, head, request.Note),
                    RequestOrigins.From(http)),
                ct);

            // The new ETag, so the caller can keep editing without another read.
            return Preconditions.Ok(VitalsResponse.From(result.Vitals, system), result.Vitals.LastUpdated);
        }
        catch (VitalsValidationException ex)
        {
            return ValidationProblems.Many(ex.Errors);
        }
        catch (EncounterNotFoundException ex)
        {
            return NotFound("Encounter not found.", ex.Message);
        }
        catch (VitalsNotFoundException ex)
        {
            return NotFound("Vitals not found.", ex.Message);
        }
        catch (ConcurrencyConflictException ex)
        {
            return Preconditions.Failed(ex.CurrentStamp);
        }
    }

    private static async Task<IResult> GetVitals(
        Guid encounterUuid, Guid vitalsUuid, string? units, GetVitalsUseCase useCase, CancellationToken ct)
    {
        if (!UnitWire.TryParseSystem(units, out var system))
            return ValidationProblems.Single("units must be 'us' or 'metric'.");

        try
        {
            var view = await useCase.ExecuteAsync(encounterUuid, vitalsUuid, ct);

            return view is null
                ? NotFound("Vitals not found.", $"Encounter {encounterUuid} has no vitals with uuid {vitalsUuid}.")
                : Preconditions.Ok(VitalsResponse.From(view, system), view.LastUpdated);
        }
        catch (EncounterNotFoundException ex)
        {
            return NotFound("Encounter not found.", ex.Message);
        }
    }

    private static Quantity? ToQuantity(QuantityRequest? request, string label, List<string> errors)
    {
        if (request is null)
            return null;

        if (UnitWire.TryParse(request.Unit, out var unit))
            return new Quantity(request.Value, unit);

        errors.Add($"{label}: '{request.Unit}' is not a known unit (lb, kg, in, cm, degF, degC).");
        return null;
    }

    private static IResult NotFound(string title, string detail) =>
        Results.Problem(title: title, detail: detail, statusCode: StatusCodes.Status404NotFound);
}
