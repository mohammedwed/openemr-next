using System.ComponentModel.DataAnnotations;

namespace OpenEmrNext.PatientManagement.Api.Contracts.Requests;

/// <summary>The body of <c>POST /api/v1/patients/{pid}/insurance/swap</c>.</summary>
/// <param name="TargetLevel">The level <paramref name="PolicyUuid"/> should move to. Parsed, not
/// attribute-validated: no fixed-list attribute expresses "primary/secondary/tertiary", so the
/// endpoint itself still rejects an unparseable value.</param>
/// <param name="PolicyUuid">The policy being moved. Whatever currently occupies <paramref name="TargetLevel"/> trades places with it.</param>
public sealed record SwapInsuranceRequest(
    [Required(ErrorMessage = "TargetLevel is required.")] string TargetLevel, Guid PolicyUuid);
