namespace OpenEmrNext.ClinicalCare.Domain.ReadModels;

/// <summary>One choice in a configured list: the id to send and the label to show.</summary>
public sealed record LookupOption(string Id, string Title);

/// <summary>
/// The choices for an immunization's coded fields (legacy lists <c>immunizations</c>, <c>drug_route</c>, <c>immunization_administered_site</c>, <c>Immunization_Completion_Status</c>,
/// <c>immunization_informationsource</c>, <c>immunization_refusal_reason</c> and <c>drug_units</c>), the active options in legacy's order. A vaccine's id is the numeric <c>option_id</c> legacy keeps in an <c>int</c> column.
/// </summary>
public sealed record ImmunizationOptions(
    IReadOnlyList<LookupOption> Vaccines, IReadOnlyList<LookupOption> Routes, IReadOnlyList<LookupOption> Sites, IReadOnlyList<LookupOption> CompletionStatuses,
    IReadOnlyList<LookupOption> InformationSources, IReadOnlyList<LookupOption> RefusalReasons, IReadOnlyList<LookupOption> Units);
