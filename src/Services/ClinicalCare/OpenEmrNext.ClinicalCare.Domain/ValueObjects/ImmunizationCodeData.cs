namespace OpenEmrNext.ClinicalCare.Domain.ValueObjects;

/// <summary>
/// The configured code lists an immunization's coded fields are checked against, read beforehand by the use case so the domain does no I/O (DOM-7). The vaccine list holds the numeric <c>option_id</c>s legacy stores in
/// the <c>int</c> column <c>immunization_id</c>; every other list holds the exact string option ids its column stores, compared exactly.
/// </summary>
/// <param name="Vaccines">Valid <c>immunizations.immunization_id</c> codes (list <c>immunizations</c>).</param>
/// <param name="Routes">Valid <c>route</c> codes (list <c>drug_route</c>).</param>
/// <param name="Sites">Valid <c>administration_site</c> codes (list <c>immunization_administered_site</c>).</param>
/// <param name="CompletionStatuses">Valid <c>completion_status</c> codes (list <c>Immunization_Completion_Status</c>, whose id is capitalised).</param>
/// <param name="InformationSources">Valid <c>information_source</c> codes (list <c>immunization_informationsource</c>).</param>
/// <param name="RefusalReasons">Valid <c>refusal_reason</c> codes (list <c>immunization_refusal_reason</c>).</param>
/// <param name="Units">Valid <c>amount_administered_unit</c> codes (list <c>drug_units</c>).</param>
public sealed record ImmunizationCodeData(
    IReadOnlySet<int> Vaccines, IReadOnlySet<string> Routes, IReadOnlySet<string> Sites, IReadOnlySet<string> CompletionStatuses,
    IReadOnlySet<string> InformationSources, IReadOnlySet<string> RefusalReasons, IReadOnlySet<string> Units);
