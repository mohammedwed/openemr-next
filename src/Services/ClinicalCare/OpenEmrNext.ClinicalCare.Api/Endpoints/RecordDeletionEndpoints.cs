using Microsoft.AspNetCore.Mvc;
using OpenEmrNext.ClinicalCare.Application;
using OpenEmrNext.ClinicalCare.Application.Ports;

namespace OpenEmrNext.ClinicalCare.Api.Endpoints;

/// <summary>
/// Deleting vitals, a SOAP note or a clinical note (M5g, ADR 0076). All three are the same operation, so they share one handler: <c>DELETE</c> with the <c>ETag</c> the caller read in <c>If-Match</c>, answered
/// <c>204</c>. The record is flagged deleted, never removed. Only a user legacy's access control gives <c>admin/super</c> may do it (403 for anyone else).
/// </summary>
/// <remarks>
/// The order of answers is fixed: not signed in (401, middleware) then no <c>If-Match</c> (428) then not allowed (403) then no such encounter or record (404) then changed since it was read (412). A user who may not
/// delete is refused before anything is looked up, so a 403 never reveals which records exist.
/// </remarks>
public static class RecordDeletionEndpoints
{
    public static IEndpointRouteBuilder MapRecordDeletionEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapDelete("/encounters/{encounterUuid:guid}/vitals/{vitalsUuid:guid}", (
                Guid encounterUuid, Guid vitalsUuid, DeleteRecordUseCase useCase, HttpContext http, [FromHeader(Name = "If-Match")] string? ifMatch, CancellationToken ct) =>
            Preconditions.TryRead(ifMatch, out var offered)
                ? DeleteAsync(useCase, http, DeletableRecord.Vitals, encounterUuid, vitalsUuid.ToString(), offered.Select(t => t.ToETag()).ToList(), ct)
                : Task.FromResult(Preconditions.Required()))
            .WithName("DeleteVitals").WithTags("Vitals")
            .WithSummary("Delete a set of vital signs (flag it deleted; needs legacy's admin/super right), conditional on the ETag the caller read");

        app.MapDelete("/encounters/{encounterUuid:guid}/clinical-notes/{noteUuid:guid}", (
                Guid encounterUuid, Guid noteUuid, DeleteRecordUseCase useCase, HttpContext http, [FromHeader(Name = "If-Match")] string? ifMatch, CancellationToken ct) =>
            Preconditions.TryRead(ifMatch, out var offered)
                ? DeleteAsync(useCase, http, DeletableRecord.ClinicalNote, encounterUuid, noteUuid.ToString(), offered.Select(t => t.ToETag()).ToList(), ct)
                : Task.FromResult(Preconditions.Required()))
            .WithName("DeleteClinicalNote").WithTags("Clinical notes")
            .WithSummary("Delete a clinical note (mark it inactive; needs legacy's admin/super right), conditional on the ETag the caller read");

        app.MapDelete("/encounters/{encounterUuid:guid}/soap-notes/{noteId:long}", (
                Guid encounterUuid, long noteId, DeleteRecordUseCase useCase, HttpContext http, [FromHeader(Name = "If-Match")] string? ifMatch, CancellationToken ct) =>
            Preconditions.TryReadContent(ifMatch, out var offered)
                ? DeleteAsync(useCase, http, DeletableRecord.SoapNote, encounterUuid, noteId.ToString(System.Globalization.CultureInfo.InvariantCulture), offered.Select(v => v.ToETag()).ToList(), ct)
                : Task.FromResult(Preconditions.Required()))
            .WithName("DeleteSoapNote").WithTags("SOAP notes")
            .WithSummary("Delete a SOAP note (flag it deleted; needs legacy's admin/super right), conditional on the ETag the caller read");

        // Problems and allergies belong to a patient, not an encounter, so their routes have no encounter in them (ADR 0080). Legacy removes the row, so this does too.
        app.MapDelete("/problems/{uuid:guid}", (
                Guid uuid, DeleteIssueUseCase useCase, HttpContext http, [FromHeader(Name = "If-Match")] string? ifMatch, CancellationToken ct) =>
            Preconditions.TryRead(ifMatch, out var offered)
                ? DeleteIssueAsync(useCase, http, DeletableIssue.Problem, uuid, offered.Select(t => t.ToETag()).ToList(), ct)
                : Task.FromResult(Preconditions.Required()))
            .WithName("DeleteProblem").WithTags("Problems")
            .WithSummary("Delete a problem (remove it and its encounter links; needs legacy's admin/super right), conditional on the ETag the caller read");

        app.MapDelete("/allergies/{uuid:guid}", (
                Guid uuid, DeleteIssueUseCase useCase, HttpContext http, [FromHeader(Name = "If-Match")] string? ifMatch, CancellationToken ct) =>
            Preconditions.TryRead(ifMatch, out var offered)
                ? DeleteIssueAsync(useCase, http, DeletableIssue.Allergy, uuid, offered.Select(t => t.ToETag()).ToList(), ct)
                : Task.FromResult(Preconditions.Required()))
            .WithName("DeleteAllergy").WithTags("Allergies")
            .WithSummary("Delete an allergy (remove it and its encounter links; needs legacy's admin/super right), conditional on the ETag the caller read");

        return app;
    }

    private static async Task<IResult> DeleteIssueAsync(
        DeleteIssueUseCase useCase, HttpContext http, DeletableIssue kind, Guid uuid, IReadOnlyList<string> offered, CancellationToken ct)
    {
        try
        {
            await useCase.ExecuteAsync(new DeleteIssueCommand(kind, uuid, offered, RequestOrigins.From(http)), ct);
            return Results.NoContent();
        }
        catch (AccessDeniedException ex)
        {
            return Results.Problem(
                title: "Not allowed.", detail: $"Deleting a record needs {ex.Right} in OpenEMR's access control, which the signed-in user does not hold.", statusCode: StatusCodes.Status403Forbidden);
        }
        catch (RecordNotFoundException ex)
        {
            return Results.Problem(title: "Record not found.", detail: ex.Message, statusCode: StatusCodes.Status404NotFound);
        }
        catch (RecordVersionConflictException ex)
        {
            return Preconditions.Failed(ex.CurrentVersion);
        }
    }

    private static async Task<IResult> DeleteAsync(
        DeleteRecordUseCase useCase, HttpContext http, DeletableRecord kind, Guid encounterUuid, string key, IReadOnlyList<string> offered, CancellationToken ct)
    {
        try
        {
            await useCase.ExecuteAsync(new DeleteRecordCommand(kind, encounterUuid, key, offered, RequestOrigins.From(http)), ct);
            return Results.NoContent();
        }
        catch (AccessDeniedException ex)
        {
            return Results.Problem(
                title: "Not allowed.", detail: $"Deleting a record needs {ex.Right} in OpenEMR's access control, which the signed-in user does not hold.", statusCode: StatusCodes.Status403Forbidden);
        }
        catch (EncounterNotFoundException ex)
        {
            return Results.Problem(title: "Encounter not found.", detail: ex.Message, statusCode: StatusCodes.Status404NotFound);
        }
        catch (RecordNotFoundException ex)
        {
            return Results.Problem(title: "Record not found.", detail: ex.Message, statusCode: StatusCodes.Status404NotFound);
        }
        catch (RecordVersionConflictException ex)
        {
            return Preconditions.Failed(ex.CurrentVersion);
        }
    }
}
