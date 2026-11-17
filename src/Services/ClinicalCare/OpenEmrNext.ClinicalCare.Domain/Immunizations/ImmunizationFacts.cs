using System.Text;
using OpenEmrNext.ClinicalCare.Domain.ValueObjects;

namespace OpenEmrNext.ClinicalCare.Domain.Immunizations;

/// <summary>
/// What a caller supplies to record or correct an immunization, raw and unchecked: <see cref="ImmunizationFacts.Create"/> owns the rules. Dates are calendar dates unless noted; every coded field is the option id of
/// a legacy list, compared exactly (research doc 14, section 4).
/// </summary>
/// <param name="EncounterNumber">The encounter's number and patient, already resolved by the use case from the encounter's uuid, or <c>null</c> for none.</param>
public sealed record ImmunizationInput(
    DateTime? AdministeredDate,
    int? ImmunizationId = null,
    string? CvxCode = null,
    string? Manufacturer = null,
    string? LotNumber = null,
    DateTime? ExpirationDate = null,
    decimal? AmountAdministered = null,
    string? AmountUnit = null,
    string? Route = null,
    string? AdministrationSite = null,
    string? CompletionStatus = null,
    string? InformationSource = null,
    string? RefusalReason = null,
    DateTime? EducationDate = null,
    DateTime? VisDate = null,
    string? AdministeredBy = null,
    long? AdministeredById = null,
    long? OrderingProvider = null,
    ImmunizationEncounter? Encounter = null,
    string? ReasonCode = null,
    string? ReasonDescription = null,
    string? Note = null,
    bool EnteredInError = false);

/// <summary>The encounter an immunization is linked to, as the rules need it: its number (what the column stores) and the patient it is for.</summary>
public sealed record ImmunizationEncounter(long Number, long PatientPid);

/// <summary>
/// The validated, cleaned facts of an immunization: the whole editable set, in the form it is stored. <see cref="Create"/> is the only way to get one, so a recording, a stored immunization and a correction can never
/// disagree about what is valid (one owner per rule, DOM-2), and every violation is collected before failing (DOM-3). Rules IM-1 to IM-8 as in docs/clinical-care/15-record-immunization-slice.md.
/// </summary>
public sealed record ImmunizationFacts
{
    // Column widths (the lesson of review findings R-1 to R-7: a value the column cannot hold must be a clean message, not a database error).
    public const int MaxCvxLength = 64, MaxManufacturerLength = 100, MaxLotLength = 50, MaxAdministeredByLength = 255, MaxReasonCodeLength = 31;
    public const int MaxRouteLength = 100, MaxSiteLength = 100, MaxUnitLength = 50, MaxCompletionLength = 50, MaxSourceLength = 31, MaxRefusalLength = 31;

    /// <summary><c>note</c> and <c>reason_description</c> are <c>text</c>: 65,535 <b>bytes</b>, not characters, so a note of multi-byte characters is checked by its UTF-8 size.</summary>
    public const int MaxTextBytes = 65_535;

    /// <summary>The largest id a signed 32-bit column holds (<c>immunization_id</c> and <c>ordering_provider</c> are <c>int(11)</c>).</summary>
    public const long MaxIntColumn = int.MaxValue;

    public const decimal MaxAmount = 100_000m;

    public static readonly DateTime MinDate = new(1900, 1, 1);
    public static readonly TimeSpan MaxFutureSlack = TimeSpan.FromDays(1);
    public static readonly TimeSpan MaxExpirationAhead = TimeSpan.FromDays(366 * 50);

    public DateTime AdministeredDate { get; }
    public int? ImmunizationId { get; }
    public string? CvxCode { get; }
    public string? Manufacturer { get; }
    public string? LotNumber { get; }
    public DateTime? ExpirationDate { get; }
    public decimal? AmountAdministered { get; }
    public string? AmountUnit { get; }
    public string? Route { get; }
    public string? AdministrationSite { get; }
    public string? CompletionStatus { get; }
    public string? InformationSource { get; }
    public string? RefusalReason { get; }
    public DateTime? EducationDate { get; }
    public DateTime? VisDate { get; }
    public string? AdministeredBy { get; }
    public long? AdministeredById { get; }
    public long? OrderingProvider { get; }
    public long? EncounterNumber { get; }
    public string? ReasonCode { get; }
    public string? ReasonDescription { get; }
    public string? Note { get; }
    public bool EnteredInError { get; }

    private ImmunizationFacts(
        DateTime administeredDate, ImmunizationInput i, string? cvx, string? manufacturer, string? lot, decimal? amount, string? unit, string? route, string? site,
        string? completion, string? source, string? refusal, string? administeredBy, string? reasonCode, string? reasonDescription, string? note)
    {
        AdministeredDate = administeredDate;
        ImmunizationId = i.ImmunizationId;
        CvxCode = cvx;
        Manufacturer = manufacturer;
        LotNumber = lot;
        ExpirationDate = i.ExpirationDate?.Date;
        AmountAdministered = amount;
        AmountUnit = unit;
        Route = route;
        AdministrationSite = site;
        CompletionStatus = completion;
        InformationSource = source;
        RefusalReason = refusal;
        EducationDate = i.EducationDate?.Date;
        VisDate = i.VisDate?.Date;
        AdministeredBy = administeredBy;
        AdministeredById = i.AdministeredById;
        OrderingProvider = i.OrderingProvider;
        EncounterNumber = i.Encounter?.Number;
        ReasonCode = reasonCode;
        ReasonDescription = reasonDescription;
        Note = note;
        EnteredInError = i.EnteredInError;
    }

    /// <param name="input">What the caller supplied.</param>
    /// <param name="patientPid">The patient the immunization is for, to check a linked encounter is theirs (IM-7).</param>
    /// <param name="now">The current instant, from the clock (DOM-8).</param>
    /// <param name="codes">The configured code lists, read beforehand by the use case.</param>
    /// <exception cref="ClinicalCareValidationException">One or more fields fail validation.</exception>
    public static ImmunizationFacts Create(ImmunizationInput input, long patientPid, DateTime now, ImmunizationCodeData codes)
    {
        ArgumentNullException.ThrowIfNull(input);
        ArgumentNullException.ThrowIfNull(codes);

        var errors = new List<string>();

        // IM-1: an administration is in the past; a day of slack covers zones.
        if (input.AdministeredDate is not { } administered)
            errors.Add("An administered date is required.");
        else if (administered < MinDate || administered > now + MaxFutureSlack)
            errors.Add($"Administered date must be between {MinDate:yyyy-MM-dd} and tomorrow.");

        // IM-2 and IM-3: which vaccine, by a configured code or a CVX code (at least one).
        var cvx = Text(input.CvxCode);
        if (input.ImmunizationId is null && cvx is null)
            errors.Add("Identify the vaccine: give an immunization code or a CVX code.");
        if (input.ImmunizationId is { } vaccine && (vaccine <= 0 || vaccine > MaxIntColumn || !codes.Vaccines.Contains(vaccine)))
            errors.Add($"Immunization {vaccine} is not a known immunization.");
        if (cvx is { Length: > MaxCvxLength })
            errors.Add($"CVX code must be at most {MaxCvxLength} characters.");

        // IM-4: text that must fit its column.
        var manufacturer = Bounded(input.Manufacturer, MaxManufacturerLength, "Manufacturer", errors);
        var lot = Bounded(input.LotNumber, MaxLotLength, "Lot number", errors);
        var administeredBy = Bounded(input.AdministeredBy, MaxAdministeredByLength, "Administered by", errors);
        var reasonCode = Bounded(input.ReasonCode, MaxReasonCodeLength, "Reason code", errors);
        var reasonDescription = Bytes(input.ReasonDescription, "Reason description", errors);
        var note = Bytes(input.Note, "Note", errors);

        // IM-5: an amount is plausible, rounded to what the 4-byte float column can faithfully hold, and has a unit.
        decimal? amount = null;
        if (input.AmountAdministered is { } given)
        {
            if (given <= 0m || given > MaxAmount)
                errors.Add($"Amount administered must be greater than 0 and at most {MaxAmount:0}.");
            else
                amount = Math.Round(given, 4, MidpointRounding.AwayFromZero);
        }

        var unit = Code(input.AmountUnit, "Amount unit", codes.Units, MaxUnitLength, errors);
        if (amount is not null && unit is null)
            errors.Add("An amount needs a unit.");

        // IM-3: the other coded fields.
        var route = Code(input.Route, "Route", codes.Routes, MaxRouteLength, errors);
        var site = Code(input.AdministrationSite, "Administration site", codes.Sites, MaxSiteLength, errors);
        var completion = Code(input.CompletionStatus, "Completion status", codes.CompletionStatuses, MaxCompletionLength, errors);
        var source = Code(input.InformationSource, "Information source", codes.InformationSources, MaxSourceLength, errors);
        var refusal = Code(input.RefusalReason, "Refusal reason", codes.RefusalReasons, MaxRefusalLength, errors);

        // IM-6: plausible dates. An expiration before the administered date is deliberately NOT refused: giving an expired vaccine is a real, reportable event.
        CheckDate(input.EducationDate, "Education date", now + MaxFutureSlack, errors);
        CheckDate(input.VisDate, "VIS date", now + MaxFutureSlack, errors);
        CheckDate(input.ExpirationDate, "Expiration date", now + MaxExpirationAhead, errors);

        // Who gave it and who ordered it: users.id values in an int or bigint column.
        if (input.AdministeredById is <= 0 or > MaxIntColumn)
            errors.Add("Administered-by id must identify a user.");
        if (input.OrderingProvider is <= 0 or > MaxIntColumn)
            errors.Add("Ordering provider must identify a user.");

        // IM-7: a linked encounter must be this patient's. Its number is what the column stores.
        if (input.Encounter is { } encounter)
        {
            if (encounter.Number <= 0)
                errors.Add("The linked encounter has no encounter number.");
            else if (encounter.PatientPid != patientPid)
                errors.Add("The encounter belongs to a different patient than the immunization.");
        }

        if (errors.Count > 0)
            throw new ClinicalCareValidationException(errors);

        return new ImmunizationFacts(
            WholeSeconds(input.AdministeredDate!.Value), input, cvx, manufacturer, lot, amount, unit, route, site, completion, source, refusal, administeredBy, reasonCode,
            reasonDescription, note);
    }

    private static string? Text(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static string? Bounded(string? value, int max, string label, List<string> errors)
    {
        var text = Text(value);
        if (text is { } t && t.Length > max)
        {
            errors.Add($"{label} must be at most {max} characters.");
            return null;
        }

        return text;
    }

    /// <summary>A <c>text</c> column holds 65,535 bytes, so the limit is the UTF-8 size, not the character count.</summary>
    private static string? Bytes(string? value, string label, List<string> errors)
    {
        if (string.IsNullOrWhiteSpace(value))
            return null;

        if (Encoding.UTF8.GetByteCount(value) > MaxTextBytes)
        {
            errors.Add($"{label} must be at most {MaxTextBytes} bytes.");
            return null;
        }

        return value;
    }

    private static string? Code(string? value, string label, IReadOnlySet<string> allowed, int maxLength, List<string> errors) =>
        Rules.IssueFieldRules.StringCode(value, label, allowed, maxLength, errors);

    private static void CheckDate(DateTime? value, string label, DateTime latest, List<string> errors)
    {
        if (value is { } date && (date < MinDate || date > latest))
            errors.Add($"{label} must be between {MinDate:yyyy-MM-dd} and {latest:yyyy-MM-dd}.");
    }

    private static DateTime WholeSeconds(DateTime value) => new(value.Ticks - value.Ticks % TimeSpan.TicksPerSecond, value.Kind);
}
