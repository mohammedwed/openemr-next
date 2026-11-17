using System.Globalization;
using OpenEmrNext.ClinicalCare.Domain.ValueObjects;

namespace OpenEmrNext.ClinicalCare.Domain.ReadModels;

/// <summary>
/// An immunization as a caller reads it: a query result, not an aggregate (ADR 0054), built tolerantly from whatever a legacy row holds (DOM-9, LEG-4). Blank text reads as <c>null</c>.
/// </summary>
/// <param name="Uuid">The public identity. A row without one is not addressable and never becomes a view (ADR 0070 #6).</param>
/// <param name="EncounterNumber">The linked encounter's number (what the column stores), or <c>null</c>.</param>
/// <param name="RecordedAt">When the row was created (<c>create_date</c>).</param>
public sealed record ImmunizationView(
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
    DateTime? RecordedAt)
{
    /// <summary>
    /// The row's concurrency version (ADR 0072): a hash of every <b>owned</b> column, so a change by any writer to any of them changes it. <see cref="Uuid"/>, the patient and <see cref="RecordedAt"/>
    /// are deliberately not in it: nothing here can change them. A test pins that every other property is.
    /// </summary>
    public ContentVersion Version() => ContentVersion.Of(
    [
        Ticks(AdministeredDate), Number(ImmunizationId), CvxCode, Manufacturer, LotNumber, Ticks(ExpirationDate),
        AmountAdministered?.ToString("0.####", CultureInfo.InvariantCulture), AmountUnit, Route, AdministrationSite, CompletionStatus, InformationSource,
        RefusalReason, Ticks(EducationDate), Ticks(VisDate), AdministeredBy, Number(AdministeredById), Number(OrderingProvider), Number(EncounterNumber),
        ReasonCode, ReasonDescription, Note, EnteredInError ? "1" : "0"
    ]);

    private static string? Ticks(DateTime? value) => value?.Ticks.ToString(CultureInfo.InvariantCulture);

    private static string? Number(long? value) => value?.ToString(CultureInfo.InvariantCulture);
}

/// <summary>One page of a patient's immunizations, most recently administered first, with the total.</summary>
public sealed record ImmunizationPage(IReadOnlyList<ImmunizationView> Items, int Total);
