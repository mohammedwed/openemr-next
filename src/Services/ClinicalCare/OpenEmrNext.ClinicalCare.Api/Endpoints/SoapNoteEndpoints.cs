using Microsoft.AspNetCore.Mvc;
using OpenEmrNext.ClinicalCare.Api.Contracts.Requests;
using OpenEmrNext.ClinicalCare.Api.Contracts.Responses;
using OpenEmrNext.ClinicalCare.Application;
using OpenEmrNext.ClinicalCare.Domain.ReadModels;
using OpenEmrNext.ClinicalCare.Domain.SoapNotes;
using OpenEmrNext.ClinicalCare.Domain.ValueObjects;

namespace OpenEmrNext.ClinicalCare.Api.Endpoints;

/// <summary>Endpoints for an encounter's SOAP notes (M5d slice 3, ADR 0073). A note has no uuid: it is addressed by its id within its encounter, and the encounter in the route authorises it.</summary>
public static class SoapNoteEndpoints
{
    public static IEndpointRouteBuilder MapSoapNoteEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapPost("/encounters/{encounterUuid:guid}/soap-notes", RecordSoapNote)
            .WithName("RecordSoapNote").WithTags("SoapNotes")
            .WithSummary("Record a SOAP note on an encounter")
            .Produces<SoapNoteRecordedResponse>(StatusCodes.Status201Created)
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status404NotFound);

        app.MapPut("/encounters/{encounterUuid:guid}/soap-notes/{noteId:long}", CorrectSoapNote)
            .WithName("CorrectSoapNote").WithTags("SoapNotes")
            .WithSummary("Replace a SOAP note's sections, conditional on the ETag the caller read")
            .Produces<SoapNoteResponse>()
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status412PreconditionFailed)
            .ProducesProblem(StatusCodes.Status428PreconditionRequired);

        app.MapGet("/encounters/{encounterUuid:guid}/soap-notes/{noteId:long}", GetSoapNote)
            .WithName("GetSoapNote").WithTags("SoapNotes")
            .WithSummary("Get one SOAP note of an encounter, with an ETag")
            .Produces<SoapNoteResponse>()
            .ProducesProblem(StatusCodes.Status404NotFound);

        app.MapGet("/encounters/{encounterUuid:guid}/soap-notes", ListSoapNotes)
            .WithName("ListSoapNotes").WithTags("SoapNotes")
            .WithSummary("List an encounter's SOAP notes, newest first, one bounded page at a time")
            .Produces<SoapNoteListResponse>()
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status404NotFound);

        return app;
    }

    private static IResult EncounterNotFound(EncounterNotFoundException ex) =>
        Results.Problem(title: "Encounter not found.", detail: ex.Message, statusCode: StatusCodes.Status404NotFound);

    private static IResult NoteNotFound(long noteId) =>
        Results.Problem(title: "SOAP note not found.", detail: $"No SOAP note {noteId} exists in this encounter.", statusCode: StatusCodes.Status404NotFound);

    private static async Task<IResult> RecordSoapNote(
        Guid encounterUuid, RecordSoapNoteRequest request, RecordSoapNoteUseCase useCase, HttpContext http, CancellationToken ct)
    {
        try
        {
            var note = await useCase.ExecuteAsync(
                new RecordSoapNoteCommand(
                    encounterUuid, new SoapInput(request.Subjective, request.Objective, request.Assessment, request.Plan), request.ProviderId, RequestOrigins.From(http)),
                ct);

            return Results.Created($"/api/v1/encounters/{encounterUuid}/soap-notes/{note.Id}", new SoapNoteRecordedResponse(note.Id, encounterUuid));
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

    private static async Task<IResult> CorrectSoapNote(
        Guid encounterUuid, long noteId, CorrectSoapNoteRequest request, CorrectSoapNoteUseCase useCase, HttpContext http,
        [FromHeader(Name = "If-Match")] string? ifMatch, CancellationToken ct)
    {
        if (!Preconditions.TryReadContent(ifMatch, out var offered))
            return Preconditions.Required();

        try
        {
            var result = await useCase.ExecuteAsync(
                new CorrectSoapNoteCommand(
                    encounterUuid, noteId, offered, new SoapInput(request.Subjective, request.Objective, request.Assessment, request.Plan), RequestOrigins.From(http)),
                ct);

            return Preconditions.Ok(SoapNoteResponse.From(result.Note), result.Note.Version());
        }
        catch (ClinicalCareValidationException ex)
        {
            return ValidationProblems.Many(ex.Errors);
        }
        catch (EncounterNotFoundException ex)
        {
            return EncounterNotFound(ex);
        }
        catch (SoapNoteNotFoundException)
        {
            return NoteNotFound(noteId);
        }
        catch (SoapNoteConflictException ex)
        {
            return Preconditions.Failed(ex.CurrentVersion);
        }
    }

    private static async Task<IResult> GetSoapNote(Guid encounterUuid, long noteId, GetSoapNoteUseCase useCase, CancellationToken ct)
    {
        try
        {
            var view = await useCase.ExecuteAsync(encounterUuid, noteId, ct);
            return view is null ? NoteNotFound(noteId) : Preconditions.Ok(SoapNoteResponse.From(view), view.Version());
        }
        catch (EncounterNotFoundException ex)
        {
            return EncounterNotFound(ex);
        }
    }

    private static async Task<IResult> ListSoapNotes(Guid encounterUuid, int? limit, int? offset, ListEncounterSoapNotesUseCase useCase, CancellationToken ct)
    {
        try
        {
            var page = await useCase.ExecuteAsync(encounterUuid, limit, offset, ct);
            return Results.Ok(new SoapNoteListResponse(page.Items.Select(SoapNoteResponse.From).ToList(), page.Total, limit ?? PageRequest.DefaultLimit, offset ?? 0));
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
