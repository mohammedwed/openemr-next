using System.Text;
using OpenEmrNext.ClinicalCare.Domain.ValueObjects;

namespace OpenEmrNext.ClinicalCare.Domain.ClinicalNotes;

/// <summary>What a caller supplies to record or correct a clinical note, raw and unchecked: <see cref="ClinicalNoteFacts.Create"/> owns the rules.</summary>
public sealed record ClinicalNoteInput(DateTime? Date, string? Type, string? Category, string? Description);

/// <summary>
/// The validated, cleaned facts of a clinical note, in the form they are stored. <see cref="Create"/> is the only way to get one, so recording and correcting a note cannot disagree about what is valid (one
/// owner per rule, DOM-2), and every violation is collected before failing (DOM-3). Rules CN-1 to CN-4, docs/clinical-care/19-clinical-notes-slice.md.
/// </summary>
/// <remarks>
/// <see cref="Code"/> and <see cref="CodeText"/> are <b>derived from the type</b> (legacy's own form copies them from the chosen type with script), so a note cannot carry a code that disagrees with its type.
/// </remarks>
public sealed record ClinicalNoteFacts
{
    public const int MinDescriptionLength = 2;

    /// <summary><c>description</c> is <c>text</c>: 65,535 <b>bytes</b>, not characters (review findings R-1 to R-7).</summary>
    public const int MaxDescriptionBytes = 65_535;

    /// <summary>The widths of the columns that hold an option id and a code.</summary>
    public const int MaxTypeLength = 100, MaxCategoryLength = 100, MaxCodeLength = 255;

    public static readonly DateTime MinDate = new(1900, 1, 1);
    public static readonly TimeSpan MaxFutureSlack = TimeSpan.FromDays(1);

    public DateTime Date { get; }
    public string Type { get; }
    public string? Category { get; }
    public string Description { get; }
    public string Code { get; }
    public string CodeText { get; }

    private ClinicalNoteFacts(DateTime date, string type, string? category, string description, string code, string codeText)
    {
        Date = date;
        Type = type;
        Category = category;
        Description = description;
        Code = code;
        CodeText = codeText;
    }

    /// <exception cref="ClinicalCareValidationException">One or more fields fail validation.</exception>
    public static ClinicalNoteFacts Create(ClinicalNoteInput input, DateTime now, ClinicalNoteCodeData codes)
    {
        ArgumentNullException.ThrowIfNull(input);
        ArgumentNullException.ThrowIfNull(codes);

        var errors = new List<string>();

        // CN-1: the note itself. Kept exactly as written (clinicians format notes), but needs two characters of substance.
        string description = "";
        if (string.IsNullOrWhiteSpace(input.Description))
            errors.Add("A note is required.");
        else if (input.Description.Trim().Length < MinDescriptionLength)
            errors.Add($"Description must be at least {MinDescriptionLength} characters.");
        else if (Encoding.UTF8.GetByteCount(input.Description) > MaxDescriptionBytes)
            errors.Add($"Description must be at most {MaxDescriptionBytes} bytes.");
        else
            description = input.Description;

        // CN-2: a note has a type, a configured one, and its code is derived from it.
        ClinicalNoteTypeOption? type = null;
        if (string.IsNullOrWhiteSpace(input.Type))
            errors.Add("A note type is required.");
        else if ((type = codes.FindType(input.Type)) is null)
            errors.Add($"Type '{input.Type}' is not a known note type.");
        else if (type.Id.Length > MaxTypeLength)
            errors.Add($"Type '{type.Id}' is longer than the {MaxTypeLength} characters a record can hold.");
        else if (type.Code.Length > MaxCodeLength)
            errors.Add($"The configured code of type '{type.Id}' is longer than the {MaxCodeLength} characters a record can hold.");

        // CN-3: a category is optional; if given it is a configured one.
        string? category = null;
        if (!string.IsNullOrWhiteSpace(input.Category))
        {
            var found = codes.FindCategory(input.Category);
            if (found is null)
                errors.Add($"Category '{input.Category}' is not a known note category.");
            else if (found.Id.Length > MaxCategoryLength)
                errors.Add($"Category '{found.Id}' is longer than the {MaxCategoryLength} characters a record can hold.");
            else
                category = found.Id;
        }

        // CN-4: the note's date is a calendar date, not in the future (a day of slack covers zones).
        if (input.Date is not { } date)
            errors.Add("A note date is required.");
        else if (date < MinDate || date > now + MaxFutureSlack)
            errors.Add($"Date must be between {MinDate:yyyy-MM-dd} and tomorrow.");

        if (errors.Count > 0)
            throw new ClinicalCareValidationException(errors);

        return new ClinicalNoteFacts(input.Date!.Value.Date, type!.Id, category, description, type.Code, type.Title);
    }
}

/// <summary>The identity a new clinical note is given: its uuid, handed in rather than invented inside the domain (DOM-7).</summary>
public sealed record ClinicalNoteIdentity
{
    public Guid Uuid { get; }

    private ClinicalNoteIdentity(Guid uuid) => Uuid = uuid;

    public static ClinicalNoteIdentity Create(Guid uuid) =>
        uuid == Guid.Empty
            ? throw new ClinicalCareValidationException(["The clinical note identity must have a uuid."])
            : new ClinicalNoteIdentity(uuid);
}

/// <summary>
/// A clinical note on an encounter: one row of legacy's <c>form_clinical_notes</c>, grouped under a parent form registered in <c>forms</c> (M5d slice 3b, ADR 0074). The aggregate root of Clinical Care's clinical
/// note slice. <see cref="Uuid"/> is its public identity; <see cref="Id"/> is the row key, <c>0</c> until the database assigns it. A correction replaces the stored facts through its own use case (DOM-5).
/// </summary>
public sealed class ClinicalNote
{
    public long Id { get; private set; }
    public Guid Uuid { get; }
    public EncounterReference Encounter { get; }
    public ClinicalNoteFacts Facts { get; }
    public long ProviderId { get; }

    public const long MaxProviderId = int.MaxValue;

    private ClinicalNote(EncounterReference encounter, ClinicalNoteFacts facts, long providerId, ClinicalNoteIdentity identity)
    {
        Uuid = identity.Uuid;
        Encounter = encounter;
        Facts = facts;
        ProviderId = providerId;
    }

    /// <exception cref="ClinicalCareValidationException">The provider id is not a user id this service can use.</exception>
    public static ClinicalNote Record(EncounterReference encounter, ClinicalNoteFacts facts, long providerId, ClinicalNoteIdentity identity)
    {
        ArgumentNullException.ThrowIfNull(encounter);
        ArgumentNullException.ThrowIfNull(facts);

        if (providerId is <= 0 or > MaxProviderId)
            throw new ClinicalCareValidationException(["ProviderId must identify the acting provider."]);

        return new ClinicalNote(encounter, facts, providerId, identity);
    }

    /// <summary>Called only by the Infrastructure adapter after the insert, to record the database-assigned key (the narrow, internal exception, DOM-5).</summary>
    internal void AssignGeneratedId(long id)
    {
        if (Id != 0)
            throw new InvalidOperationException("The clinical note's database id has already been assigned.");

        Id = id;
    }
}
