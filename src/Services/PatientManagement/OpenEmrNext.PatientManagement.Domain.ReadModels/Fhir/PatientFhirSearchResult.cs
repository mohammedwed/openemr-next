namespace OpenEmrNext.PatientManagement.Domain.Fhir.ReadModels;

/// <summary>One bounded page of a patient search, with the number of patients that matched in all, so a caller can tell a
/// complete answer from a page of it.</summary>
public sealed record PatientFhirSearchResult(IReadOnlyList<PatientFhirView> Items, int Total);
