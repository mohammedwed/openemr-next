using OpenEmrNext.ClinicalCare.Api.Contracts.Requests;
using OpenEmrNext.ClinicalCare.Api.Contracts.Responses;
using OpenEmrNext.ClinicalCare.Application;
using OpenEmrNext.ClinicalCare.Domain.ReadModels;
using OpenEmrNext.ClinicalCare.Domain.ValueObjects;

namespace OpenEmrNext.ClinicalCare.Api.Endpoints;

/// <summary>
/// Endpoints for the problems and allergies an encounter addressed (M5c slice 4, ADR 0070). They live under the encounter, which the gateway already routes to this service, and
/// take the issue's uuid in the path: the link is a relation between two resources that already exist, not a resource of its own.
/// </summary>
public static class EncounterIssueEndpoints
{
    public static IEndpointRouteBuilder MapEncounterIssueEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapPut("/encounters/{encounterUuid:guid}/issues/{issueUuid:guid}", LinkIssue)
            .WithName("LinkIssueToEncounter")
            .WithTags("Encounter issues")
            .WithSummary("Link a problem or an allergy to an encounter that addressed it (idempotent)")
            .Produces<EncounterIssueLinkResponse>(StatusCodes.Status200OK)
            .Produces<EncounterIssueLinkResponse>(StatusCodes.Status201Created)
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status404NotFound);

        app.MapDelete("/encounters/{encounterUuid:guid}/issues/{issueUuid:guid}", UnlinkIssue)
            .WithName("UnlinkIssueFromEncounter")
            .WithTags("Encounter issues")
            .WithSummary("Remove the link between an encounter and an issue (the issue and the encounter are untouched; idempotent)")
            .Produces(StatusCodes.Status204NoContent)
            .ProducesProblem(StatusCodes.Status404NotFound);

        app.MapGet("/encounters/{encounterUuid:guid}/issues", ListIssues)
            .WithName("ListEncounterIssues")
            .WithTags("Encounter issues")
            .WithSummary("List the problems and allergies linked to an encounter, one bounded page at a time")
            .Produces<EncounterIssueListResponse>()
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status404NotFound);

        return app;
    }

    private static async Task<IResult> LinkIssue(
        Guid encounterUuid, Guid issueUuid, LinkIssueRequest? request, LinkIssueToEncounterUseCase useCase, HttpContext http, CancellationToken ct)
    {
        try
        {
            var result = await useCase.ExecuteAsync(
                new LinkIssueToEncounterCommand(encounterUuid, issueUuid, request?.Resolved ?? false, RequestOrigins.From(http)), ct);

            var body = new EncounterIssueLinkResponse(result.LinkUuid, result.EncounterUuid, result.IssueUuid, result.Resolved);

            return result.Created
                ? Results.Created($"/api/v1/encounters/{encounterUuid}/issues", body)
                : Results.Ok(body);
        }
        catch (ClinicalCareValidationException ex)
        {
            return ValidationProblems.Many(ex.Errors);
        }
        catch (EncounterNotFoundException ex)
        {
            return Results.Problem(title: "Encounter not found.", detail: ex.Message, statusCode: StatusCodes.Status404NotFound);
        }
        catch (IssueNotFoundException ex)
        {
            return Results.Problem(title: "Issue not found.", detail: ex.Message, statusCode: StatusCodes.Status404NotFound);
        }
    }

    private static async Task<IResult> UnlinkIssue(
        Guid encounterUuid, Guid issueUuid, UnlinkIssueFromEncounterUseCase useCase, HttpContext http, CancellationToken ct)
    {
        try
        {
            // 204 whether or not there was a link to remove: the caller's intent, that the two are not linked, holds either way.
            await useCase.ExecuteAsync(new UnlinkIssueFromEncounterCommand(encounterUuid, issueUuid, RequestOrigins.From(http)), ct);
            return Results.NoContent();
        }
        catch (EncounterNotFoundException ex)
        {
            return Results.Problem(title: "Encounter not found.", detail: ex.Message, statusCode: StatusCodes.Status404NotFound);
        }
        catch (IssueNotFoundException ex)
        {
            return Results.Problem(title: "Issue not found.", detail: ex.Message, statusCode: StatusCodes.Status404NotFound);
        }
    }

    private static async Task<IResult> ListIssues(
        Guid encounterUuid, int? limit, int? offset, ListEncounterIssuesUseCase useCase, CancellationToken ct)
    {
        try
        {
            var page = await useCase.ExecuteAsync(encounterUuid, limit, offset, ct);

            return Results.Ok(new EncounterIssueListResponse(
                page.Items.Select(EncounterIssueResponse.From).ToList(), page.Total, limit ?? PageRequest.DefaultLimit, offset ?? 0));
        }
        catch (ClinicalCareValidationException ex)
        {
            return ValidationProblems.Many(ex.Errors);
        }
        catch (EncounterNotFoundException ex)
        {
            return Results.Problem(title: "Encounter not found.", detail: ex.Message, statusCode: StatusCodes.Status404NotFound);
        }
    }
}
