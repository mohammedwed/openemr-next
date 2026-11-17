using Microsoft.AspNetCore.Mvc;
using OpenEmrNext.ClinicalCare.Api.Contracts.Requests;
using OpenEmrNext.ClinicalCare.Api.Contracts.Responses;
using OpenEmrNext.ClinicalCare.Application;
using OpenEmrNext.ClinicalCare.Domain.ReadModels;
using OpenEmrNext.ClinicalCare.Domain.ValueObjects;

namespace OpenEmrNext.ClinicalCare.Api.Endpoints;

/// <summary>Endpoints for a patient's problem list (M5c, ADR 0070).</summary>
public static class ProblemEndpoints
{
    public static IEndpointRouteBuilder MapProblemEndpoints(this IEndpointRouteBuilder app)
    {
        // A flat /problems route, not /patients/{uuid}/problems: everything under /api/v1/patients/ already belongs to PatientManagement at the gateway,
        // and one prefix must belong to one service (ADR 0064).
        app.MapPost("/problems", RecordProblem)
            .WithName("RecordProblem")
            .WithTags("Problems")
            .WithSummary("Record a problem on a registered patient's list")
            .Produces<ProblemRecordedResponse>(StatusCodes.Status201Created)
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status404NotFound);

        app.MapPut("/problems/{uuid:guid}", UpdateProblem)
            .WithName("UpdateProblem")
            .WithTags("Problems")
            .WithSummary("Replace a problem's editable fields, conditional on the ETag the caller read (resolve it by sending an end date)")
            .Produces<ProblemResponse>()
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status412PreconditionFailed)
            .ProducesProblem(StatusCodes.Status428PreconditionRequired);

        app.MapGet("/problems/{uuid:guid}", GetProblem)
            .WithName("GetProblem")
            .WithTags("Problems")
            .WithSummary("Get one problem by its uuid, with an ETag")
            .Produces<ProblemResponse>()
            .ProducesProblem(StatusCodes.Status404NotFound);

        app.MapGet("/problems", ListProblems)
            .WithName("ListProblems")
            .WithTags("Problems")
            .WithSummary("List one patient's problems, newest first, one bounded page at a time")
            .Produces<ProblemListResponse>()
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status404NotFound);

        return app;
    }

    private static async Task<IResult> RecordProblem(
        RecordProblemRequest request, RecordProblemUseCase useCase, HttpContext http, CancellationToken ct)
    {
        try
        {
            var problem = await useCase.ExecuteAsync(
                new RecordProblemCommand(
                    request.PatientUuid, request.Title, ClientTime.ToServerLocal(request.BeginDate), ClientTime.ToServerLocal(request.EndDate),
                    request.Diagnosis, request.Outcome, request.Occurrence, request.ReferredBy, request.Comments, request.ProviderId,
                    RequestOrigins.From(http), request.Verification),
                ct);

            return Results.Created($"/api/v1/problems/{problem.Uuid}", new ProblemRecordedResponse(problem.Uuid, problem.Patient.Uuid));
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

    private static async Task<IResult> UpdateProblem(
        Guid uuid, UpdateProblemRequest request, UpdateProblemUseCase useCase, HttpContext http,
        [FromHeader(Name = "If-Match")] string? ifMatch, CancellationToken ct)
    {
        // Before anything else: an update must say which version it is based on (ADR 0068 #3).
        if (!Preconditions.TryRead(ifMatch, out var offered))
            return Preconditions.Required();

        try
        {
            var result = await useCase.ExecuteAsync(
                new UpdateProblemCommand(
                    uuid, offered, request.Title, ClientTime.ToServerLocal(request.BeginDate), ClientTime.ToServerLocal(request.EndDate), request.Diagnosis,
                    request.Outcome, request.Occurrence, request.ReferredBy, request.Comments, request.Verification, RequestOrigins.From(http)),
                ct);

            // The new ETag, so the caller can keep editing without another read.
            return Preconditions.Ok(ProblemResponse.From(result.Problem), result.Problem.LastModified);
        }
        catch (ClinicalCareValidationException ex)
        {
            return ValidationProblems.Many(ex.Errors);
        }
        catch (ProblemNotFoundException ex)
        {
            return Results.Problem(title: "Problem not found.", detail: ex.Message, statusCode: StatusCodes.Status404NotFound);
        }
        catch (ConcurrencyConflictException ex)
        {
            return Preconditions.Failed(ex.CurrentStamp);
        }
    }

    private static async Task<IResult> GetProblem(Guid uuid, GetProblemUseCase useCase, CancellationToken ct)
    {
        var view = await useCase.ExecuteAsync(uuid, ct);

        // Also 404 for a row of another issue type (ADR 0070 #2): from here an allergy is not a problem.
        return view is null
            ? Results.Problem(
                title: "Problem not found.", detail: $"No problem with uuid {uuid} exists.", statusCode: StatusCodes.Status404NotFound)
            : Preconditions.Ok(ProblemResponse.From(view), view.LastModified);
    }

    private static async Task<IResult> ListProblems(
        Guid? patientUuid, int? limit, int? offset, ListPatientProblemsUseCase useCase, CancellationToken ct)
    {
        // A list is always one patient's: there is no "all problems" read, which would be unbounded (API-6).
        if (patientUuid is null || patientUuid == Guid.Empty)
            return ValidationProblems.Single("patientUuid is required.");

        try
        {
            var page = await useCase.ExecuteAsync(patientUuid.Value, limit, offset, ct);

            return Results.Ok(new ProblemListResponse(
                page.Items.Select(ProblemResponse.From).ToList(), page.Total, limit ?? PageRequest.DefaultLimit, offset ?? 0));
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
