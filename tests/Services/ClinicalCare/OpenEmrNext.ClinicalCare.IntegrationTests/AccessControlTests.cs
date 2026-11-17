namespace OpenEmrNext.ClinicalCare.IntegrationTests;

using OpenEmrNext.ClinicalCare.Application.Ports;
using OpenEmrNext.ClinicalCare.Infrastructure;

/// <summary>
/// The permission check (M5g, ADR 0076) against a real MariaDB with legacy's own <c>gacl_*</c> DDL, as the production principal: it must answer as OpenEMR's <c>aclCheckCore</c> does for the same rules. The rules here are
/// a small tree of groups with every shape legacy's administration screens can produce: a superuser group, a group that other groups inherit from, a direct grant, a refusal, a disabled rule, and a rule scoped to an
/// extended object.
/// </summary>
[Collection("ClinicalCareContainer")]
public class AccessControlTests(ClinicalCareContainerFixture fixture) : IAsyncLifetime
{
    private static readonly AccessRight Med = new("patients", "med");
    private static readonly AccessRight Notes = new("patients", "notes");
    private static readonly AccessRight Trash = new("patients", "trash");
    private static readonly AccessRight Scoped = new("patients", "scoped");

    public async Task InitializeAsync()
    {
        await fixture.ResetAsync();

        // Groups (nested set): users(1..8) contains doctors(2..5) which contains physicians(3..4), and administrators(6..7).
        await fixture.RootExecuteAsync(
            """
            INSERT INTO gacl_aro_groups (id, parent_id, lft, rgt, name, value) VALUES
                (10, 0, 1, 8, 'OpenEMR Users', 'users'),
                (11, 10, 2, 5, 'Doctors', 'doc'),
                (12, 11, 3, 4, 'Physicians', 'phys'),
                (13, 10, 6, 7, 'Administrators', 'admin')
            """);

        // Users: a superuser, a physician (inherits Doctors), a physician refused one right, a user with a direct grant, and one with nothing.
        await fixture.RootExecuteAsync(
            """
            INSERT INTO gacl_aro (id, section_value, value, order_value, name, hidden) VALUES
                (1, 'users', 'root_admin', 1, 'Root Admin', 0),
                (2, 'users', 'dr_deep', 2, 'Dr Deep', 0),
                (3, 'users', 'direct_user', 3, 'Direct User', 0),
                (4, 'users', 'blocked_doc', 4, 'Blocked Doc', 0),
                (5, 'users', 'nobody', 5, 'Nobody', 0),
                (6, 'other', 'dr_deep', 6, 'A same-named object in another section', 0)
            """);
        await fixture.RootExecuteAsync("INSERT INTO gacl_groups_aro_map (group_id, aro_id) VALUES (13, 1), (12, 2), (12, 4)");

        // Rules. 1: Administrators hold admin/super. 2: Doctors hold patients/med (so Physicians inherit it). 3: direct_user holds patients/notes.
        // 4: blocked_doc is refused patients/med by name. 5: a disabled rule for patients/trash. 6: a rule for patients/scoped that names an extended object.
        await fixture.RootExecuteAsync(
            """
            INSERT INTO gacl_acl (id, section_value, allow, enabled, updated_date) VALUES
                (1, 'user', 1, 1, 100), (2, 'user', 1, 1, 100), (3, 'user', 1, 1, 100), (4, 'user', 0, 1, 100), (5, 'user', 1, 0, 100), (6, 'user', 1, 1, 100)
            """);
        await fixture.RootExecuteAsync(
            """
            INSERT INTO gacl_aco_map (acl_id, section_value, value) VALUES
                (1, 'admin', 'super'), (2, 'patients', 'med'), (3, 'patients', 'notes'), (4, 'patients', 'med'), (5, 'patients', 'trash'), (6, 'patients', 'scoped')
            """);
        await fixture.RootExecuteAsync("INSERT INTO gacl_aro_groups_map (acl_id, group_id) VALUES (1, 13), (2, 11), (5, 10), (6, 10)");
        await fixture.RootExecuteAsync("INSERT INTO gacl_aro_map (acl_id, section_value, value) VALUES (3, 'users', 'direct_user'), (4, 'users', 'blocked_doc')");
        await fixture.RootExecuteAsync("INSERT INTO gacl_axo_map (acl_id, section_value, value) VALUES (6, 'patient_record', '42')");
    }

    public Task DisposeAsync() => Task.CompletedTask;

    private async Task<bool> AllowedAsync(string? username, AccessRight right)
    {
        await using var db = fixture.CreateDbContext();
        return await new EfAccessControl(db).IsAllowedAsync(username, right, CancellationToken.None);
    }

    [Fact]
    public async Task A_superuser_holds_admin_super_and_every_other_right_without_a_rule_for_it()
    {
        Assert.True(await AllowedAsync("root_admin", AccessRight.AdminSuper));
        Assert.True(await AllowedAsync("root_admin", AccessRight.DeleteRecords));
        Assert.True(await AllowedAsync("root_admin", Notes));
        Assert.True(await AllowedAsync("root_admin", new AccessRight("anything", "at all")));
    }

    [Fact]
    public async Task A_member_of_a_group_inside_another_inherits_what_the_outer_group_is_granted()
    {
        // Physicians sit inside Doctors, which holds patients/med.
        Assert.True(await AllowedAsync("dr_deep", Med));
    }

    [Fact]
    public async Task What_a_group_does_not_hold_is_not_held_by_its_members()
    {
        Assert.False(await AllowedAsync("dr_deep", AccessRight.AdminSuper));
        Assert.False(await AllowedAsync("dr_deep", AccessRight.DeleteRecords));
        Assert.False(await AllowedAsync("dr_deep", Notes));
    }

    [Fact]
    public async Task A_right_granted_to_one_user_by_name_is_held_by_that_user_only()
    {
        Assert.True(await AllowedAsync("direct_user", Notes));
        Assert.False(await AllowedAsync("direct_user", Med));
        Assert.False(await AllowedAsync("dr_deep", Notes));
    }

    [Fact]
    public async Task A_refusal_wins_over_a_group_that_allows()
    {
        // blocked_doc is in Physicians (so in Doctors, which allows patients/med) but has a rule refusing it by name.
        Assert.False(await AllowedAsync("blocked_doc", Med));
        Assert.True(await AllowedAsync("dr_deep", Med));
    }

    [Fact]
    public async Task A_disabled_rule_grants_nothing()
    {
        Assert.False(await AllowedAsync("dr_deep", Trash));
        Assert.False(await AllowedAsync("direct_user", Trash));
    }

    [Fact]
    public async Task A_rule_scoped_to_an_extended_object_does_not_grant_the_general_right()
    {
        Assert.False(await AllowedAsync("dr_deep", Scoped));
        Assert.False(await AllowedAsync("direct_user", Scoped));
    }

    [Theory]
    [InlineData("nobody")]
    [InlineData("no_such_user")]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public async Task Whoever_no_rule_names_holds_nothing_including_no_user_at_all(string? username)
    {
        Assert.False(await AllowedAsync(username, AccessRight.DeleteRecords));
        Assert.False(await AllowedAsync(username, Med));
    }

    [Fact]
    public async Task A_user_is_matched_in_the_users_section_only()
    {
        // The seeded 'other'/'dr_deep' object is a different object from users/dr_deep and has no rules: removing the real one leaves nothing behind.
        await fixture.RootExecuteAsync("DELETE FROM gacl_groups_aro_map WHERE aro_id = 2");

        Assert.False(await AllowedAsync("dr_deep", Med));
    }

    [Fact]
    public async Task Usernames_compare_as_legacys_columns_do()
    {
        Assert.True(await AllowedAsync("ROOT_ADMIN", AccessRight.DeleteRecords));
    }

    [Theory]
    [InlineData("x' OR '1'='1")]
    [InlineData("root_admin' --")]
    [InlineData("root_admin'; DROP TABLE gacl_acl; --")]
    public async Task A_hostile_username_is_data_not_sql(string username)
    {
        Assert.False(await AllowedAsync(username, AccessRight.DeleteRecords));
        Assert.True(await AllowedAsync("root_admin", AccessRight.DeleteRecords));   // and nothing was dropped
    }

    [Fact]
    public async Task A_rule_changed_in_legacy_takes_effect_on_the_next_question_with_nothing_cached()
    {
        Assert.True(await AllowedAsync("root_admin", AccessRight.DeleteRecords));

        await fixture.RootExecuteAsync("UPDATE gacl_acl SET enabled = 0 WHERE id = 1");

        Assert.False(await AllowedAsync("root_admin", AccessRight.DeleteRecords));
    }
}
