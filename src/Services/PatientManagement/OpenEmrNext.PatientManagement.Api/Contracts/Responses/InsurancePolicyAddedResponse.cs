namespace OpenEmrNext.PatientManagement.Api.Contracts.Responses;

/// <summary>The body of a successful <c>POST /api/v1/patients/{pid}/insurance</c>.</summary>
public sealed record InsurancePolicyAddedResponse(
    Guid Uuid, long Id, string Level, long CompanyId, string PolicyNumber, DateOnly StartDate, DateOnly? EndDate);
