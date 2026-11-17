using OpenEmrNext.ClinicalCare.Application.Ports;
using OpenEmrNext.ClinicalCare.Domain.Allergies;
using OpenEmrNext.ClinicalCare.Infrastructure;

namespace OpenEmrNext.ClinicalCare.IntegrationTests;

/// <summary>
/// The allergy write adapter against a real MariaDB, as the production principal (LEG-2, ADR 0070): the <c>lists</c> row legacy would write, the rows that
/// go with it, the same atomicity and retry guarantees as problems, and the two NOT NULL columns an absent value must not violate.
/// </summary>
[Collection("ClinicalCareContainer")]
public class AllergyRegistryTests(ClinicalCareContainerFixture fixture) : IAsyncLifetime
{
    public Task InitializeAsync() => fixture.ResetAsync();
    public Task DisposeAsync() => Task.CompletedTask;

    private static AllergyRecordedEvent EventFor(Allergy a) => new(a.Uuid, a.Patient.Uuid, a.Patient.Pid, AllergyTestData.Now.ToUniversalTime());

    internal static AuditEntry AuditFor(Allergy a) =>
        new(AllergyTestData.Now.ToUniversalTime(), new AuditActor($"user:{a.ProviderId}", a.ProviderId, Verified: false),
            AuditActions.AllergyRecord, a.Uuid, AuditOutcome.Success, correlationId: "corr-allergy", sourceAddress: "10.8.8.8");

    private async Task RegisterAsync(Allergy allergy)
    {
        await using var db = fixture.CreateDbContext();
        await new EfAllergyRegistry(db, new EfAuditLog(db)).RegisterAsync(allergy, EventFor(allergy), AuditFor(allergy), CancellationToken.None);
    }

    private Task<T?> Col<T>(string column, string where = "1=1") => fixture.RootScalarAsync<T>($"SELECT {column} FROM lists WHERE {where}");

    [Fact]
    public async Task Register_writes_the_lists_row_with_legacy_shaped_values()
    {
        var allergy = AllergyTestData.NewAllergy(begin: new DateTime(2015, 3, 2), end: new DateTime(2020, 1, 1));

        await RegisterAsync(allergy);

        Assert.True(allergy.Id > 0);
        Assert.Equal("allergy", await Col<string>("type"));
        Assert.Equal("Penicillin", await Col<string>("title"));
        Assert.Equal(new DateTime(2015, 3, 2), await Col<DateTime>("begdate"));
        Assert.Equal(new DateTime(2020, 1, 1), await Col<DateTime>("enddate"));
        Assert.Equal("SNOMED-CT:91936005", await Col<string>("diagnosis"));
        Assert.Equal("hives", await Col<string>("reaction"));
        Assert.Equal("severe", await Col<string>("severity_al"));
        Assert.Equal("confirmed", await Col<string>("verification"));
        Assert.Equal("anaphylaxis", await Col<string>("comments"));
        Assert.Equal(1L, await Col<long>("activity"));
        Assert.Equal(EncounterTestData.PatientPid, await Col<long>("pid"));
        Assert.Equal(AllergyTestData.Now, await Col<DateTime>("`date`"));
        Assert.Equal("dr_who", await Col<string>("`user`"));
    }

    [Fact]
    public async Task An_allergy_with_no_begin_date_stores_null_for_it()
    {
        await RegisterAsync(AllergyTestData.NewAllergy(begin: null));

        Assert.Null(await Col<DateTime?>("begdate"));
    }

    [Fact]
    public async Task Absent_coded_fields_do_not_violate_the_not_null_columns_reaction_and_verification_are_stored_empty_and_severity_null()
    {
        // lists.reaction and lists.verification are NOT NULL DEFAULT ''; under this service's strict sql_mode a NULL would be an error, not a silent ''.
        await RegisterAsync(AllergyTestData.NewAllergy(reaction: null, severity: null, verification: null));

        Assert.Equal("", await Col<string>("reaction"));
        Assert.Equal("", await Col<string>("verification"));
        Assert.Null(await Col<string?>("severity_al"));
    }

    [Fact]
    public async Task The_uuid_is_stored_and_registered_and_the_allergy_list_is_marked_addressed()
    {
        var allergy = AllergyTestData.NewAllergy();

        await RegisterAsync(allergy);

        var hex = allergy.Uuid.ToString("N").ToUpperInvariant();
        Assert.Equal(hex, await Col<string>("HEX(uuid)"));
        Assert.Equal(1L, await fixture.RootScalarAsync<long>($"SELECT COUNT(*) FROM uuid_registry WHERE table_name = 'lists' AND HEX(uuid) = '{hex}'"));
        Assert.Equal("allergy", await fixture.RootScalarAsync<string>("SELECT type FROM lists_touch"));
    }

    [Fact]
    public async Task The_allergy_and_problem_lists_are_touched_separately()
    {
        await fixture.RootExecuteAsync($"INSERT INTO lists_touch (pid, type, `date`) VALUES ({EncounterTestData.PatientPid}, 'medical_problem', '2020-01-01 00:00:00')");

        await RegisterAsync(AllergyTestData.NewAllergy());

        Assert.Equal(2L, await fixture.RootScalarAsync<long>("SELECT COUNT(*) FROM lists_touch"));
        Assert.Equal(new DateTime(2020, 1, 1), await fixture.RootScalarAsync<DateTime>("SELECT `date` FROM lists_touch WHERE type = 'medical_problem'"));
    }

    [Fact]
    public async Task Register_writes_its_event_and_audit_row_and_neither_holds_clinical_content()
    {
        var allergy = AllergyTestData.NewAllergy();

        await RegisterAsync(allergy);

        var payload = await fixture.RootScalarAsync<string>("SELECT payload FROM clinical_care_outbox WHERE event_type = 'AllergyRecorded'");
        Assert.DoesNotContain("Penicillin", payload);
        Assert.DoesNotContain("hives", payload);
        Assert.Equal("allergy.record", await fixture.RootScalarAsync<string>("SELECT action FROM clinical_care_audit"));
        Assert.Equal(allergy.Uuid.ToString(), await fixture.RootScalarAsync<string>("SELECT CONCAT(subject, '') FROM clinical_care_audit"));
        Assert.Null(await fixture.RootScalarAsync<string?>("SELECT details FROM clinical_care_audit"));
    }

    [Fact]
    public async Task OPS_9_registering_the_same_identity_again_writes_nothing_twice()
    {
        var uuid = Guid.CreateVersion7();

        await RegisterAsync(AllergyTestData.NewAllergy(uuid: uuid));
        await RegisterAsync(AllergyTestData.NewAllergy(uuid: uuid));

        Assert.Equal(1L, await fixture.RootScalarAsync<long>("SELECT COUNT(*) FROM lists"));
        Assert.Equal(1L, await fixture.RootScalarAsync<long>("SELECT COUNT(*) FROM clinical_care_outbox"));
        Assert.Equal(1L, await fixture.RootScalarAsync<long>("SELECT COUNT(*) FROM clinical_care_audit"));
    }

    [Fact]
    public async Task QLT_12_a_failure_part_way_through_leaves_no_partial_allergy()
    {
        var allergy = AllergyTestData.NewAllergy();
        await fixture.RootExecuteAsync($"INSERT INTO uuid_registry (uuid, table_name) VALUES (UNHEX('{allergy.Uuid:N}'), 'lists')");

        await Assert.ThrowsAnyAsync<Exception>(() => RegisterAsync(allergy));

        Assert.Equal(0L, await fixture.RootScalarAsync<long>("SELECT COUNT(*) FROM lists"));
        Assert.Equal(0L, await fixture.RootScalarAsync<long>("SELECT COUNT(*) FROM lists_touch"));
        Assert.Equal(0, allergy.Id);
    }

    [Fact]
    public async Task SEC_9_an_allergy_cannot_commit_without_its_audit_row()
    {
        var allergy = AllergyTestData.NewAllergy();
        await using var db = fixture.CreateDbContext();

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            new EfAllergyRegistry(db, new ThrowingAuditLog()).RegisterAsync(allergy, EventFor(allergy), AuditFor(allergy), CancellationToken.None));

        Assert.Equal(0L, await fixture.RootScalarAsync<long>("SELECT COUNT(*) FROM lists"));
        Assert.Equal(0L, await fixture.RootScalarAsync<long>("SELECT COUNT(*) FROM lists_touch"));
        Assert.Equal(0L, await fixture.RootScalarAsync<long>("SELECT COUNT(*) FROM clinical_care_outbox"));
        Assert.Equal(0, allergy.Id);
    }

    [Fact]
    public async Task Rows_of_other_issue_types_are_left_alone_and_a_problem_with_the_same_title_is_not_confused_with_it()
    {
        await fixture.RootExecuteAsync(
            "INSERT INTO lists (uuid, type, title, pid, activity) VALUES " +
            "(UNHEX('11111111111141118111111111111111'), 'medical_problem', 'Penicillin', 42, 1), " +
            "(UNHEX('22222222222242228222222222222222'), 'medication', 'Aspirin', 42, 1)");

        await RegisterAsync(AllergyTestData.NewAllergy(title: "Penicillin"));

        Assert.Equal(3L, await fixture.RootScalarAsync<long>("SELECT COUNT(*) FROM lists"));
        Assert.Equal(1L, await fixture.RootScalarAsync<long>("SELECT COUNT(*) FROM lists WHERE type = 'allergy'"));
        Assert.Equal(1L, await fixture.RootScalarAsync<long>("SELECT COUNT(*) FROM lists WHERE type = 'medical_problem'"));
    }
}
