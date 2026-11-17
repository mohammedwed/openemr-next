using Microsoft.AspNetCore.Mvc;
using OpenEmrNext.ClinicalCare.Api.Contracts.Requests;
using OpenEmrNext.ClinicalCare.Api.Contracts.Responses;
using OpenEmrNext.ClinicalCare.Application;
using OpenEmrNext.ClinicalCare.Domain.Immunizations;
using OpenEmrNext.ClinicalCare.Domain.ReadModels;
using OpenEmrNext.ClinicalCare.Domain.ValueObjects;

namespace OpenEmrNext.ClinicalCare.Api.Endpoints;

/// <summary>Endpoints for a patient's immunizations (M5d slice 2, ADR 0072). A flat <c>/immunizations</c> route for the reason the problem routes are flat (ADR 0064).</summary>
public static class ImmunizationEndpoints
{
    public static IEndpointRouteBuilder MapImmunizationEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapPost("/immunizations", RecordImmunization)
            .WithName("RecordImmunization").WithTags("Immunizations")
            .WithSummary("Record an immunization on a registered patient's record")
            .Produces<ImmunizationRecordedResponse>(StatusCodes.Status201Created)
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status404NotFound);

        app.MapPut("/immunizations/{uuid:guid}", CorrectImmunization)
            .WithName("CorrectImmunization").WithTags("Immunizations")
            .WithSummary("Replace an immunization's editable fields, conditional on the ETag the caller read (set enteredInError to void a mistaken entry)")
            .Produces<ImmunizationResponse>()
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status412PreconditionFailed)
            .ProducesProblem(StatusCodes.Status428PreconditionRequired);

        app.MapGet("/immunizations/{uuid:guid}", GetImmunization)
            .WithName("GetImmunization").WithTags("Immunizations")
            .WithSummary("Get one immunization by its uuid, with an ETag")
            .Produces<ImmunizationResponse>()
            .ProducesProblem(StatusCodes.Status404NotFound);

        app.MapGet("/immunizations", ListImmunizations)
            .WithName("ListImmunizations").WithTags("Immunizations")
            .WithSummary("List one patient's immunizations, most recently administered first, one bounded page at a time")
            .Produces<ImmunizationListResponse>()
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status404NotFound);

        return app;
    }

    private static ImmunizationInput Input(
        DateTime? administered, int? immunizationId, string? cvx, string? manufacturer, string? lot, DateTime? expiration, decimal? amount, string? unit, string? route,
        string? site, string? completion, string? source, string? refusal, DateTime? education, DateTime? vis, string? administeredBy, long? administeredById,
        long? orderingProvider, string? reasonCode, string? reasonDescription, string? note, bool inError) =>
        new(ClientTime.ToServerLocal(administered), immunizationId, cvx, manufacturer, lot, ClientTime.ToServerLocal(expiration), amount, unit, route, site, completion,
            source, refusal, ClientTime.ToServerLocal(education), ClientTime.ToServerLocal(vis), administeredBy, administeredById, orderingProvider, null, reasonCode,
            reasonDescription, note, inError);

    private static async Task<IResult> RecordImmunization(RecordImmunizationRequest r, RecordImmunizationUseCase useCase, HttpContext http, CancellationToken ct)
    {
        try
        {
            var immunization = await useCase.ExecuteAsync(
                new RecordImmunizationCommand(
                    r.PatientUuid,
                    Input(r.AdministeredDate, r.ImmunizationId, r.CvxCode, r.Manufacturer, r.LotNumber, r.ExpirationDate, r.AmountAdministered, r.AmountUnit, r.Route,
                        r.AdministrationSite, r.CompletionStatus, r.InformationSource, r.RefusalReason, r.EducationDate, r.VisDate, r.AdministeredBy,
                        r.AdministeredById, r.OrderingProvider, r.ReasonCode, r.ReasonDescription, r.Note, r.EnteredInError),
                    r.EncounterUuid, RequestOrigins.ProviderOf(http, r.ProviderId), RequestOrigins.From(http)),
                ct);

            return Results.Created(
                $"/api/v1/immunizations/{immunization.Uuid}", new ImmunizationRecordedResponse(immunization.Uuid, immunization.Patient.Uuid));
        }
        catch (ClinicalCareValidationException ex)
        {
            return ValidationProblems.Many(ex.Errors);
        }
        catch (PatientNotFoundException ex)
        {
            return Results.Problem(title: "Patient not found.", detail: ex.Message, statusCode: StatusCodes.Status404NotFound);
        }
        catch (EncounterNotFoundException ex)
        {
            return Results.Problem(title: "Encounter not found.", detail: ex.Message, statusCode: StatusCodes.Status404NotFound);
        }
    }

    private static async Task<IResult> CorrectImmunization(
        Guid uuid, CorrectImmunizationRequest r, CorrectImmunizationUseCase useCase, HttpContext http, [FromHeader(Name = "If-Match")] string? ifMatch, CancellationToken ct)
    {
        if (!Preconditions.TryReadContent(ifMatch, out var offered))
            return Preconditions.Required();

        try
        {
            var result = await useCase.ExecuteAsync(
                new CorrectImmunizationCommand(
                    uuid, offered,
                    Input(r.AdministeredDate, r.ImmunizationId, r.CvxCode, r.Manufacturer, r.LotNumber, r.ExpirationDate, r.AmountAdministered, r.AmountUnit, r.Route,
                        r.AdministrationSite, r.CompletionStatus, r.InformationSource, r.RefusalReason, r.EducationDate, r.VisDate, r.AdministeredBy,
                        r.AdministeredById, r.OrderingProvider, r.ReasonCode, r.ReasonDescription, r.Note, r.EnteredInError),
                    r.EncounterUuid, RequestOrigins.From(http)),
                ct);

            return Preconditions.Ok(ImmunizationResponse.From(result.Immunization), result.Immunization.Version());
        }
        catch (ClinicalCareValidationException ex)
        {
            return ValidationProblems.Many(ex.Errors);
        }
        catch (ImmunizationNotFoundException ex)
        {
            return Results.Problem(title: "Immunization not found.", detail: ex.Message, statusCode: StatusCodes.Status404NotFound);
        }
        catch (EncounterNotFoundException ex)
        {
            return Results.Problem(title: "Encounter not found.", detail: ex.Message, statusCode: StatusCodes.Status404NotFound);
        }
        catch (ImmunizationConflictException ex)
        {
            return Preconditions.Failed(ex.CurrentVersion);
        }
    }

    private static async Task<IResult> GetImmunization(Guid uuid, GetImmunizationUseCase useCase, CancellationToken ct)
    {
        var view = await useCase.ExecuteAsync(uuid, ct);
        return view is null
            ? Results.Problem(title: "Immunization not found.", detail: $"No immunization with uuid {uuid} exists.", statusCode: StatusCodes.Status404NotFound)
            : Preconditions.Ok(ImmunizationResponse.From(view), view.Version());
    }

    private static async Task<IResult> ListImmunizations(Guid? patientUuid, int? limit, int? offset, ListPatientImmunizationsUseCase useCase, CancellationToken ct)
    {
        if (patientUuid is null || patientUuid == Guid.Empty)
            return ValidationProblems.Single("patientUuid is required.");

        try
        {
            var page = await useCase.ExecuteAsync(patientUuid.Value, limit, offset, ct);
            return Results.Ok(new ImmunizationListResponse(
                page.Items.Select(ImmunizationResponse.From).ToList(), page.Total, limit ?? PageRequest.DefaultLimit, offset ?? 0));
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
