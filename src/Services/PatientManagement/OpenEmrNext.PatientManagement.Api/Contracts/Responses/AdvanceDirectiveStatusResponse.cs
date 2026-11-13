namespace OpenEmrNext.PatientManagement.Api.Contracts.Responses;

public sealed record AdvanceDirectiveStatusResponse(string Status, DateTime? ReviewedAtUtc, long? AuthenticatorUserId);
