using OpenEmrNext.ClinicalCare.Application.Ports;
using OpenEmrNext.ClinicalCare.Infrastructure;

namespace OpenEmrNext.ClinicalCare.IntegrationTests;

/// <summary>
/// The anti-corruption adapter against a real MariaDB with the real legacy DDL, connecting as the production
/// principal (QLT-8, LEG-7). Rules CC-5 to CC-7, CC-11 and the failure behaviour (QLT-12) live here because
/// they are about what actually lands in legacy's tables.
/// </summary>
[Collection("ClinicalCareContainer")]
public class EncounterRegistryTests(ClinicalCareContainerFixture fixture) : IAsyncLifetime
{
    public Task InitializeAsync() => fixture.ResetAsync();
    public Task DisposeAsync() => Task.CompletedTask;

    private static EncounterCreatedEvent EventFor(OpenEmrNext.ClinicalCare.Domain.Encounter e) =>
        new(e.Uuid, e.Number, e.Patient.Uuid, e.Patient.Pid, e.ClassCode, e.Date, EncounterTestData.Now.ToUniversalTime());

    internal static AuditEntry AuditFor(OpenEmrNext.ClinicalCare.Domain.Encounter e) =>
        new(EncounterTestData.Now.ToUniversalTime(), new AuditActor($"user:{e.ProviderId}", e.ProviderId, Verified: false),
            AuditActions.EncounterCreate, e.Uuid, AuditOutcome.Success, correlationId: "corr-enc", sourceAddress: "10.1.2.3");

    private async Task RegisterAsync(OpenEmrNext.ClinicalCare.Domain.Encounter encounter)
    {
        await using var db = fixture.CreateDbContext();
        await new EfEncounterRegistry(db, new EfAuditLog(db)).RegisterAsync(encounter, EventFor(encounter), AuditFor(encounter), CancellationToken.None);
    }

    [Fact]
    public async Task Register_writes_the_form_encounter_row_with_legacy_shaped_values()
    {
        var encounter = EncounterTestData.NewEncounter();

        await RegisterAsync(encounter);

        Assert.True(encounter.Id > 0);
        Assert.Equal(1L, await fixture.RootScalarAsync<long>($"SELECT COUNT(*) FROM form_encounter WHERE id = {encounter.Id}"));
        // Stored in RFC 4122 byte order (what PHP's Ramsey getBytes() produces), checked against the plain text form.
        Assert.Equal(
            encounter.Uuid.ToString("N").ToUpperInvariant(),
            await fixture.RootScalarAsync<string>($"SELECT HEX(uuid) FROM form_encounter WHERE id = {encounter.Id}"));
        Assert.Equal(EncounterTestData.PatientPid, await fixture.RootScalarAsync<long>($"SELECT pid FROM form_encounter WHERE id = {encounter.Id}"));
        Assert.Equal(1001L, await fixture.RootScalarAsync<long>($"SELECT encounter FROM form_encounter WHERE id = {encounter.Id}"));
        Assert.Equal("AMB", await fixture.RootScalarAsync<string>($"SELECT class_code FROM form_encounter WHERE id = {encounter.Id}"));
        Assert.Equal("Main Clinic", await fixture.RootScalarAsync<string>($"SELECT facility FROM form_encounter WHERE id = {encounter.Id}"));
        Assert.Equal(3L, await fixture.RootScalarAsync<long>($"SELECT facility_id FROM form_encounter WHERE id = {encounter.Id}"));
        Assert.Equal(11L, await fixture.RootScalarAsync<long>($"SELECT pos_code FROM form_encounter WHERE id = {encounter.Id}"));
        Assert.Equal(7L, await fixture.RootScalarAsync<long>($"SELECT provider_id FROM form_encounter WHERE id = {encounter.Id}"));
        Assert.Equal(5L, await fixture.RootScalarAsync<long>($"SELECT pc_catid FROM form_encounter WHERE id = {encounter.Id}"));
        Assert.Equal("Cough", await fixture.RootScalarAsync<string>($"SELECT reason FROM form_encounter WHERE id = {encounter.Id}"));
    }

    [Fact]
    public async Task CC_7_Register_also_writes_the_legacy_newpatient_forms_row()
    {
        var encounter = EncounterTestData.NewEncounter();

        await RegisterAsync(encounter);

        Assert.Equal(1L, await fixture.RootScalarAsync<long>("SELECT COUNT(*) FROM forms"));
        Assert.Equal("newpatient", await fixture.RootScalarAsync<string>("SELECT formdir FROM forms"));
        Assert.Equal("New Patient Encounter", await fixture.RootScalarAsync<string>("SELECT form_name FROM forms"));
        // form_id is the form_encounter row's id, and encounter is the business number: the two different
        // "identifiers" the m2 inventory warns about, each in the right column.
        Assert.Equal(encounter.Id, await fixture.RootScalarAsync<long>("SELECT form_id FROM forms"));
        Assert.Equal(1001L, await fixture.RootScalarAsync<long>("SELECT encounter FROM forms"));
        Assert.Equal(EncounterTestData.PatientPid, await fixture.RootScalarAsync<long>("SELECT pid FROM forms"));
        Assert.Equal("dr_who", await fixture.RootScalarAsync<string>("SELECT user FROM forms"));
        Assert.Equal("Default", await fixture.RootScalarAsync<string>("SELECT groupname FROM forms"));
        Assert.Equal(0L, await fixture.RootScalarAsync<long>("SELECT deleted FROM forms"));
        Assert.Equal(1L, await fixture.RootScalarAsync<long>("SELECT authorized FROM forms"));
    }

    [Fact]
    public async Task F_CC_10_a_provider_id_above_127_does_not_overflow_the_tinyint_authorized_column()
    {
        // Legacy writes the provider id into forms.authorized (tinyint) and, with sql_mode = '', silently truncates.
        // This service is strict, so that would be a hard error; it writes the 0/1 flag instead (CC-7 deviation).
        var encounter = EncounterTestData.NewEncounter(providerId: 5000);

        await RegisterAsync(encounter);

        Assert.True(encounter.Id > 0);
        Assert.Equal(1L, await fixture.RootScalarAsync<long>("SELECT authorized FROM forms"));
        Assert.Equal(5000L, await fixture.RootScalarAsync<long>("SELECT provider_id FROM form_encounter"));
    }

    [Fact]
    public async Task CC_7_an_unknown_provider_still_registers_with_a_null_user_which_is_a_tracked_finding()
    {
        // F-CC-6: provider existence is not validated yet. The adapter does not invent a username.
        var encounter = EncounterTestData.NewEncounter(providerId: 999);

        await RegisterAsync(encounter);

        Assert.Null(await fixture.RootScalarAsync<string>("SELECT user FROM forms"));
        Assert.Equal("Default", await fixture.RootScalarAsync<string>("SELECT groupname FROM forms"));
    }

    [Fact]
    public async Task CC_6_Register_records_the_uuid_in_the_legacy_uuid_registry()
    {
        var encounter = EncounterTestData.NewEncounter();

        await RegisterAsync(encounter);

        var hex = encounter.Uuid.ToString("N").ToUpperInvariant();
        Assert.Equal("form_encounter", await fixture.RootScalarAsync<string>($"SELECT table_name FROM uuid_registry WHERE HEX(uuid) = '{hex}'"));
        Assert.Equal("id", await fixture.RootScalarAsync<string>("SELECT table_id FROM uuid_registry"));
    }

    [Fact]
    public async Task CC_11_Register_writes_the_event_in_its_envelope_to_the_outbox()
    {
        var encounter = EncounterTestData.NewEncounter();

        await RegisterAsync(encounter);

        Assert.Equal(1L, await fixture.RootScalarAsync<long>("SELECT COUNT(*) FROM clinical_care_outbox"));
        Assert.Equal("EncounterCreated", await fixture.RootScalarAsync<string>("SELECT event_type FROM clinical_care_outbox"));
        Assert.Null(await fixture.RootScalarAsync<string>("SELECT published_at FROM clinical_care_outbox"));

        var json = await fixture.RootScalarAsync<string>("SELECT payload FROM clinical_care_outbox");
        using var doc = System.Text.Json.JsonDocument.Parse(json!);
        var root = doc.RootElement;
        Assert.Equal("EncounterCreated", root.GetProperty("eventType").GetString());
        Assert.Equal(1, root.GetProperty("schemaVersion").GetInt32());
        Assert.Equal(encounter.Uuid.ToString(), root.GetProperty("subject").GetString());
        Assert.Equal("user:7", root.GetProperty("actor").GetString());
        Assert.True(Guid.TryParse(root.GetProperty("eventId").GetString(), out _));
        Assert.False(string.IsNullOrEmpty(root.GetProperty("correlationId").GetString()));
        var payload = root.GetProperty("payload");
        Assert.Equal(encounter.Uuid, payload.GetProperty("encounterUuid").GetGuid());
        Assert.Equal(1001, payload.GetProperty("encounterNumber").GetInt64());
        Assert.Equal(EncounterTestData.PatientUuid, payload.GetProperty("patientUuid").GetGuid());
        Assert.Equal(EncounterTestData.PatientPid, payload.GetProperty("patientPid").GetInt64());
        Assert.Equal("AMB", payload.GetProperty("classCode").GetString());
    }

    [Fact]
    public async Task OPS_9_registering_the_same_identity_again_writes_nothing_twice()
    {
        // Simulates the retry after a commit whose acknowledgement was lost: same uuid, same number.
        var uuid = Guid.CreateVersion7();
        var first = EncounterTestData.NewEncounter(uuid: uuid);
        var retried = EncounterTestData.NewEncounter(uuid: uuid);

        await RegisterAsync(first);
        await RegisterAsync(retried);

        Assert.Equal(first.Id, retried.Id);
        Assert.Equal(1L, await fixture.RootScalarAsync<long>("SELECT COUNT(*) FROM form_encounter"));
        Assert.Equal(1L, await fixture.RootScalarAsync<long>("SELECT COUNT(*) FROM forms"));
        Assert.Equal(1L, await fixture.RootScalarAsync<long>("SELECT COUNT(*) FROM uuid_registry"));
        Assert.Equal(1L, await fixture.RootScalarAsync<long>("SELECT COUNT(*) FROM clinical_care_outbox"));
    }

    [Fact]
    public async Task QLT_12_a_failure_part_way_through_leaves_no_partial_encounter()
    {
        // Force the third write to fail: the uuid is already in the registry, so its primary key collides after
        // form_encounter and forms have been written inside the same transaction.
        var encounter = EncounterTestData.NewEncounter();
        await fixture.RootExecuteAsync(
            $"INSERT INTO uuid_registry (uuid, table_name) VALUES (UNHEX('{encounter.Uuid:N}'), 'form_encounter')");

        await Assert.ThrowsAnyAsync<Exception>(() => RegisterAsync(encounter));

        Assert.Equal(0L, await fixture.RootScalarAsync<long>("SELECT COUNT(*) FROM form_encounter"));
        Assert.Equal(0L, await fixture.RootScalarAsync<long>("SELECT COUNT(*) FROM forms"));
        Assert.Equal(0L, await fixture.RootScalarAsync<long>("SELECT COUNT(*) FROM clinical_care_outbox"));
        Assert.Equal(0, encounter.Id);
    }

    [Fact]
    public async Task Register_writes_its_audit_row_in_the_same_transaction()
    {
        var encounter = EncounterTestData.NewEncounter();

        await RegisterAsync(encounter);

        Assert.Equal(1L, await fixture.RootScalarAsync<long>("SELECT COUNT(*) FROM clinical_care_audit"));
        Assert.Equal("encounter.create", await fixture.RootScalarAsync<string>("SELECT action FROM clinical_care_audit"));
        Assert.Equal(encounter.Uuid.ToString(), await fixture.RootScalarAsync<string>("SELECT CONCAT(subject, '') FROM clinical_care_audit"));
        Assert.Equal("success", await fixture.RootScalarAsync<string>("SELECT outcome FROM clinical_care_audit"));
        Assert.Equal($"user:{encounter.ProviderId}", await fixture.RootScalarAsync<string>("SELECT actor FROM clinical_care_audit"));
        Assert.Equal(0L, await fixture.RootScalarAsync<long>("SELECT actor_verified FROM clinical_care_audit"));
        Assert.Equal("corr-enc", await fixture.RootScalarAsync<string>("SELECT correlation_id FROM clinical_care_audit"));
        Assert.Equal("10.1.2.3", await fixture.RootScalarAsync<string>("SELECT source_address FROM clinical_care_audit"));
    }

    [Fact]
    public async Task OPS_9_a_retried_registration_does_not_audit_twice()
    {
        var uuid = Guid.CreateVersion7();

        await RegisterAsync(EncounterTestData.NewEncounter(uuid: uuid));
        await RegisterAsync(EncounterTestData.NewEncounter(uuid: uuid));

        Assert.Equal(1L, await fixture.RootScalarAsync<long>("SELECT COUNT(*) FROM clinical_care_audit"));
    }

    [Fact]
    public async Task SEC_9_an_encounter_cannot_commit_without_its_audit_row()
    {
        // The audit insert is the last statement before the commit. If it fails, everything before it must roll back.
        var encounter = EncounterTestData.NewEncounter();
        await using var db = fixture.CreateDbContext();

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            new EfEncounterRegistry(db, new ThrowingAuditLog()).RegisterAsync(encounter, EventFor(encounter), AuditFor(encounter), CancellationToken.None));

        Assert.Equal(0L, await fixture.RootScalarAsync<long>("SELECT COUNT(*) FROM form_encounter"));
        Assert.Equal(0L, await fixture.RootScalarAsync<long>("SELECT COUNT(*) FROM forms"));
        Assert.Equal(0L, await fixture.RootScalarAsync<long>("SELECT COUNT(*) FROM uuid_registry"));
        Assert.Equal(0L, await fixture.RootScalarAsync<long>("SELECT COUNT(*) FROM clinical_care_outbox"));
        Assert.Equal(0L, await fixture.RootScalarAsync<long>("SELECT COUNT(*) FROM clinical_care_audit"));
    }

    [Fact]
    public async Task A_legacy_encounter_row_is_left_alone()
    {
        // Another legacy writer's row already in the table: this adapter only ever adds its own.
        await fixture.RootExecuteAsync(
            "INSERT INTO form_encounter (uuid, date, pid, encounter, reason) VALUES (UNHEX('11111111111141118111111111111111'), NOW(), 42, 500, 'legacy visit')");

        await RegisterAsync(EncounterTestData.NewEncounter());

        Assert.Equal(2L, await fixture.RootScalarAsync<long>("SELECT COUNT(*) FROM form_encounter"));
        Assert.Equal("legacy visit", await fixture.RootScalarAsync<string>("SELECT reason FROM form_encounter WHERE encounter = 500"));
    }
}
