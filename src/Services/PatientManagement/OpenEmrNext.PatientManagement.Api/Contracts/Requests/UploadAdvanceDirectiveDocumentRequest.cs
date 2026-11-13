namespace OpenEmrNext.PatientManagement.Api.Contracts.Requests;

/// <summary>Body of <c>POST /api/v1/patients/{pid}/advance-directives/documents</c>. <c>ContentBase64</c>
/// carries the file bytes -- a JSON body, like every other endpoint in this service, rather than
/// multipart/form-data.</summary>
public sealed record UploadAdvanceDirectiveDocumentRequest(
    string Type, string FileName, string MimeType, string ContentBase64, DateOnly? EffectiveDate, long OwnerId);
