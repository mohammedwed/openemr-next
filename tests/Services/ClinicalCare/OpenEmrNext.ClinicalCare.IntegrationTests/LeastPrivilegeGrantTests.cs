using Microsoft.EntityFrameworkCore;

namespace OpenEmrNext.ClinicalCare.IntegrationTests;

/// <summary>
/// LEG-6 as a test: the principal the seed script creates can do what the service needs, and nothing more.
/// Each denial is a statement a future change might be tempted to add; if the grant set widens, this fails.
/// </summary>
[Collection("ClinicalCareContainer")]
public class LeastPrivilegeGrantTests(ClinicalCareContainerFixture fixture) : IAsyncLifetime
{
    public Task InitializeAsync() => fixture.ResetAsync();
    public Task DisposeAsync() => Task.CompletedTask;

    private async Task AssertDeniedAsync(string sql)
    {
        await using var db = fixture.CreateDbContext();

        var ex = await Assert.ThrowsAnyAsync<Exception>(() => db.Database.ExecuteSqlRawAsync(sql));

        Assert.Contains("denied", ex.ToString(), StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData("UPDATE form_encounter SET billing_note = 'changed'")]
    [InlineData("UPDATE form_encounter SET sensitivity = 'high'")]
    [InlineData("UPDATE form_encounter SET pid = 1")]
    [InlineData("UPDATE form_encounter SET encounter = 1")]
    [InlineData("UPDATE form_encounter SET uuid = NULL")]
    [InlineData("UPDATE form_encounter SET in_collection = 1")]
    [InlineData("UPDATE form_encounter SET last_level_billed = 1")]
    [InlineData("UPDATE form_encounter SET last_update = NOW()")]
    [InlineData("UPDATE form_encounter SET reason = 'changed', billing_note = 'x'")]
    [InlineData("DELETE FROM form_encounter")]
    // A forms row can be flagged deleted and nothing else about it changed (ADR 0076): every other column, and removing the row, stays refused.
    [InlineData("UPDATE forms SET formdir = 'vitals'")]
    [InlineData("UPDATE forms SET form_id = 1")]
    [InlineData("UPDATE forms SET pid = 1")]
    [InlineData("UPDATE forms SET encounter = 1")]
    [InlineData("UPDATE forms SET `user` = 'x'")]
    [InlineData("UPDATE forms SET authorized = 1")]
    [InlineData("UPDATE forms SET deleted = 1, formdir = 'x'")]
    [InlineData("DELETE FROM forms")]
    [InlineData("DELETE FROM uuid_registry")]
    [InlineData("DELETE FROM sequences")]
    [InlineData("DELETE FROM form_clinical_notes")]
    [InlineData("UPDATE form_clinical_notes SET `user` = 'x'")]
    [InlineData("UPDATE form_clinical_notes SET groupname = 'x'")]
    [InlineData("UPDATE form_clinical_notes SET authorized = 0")]
    [InlineData("UPDATE form_clinical_notes SET pid = 1")]
    [InlineData("UPDATE form_clinical_notes SET encounter = '1'")]
    [InlineData("UPDATE form_clinical_notes SET form_id = 1")]
    [InlineData("UPDATE form_clinical_notes SET uuid = NULL")]
    [InlineData("UPDATE form_clinical_notes SET note_related_to = 'x'")]
    [InlineData("UPDATE form_clinical_notes SET external_id = 'x'")]
    [InlineData("UPDATE form_clinical_notes SET description = 'x', pid = 1")]
    [InlineData("UPDATE list_options SET title = 'x'")]
    [InlineData("DELETE FROM form_soap")]
    [InlineData("UPDATE form_soap SET pid = 1")]
    [InlineData("INSERT INTO openemr_postcalendar_categories (pc_catname) VALUES ('x')")]
    [InlineData("UPDATE openemr_postcalendar_categories SET pc_active = 0")]
    [InlineData("DELETE FROM openemr_postcalendar_categories")]
    [InlineData("UPDATE facility SET inactive = 1")]
    [InlineData("UPDATE form_soap SET `date` = NOW()")]
    [InlineData("UPDATE form_soap SET `user` = 'x'")]
    [InlineData("UPDATE form_soap SET groupname = 'x'")]
    [InlineData("UPDATE form_soap SET authorized = 0")]
    [InlineData("UPDATE form_soap SET activity = 0")]
    [InlineData("UPDATE form_soap SET plan = 'x', pid = 1")]
    [InlineData("DELETE FROM immunizations")]
    [InlineData("UPDATE immunizations SET patient_id = 1")]
    [InlineData("UPDATE immunizations SET uuid = NULL")]
    [InlineData("UPDATE immunizations SET create_date = NOW()")]
    [InlineData("UPDATE immunizations SET created_by = 1")]
    [InlineData("UPDATE immunizations SET updated_by = 1")]
    [InlineData("UPDATE immunizations SET external_id = 'x'")]
    [InlineData("UPDATE immunizations SET note = 'x', patient_id = 1")]
    [InlineData("UPDATE form_vitals SET `user` = 'x'")]
    [InlineData("UPDATE form_vitals SET groupname = 'x'")]
    [InlineData("UPDATE form_vitals SET `date` = NOW()")]
    [InlineData("UPDATE form_vitals SET authorized = 0")]
    [InlineData("UPDATE form_vitals SET activity = 0")]
    [InlineData("UPDATE form_vitals SET pid = 1")]
    [InlineData("UPDATE form_vitals SET uuid = NULL")]
    [InlineData("UPDATE form_vitals SET last_updated = NOW()")]
    [InlineData("UPDATE form_vitals SET weight = 1, `user` = 'x'")]
    [InlineData("DELETE FROM form_vitals")]
    [InlineData("INSERT INTO patient_data (pid) VALUES (1)")]
    [InlineData("UPDATE patient_data SET fname = 'x'")]
    [InlineData("INSERT INTO users (username) VALUES ('intruder')")]
    [InlineData("UPDATE users SET active = 0")]
    [InlineData("UPDATE users SET uuid = NULL")]
    [InlineData("UPDATE users SET username = 'x'")]
    [InlineData("UPDATE users SET fname = 'x'")]
    [InlineData("UPDATE users SET authorized = 0")]
    [InlineData("UPDATE gacl_acl SET allow = 1")]
    [InlineData("UPDATE gacl_aro_groups SET lft = 1")]
    [InlineData("INSERT INTO gacl_aro_map (acl_id, section_value, value) VALUES (1, 'users', 'x')")]
    [InlineData("INSERT INTO gacl_aro_groups_map (acl_id, group_id) VALUES (1, 1)")]
    [InlineData("INSERT INTO gacl_groups_aro_map (group_id, aro_id) VALUES (1, 1)")]
    [InlineData("DELETE FROM gacl_aco_map")]
    [InlineData("DELETE FROM gacl_aro")]
    [InlineData("DROP TABLE form_encounter")]
    public async Task The_production_principal_cannot_change_what_it_does_not_own(string sql) =>
        await AssertDeniedAsync(sql);

    [Theory]
    [InlineData("SELECT password FROM users")]
    [InlineData("SELECT email FROM users")]
    [InlineData("SELECT mname FROM users")]
    [InlineData("SELECT phone FROM users")]
    [InlineData("SELECT title FROM users")]
    [InlineData("SELECT specialty FROM users")]
    [InlineData("SELECT fname, password FROM users")]
    [InlineData("SELECT npi FROM users")]
    [InlineData("SELECT federaldrugid FROM users")]
    [InlineData("SELECT * FROM users")]
    [InlineData("SELECT * FROM gacl_acl")]
    [InlineData("SELECT note FROM gacl_acl")]
    [InlineData("SELECT return_value FROM gacl_acl")]
    [InlineData("SELECT updated_date FROM gacl_acl")]
    [InlineData("SELECT name FROM gacl_aro")]
    [InlineData("SELECT hidden FROM gacl_aro")]
    [InlineData("SELECT name FROM gacl_aro_groups")]
    [InlineData("SELECT parent_id FROM gacl_aro_groups")]
    [InlineData("SELECT * FROM gacl_axo_map")]
    [InlineData("SELECT fname FROM patient_data")]
    [InlineData("SELECT * FROM patient_data")]
    [InlineData("SELECT uuid FROM patient_data")]
    [InlineData("SELECT pid FROM patient_data")]
    [InlineData("SELECT phone FROM facility")]
    [InlineData("SELECT street FROM facility")]
    [InlineData("SELECT federal_ein FROM facility")]
    [InlineData("SELECT pc_catcolor FROM openemr_postcalendar_categories")]
    [InlineData("SELECT pc_catdesc FROM openemr_postcalendar_categories")]
    [InlineData("SELECT pc_recurrspec FROM openemr_postcalendar_categories")]
    [InlineData("SELECT codes FROM list_options")]
    [InlineData("SELECT toggle_setting_1 FROM list_options")]
    [InlineData("SELECT mapping FROM list_options")]
    [InlineData("SELECT * FROM list_options")]
    public async Task The_production_principal_cannot_read_columns_or_tables_it_has_no_need_for(string sql)
    {
        await using var db = fixture.CreateDbContext();

        // Reading is done through a command that actually executes (not a lazy query), so the privilege check runs.
        var ex = await Assert.ThrowsAnyAsync<Exception>(async () =>
        {
            await using var command = db.Database.GetDbConnection().CreateCommand();
            await db.Database.OpenConnectionAsync();
            command.CommandText = sql;
            await command.ExecuteScalarAsync();
        });

        Assert.Contains("denied", ex.ToString(), StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData("date")]
    [InlineData("onset_date")]
    [InlineData("reason")]
    [InlineData("class_code")]
    [InlineData("pc_catid")]
    [InlineData("facility_id")]
    [InlineData("facility")]
    [InlineData("pos_code")]
    [InlineData("provider_id")]
    public async Task The_production_principal_may_update_exactly_the_encounter_columns_it_owns(string column)
    {
        await using var db = fixture.CreateDbContext();

        // Setting a column to itself is enough to exercise the privilege check; no row is needed. The column name is one of this
        // theory's own constants, never input, which is why the interpolation (EF1002) is safe here.
#pragma warning disable EF1002
        await db.Database.ExecuteSqlRawAsync($"UPDATE form_encounter SET `{column}` = `{column}`");
#pragma warning restore EF1002
    }

    [Theory]
    [InlineData("SELECT id, uuid, `date`, type, title, begdate, enddate, diagnosis, outcome, occurrence, referredby, comments, activity, pid, `user`, modifydate, reaction, severity_al, verification FROM lists")]
    [InlineData("SELECT pid, type, `date` FROM lists_touch")]
    public async Task The_production_principal_can_read_the_issue_columns_a_problem_or_an_allergy_uses(string sql)
    {
        await using var db = fixture.CreateDbContext();
        await using var command = db.Database.GetDbConnection().CreateCommand();
        await db.Database.OpenConnectionAsync();
        command.CommandText = sql;

        await command.ExecuteScalarAsync();   // must not throw
    }

    [Theory]
    [InlineData("SELECT udi FROM lists")]
    [InlineData("SELECT udi_data FROM lists")]
    [InlineData("SELECT injury_part FROM lists")]
    [InlineData("SELECT injury_type FROM lists")]
    [InlineData("SELECT reinjury_id FROM lists")]
    [InlineData("SELECT destination FROM lists")]
    [InlineData("SELECT external_id FROM lists")]
    [InlineData("SELECT erx_source FROM lists")]
    [InlineData("SELECT * FROM lists")]
    [InlineData("SELECT * FROM lists_medication")]
    public async Task The_production_principal_cannot_read_issue_columns_or_tables_a_problem_has_no_need_for(string sql)
    {
        await using var db = fixture.CreateDbContext();

        var ex = await Assert.ThrowsAnyAsync<Exception>(async () =>
        {
            await using var command = db.Database.GetDbConnection().CreateCommand();
            await db.Database.OpenConnectionAsync();
            command.CommandText = sql;
            await command.ExecuteScalarAsync();
        });

        Assert.Contains("denied", ex.ToString(), StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData("UPDATE lists SET type = 'medication'")]
    [InlineData("UPDATE lists SET uuid = NULL")]
    [InlineData("UPDATE lists SET pid = 1")]
    [InlineData("UPDATE lists SET `date` = NOW()")]
    [InlineData("UPDATE lists SET `user` = 'x'")]
    [InlineData("UPDATE lists SET groupname = 'x'")]
    [InlineData("UPDATE lists SET activity = 0")]
    [InlineData("UPDATE lists SET modifydate = NOW()")]
    [InlineData("UPDATE lists SET udi = 'x'")]
    [InlineData("UPDATE lists SET injury_part = 'x'")]
    [InlineData("UPDATE lists SET list_option_id = 'x'")]
    [InlineData("UPDATE lists SET title = 'changed', udi = 'x'")]
    [InlineData("DELETE FROM lists")]
    [InlineData("DELETE FROM lists_touch WHERE pid = 1")]
    [InlineData("UPDATE lists_touch SET `date` = NOW()")]
    [InlineData("INSERT INTO lists (uuid, type, udi) VALUES (NULL, 'medical_problem', 'x')")]
    [InlineData("INSERT INTO lists (uuid, type, list_option_id) VALUES (NULL, 'allergy', 'x')")]
    [InlineData("INSERT INTO lists (uuid, type, subtype) VALUES (NULL, 'allergy', 'x')")]
    [InlineData("INSERT INTO lists (uuid, type, injury_part) VALUES (NULL, 'medical_problem', 'x')")]
    [InlineData("INSERT INTO lists_medication (list_id) VALUES (1)")]
    [InlineData("UPDATE issue_encounter SET resolved = 1")]
    [InlineData("UPDATE issue_encounter SET created_by = 1")]
    [InlineData("DELETE FROM uuid_registry WHERE table_name = 'issue_encounter'")]
    [InlineData("TRUNCATE TABLE issue_encounter")]
    public async Task The_production_principal_cannot_change_or_remove_issues_beyond_inserting_a_problems_columns(string sql) =>
        await AssertDeniedAsync(sql);

    [Fact]
    public async Task The_production_principal_can_flag_a_registry_row_deleted_and_a_clinical_note_inactive_and_that_is_all_a_deletion_needs()
    {
        await using var db = fixture.CreateDbContext();

        // ADR 0076: the two flags a deletion sets, on rows that do not exist here (zero rows matched is still a statement the grant allowed).
        await db.Database.ExecuteSqlRawAsync("UPDATE forms SET deleted = 1 WHERE id = -1");
        await db.Database.ExecuteSqlRawAsync("UPDATE form_clinical_notes SET activity = 0 WHERE id = -1");
    }

    [Fact]
    public async Task The_production_principal_can_read_exactly_the_user_columns_that_identify_the_acting_user_and_name_a_provider()
    {
        await using var db = fixture.CreateDbContext();

        // The forwarded uuid resolves to an id and username, and an inactive account is refused (ADR 0075); the providers lookup (M5f) adds the name and legacy's authorized flag.
        // Seven columns, none of the credential or contact ones.
        await db.Database.ExecuteSqlRawAsync("SELECT id, username, uuid, active, fname, lname, authorized FROM users LIMIT 1");
    }

    [Fact]
    public async Task The_production_principal_can_read_exactly_the_access_control_columns_the_permission_check_joins()
    {
        await using var db = fixture.CreateDbContext();

        // ADR 0076: the single decision query, written out in full, so a column dropped from the grant fails here and not on the first delete in a running stack.
        await db.Database.ExecuteSqlRawAsync(
            """
            SELECT a.id, a.allow, ac.section_value, ac.value, ar.section_value, ar.value, ax.acl_id, axg.acl_id, arg.group_id,
                   o.id, o.section_value, o.value, gm.group_id, gm.aro_id, g1.id, g1.lft, g1.rgt
            FROM gacl_acl a
            LEFT JOIN gacl_aco_map ac ON ac.acl_id = a.id
            LEFT JOIN gacl_aro_map ar ON ar.acl_id = a.id
            LEFT JOIN gacl_aro_groups_map arg ON arg.acl_id = a.id
            LEFT JOIN gacl_axo_map ax ON ax.acl_id = a.id
            LEFT JOIN gacl_axo_groups_map axg ON axg.acl_id = a.id
            LEFT JOIN gacl_aro o ON o.id = 0
            LEFT JOIN gacl_groups_aro_map gm ON gm.aro_id = o.id
            LEFT JOIN gacl_aro_groups g1 ON g1.id = gm.group_id
            WHERE a.enabled = 1 LIMIT 1
            """);
    }

    [Fact]
    public async Task The_production_principal_can_read_the_option_columns_the_lookups_use_including_a_note_types_loinc_code_and_whether_it_is_active()
    {
        await using var db = fixture.CreateDbContext();

        // Granted for the clinical note types (ADR 0074): notes holds the LOINC code legacy stores with the type, activity whether it is still offered.
        await db.Database.ExecuteSqlRawAsync("SELECT list_id, option_id, title, seq, is_default, notes, activity FROM list_options LIMIT 1");
    }

    [Theory]
    [InlineData("date")]
    [InlineData("code")]
    [InlineData("codetext")]
    [InlineData("description")]
    [InlineData("clinical_notes_type")]
    [InlineData("clinical_notes_category")]
    public async Task The_production_principal_may_update_exactly_the_clinical_note_columns_it_owns(string column)
    {
        await using var db = fixture.CreateDbContext();

        // The column name is one of this theory's own constants, never input, which is why the interpolation (EF1002) is safe here.
#pragma warning disable EF1002
        await db.Database.ExecuteSqlRawAsync($"UPDATE form_clinical_notes SET `{column}` = `{column}`");
#pragma warning restore EF1002
    }

    [Theory]
    [InlineData("subjective")]
    [InlineData("objective")]
    [InlineData("assessment")]
    [InlineData("plan")]
    public async Task The_production_principal_may_update_exactly_the_four_soap_sections(string column)
    {
        await using var db = fixture.CreateDbContext();

        // The column name is one of this theory's own constants, never input, which is why the interpolation (EF1002) is safe here.
#pragma warning disable EF1002
        await db.Database.ExecuteSqlRawAsync($"UPDATE form_soap SET `{column}` = `{column}`");
#pragma warning restore EF1002
    }

    [Theory]
    [InlineData("administered_date")]
    [InlineData("immunization_id")]
    [InlineData("cvx_code")]
    [InlineData("manufacturer")]
    [InlineData("lot_number")]
    [InlineData("administered_by_id")]
    [InlineData("administered_by")]
    [InlineData("education_date")]
    [InlineData("vis_date")]
    [InlineData("note")]
    [InlineData("amount_administered")]
    [InlineData("amount_administered_unit")]
    [InlineData("expiration_date")]
    [InlineData("route")]
    [InlineData("administration_site")]
    [InlineData("added_erroneously")]
    [InlineData("completion_status")]
    [InlineData("information_source")]
    [InlineData("refusal_reason")]
    [InlineData("ordering_provider")]
    [InlineData("reason_code")]
    [InlineData("reason_description")]
    [InlineData("encounter_id")]
    [InlineData("update_date")]
    public async Task The_production_principal_may_update_exactly_the_immunization_columns_it_owns(string column)
    {
        await using var db = fixture.CreateDbContext();

        // Setting a column to itself is enough to exercise the privilege check; no row is needed. The column name is one of this theory's own constants.
#pragma warning disable EF1002
        await db.Database.ExecuteSqlRawAsync($"UPDATE immunizations SET `{column}` = `{column}`");
#pragma warning restore EF1002
    }

    [Theory]
    [InlineData("weight")]
    [InlineData("height")]
    [InlineData("temperature")]
    [InlineData("temp_method")]
    [InlineData("bps")]
    [InlineData("bpd")]
    [InlineData("pulse")]
    [InlineData("respiration")]
    [InlineData("oxygen_saturation")]
    [InlineData("waist_circ")]
    [InlineData("head_circ")]
    [InlineData("BMI")]
    [InlineData("BMI_status")]
    [InlineData("note")]
    public async Task The_production_principal_may_update_exactly_the_vitals_measurement_columns(string column)
    {
        await using var db = fixture.CreateDbContext();

        // Setting a column to itself is enough to exercise the privilege check; no row is needed. The column name is one of this theory's own constants, never
        // input, which is why the interpolation (EF1002) is safe here.
#pragma warning disable EF1002
        await db.Database.ExecuteSqlRawAsync($"UPDATE form_vitals SET `{column}` = `{column}`");
#pragma warning restore EF1002
    }

    [Theory]
    [InlineData("title")]
    [InlineData("begdate")]
    [InlineData("enddate")]
    [InlineData("diagnosis")]
    [InlineData("outcome")]
    [InlineData("occurrence")]
    [InlineData("referredby")]
    [InlineData("comments")]
    [InlineData("verification")]
    [InlineData("reaction")]
    [InlineData("severity_al")]
    public async Task The_production_principal_may_update_exactly_the_issue_columns_a_problem_or_an_allergy_owns(string column)
    {
        await using var db = fixture.CreateDbContext();

        // Setting a column to itself is enough to exercise the privilege check; no row is needed. The column name is one of this theory's own constants, never
        // input, which is why the interpolation (EF1002) is safe here.
#pragma warning disable EF1002
        await db.Database.ExecuteSqlRawAsync($"UPDATE lists SET `{column}` = `{column}`");
#pragma warning restore EF1002
    }

    [Fact]
    public async Task The_production_principal_can_read_make_and_remove_issue_links_but_not_edit_them()
    {
        await using var db = fixture.CreateDbContext();

        await db.Database.ExecuteSqlRawAsync("INSERT INTO issue_encounter (uuid, pid, list_id, encounter, resolved, created_by, updated_by) VALUES (NULL, 1, 1, 1, 0, NULL, NULL)");
        var count = await db.Database.SqlQueryRaw<long>("SELECT COUNT(*) AS Value FROM issue_encounter").SingleAsync();
        await db.Database.ExecuteSqlRawAsync("DELETE FROM issue_encounter WHERE pid = 1");

        Assert.Equal(1L, count);
    }

    [Fact]
    public async Task The_production_principal_can_insert_exactly_the_columns_a_problem_or_an_allergy_uses()
    {
        await using var db = fixture.CreateDbContext();

        await db.Database.ExecuteSqlRawAsync(
            "INSERT INTO lists (uuid, `date`, type, title, begdate, enddate, diagnosis, outcome, occurrence, referredby, comments, activity, pid, `user`, groupname, reaction, severity_al, verification) " +
            "VALUES (NULL, NOW(), 'allergy', 't', NOW(), NULL, NULL, 0, 0, NULL, NULL, 1, 1, NULL, 'Default', 'hives', 'severe', 'confirmed')");
    }

    [Fact]
    public async Task The_production_principal_owns_its_outbox_completely()
    {
        await using var db = fixture.CreateDbContext();

        await db.Database.ExecuteSqlRawAsync(
            "INSERT INTO clinical_care_outbox (event_type, payload, occurred_at) VALUES ('x', 'payload', NOW())");
        await db.Database.ExecuteSqlRawAsync("UPDATE clinical_care_outbox SET published_at = NOW()");
        await db.Database.ExecuteSqlRawAsync("DELETE FROM clinical_care_outbox");
    }

    [Fact]
    public async Task Running_the_seed_script_again_is_harmless_and_the_principal_still_works()
    {
        // The fixture already ran it once. A second run must succeed and leave a working user (LEG-18).
        await fixture.RunSeedScriptAsync();
        await using var db = fixture.CreateDbContext();

        Assert.Equal(1000L, await db.Database.SqlQueryRaw<long>("SELECT id AS Value FROM sequences").SingleAsync());
    }
}
