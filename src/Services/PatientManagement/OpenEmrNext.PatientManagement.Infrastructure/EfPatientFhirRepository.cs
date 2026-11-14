using Microsoft.EntityFrameworkCore;
using OpenEmrNext.PatientManagement.Application.Ports;
using OpenEmrNext.PatientManagement.Domain.Fhir;
using OpenEmrNext.PatientManagement.Domain.Fhir.ReadModels;
using OpenEmrNext.PatientManagement.Domain.Fhir.Rules;
using OpenEmrNext.PatientManagement.Domain.Fhir.ValueObjects;

namespace OpenEmrNext.PatientManagement.Infrastructure;

/// <summary>
/// The adapter side of <see cref="IPatientFhirRepository"/>. Raw SQL against <c>patient_data</c>,
/// <c>users</c> (for <c>generalPractitioner</c>), and <c>list_options</c> (for every coded extension),
/// ported from <c>FhirPatientService::parseOpenEMRRecord()</c> and its field-level helpers. The
/// list-options resolution and legacy's own "Unknown"/data-absent-reason defaulting both happen here,
/// not in Domain -- the same reasoning <see cref="EfDuplicateScorer"/> already documents for keeping a
/// bulk/configured-data-dependent formula where its I/O already is, rather than forcing it pure.
/// </summary>
public sealed class EfPatientFhirRepository(PatientManagementDbContext db) : IPatientFhirRepository
{
    private const string NullFlavorSystem = "http://terminology.hl7.org/CodeSystem/v3-NullFlavor";
    private const string RaceEthnicitySystem = "urn:oid:2.16.840.1.113883.6.238";
    private const string DataAbsentReasonSystem = "http://terminology.hl7.org/CodeSystem/data-absent-reason";
    private const string LanguageBcp47System = "urn:ietf:bcp:47";
    private const string EthnicitySystem = "urn:oid:2.16.840.1.113883.6.238";
    private const string TribalAffiliationSystem = "http://terminology.hl7.org/CodeSystem/v3-TribalEntityUS";
    private const string DataAbsentCode = "unknown";

    /// <summary>Every <c>list_options</c> row this mapping could ever need, for the lists it resolves
    /// against (race, ethnicity, administrative sex, gender identity, tribal affiliation,
    /// interpreter-needed, language).</summary>
    private static readonly string[] ResolvedListIds =
        ["race", "ethnicity", "administrative_sex", "gender", "tribal_affiliations", "yes_no_unknown", "language"];

    public async Task<PatientFhirView?> GetByUuidAsync(Guid uuid, CancellationToken ct)
    {
        var uuidBytes = Rfc4122GuidConverter.ToRfc4122Bytes(uuid);
        var row = await db.Database.SqlQueryRaw<RawPatientRow>(BaseSelect + " WHERE patient_data.uuid = {0}", uuidBytes)
            .SingleOrDefaultAsync(ct);
        if (row is null)
            return null;

        var listOptions = await LoadListOptionsAsync(ct);
        return BuildView(row, listOptions);
    }

    public async Task<IReadOnlyList<PatientFhirView>> SearchAsync(PatientFhirSearchCriteria criteria, CancellationToken ct)
    {
        var sql = BaseSelect + " WHERE 1=1";
        var parameters = new List<object>();
        void AddLike(string column, string? value)
        {
            if (string.IsNullOrEmpty(value)) return;
            sql += $" AND {column} LIKE {{{parameters.Count}}}";
            parameters.Add($"%{value}%");
        }
        void AddEquals(string column, object? value)
        {
            if (value is null) return;
            sql += $" AND {column} = {{{parameters.Count}}}";
            parameters.Add(value);
        }

        if (criteria.Id is { } id)
            AddEquals("patient_data.uuid", Rfc4122GuidConverter.ToRfc4122Bytes(id));
        if (!string.IsNullOrEmpty(criteria.Identifier))
        {
            sql += $" AND (ss = {{{parameters.Count}}} OR pubpid = {{{parameters.Count}}})";
            parameters.Add(criteria.Identifier);
        }
        AddLike("fname", criteria.Given);
        AddLike("mname", criteria.Given);
        AddLike("lname", criteria.Family);
        if (!string.IsNullOrEmpty(criteria.Name))
        {
            sql += $" AND (fname LIKE {{{parameters.Count}}} OR lname LIKE {{{parameters.Count}}})";
            parameters.Add($"%{criteria.Name}%");
        }
        AddEquals("DOB", criteria.BirthDate?.ToString("yyyy-MM-dd"));
        if (!string.IsNullOrEmpty(criteria.Gender))
        {
            sql += $" AND LOWER(sex) = {{{parameters.Count}}}";
            parameters.Add(criteria.Gender.ToLowerInvariant());
        }
        AddLike("street", criteria.Address);
        AddLike("city", criteria.AddressCity);
        AddLike("postal_code", criteria.AddressPostalCode);
        AddLike("state", criteria.AddressState);
        if (!string.IsNullOrEmpty(criteria.Email))
        {
            sql += $" AND (email = {{{parameters.Count}}} OR email_direct = {{{parameters.Count}}})";
            parameters.Add(criteria.Email);
        }
        if (!string.IsNullOrEmpty(criteria.Phone))
        {
            sql += $" AND (phone_home = {{{parameters.Count}}} OR phone_biz = {{{parameters.Count}}} OR phone_cell = {{{parameters.Count}}})";
            parameters.Add(criteria.Phone);
        }
        if (!string.IsNullOrEmpty(criteria.Telecom))
        {
            sql += $" AND (email = {{{parameters.Count}}} OR email_direct = {{{parameters.Count}}} OR phone_home = {{{parameters.Count}}} OR phone_biz = {{{parameters.Count}}} OR phone_cell = {{{parameters.Count}}})";
            parameters.Add(criteria.Telecom);
        }
        if (criteria.LastUpdatedSince is { } since)
        {
            sql += $" AND patient_data.date >= {{{parameters.Count}}}";
            parameters.Add(since);
        }
        if (criteria.GeneralPractitioner is { } gp)
        {
            sql += $" AND provider.uuid = {{{parameters.Count}}}";
            parameters.Add(Rfc4122GuidConverter.ToRfc4122Bytes(gp));
        }

        var rows = await db.Database.SqlQueryRaw<RawPatientRow>(sql, parameters.ToArray()).ToListAsync(ct);
        if (rows.Count == 0)
            return [];

        var listOptions = await LoadListOptionsAsync(ct);
        return [.. rows.Select(row => BuildView(row, listOptions))];
    }

    private const string BaseSelect =
        """
        SELECT patient_data.uuid AS Uuid, patient_data.date AS LastUpdated,
               title AS Title, fname AS FirstName, mname AS MiddleName, lname AS LastName, suffix AS Suffix,
               street AS Street, street_line_2 AS StreetLine2, city AS City, state AS State,
               postal_code AS PostalCode, country_code AS CountryCode,
               phone_home AS PhoneHome, phone_biz AS PhoneBiz, phone_cell AS PhoneCell,
               email AS Email, email_direct AS EmailDirect,
               CAST(DOB AS CHAR(10)) AS DateOfBirth, sex AS Sex, race AS Race, ethnicity AS Ethnicity,
               ss AS Ssn, pubpid AS PublicPatientId, language AS Language,
               provider.uuid AS ProviderUuid,
               gender_identity AS GenderIdentity, sex_identified AS SexIdentified,
               tribal_affiliations AS TribalAffiliations, interpreter_needed AS InterpreterNeeded,
               deceased_date AS DeceasedDate
        FROM patient_data
        LEFT JOIN (SELECT id, uuid FROM users) provider ON patient_data.providerID = provider.id
        """;

    private PatientFhirView BuildView(RawPatientRow row, IReadOnlyDictionary<(string ListId, string OptionId), RawListOption> listOptions)
    {
        var (gender, birthSexCode) = PatientGenderMapping.Resolve(row.Sex);

        var race = ResolveRace(row.Race, listOptions);
        var (ethnicityCategory, ethnicityText) = ResolveEthnicity(row.Ethnicity, listOptions);
        var sexIdentified = ResolveCodedList("administrative_sex", row.SexIdentified, DataAbsentReasonSystem, listOptions);
        var genderIdentity = ResolveGenderIdentity(row.GenderIdentity, listOptions);
        var tribalAffiliation = ResolveTribalAffiliation(row.TribalAffiliations, listOptions);
        var interpreterNeeded = ResolveCodedList("yes_no_unknown", row.InterpreterNeeded, DataAbsentReasonSystem, listOptions);
        var language = ResolveLanguage(row.Language, listOptions);

        return new PatientFhirView(
            Rfc4122GuidConverter.FromRfc4122Bytes(row.Uuid), row.LastUpdated,
            row.Title, row.FirstName ?? "", row.MiddleName, row.LastName ?? "", row.Suffix,
            row.Street, row.StreetLine2, row.City, row.State, row.PostalCode, row.CountryCode,
            row.PhoneHome, row.PhoneBiz, row.PhoneCell, row.Email, row.EmailDirect,
            TryParseDate(row.DateOfBirth), gender, birthSexCode,
            race, ethnicityCategory, ethnicityText,
            row.Ssn, row.PublicPatientId,
            language,
            row.ProviderUuid is { Length: 16 } pu ? Rfc4122GuidConverter.FromRfc4122Bytes(pu) : null,
            genderIdentity, sexIdentified, tribalAffiliation, interpreterNeeded,
            row.DeceasedDate);
    }

    /// <summary>Ports <c>parseOpenEMRRaceRecord()</c>: unlike ethnicity, race always resolves to
    /// something -- "Unknown" when blank, "Asked but no answer" for a decline, the real OMB code
    /// otherwise.</summary>
    private static ResolvedCode ResolveRace(string? race, IReadOnlyDictionary<(string, string), RawListOption> listOptions)
    {
        if (string.IsNullOrEmpty(race))
            return ResolvedCode.Create(NullFlavorSystem, "UNK", "Unknown");

        if (race is "decline_to_specify" or "declne_to_specfy")
            return ResolvedCode.Create(NullFlavorSystem, "ASKU", "Asked but no answer");

        var option = LookupListOption(listOptions, "race", race);
        return option is null
            ? ResolvedCode.Create(NullFlavorSystem, "UNK", "Unknown")
            : ResolvedCode.Create(RaceEthnicitySystem, option.Notes ?? "UNK", option.Title ?? "Unknown");
    }

    /// <summary>Ports <c>parseOpenEMREthnicityRecord()</c>: unlike race, a blank ethnicity gets no
    /// extension at all (a <c>null</c> ombCategory signals "omit"); a decline gets text only, no
    /// ombCategory coding.</summary>
    private static (ResolvedCode? Category, string Text) ResolveEthnicity(string? ethnicity, IReadOnlyDictionary<(string, string), RawListOption> listOptions)
    {
        if (string.IsNullOrEmpty(ethnicity))
            return (null, "");

        var option = LookupListOption(listOptions, "ethnicity", ethnicity);
        if (option is null)
            return (null, "");

        var text = option.Title ?? "";
        if (ethnicity is "decline_to_specify" or "declne_to_specfy")
            return (null, text);

        return (ResolvedCode.Create(EthnicitySystem, option.Notes ?? "UNK", text), text);
    }

    /// <summary>A <c>null</c> result means the gender-identity extension is omitted entirely (legacy's
    /// own <c>if (!empty($dataRecord['gender_identity']))</c> guard) -- a literal stored value of
    /// <c>"UNK"</c> is still non-empty, so it resolves to a real (if unknown-coded) value instead.</summary>
    private static ResolvedCode? ResolveGenderIdentity(string? genderIdentity, IReadOnlyDictionary<(string, string), RawListOption> listOptions)
    {
        if (string.IsNullOrEmpty(genderIdentity))
            return null;

        if (genderIdentity == "asked-declined")
            return ResolvedCode.Create(DataAbsentReasonSystem, "asked-declined", "Asked But Declined");
        if (genderIdentity == "OTH")
            return ResolvedCode.Create(NullFlavorSystem, "OTH", "Other");
        if (genderIdentity == "UNK")
            return ResolvedCode.Create(NullFlavorSystem, "UNK", "Unknown");

        var option = LookupListOption(listOptions, "gender", genderIdentity);
        if (option is null)
            return ResolvedCode.Create(NullFlavorSystem, "UNK", "Unknown");

        var (system, code) = SplitCode(option.Codes);
        return ResolvedCode.Create(system, code, option.Title ?? "Unknown");
    }

    private static ResolvedCode? ResolveTribalAffiliation(string? tribalAffiliations, IReadOnlyDictionary<(string, string), RawListOption> listOptions)
    {
        if (string.IsNullOrEmpty(tribalAffiliations))
            return null;

        var option = LookupListOption(listOptions, "tribal_affiliations", tribalAffiliations);
        return option is null ? null : ResolvedCode.Create(TribalAffiliationSystem, option.OptionId ?? "", option.Title);
    }

    private static ResolvedCode? ResolveLanguage(string? language, IReadOnlyDictionary<(string, string), RawListOption> listOptions)
    {
        if (string.IsNullOrEmpty(language))
            return null;

        var option = LookupListOption(listOptions, "language", language);
        return option is null || option.Notes is null ? null : ResolvedCode.Create(LanguageBcp47System, option.Notes, option.Title);
    }

    /// <summary>Shared shape for administrative-sex/interpreter-needed: both resolve via
    /// <c>list_options.codes</c> (<c>SYSTEM:CODE</c>), defaulting to a data-absent-reason "unknown"
    /// when blank or not found.</summary>
    private static ResolvedCode ResolveCodedList(
        string listId, string? optionId, string defaultSystem, IReadOnlyDictionary<(string, string), RawListOption> listOptions)
    {
        if (string.IsNullOrEmpty(optionId))
            return ResolvedCode.Create(defaultSystem, DataAbsentCode, "Unknown");

        var option = LookupListOption(listOptions, listId, optionId);
        if (option is null)
            return ResolvedCode.Create(defaultSystem, DataAbsentCode, "Unknown");

        var (system, code) = SplitCode(option.Codes);
        return ResolvedCode.Create(system, code, option.Title ?? "Unknown");
    }

    /// <summary>Ports <c>CodeTypesService::parseCode()</c> + the two code types actually seeded for
    /// these lists (<c>getSystemForCodeType</c>): <c>SNOMED-CT</c> and <c>DataAbsentReason</c>.</summary>
    private static (string System, string Code) SplitCode(string? codes)
    {
        if (string.IsNullOrEmpty(codes) || !codes.Contains(':'))
            return (DataAbsentReasonSystem, DataAbsentCode);

        var parts = codes.Split(':', 2);
        var system = parts[0] switch
        {
            "SNOMED-CT" => "http://snomed.info/sct",
            "DataAbsentReason" => DataAbsentReasonSystem,
            _ => DataAbsentReasonSystem
        };
        return (system, parts[1]);
    }

    /// <summary>
    /// Every <c>list_options</c> row this mapping could possibly need, loaded once per
    /// <see cref="GetByUuidAsync"/>/<see cref="SearchAsync"/> call instead of once per patient per
    /// coded field -- the previous per-field lookup made a 50-patient search issue ~350 queries (up to
    /// 7 per patient). <c>list_options</c> is configuration data (bounded, not scaled by patient
    /// count), so loading every row for the lists this mapping touches, once, is always cheaper than
    /// even a handful of per-patient lookups.
    /// </summary>
    private async Task<IReadOnlyDictionary<(string ListId, string OptionId), RawListOption>> LoadListOptionsAsync(CancellationToken ct)
    {
        // The {0}..{6} placeholders are static, matching ResolvedListIds' own fixed length exactly --
        // not runtime-built from any value this query ever sees, unlike the actual list ids themselves,
        // which only ever reach the database as SqlQueryRaw's own positional parameters below.
        const string placeholders = "{0}, {1}, {2}, {3}, {4}, {5}, {6}";
        var rows = await db.Database.SqlQueryRaw<RawListOptionRow>(
            "SELECT list_id AS ListId, option_id AS OptionId, title AS Title, notes AS Notes, codes AS Codes "
                + "FROM list_options WHERE list_id IN (" + placeholders + ")",
            ResolvedListIds).ToListAsync(ct);

        return rows.ToDictionary(
            r => (r.ListId ?? "", r.OptionId ?? ""),
            r => new RawListOption { OptionId = r.OptionId, Title = r.Title, Notes = r.Notes, Codes = r.Codes });
    }

    private static RawListOption? LookupListOption(
        IReadOnlyDictionary<(string, string), RawListOption> listOptions, string listId, string? optionId) =>
        !string.IsNullOrEmpty(optionId) && listOptions.TryGetValue((listId, optionId), out var option) ? option : null;

    private static DateOnly? TryParseDate(string? stored) =>
        DateOnly.TryParseExact(stored, "yyyy-MM-dd", out var day) ? day : null;

    private sealed class RawPatientRow
    {
        public byte[] Uuid { get; set; } = [];
        public DateTime LastUpdated { get; set; }
        public string? Title { get; set; }
        public string? FirstName { get; set; }
        public string? MiddleName { get; set; }
        public string? LastName { get; set; }
        public string? Suffix { get; set; }
        public string? Street { get; set; }
        public string? StreetLine2 { get; set; }
        public string? City { get; set; }
        public string? State { get; set; }
        public string? PostalCode { get; set; }
        public string? CountryCode { get; set; }
        public string? PhoneHome { get; set; }
        public string? PhoneBiz { get; set; }
        public string? PhoneCell { get; set; }
        public string? Email { get; set; }
        public string? EmailDirect { get; set; }
        public string? DateOfBirth { get; set; }
        public string? Sex { get; set; }
        public string? Race { get; set; }
        public string? Ethnicity { get; set; }
        public string? Ssn { get; set; }
        public string? PublicPatientId { get; set; }
        public string? Language { get; set; }
        public byte[]? ProviderUuid { get; set; }
        public string? GenderIdentity { get; set; }
        public string? SexIdentified { get; set; }
        public string? TribalAffiliations { get; set; }
        public string? InterpreterNeeded { get; set; }
        public DateTime? DeceasedDate { get; set; }
    }

    private sealed class RawListOption
    {
        public string? OptionId { get; set; }
        public string? Title { get; set; }
        public string? Notes { get; set; }
        public string? Codes { get; set; }
    }

    private sealed class RawListOptionRow
    {
        public string? ListId { get; set; }
        public string? OptionId { get; set; }
        public string? Title { get; set; }
        public string? Notes { get; set; }
        public string? Codes { get; set; }
    }
}
