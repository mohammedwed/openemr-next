using OpenEmrNext.PatientManagement.Domain.AdvanceDirectives.ValueObjects;

namespace OpenEmrNext.PatientManagement.Domain.AdvanceDirectives.ReadModels;

/// <summary>
/// One row as read back from <c>documents</c>, already classified via
/// <see cref="AdvanceDirectiveTypeExtensions.ClassifyFromName"/> -- <c>Type</c> is <c>null</c> for a
/// pre-existing legacy row tagged under the "Advance Directive" category that doesn't match any of the
/// three keyword patterns, legacy's own generic "Advance Directive" bucket.
/// </summary>
public sealed record AdvanceDirectiveDocumentSummary(
    long Id, Guid Uuid, AdvanceDirectiveType? Type, string Name, string MimeType,
    int Size, DateOnly? EffectiveDate, long? OwnerId);
