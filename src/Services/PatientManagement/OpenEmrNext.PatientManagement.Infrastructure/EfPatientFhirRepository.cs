using Microsoft.EntityFrameworkCore;
using OpenEmrNext.PatientManagement.Application.Ports;
using OpenEmrNext.PatientManagement.Domain.Fhir;

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

    public async Task<PatientFhirView?> GetByUuidAsync(Guid uuid, CancellationToken ct)
    {
        var uuidBytes = Rfc4122GuidConverter.ToRfc4122Bytes(uuid);
        var row = await db.Database.SqlQueryRaw<RawPatientRow>(BaseSelect + " WHERE patient_data.uuid = {0}", uuidBytes)
            .SingleOrDefaultAsync(ct);
        return row is null ? null : await BuildViewAsync(row, ct);
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
        var views = new List<PatientFhirView>(rows.Count);
        foreach (var row in rows)
            views.Add(await BuildViewAsync(row, ct));
        return views;
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

    private async Task<PatientFhirView> BuildViewAsync(RawPatientRow row, CancellationToken ct)
    {
        var (gender, birthSexCode) = Domain.Fhir.PatientGenderMapping.Resolve(row.Sex);

        var (raceSystem, raceCode, raceDisplay) = await ResolveRaceAsync(row.Race, ct);
        var (ethSystem, ethCode, ethDisplay, ethText) = await ResolveEthnicityAsync(row.Ethnicity, ct);
        var (sexIdSystem, sexIdCode, sexIdDisplay) = await ResolveCodedListAsync(
            "administrative_sex", row.SexIdentified, DataAbsentReasonSystem, ct);
        var (hasGenderIdentity, giSystem, giCode, giDisplay) = await ResolveGenderIdentityAsync(row.GenderIdentity, ct);
        var (tribalSystem, tribalCode, tribalDisplay) = await ResolveTribalAffiliationAsync(row.TribalAffiliations, ct);
        var (interpSystem, interpCode, interpDisplay) = await ResolveCodedListAsync(
            "yes_no_unknown", row.InterpreterNeeded, DataAbsentReasonSystem, ct);
        var (langKnown, langCode, langDisplay) = await ResolveLanguageAsync(row.Language, ct);

        return new PatientFhirView(
            Rfc4122GuidConverter.FromRfc4122Bytes(row.Uuid), row.LastUpdated,
            row.Title, row.FirstName ?? "", row.MiddleName, row.LastName ?? "", row.Suffix,
            row.Street, row.StreetLine2, row.City, row.State, row.PostalCode, row.CountryCode,
            row.PhoneHome, row.PhoneBiz, row.PhoneCell, row.Email, row.EmailDirect,
            TryParseDate(row.DateOfBirth), gender, birthSexCode,
            raceSystem, raceCode, raceDisplay,
            ethSystem, ethCode, ethDisplay, ethText,
            row.Ssn, row.PublicPatientId,
            langKnown, langCode, langDisplay,
            row.ProviderUuid is { Length: 16 } pu ? Rfc4122GuidConverter.FromRfc4122Bytes(pu) : null,
            hasGenderIdentity, giSystem, giCode, giDisplay,
            sexIdSystem, sexIdCode, sexIdDisplay,
            tribalSystem, tribalCode, tribalDisplay,
            interpSystem, interpCode, interpDisplay,
            row.DeceasedDate);
    }

    /// <summary>Ports <c>parseOpenEMRRaceRecord()</c>: unlike ethnicity, race always resolves to
    /// something -- "Unknown" when blank, "Asked but no answer" for a decline, the real OMB code
    /// otherwise.</summary>
    private async Task<(string System, string Code, string Display)> ResolveRaceAsync(string? race, CancellationToken ct)
    {
        if (string.IsNullOrEmpty(race))
            return (NullFlavorSystem, "UNK", "Unknown");

        if (race is "decline_to_specify" or "declne_to_specfy")
            return (NullFlavorSystem, "ASKU", "Asked but no answer");

        var option = await LookupListOptionAsync("race", race, ct);
        return option is null
            ? (NullFlavorSystem, "UNK", "Unknown")
            : (RaceEthnicitySystem, option.Notes ?? "UNK", option.Title ?? "Unknown");
    }

    /// <summary>Ports <c>parseOpenEMREthnicityRecord()</c>: unlike race, a blank ethnicity gets no
    /// extension at all (<c>EthnicitySystem</c> null signals "omit"); a decline gets text only, no
    /// ombCategory coding.</summary>
    private async Task<(string? System, string? Code, string? Display, string Text)> ResolveEthnicityAsync(
        string? ethnicity, CancellationToken ct)
    {
        if (string.IsNullOrEmpty(ethnicity))
            return (null, null, null, "");

        var option = await LookupListOptionAsync("ethnicity", ethnicity, ct);
        if (option is null)
            return (null, null, null, "");

        var text = option.Title ?? "";
        if (ethnicity is "decline_to_specify" or "declne_to_specfy")
            return (null, null, null, text);

        return (EthnicitySystem, option.Notes ?? "UNK", text, text);
    }

    private async Task<(bool Known, string System, string Code, string Display)> ResolveGenderIdentityAsync(
        string? genderIdentity, CancellationToken ct)
    {
        if (string.IsNullOrEmpty(genderIdentity))
            return (false, NullFlavorSystem, "UNK", "Unknown");

        if (genderIdentity == "asked-declined")
            return (true, DataAbsentReasonSystem, "asked-declined", "Asked But Declined");
        if (genderIdentity == "OTH")
            return (true, NullFlavorSystem, "OTH", "Other");
        if (genderIdentity == "UNK")
            return (true, NullFlavorSystem, "UNK", "Unknown");

        var option = await LookupListOptionAsync("gender", genderIdentity, ct);
        if (option is null)
            return (false, NullFlavorSystem, "UNK", "Unknown");

        var (system, code) = SplitCode(option.Codes);
        return (true, system, code, option.Title ?? "Unknown");
    }

    private async Task<(string? System, string? Code, string? Display)> ResolveTribalAffiliationAsync(
        string? tribalAffiliations, CancellationToken ct)
    {
        if (string.IsNullOrEmpty(tribalAffiliations))
            return (null, null, null);

        var option = await LookupListOptionAsync("tribal_affiliations", tribalAffiliations, ct);
        return option is null ? (null, null, null) : (TribalAffiliationSystem, option.OptionId, option.Title);
    }

    private async Task<(bool Known, string? Code, string? Display)> ResolveLanguageAsync(string? language, CancellationToken ct)
    {
        if (string.IsNullOrEmpty(language))
            return (false, null, null);

        var option = await LookupListOptionAsync("language", language, ct);
        return option is null ? (false, null, null) : (true, option.Notes, option.Title);
    }

    /// <summary>Shared shape for administrative-sex/interpreter-needed: both resolve via
    /// <c>list_options.codes</c> (<c>SYSTEM:CODE</c>), defaulting to a data-absent-reason "unknown"
    /// when blank or not found.</summary>
    private async Task<(string System, string Code, string Display)> ResolveCodedListAsync(
        string listId, string? optionId, string defaultSystem, CancellationToken ct)
    {
        if (string.IsNullOrEmpty(optionId))
            return (defaultSystem, DataAbsentCode, "Unknown");

        var option = await LookupListOptionAsync(listId, optionId, ct);
        if (option is null)
            return (defaultSystem, DataAbsentCode, "Unknown");

        var (system, code) = SplitCode(option.Codes);
        return (system, code, option.Title ?? "Unknown");
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

    private async Task<RawListOption?> LookupListOptionAsync(string listId, string? optionId, CancellationToken ct)
    {
        if (string.IsNullOrEmpty(optionId))
            return null;

        return await db.Database.SqlQueryRaw<RawListOption>(
            "SELECT option_id AS OptionId, title AS Title, notes AS Notes, codes AS Codes FROM list_options WHERE list_id = {0} AND option_id = {1}",
            listId, optionId).SingleOrDefaultAsync(ct);
    }

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
}
