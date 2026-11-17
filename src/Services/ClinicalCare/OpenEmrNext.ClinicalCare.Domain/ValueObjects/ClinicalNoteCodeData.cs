namespace OpenEmrNext.ClinicalCare.Domain.ValueObjects;

/// <summary>A configured clinical note type (legacy list <c>Clinical_Note_Type</c>): its id, label, the LOINC code legacy keeps in the option's <c>notes</c> column, and whether it is still offered.</summary>
public sealed record ClinicalNoteTypeOption(string Id, string Title, string Code, bool Active);

/// <summary>A configured clinical note category (legacy list <c>Clinical_Note_Category</c>).</summary>
public sealed record ClinicalNoteCategoryOption(string Id, string Title, bool Active);

/// <summary>
/// The configured lists a clinical note's coded fields are checked against, in legacy's <c>seq</c> order, read beforehand by the use case so the domain does no I/O (DOM-7). As elsewhere in this service,
/// validation accepts any configured option whatever its <c>activity</c> (legacy's own validation filters on it nowhere); only the lookups offered to a UI are limited to active ones.
/// </summary>
public sealed record ClinicalNoteCodeData(IReadOnlyList<ClinicalNoteTypeOption> Types, IReadOnlyList<ClinicalNoteCategoryOption> Categories)
{
    public ClinicalNoteTypeOption? FindType(string id) => Types.FirstOrDefault(t => string.Equals(t.Id, id, StringComparison.Ordinal));

    public ClinicalNoteCategoryOption? FindCategory(string id) => Categories.FirstOrDefault(c => string.Equals(c.Id, id, StringComparison.Ordinal));
}
