namespace OpenEmrNext.ClinicalCare.Domain.ValueObjects;

/// <summary>One entry of legacy's <c>_ActEncounterCode</c> list (<c>list_options</c>): a class of encounter.</summary>
/// <param name="Code">The list's <c>option_id</c>, for example <c>AMB</c>. Stored in <c>form_encounter.class_code</c> (varchar 10).</param>
/// <param name="Title">The display title, for example <c>Outpatient</c>.</param>
/// <param name="IsDefault">Whether legacy flags it as the default (<c>is_default</c>); <c>AMB</c> in the snapshot.</param>
public sealed record EncounterClassOption(string Code, string Title, bool IsDefault);

/// <summary>A legacy <c>facility</c> row, reduced to what an encounter copies from it.</summary>
/// <param name="Id">The facility id.</param>
/// <param name="Name">The name legacy copies into <c>form_encounter.facility</c> (denormalized).</param>
/// <param name="PosCode">The facility's place-of-service code, the default for the encounter (<c>facility.pos_code</c>).</param>
public sealed record FacilityOption(int Id, string Name, int? PosCode);

/// <summary>
/// What <see cref="EncounterCreation.Create"/> needs to know about the outside world, read beforehand by the
/// use case through ports so the domain stays a pure function of its inputs (DOM-12, DOM-19) and can still
/// report every problem in one pass (DOM-3). Reference data, not a rule-bearing type: it mirrors rows legacy
/// already holds, so it has no factory and is read tolerantly (DOM-9).
/// </summary>
/// <param name="Classes">Every entry of <c>_ActEncounterCode</c>, in list order.</param>
/// <param name="Facility">The facility named by the request, or <c>null</c> when none was named or it does not exist.</param>
public sealed record EncounterReferenceData(IReadOnlyList<EncounterClassOption> Classes, FacilityOption? Facility);
