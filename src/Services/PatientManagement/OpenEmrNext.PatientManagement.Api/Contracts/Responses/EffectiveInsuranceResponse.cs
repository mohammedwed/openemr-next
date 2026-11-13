namespace OpenEmrNext.PatientManagement.Api.Contracts.Responses;

/// <summary>One policy in the body of <c>GET /api/v1/patients/{pid}/insurance/effective</c>.</summary>
public sealed record EffectiveInsuranceResponse(
    string Level, long Id, Guid? Uuid, long CompanyId, DateOnly? Start, DateOnly? End);
