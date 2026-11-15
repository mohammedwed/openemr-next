using System.ComponentModel.DataAnnotations;
using OpenEmrNext.PatientManagement.Domain.Insurance;

namespace OpenEmrNext.PatientManagement.Api.Contracts.Requests;

/// <summary>
/// The body of <c>POST /api/v1/patients/{pid}/insurance</c>. The bounds here mirror the field-level
/// rules <see cref="InsuranceCoverage.AddPolicy"/> enforces (<c>InsurancePolicy.Add</c>'s own checks,
/// internal to Domain) -- restated here to fail at the model-binding boundary, not to replace them.
/// </summary>
/// <param name="Level">"primary", "secondary", or "tertiary" -- matching legacy OpenEMR's own stored
/// values exactly. Parsed, not attribute-validated: no fixed-list attribute expresses this, so the
/// endpoint itself still rejects an unparseable value.</param>
public sealed record AddInsurancePolicyRequest(
    [Required(ErrorMessage = "Level is required.")] string Level,
    [Range(1, long.MaxValue, ErrorMessage = "Company must be a valid insurance company id.")] long CompanyId,
    [Required(ErrorMessage = "Policy number must be between 2 and 255 characters.")]
    [StringLength(255, MinimumLength = 2, ErrorMessage = "Policy number must be between 2 and 255 characters.")]
    string PolicyNumber,
    [StringLength(255, MinimumLength = 2, ErrorMessage = "Group number must be between 2 and 255 characters.")] string? GroupNumber,
    [StringLength(255, MinimumLength = 2, ErrorMessage = "Plan name must be between 2 and 255 characters.")] string? PlanName,
    DateOnly StartDate,
    DateOnly? EndDate);
