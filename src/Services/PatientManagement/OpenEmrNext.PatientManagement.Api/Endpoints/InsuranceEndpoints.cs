using OpenEmrNext.PatientManagement.Api.Contracts.Requests;
using OpenEmrNext.PatientManagement.Api.Contracts.Responses;
using OpenEmrNext.PatientManagement.Application;
using OpenEmrNext.PatientManagement.Domain.Insurance;

namespace OpenEmrNext.PatientManagement.Api.Endpoints;

/// <summary>Endpoints for a patient's insurance policies.</summary>
public static class InsuranceEndpoints
{
    public static IEndpointRouteBuilder MapInsuranceEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapPost("/patients/{pid:long}/insurance", AddInsurancePolicy)
            .WithName("AddInsurancePolicy")
            .WithTags("Insurance")
            .WithSummary("Add a new insurance policy for a patient")
            .Produces<InsurancePolicyAddedResponse>(StatusCodes.Status201Created)
            .ProducesValidationProblem();

        app.MapGet("/patients/{pid:long}/insurance/effective", GetEffectiveInsurance)
            .WithName("GetEffectiveInsurance")
            .WithTags("Insurance")
            .WithSummary("Get the insurance policy in force at each level, on a given day")
            .Produces<IReadOnlyList<EffectiveInsuranceResponse>>();

        app.MapPost("/patients/{pid:long}/insurance/swap", SwapInsurance)
            .WithName("SwapInsurance")
            .WithTags("Insurance")
            .WithSummary("Swap which level a policy occupies with whatever currently occupies the target level")
            .Produces(StatusCodes.Status204NoContent)
            .Produces(StatusCodes.Status404NotFound)
            .ProducesValidationProblem();

        return app;
    }

    private static async Task<IResult> AddInsurancePolicy(
        long pid, AddInsurancePolicyRequest request, AddInsurancePolicyUseCase useCase, CancellationToken ct)
    {
        var level = InsuranceLevelExtensions.TryParse(request.Level);
        if (level is null)
        {
            return Results.ValidationProblem(new Dictionary<string, string[]>
            {
                ["errors"] = ["Level must be \"primary\", \"secondary\", or \"tertiary\"."]
            });
        }

        InsurancePolicy policy;
        try
        {
            policy = await useCase.ExecuteAsync(
                pid, level.Value, request.CompanyId, request.PolicyNumber,
                request.GroupNumber, request.PlanName, request.StartDate, request.EndDate, ct);
        }
        catch (InsurancePolicyValidationException ex)
        {
            return Results.ValidationProblem(new Dictionary<string, string[]> { ["errors"] = [.. ex.Errors] });
        }

        return Results.Created(
            $"/api/v1/patients/{pid}/insurance/{policy.Uuid}",
            new InsurancePolicyAddedResponse(
                policy.Uuid, policy.Id, policy.Level.ToWireValue(), policy.CompanyId,
                policy.PolicyNumber, policy.StartDate, policy.EndDate));
    }

    private static async Task<IResult> GetEffectiveInsurance(
        long pid, DateOnly? day, GetEffectiveInsuranceUseCase useCase, CancellationToken ct)
    {
        var onDay = day ?? DateOnly.FromDateTime(DateTime.UtcNow);
        var effective = await useCase.ExecuteAsync(pid, onDay, ct);

        return Results.Ok(effective
            .Select(p => new EffectiveInsuranceResponse(p.Level.ToWireValue(), p.Id, p.Uuid, p.CompanyId, p.Start, p.End))
            .ToList());
    }

    private static async Task<IResult> SwapInsurance(
        long pid, SwapInsuranceRequest request, SwapInsuranceUseCase useCase, CancellationToken ct)
    {
        var level = InsuranceLevelExtensions.TryParse(request.TargetLevel);
        if (level is null)
        {
            return Results.ValidationProblem(new Dictionary<string, string[]>
            {
                ["errors"] = ["TargetLevel must be \"primary\", \"secondary\", or \"tertiary\"."]
            });
        }

        try
        {
            await useCase.ExecuteAsync(pid, level.Value, request.PolicyUuid, ct);
        }
        catch (KeyNotFoundException ex)
        {
            return Results.NotFound(ex.Message);
        }

        return Results.NoContent();
    }
}
