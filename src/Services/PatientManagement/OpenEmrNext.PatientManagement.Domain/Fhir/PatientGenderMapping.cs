namespace OpenEmrNext.PatientManagement.Domain.Fhir;

/// <summary>
/// Pure port of <c>FhirPatientService::parseOpenEMRGenderAndBirthSex()</c>, targeting US Core 8.0.0
/// only (legacy branches on three profile versions; this service targets the current one, not a
/// multi-version compatibility matrix nothing here needs to serve). No I/O -- a deterministic switch
/// on the same raw, free-text <c>patient_data.sex</c> value <see cref="Patient"/> already stores, unlike
/// race/ethnicity/administrative-sex/tribal-affiliation/interpreter-needed, which all depend on
/// configured <c>list_options</c> rows and so are resolved in Infrastructure instead.
/// </summary>
public static class PatientGenderMapping
{
    /// <returns>
    /// <c>Gender</c>: the FHIR <c>administrative-gender</c> code (<c>male</c>/<c>female</c>/<c>other</c>/
    /// <c>unknown</c>). <c>BirthSexCode</c>: the US Core 8.0.0 birthsex extension's bare code
    /// (<c>M</c>/<c>F</c>/<c>OTH</c>/<c>ASKU</c>/<c>asked-declined</c>/<c>UNK</c>).
    /// </returns>
    public static (string Gender, string BirthSexCode) Resolve(string? sex)
    {
        var value = (sex ?? "").ToLowerInvariant();

        var birthSexCode = value switch
        {
            "male" => "M",
            "female" => "F",
            "oth" => "OTH",
            "asku" => "ASKU",
            "asked-declined" => "asked-declined",
            _ => "UNK"
        };

        var gender = value switch
        {
            "male" or "female" => value,
            "unk" => "unknown",
            _ => "other"
        };

        return (gender, birthSexCode);
    }
}
