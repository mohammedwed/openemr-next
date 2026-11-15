using System.ComponentModel.DataAnnotations;

namespace OpenEmrNext.PatientManagement.Api.Contracts.Requests;

/// <summary>
/// Body of <c>POST /api/v1/patients/{pid}/advance-directives/documents</c>. <c>ContentBase64</c>
/// carries the file bytes -- a JSON body, like every other endpoint in this service, rather than
/// multipart/form-data. The bounds here mirror
/// <see cref="OpenEmrNext.PatientManagement.Domain.AdvanceDirectives.AdvanceDirectiveDocument.Upload"/>'s
/// own rules -- restated here to fail at the model-binding boundary, not to replace the Domain
/// factory's own check (including its own, separate <c>MaxContentBytes</c> check on the *decoded*
/// bytes, which this attribute cannot express against a base64 string's own length).
/// </summary>
/// <param name="OwnerId">The id of the user uploading the document. Caller-supplied for now, not
/// verified against any authenticated session -- an already-accepted gap inherited from ADR 0048 (no
/// authentication wired into this service yet). Revisit once that gap closes.</param>
public sealed record UploadAdvanceDirectiveDocumentRequest(
    [Required(ErrorMessage = "Type is required.")] string Type,
    [Required(ErrorMessage = "File name must be between 1 and 255 characters.")]
    [StringLength(255, MinimumLength = 1, ErrorMessage = "File name must be between 1 and 255 characters.")]
    string FileName,
    [Required(ErrorMessage = "MimeType is required.")] string MimeType,
    [Required(ErrorMessage = "Content must not be empty.")] string ContentBase64,
    DateOnly? EffectiveDate,
    [Range(1, long.MaxValue, ErrorMessage = "OwnerId must identify the uploading user.")] long OwnerId);
