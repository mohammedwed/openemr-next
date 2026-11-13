namespace OpenEmrNext.PatientManagement.Api.Contracts.Requests;

/// <summary>The body of <c>POST /api/v1/patients/{pid}/insurance</c>.</summary>
/// <param name="Level">"primary", "secondary", or "tertiary" -- matching legacy OpenEMR's own stored values exactly.</param>
public sealed record AddInsurancePolicyRequest(
    string Level,
    long CompanyId,
    string PolicyNumber,
    string? GroupNumber,
    string? PlanName,
    DateOnly StartDate,
    DateOnly? EndDate);
