using OpenEmrNext.ClinicalCare.Application.Ports;
using OpenEmrNext.ClinicalCare.Domain.ValueObjects;
using OpenEmrNext.ClinicalCare.Domain.VitalSigns;
using OpenEmrNext.ClinicalCare.Infrastructure;

namespace OpenEmrNext.ClinicalCare.IntegrationTests;

/// <summary>
/// The vitals anti-corruption adapter against a real MariaDB with the real legacy DDL, as the production principal
/// (QLT-8). Rule ids as in docs/clinical-care/02-record-vitals-slice.md.
/// </summary>
[Collection("ClinicalCareContainer")]
public class VitalsRegistryTests(ClinicalCareContainerFixture fixture) : IAsyncLifetime
{
    public Task InitializeAsync() => fixture.ResetAsync();
    public Task DisposeAsync() => Task.CompletedTask;

    private static readonly DateTime Now = new(2026, 11, 16, 9, 30, 0);

    internal static Vitals NewVitals(VitalsInput? input = null, long number = 1001, long pid = 42, long providerId = 7, Guid? uuid = null)
    {
        var recording = VitalsRecording.Create(
            EncounterReference.Create(Guid.NewGuid(), number, EncounterTestData.PatientUuid, pid),
            input ?? new VitalsInput(
                Weight: new Quantity(70m, MeasurementUnit.Kilogram), Height: new Quantity(175m, MeasurementUnit.Centimeter),
                Temperature: new Quantity(37m, MeasurementUnit.DegreeCelsius), TemperatureMethod: "Oral",
                Systolic: 120, Diastolic: 80, Pulse: 64, Respiration: 16, OxygenSaturation: 98m, Note: "calm"),
            providerId, Now);
        return Vitals.Record(recording, VitalsIdentity.Create(uuid ?? Guid.CreateVersion7()));
    }

    private static VitalsRecordedEvent EventFor(Vitals v) => new(
        v.Uuid, v.Encounter.Uuid, v.Encounter.Number, v.Encounter.Patient.Uuid, v.Encounter.Patient.Pid,
        v.MeasurementsTaken, Now.ToUniversalTime());

    private async Task RegisterAsync(Vitals vitals)
    {
        await using var db = fixture.CreateDbContext();
        await new EfVitalsRegistry(db).RegisterAsync(vitals, EventFor(vitals), CancellationToken.None);
    }

    [Fact]
    public async Task CV_4_the_form_vitals_row_holds_us_units_and_the_derived_bmi()
    {
        var vitals = NewVitals();

        await RegisterAsync(vitals);

        Assert.True(vitals.Id > 0);
        var id = vitals.Id;
        Assert.Equal(vitals.Uuid.ToString("N").ToUpperInvariant(), await fixture.RootScalarAsync<string>($"SELECT HEX(uuid) FROM form_vitals WHERE id = {id}"));
        Assert.Equal(42L, await fixture.RootScalarAsync<long>($"SELECT pid FROM form_vitals WHERE id = {id}"));
        // 70 kg is 154.323584 lb, 175 cm is 68.897638 in, 37 C is 98.6 F: stored in US units, as legacy stores them.
        Assert.Equal("154.323584", await fixture.RootScalarAsync<string>($"SELECT weight FROM form_vitals WHERE id = {id}"));
        Assert.Equal("68.897638", await fixture.RootScalarAsync<string>($"SELECT height FROM form_vitals WHERE id = {id}"));
        Assert.Equal("98.600000", await fixture.RootScalarAsync<string>($"SELECT temperature FROM form_vitals WHERE id = {id}"));
        Assert.Equal("22.854881", await fixture.RootScalarAsync<string>($"SELECT BMI FROM form_vitals WHERE id = {id}"));
        Assert.Equal("Normal", await fixture.RootScalarAsync<string>($"SELECT BMI_status FROM form_vitals WHERE id = {id}"));
    }

    [Fact]
    public async Task CV_8_blood_pressure_is_stored_as_whole_number_text_and_the_rest_as_legacy_expects()
    {
        var vitals = NewVitals();

        await RegisterAsync(vitals);

        Assert.Equal("120", await fixture.RootScalarAsync<string>("SELECT bps FROM form_vitals"));
        Assert.Equal("80", await fixture.RootScalarAsync<string>("SELECT bpd FROM form_vitals"));
        Assert.Equal("64.000000", await fixture.RootScalarAsync<string>("SELECT pulse FROM form_vitals"));
        Assert.Equal("16.000000", await fixture.RootScalarAsync<string>("SELECT respiration FROM form_vitals"));
        Assert.Equal("98.00", await fixture.RootScalarAsync<string>("SELECT oxygen_saturation FROM form_vitals"));
        Assert.Equal("Oral", await fixture.RootScalarAsync<string>("SELECT temp_method FROM form_vitals"));
        Assert.Equal("calm", await fixture.RootScalarAsync<string>("SELECT note FROM form_vitals"));
        Assert.Equal(1L, await fixture.RootScalarAsync<long>("SELECT activity FROM form_vitals"));
        Assert.Equal(1L, await fixture.RootScalarAsync<long>("SELECT authorized FROM form_vitals"));
        Assert.Equal("dr_who", await fixture.RootScalarAsync<string>("SELECT user FROM form_vitals"));
        Assert.Equal("Default", await fixture.RootScalarAsync<string>("SELECT groupname FROM form_vitals"));
    }

    [Fact]
    public async Task CV_12_a_measurement_that_was_not_taken_is_stored_as_zero_the_legacy_not_recorded_value()
    {
        var vitals = NewVitals(new VitalsInput(Pulse: 64));

        await RegisterAsync(vitals);

        Assert.Equal("0.000000", await fixture.RootScalarAsync<string>("SELECT weight FROM form_vitals"));
        Assert.Equal("0.000000", await fixture.RootScalarAsync<string>("SELECT height FROM form_vitals"));
        Assert.Equal("0.000000", await fixture.RootScalarAsync<string>("SELECT BMI FROM form_vitals"));
        Assert.Null(await fixture.RootScalarAsync<string>("SELECT bps FROM form_vitals"));
        Assert.Null(await fixture.RootScalarAsync<string>("SELECT BMI_status FROM form_vitals"));
        Assert.Null(await fixture.RootScalarAsync<string>("SELECT temp_method FROM form_vitals"));
    }

    [Fact]
    public async Task CV_10_the_forms_row_links_the_vitals_to_the_encounter_by_number_and_pid()
    {
        var vitals = NewVitals(number: 1234, pid: 77);

        await RegisterAsync(vitals);

        Assert.Equal(1L, await fixture.RootScalarAsync<long>("SELECT COUNT(*) FROM forms"));
        Assert.Equal("vitals", await fixture.RootScalarAsync<string>("SELECT formdir FROM forms"));
        Assert.Equal("Vitals", await fixture.RootScalarAsync<string>("SELECT form_name FROM forms"));
        Assert.Equal(vitals.Id, await fixture.RootScalarAsync<long>("SELECT form_id FROM forms"));
        Assert.Equal(1234L, await fixture.RootScalarAsync<long>("SELECT encounter FROM forms"));
        Assert.Equal(77L, await fixture.RootScalarAsync<long>("SELECT pid FROM forms"));
        Assert.Equal("dr_who", await fixture.RootScalarAsync<string>("SELECT user FROM forms"));
        Assert.Equal(1L, await fixture.RootScalarAsync<long>("SELECT authorized FROM forms"));
        Assert.Equal(0L, await fixture.RootScalarAsync<long>("SELECT deleted FROM forms"));
    }

    [Fact]
    public async Task CV_11_the_uuid_is_recorded_in_the_legacy_registry_under_form_vitals()
    {
        var vitals = NewVitals();

        await RegisterAsync(vitals);

        var hex = vitals.Uuid.ToString("N").ToUpperInvariant();
        Assert.Equal("form_vitals", await fixture.RootScalarAsync<string>($"SELECT table_name FROM uuid_registry WHERE HEX(uuid) = '{hex}'"));
    }

    [Fact]
    public async Task CV_13_the_event_is_in_the_outbox_naming_the_measurements_but_holding_no_values()
    {
        var vitals = NewVitals();

        await RegisterAsync(vitals);

        Assert.Equal("VitalsRecorded", await fixture.RootScalarAsync<string>("SELECT event_type FROM clinical_care_outbox"));
        var json = await fixture.RootScalarAsync<string>("SELECT payload FROM clinical_care_outbox");
        using var doc = System.Text.Json.JsonDocument.Parse(json!);
        var payload = doc.RootElement.GetProperty("payload");
        Assert.Equal(vitals.Uuid, payload.GetProperty("vitalsUuid").GetGuid());
        Assert.Equal(1001, payload.GetProperty("encounterNumber").GetInt64());
        Assert.Contains("weight", payload.GetProperty("measurementsTaken").EnumerateArray().Select(e => e.GetString()));
        Assert.DoesNotContain("154", json);   // no weight value
        Assert.DoesNotContain("calm", json);  // no note text
    }

    [Fact]
    public async Task OPS_9_registering_the_same_identity_again_writes_nothing_twice()
    {
        var uuid = Guid.CreateVersion7();
        var first = NewVitals(uuid: uuid);
        var retried = NewVitals(uuid: uuid);

        await RegisterAsync(first);
        await RegisterAsync(retried);

        Assert.Equal(first.Id, retried.Id);
        Assert.Equal(1L, await fixture.RootScalarAsync<long>("SELECT COUNT(*) FROM form_vitals"));
        Assert.Equal(1L, await fixture.RootScalarAsync<long>("SELECT COUNT(*) FROM forms"));
        Assert.Equal(1L, await fixture.RootScalarAsync<long>("SELECT COUNT(*) FROM clinical_care_outbox"));
    }

    [Fact]
    public async Task QLT_12_a_failure_part_way_through_leaves_no_partial_vitals()
    {
        var vitals = NewVitals();
        await fixture.RootExecuteAsync($"INSERT INTO uuid_registry (uuid, table_name) VALUES (UNHEX('{vitals.Uuid:N}'), 'form_vitals')");

        await Assert.ThrowsAnyAsync<Exception>(() => RegisterAsync(vitals));

        Assert.Equal(0L, await fixture.RootScalarAsync<long>("SELECT COUNT(*) FROM form_vitals"));
        Assert.Equal(0L, await fixture.RootScalarAsync<long>("SELECT COUNT(*) FROM forms"));
        Assert.Equal(0L, await fixture.RootScalarAsync<long>("SELECT COUNT(*) FROM clinical_care_outbox"));
        Assert.Equal(0, vitals.Id);
    }

    [Fact]
    public async Task A_provider_id_above_127_still_records_because_authorized_is_the_flag_not_the_id()
    {
        var vitals = NewVitals(providerId: 5000);

        await RegisterAsync(vitals);

        Assert.True(vitals.Id > 0);
        Assert.Equal(1L, await fixture.RootScalarAsync<long>("SELECT authorized FROM form_vitals"));
    }
}
