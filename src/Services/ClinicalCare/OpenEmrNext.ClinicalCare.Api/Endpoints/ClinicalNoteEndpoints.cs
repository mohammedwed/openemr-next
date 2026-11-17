using Microsoft.AspNetCore.Mvc;
using OpenEmrNext.ClinicalCare.Api.Contracts.Requests;
using OpenEmrNext.ClinicalCare.Api.Contracts.Responses;
using OpenEmrNext.ClinicalCare.Application;
using OpenEmrNext.ClinicalCare.Domain.ClinicalNotes;
using OpenEmrNext.ClinicalCare.Domain.ReadModels;
using OpenEmrNext.ClinicalCare.Domain.ValueObjects;

namespace OpenEmrNext.ClinicalCare.Api.Endpoints;

/// <summary>Endpoints for an encounter's clinical notes (M5d slice 3b, ADR 0074). A note belongs to its encounter, and the encounter in the route authorises it.</summary>
public static class ClinicalNoteEndpoints
{
    public static IEndpointRouteBuilder MapClinicalNoteEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapPost("/encounters/{encounterUuid:guid}/clinical-notes", RecordNote)
            .WithName("RecordClinicalNote").WithTags("ClinicalNotes")
            .WithSummary("Record a clinical note on an encounter")
            .Produces<ClinicalNoteRecordedResponse>(StatusCodes.Status201Created)
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status404NotFound);

        app.MapPut("/encounters/{encounterUuid:guid}/clinical-notes/{noteUuid:guid}", CorrectNote)
            .WithName("CorrectClinicalNote").WithTags("ClinicalNotes")
            .WithSummary("Replace a clinical note's editable fields, conditional on the ETag the caller read")
            .Produces<ClinicalNoteResponse>()
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status412PreconditionFailed)
            .ProducesProblem(StatusCodes.Status428PreconditionRequired);

        app.MapGet("/encounters/{encounterUuid:guid}/clinical-notes/{noteUuid:guid}", GetNote)
            .WithName("GetClinicalNote").WithTags("ClinicalNotes")
            .WithSummary("Get one clinical note of an encounter, with an ETag")
            .Produces<ClinicalNoteResponse>()
            .ProducesProblem(StatusCodes.Status404NotFound);

        app.MapGet("/encounters/{encounterUuid:guid}/clinical-notes", ListNotes)
            .WithName("ListClinicalNotes").WithTags("ClinicalNotes")
            .WithSummary("List an encounter's clinical notes, newest first, one bounded page at a time")
            .Produces<ClinicalNoteListResponse>()
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status404NotFound);

        return app;
    }

    private static IResult EncounterNotFound(EncounterNotFoundException ex) =>
        Results.Problem(title: "Encounter not found.", detail: ex.Message, statusCode: StatusCodes.Status404NotFound);

    private static IResult NoteNotFound(Guid noteUuid) =>
        Results.Problem(title: "Clinical note not found.", detail: $"No clinical note with uuid {noteUuid} exists in this encounter.", statusCode: StatusCodes.Status404NotFound);

    private static async Task<IResult> RecordNote(Guid encounterUuid, RecordClinicalNoteRequest request, RecordClinicalNoteUseCase useCase, HttpContext http, CancellationToken ct)
    {
        try
        {
            var note = await useCase.ExecuteAsync(
                new RecordClinicalNoteCommand(
                    encounterUuid, new ClinicalNoteInput(ClientTime.ToServerLocal(request.Date), request.Type, request.Category, request.Description), RequestOrigins.ProviderOf(http, request.ProviderId),
                    RequestOrigins.From(http)),
                ct);

            return Results.Created($"/api/v1/encounters/{encounterUuid}/clinical-notes/{note.Uuid}", new ClinicalNoteRecordedResponse(note.Uuid, encounterUuid));
        }
        catch (ClinicalCareValidationException ex)
        {
            return ValidationProblems.Many(ex.Errors);
        }
        catch (EncounterNotFoundException ex)
        {
            return EncounterNotFound(ex);
        }
    }

    private static async Task<IResult> CorrectNote(
        Guid encounterUuid, Guid noteUuid, CorrectClinicalNoteRequest request, CorrectClinicalNoteUseCase useCase, HttpContext http,
        [FromHeader(Name = "If-Match")] string? ifMatch, CancellationToken ct)
    {
        if (!Preconditions.TryRead(ifMatch, out var offered))
            return Preconditions.Required();

        try
        {
            var result = await useCase.ExecuteAsync(
                new CorrectClinicalNoteCommand(
                    encounterUuid, noteUuid, offered,
                    new ClinicalNoteInput(ClientTime.ToServerLocal(request.Date), request.Type, request.Category, request.Description), RequestOrigins.From(http)),
                ct);

            return Preconditions.Ok(ClinicalNoteResponse.From(result.Note), result.Note.LastUpdated);
        }
        catch (ClinicalCareValidationException ex)
        {
            return ValidationProblems.Many(ex.Errors);
        }
        catch (EncounterNotFoundException ex)
        {
            return EncounterNotFound(ex);
        }
        catch (ClinicalNoteNotFoundException)
        {
            return NoteNotFound(noteUuid);
        }
        catch (ConcurrencyConflictException ex)
        {
            return Preconditions.Failed(ex.CurrentStamp);
        }
    }

    private static async Task<IResult> GetNote(Guid encounterUuid, Guid noteUuid, GetClinicalNoteUseCase useCase, CancellationToken ct)
    {
        try
        {
            var view = await useCase.ExecuteAsync(encounterUuid, noteUuid, ct);
            return view is null ? NoteNotFound(noteUuid) : Preconditions.Ok(ClinicalNoteResponse.From(view), view.LastUpdated);
        }
        catch (EncounterNotFoundException ex)
        {
            return EncounterNotFound(ex);
        }
    }

    private static async Task<IResult> ListNotes(Guid encounterUuid, int? limit, int? offset, ListEncounterClinicalNotesUseCase useCase, CancellationToken ct)
    {
        try
        {
            var page = await useCase.ExecuteAsync(encounterUuid, limit, offset, ct);
            return Results.Ok(new ClinicalNoteListResponse(page.Items.Select(ClinicalNoteResponse.From).ToList(), page.Total, limit ?? PageRequest.DefaultLimit, offset ?? 0));
        }
        catch (ClinicalCareValidationException ex)
        {
            return ValidationProblems.Many(ex.Errors);
        }
        catch (EncounterNotFoundException ex)
        {
            return EncounterNotFound(ex);
        }
    }
}
