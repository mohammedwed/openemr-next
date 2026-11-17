using OpenEmrNext.ClinicalCare.Domain.ReadModels;

namespace OpenEmrNext.ClinicalCare.Api.Contracts.Responses;

/// <summary>What <c>POST /api/v1/immunizations</c> returns.</summary>
public sealed record ImmunizationRecordedResponse(Guid Uuid, Guid PatientUuid);

/// <summary>One immunization as the API returns it. <paramref name="ETag"/> is its content version (ADR 0072), the same value as the header; send it in <c>If-Match</c> to correct it.</summary>
public sealed record ImmunizationResponse(
    Guid Uuid,
    Guid? PatientUuid,
    long? PatientPid,
    DateTime? AdministeredDate,
    int? ImmunizationId,
    string? CvxCode,
    string? Manufacturer,
    string? LotNumber,
    DateTime? ExpirationDate,
    decimal? AmountAdministered,
    string? AmountUnit,
    string? Route,
    string? AdministrationSite,
    string? CompletionStatus,
    string? InformationSource,
    string? RefusalReason,
    DateTime? EducationDate,
    DateTime? VisDate,
    string? AdministeredBy,
    long? AdministeredById,
    long? OrderingProvider,
    long? EncounterNumber,
    string? ReasonCode,
    string? ReasonDescription,
    string? Note,
    bool EnteredInError,
    DateTime? RecordedAt,
    string ETag)
{
    public static ImmunizationResponse From(ImmunizationView v) => new(
        v.Uuid, v.PatientUuid, v.PatientPid, v.AdministeredDate, v.ImmunizationId, v.CvxCode, v.Manufacturer, v.LotNumber, v.ExpirationDate, v.AmountAdministered,
        v.AmountUnit, v.Route, v.AdministrationSite, v.CompletionStatus, v.InformationSource, v.RefusalReason, v.EducationDate, v.VisDate, v.AdministeredBy,
        v.AdministeredById, v.OrderingProvider, v.EncounterNumber, v.ReasonCode, v.ReasonDescription, v.Note, v.EnteredInError, v.RecordedAt, v.Version().ToETag());
}

/// <summary>One bounded page of a patient's immunizations, most recently administered first.</summary>
public sealed record ImmunizationListResponse(IReadOnlyList<ImmunizationResponse> Items, int Total, int Limit, int Offset);
