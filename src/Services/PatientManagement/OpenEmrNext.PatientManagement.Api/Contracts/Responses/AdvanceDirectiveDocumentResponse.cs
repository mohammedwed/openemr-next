namespace OpenEmrNext.PatientManagement.Api.Contracts.Responses;

/// <summary>One advance-directive document, in the upload response and the listing. <c>Type</c> is
/// <c>null</c> for a pre-existing legacy document that doesn't match any of the three keyword patterns
/// (legacy's own generic "Advance Directive" bucket).</summary>
public sealed record AdvanceDirectiveDocumentResponse(
    long Id, Guid Uuid, string? Type, string Name, string MimeType, int Size,
    DateOnly? EffectiveDate, long? OwnerId);
