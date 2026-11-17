using OpenEmrNext.ClinicalCare.Domain.Immunizations;
using OpenEmrNext.ClinicalCare.Domain.ValueObjects;

namespace OpenEmrNext.ClinicalCare.IntegrationTests;

/// <summary>Shared fixtures for the immunization adapter and endpoint tests.</summary>
internal static class ImmunizationTestData
{
    public static readonly DateTime Now = new(2026, 11, 17, 9, 30, 0);
    public static readonly DateTime Given = new(2026, 11, 10, 8, 15, 30);

    /// <summary>The code lists <see cref="SeedCodesAsync"/> writes, for building facts in-process.</summary>
    public static readonly ImmunizationCodeData Codes = new(
        new HashSet<int> { 6, 7, 27 }, new HashSet<string> { "intramuscular", "bymouth" }, new HashSet<string> { "left_arm", "right_arm" },
        new HashSet<string> { "Completed", "Refused" }, new HashSet<string> { "00", "01" }, new HashSet<string> { "OSTUDY", "PATOBJ" },
        new HashSet<string> { "1", "9" });

    public static async Task SeedCodesAsync(ClinicalCareContainerFixture fixture) =>
        await fixture.RootExecuteAsync(
            """
            INSERT INTO list_options (list_id, option_id, title, seq, is_default) VALUES
                ('immunizations', '6', 'DT 1', 1, 0), ('immunizations', '7', 'DT 2', 2, 0), ('immunizations', '27', 'Flu', 3, 0), ('immunizations', 'not-a-number', 'Odd', 4, 0),
                ('drug_route', 'intramuscular', 'Intramuscular', 1, 0), ('drug_route', 'bymouth', 'By Mouth', 2, 0),
                ('immunization_administered_site', 'left_arm', 'Left Arm', 1, 0), ('immunization_administered_site', 'right_arm', 'Right Arm', 2, 0),
                ('Immunization_Completion_Status', 'Completed', 'completed', 1, 0), ('Immunization_Completion_Status', 'Refused', 'Refused', 2, 0),
                ('immunization_informationsource', '00', 'New record', 1, 0), ('immunization_informationsource', '01', 'Historical', 2, 0),
                ('immunization_refusal_reason', 'OSTUDY', 'Study', 1, 0), ('immunization_refusal_reason', 'PATOBJ', 'Objection', 2, 0),
                ('drug_units', '1', 'mg', 1, 0), ('drug_units', '9', 'mL', 2, 0),
                ('other_list', 'intramuscular', 'A different list must never leak in', 1, 0)
            """);

    public static ImmunizationInput Input(DateTime? given = null, string? note = "arm sore", string? lot = "L1") =>
        new(given ?? Given, 6, "140", "Acme", lot, new DateTime(2027, 1, 1), 0.5m, "1", "intramuscular", "left_arm", "Completed", "00", null,
            new DateTime(2026, 11, 10), new DateTime(2026, 8, 6), "Nurse Joy", 7, 9, null, "R1", "why", note, false);

    public static Immunization New(ImmunizationInput? input = null, long pid = 42, long provider = 7, Guid? uuid = null, Guid? patientUuid = null) =>
        Immunization.Record(
            PatientReference.Create(patientUuid ?? EncounterTestData.PatientUuid, pid),
            ImmunizationFacts.Create(input ?? Input(), pid, Now, Codes), provider, ImmunizationIdentity.Create(uuid ?? Guid.CreateVersion7()), Now);

    /// <summary>The SQL literal of a uuid in the binary form the table stores.</summary>
    public static string Hex(Guid uuid) => $"UNHEX('{uuid:N}')";
}
