using OpenEmrNext.PatientManagement.Api.Authentication;
using OpenEmrNext.PatientManagement.Api.Contracts.Requests;
using OpenEmrNext.PatientManagement.Api.Contracts.Responses;
using OpenEmrNext.PatientManagement.Application;
using OpenEmrNext.PatientManagement.Domain.AdvanceDirectives;
using OpenEmrNext.PatientManagement.Domain.AdvanceDirectives.ValueObjects;

namespace OpenEmrNext.PatientManagement.Api.Endpoints;

/// <summary>Endpoints for a patient's advance directives: uploaded documents and the two status flags.</summary>
public static class AdvanceDirectiveEndpoints
{
    public static IEndpointRouteBuilder MapAdvanceDirectiveEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapPost("/patients/{pid:long}/advance-directives/documents", UploadDocument)
            .WithName("UploadAdvanceDirectiveDocument")
            .WithTags("AdvanceDirectives")
            .WithSummary("Upload an advance directive document for a patient")
            .Produces<AdvanceDirectiveDocumentResponse>(StatusCodes.Status201Created)
            .ProducesValidationProblem();

        app.MapGet("/patients/{pid:long}/advance-directives/documents", GetDocuments)
            .WithName("GetAdvanceDirectiveDocuments")
            .WithTags("AdvanceDirectives")
            .WithSummary("List a patient's advance directive documents")
            .Produces<IReadOnlyList<AdvanceDirectiveDocumentResponse>>();

        app.MapGet("/patients/{pid:long}/advance-directives/documents/{uuid:guid}/content", DownloadDocumentContent)
            .WithName("DownloadAdvanceDirectiveDocumentContent")
            .WithTags("AdvanceDirectives")
            .WithSummary("Download an advance directive document's file content")
            .Produces(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status404NotFound);

        app.MapPut("/patients/{pid:long}/advance-directives/status", SetStatus)
            .WithName("SetAdvanceDirectiveStatus")
            .WithTags("AdvanceDirectives")
            .WithSummary("Set whether a patient has completed an advance directive, and who reviewed it")
            .Produces(StatusCodes.Status204NoContent)
            .Produces(StatusCodes.Status404NotFound)
            .ProducesValidationProblem();

        app.MapGet("/patients/{pid:long}/advance-directives/status", GetStatus)
            .WithName("GetAdvanceDirectiveStatus")
            .WithTags("AdvanceDirectives")
            .WithSummary("Get a patient's advance directive status")
            .Produces<AdvanceDirectiveStatusResponse>();

        return app;
    }

    private static async Task<IResult> UploadDocument(
        long pid, UploadAdvanceDirectiveDocumentRequest request, UploadAdvanceDirectiveDocumentUseCase useCase, HttpContext http, CancellationToken ct)
    {
        var type = AdvanceDirectiveTypeExtensions.TryParse(request.Type);
        if (type is null)
            return ValidationProblems.Single("Type must be \"living_will\", \"durable_power_attorney\", or \"dnr_order\".");

        byte[] content;
        try
        {
            content = Convert.FromBase64String(request.ContentBase64);
        }
        catch (FormatException)
        {
            return ValidationProblems.Single("ContentBase64 must be valid base64.");
        }

        AdvanceDirectiveDocument document;
        try
        {
            document = await useCase.ExecuteAsync(
                pid, type.Value, request.FileName, request.MimeType, content, request.EffectiveDate, http.ActingUserId(request.OwnerId), ct, AuditOrigins.From(http));
        }
        catch (AdvanceDirectiveDocumentValidationException ex)
        {
            return ValidationProblems.Many(ex.Errors);
        }

        return Results.Created(
            $"/api/v1/patients/{pid}/advance-directives/documents/{document.Uuid}/content",
            new AdvanceDirectiveDocumentResponse(
                document.Id, document.Uuid, document.Type.ToWireValue(), document.Name,
                document.MimeType, document.Size, document.EffectiveDate, document.OwnerId));
    }

    private static async Task<IResult> GetDocuments(
        long pid, GetAdvanceDirectiveDocumentsUseCase useCase, CancellationToken ct)
    {
        var documents = await useCase.ExecuteAsync(pid, ct);

        return Results.Ok(documents
            .Select(d => new AdvanceDirectiveDocumentResponse(
                d.Id, d.Uuid, d.Type?.ToWireValue(), d.Name, d.MimeType, d.Size, d.EffectiveDate, d.OwnerId))
            .ToList());
    }

    private static async Task<IResult> DownloadDocumentContent(
        long pid, Guid uuid, DownloadAdvanceDirectiveDocumentUseCase useCase, CancellationToken ct)
    {
        var result = await useCase.ExecuteAsync(pid, uuid, ct);
        if (result is null)
            return Results.NotFound();

        return Results.Bytes(result.Value.Content, result.Value.MimeType, result.Value.FileName);
    }

    private static async Task<IResult> SetStatus(
        long pid, SetAdvanceDirectiveStatusRequest request, SetAdvanceDirectiveStatusUseCase useCase, HttpContext http, CancellationToken ct)
    {
        var status = AdvanceDirectiveCompletionStatusExtensions.TryParseWireValue(request.Status);
        if (status is null)
            return ValidationProblems.Single("Status must be \"yes\", \"no\", or \"unknown\".");

        var done = await useCase.ExecuteAsync(
            pid, status.Value, request.ReviewedAtUtc, http.ActingUserId(request.AuthenticatorUserId), ct, AuditOrigins.From(http));

        // Setting the status of a patient that does not exist used to answer 204 and change nothing; it is 404 now, because a no-op cannot be audited as a success.
        return done ? Results.NoContent() : Results.NotFound();
    }

    private static async Task<IResult> GetStatus(
        long pid, GetAdvanceDirectiveStatusUseCase useCase, CancellationToken ct)
    {
        var (status, reviewedAtUtc, authenticatorUserId) = await useCase.ExecuteAsync(pid, ct);
        return Results.Ok(new AdvanceDirectiveStatusResponse(status.ToWireValue(), reviewedAtUtc, authenticatorUserId));
    }
}
