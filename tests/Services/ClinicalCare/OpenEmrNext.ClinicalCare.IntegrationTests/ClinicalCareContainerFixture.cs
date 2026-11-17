using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using MySqlConnector;
using OpenEmrNext.ClinicalCare.Infrastructure;
using Testcontainers.MariaDb;

namespace OpenEmrNext.ClinicalCare.IntegrationTests;

/// <summary>
/// A throwaway MariaDB container per test run (ADR 0050), built so the tests prove something about the real
/// deployment, not a friendlier copy of it (QLT-7, QLT-8):
/// <list type="bullet">
///   <item>The legacy tables are created from the <b>real DDL in <c>snapshots/db.sql</c></b>, not hand-trimmed
///     copies, so real defaults, keys and the missing foreign keys are all present.</item>
///   <item>The least-privilege user is created by <b>running the real seed script</b>
///     (<c>scripts/seeds/clinical-care-db.sh</c>) inside the container, and every test connects as that user.
///     A grant that is missing or too narrow fails here, not first in the running stack (the gap ADR 0050 and
///     PatientManagement's seed script both record).</item>
/// </list>
/// Setup and assertions use a separate root connection, which production code never has.
/// </summary>
public sealed class ClinicalCareContainerFixture : IAsyncLifetime
{
    private const string RootPassword = "clinical_care_root_pw";
    private const string DatabaseName = "openemr_test";

    public const string User = "clinical_care_test";
    public const string DbPassword = "clinical_care_test_pw";

    // The same image the Compose stack runs (.env.example's MARIADB_IMAGE).
    private readonly MariaDbContainer _container = new MariaDbBuilder()
        .WithImage("mariadb:12.3.3")
        .WithUsername("root")
        .WithPassword(RootPassword)
        .WithDatabase(DatabaseName)
        .Build();

    /// <summary>The legacy tables this service reads or writes, created from the snapshot's own DDL.</summary>
    private static readonly string[] LegacyTables =
        ["patient_data", "users", "groups", "facility", "list_options", "form_encounter", "form_vitals", "forms", "uuid_registry", "sequences",
         // The issue tables (M5c): lists is shared by every issue type, and the last two exist so the grant tests can prove they are unreadable.
         "lists", "lists_touch", "lists_medication", "issue_encounter",
         // Immunizations (M5d slice 2): its own table, shared with no other record type.
         "immunizations",
         // SOAP notes (M5d slice 3): linked to an encounter through the forms row, like vitals.
         "form_soap",
         // Clinical notes (M5d slice 3b): grouped under a parent form registered in forms.
         "form_clinical_notes",
         // Visit categories (M5d slice 4): the lookup reads a few columns of legacy's calendar categories.
         "openemr_postcalendar_categories",
         // Legacy's access control (M5g, ADR 0076): the nine tables the permission check reads.
         "gacl_acl", "gacl_aco_map", "gacl_aro_map", "gacl_axo_map", "gacl_aro_groups_map", "gacl_axo_groups_map", "gacl_aro", "gacl_groups_aro_map", "gacl_aro_groups"];

    /// <summary>The uuid of the seeded user (id 7, <c>dr_who</c>) the test client acts as: what the gateway would forward once it had verified their session (ADR 0075).</summary>
    public static readonly Guid TestUserUuid = Guid.Parse("7e57c0de-0000-4000-8000-000000000007");

    public string Hostname => _container.Hostname;
    public ushort Port => _container.GetMappedPublicPort(3306);
    public string Database => DatabaseName;

    /// <summary>Root connection: setup and assertions only. Production code never has this.</summary>
    public string RootConnectionString => _container.GetConnectionString();

    /// <summary>The connection string for the production principal the seed script created.</summary>
    public string ProductionConnectionString =>
        $"Server={Hostname};Port={Port};Database={DatabaseName};User={User};Password={DbPassword};";

    public async Task InitializeAsync()
    {
        await _container.StartAsync();

        await using (var root = new MySqlConnection(RootConnectionString))
        {
            await root.OpenAsync();
            var snapshot = await File.ReadAllTextAsync(RepoFiles.Find("snapshots/db.sql"));
            foreach (var table in LegacyTables)
                await ExecuteAsync(root, ExtractCreateTable(snapshot, table));
        }

        await RunSeedScriptAsync();
    }

    /// <summary>
    /// Runs the real seed script, unchanged, as root over the container's own socket. Public so a test can run it
    /// a second time and prove it is idempotent (LEG-18).
    /// </summary>
    public async Task RunSeedScriptAsync()
    {
        var script = await File.ReadAllBytesAsync(RepoFiles.Find("scripts/seeds/clinical-care-db.sh"));
        await _container.CopyAsync(script, "/seed/clinical-care-db.sh");
        var result = await _container.ExecAsync(
        [
            "bash", "-c",
            $"DB_HOST=localhost DB_NAME={DatabaseName} CLINICAL_CARE_DB_USER={User} " +
            $"CLINICAL_CARE_DB_PASSWORD={DbPassword} MYSQL_PWD={RootPassword} bash /seed/clinical-care-db.sh"
        ]);
        if (result.ExitCode != 0)
            throw new InvalidOperationException($"The seed script failed ({result.ExitCode}): {result.Stderr}{result.Stdout}");
    }

    public async Task DisposeAsync() => await _container.DisposeAsync();

    /// <summary>A context connecting as the production principal, configured as the service configures it.</summary>
    public ClinicalCareDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<ClinicalCareDbContext>()
            .UseMySql(ProductionConnectionString, new MariaDbServerVersion(new Version(12, 3, 3)),
                mySqlOptions => mySqlOptions.EnableRetryOnFailure())
            .Options;
        return new ClinicalCareDbContext(options);
    }

    /// <summary>Clears the rows tests create and re-seeds the reference rows every test starts from.</summary>
    public async Task ResetAsync()
    {
        await using var root = new MySqlConnection(RootConnectionString);
        await root.OpenAsync();
        foreach (var table in new[]
                 { "form_encounter", "form_vitals", "forms", "uuid_registry", "clinical_care_outbox", "clinical_care_audit", "lists", "lists_touch", "issue_encounter", "immunizations", "form_soap", "form_clinical_notes", "openemr_postcalendar_categories", "clinical_care_patient_ref", "clinical_care_processed_events", "gacl_acl", "gacl_aco_map", "gacl_aro_map", "gacl_axo_map", "gacl_aro_groups_map", "gacl_axo_groups_map", "gacl_aro", "gacl_groups_aro_map", "gacl_aro_groups", "patient_data", "users", "groups", "facility", "list_options", "sequences" })
            await ExecuteAsync(root, $"DELETE FROM `{table}`");

        // Reference rows: ids and values chosen to match the real snapshot (AMB is the default class; list order is `seq`).
        await ExecuteAsync(root, "INSERT INTO sequences VALUES (1000)");
        await ExecuteAsync(root, $"INSERT INTO users (id, username, uuid, active) VALUES (7, 'dr_who', UNHEX('{TestUserUuid:N}'), 1)");
        await ExecuteAsync(root, "INSERT INTO `groups` (name, `user`) VALUES ('Default', 'dr_who')");
        await ExecuteAsync(root, "INSERT INTO facility (id, name, pos_code) VALUES (3, 'Main Clinic', 11), (4, 'Unnamed POS', NULL)");
        await ExecuteAsync(root,
            """
            INSERT INTO list_options (list_id, option_id, title, seq, is_default) VALUES
                ('_ActEncounterCode', 'ACUTE', 'Inpatient Acute', 60, 0),
                ('_ActEncounterCode', 'AMB', 'Outpatient', 10, 1),
                ('_ActEncounterCode', 'EMER', 'Emergency Dept', 20, 0),
                ('other_list', 'AMB', 'A different list must never leak in', 1, 1)
            """);
    }

    /// <summary>
    /// Makes a patient known to Clinical Care the way an event does: a row in its own reference table (uuid and pid, nothing
    /// else). The stand-in that read <c>patient_data</c> is gone (ADR 0066), so this is the only way a patient exists here.
    /// </summary>
    public async Task SeedPatientAsync(Guid uuid, long pid)
    {
        await using var root = new MySqlConnection(RootConnectionString);
        await root.OpenAsync();
        // UNHEX of the uuid's text with dashes removed is the RFC 4122 byte order legacy stores, computed here
        // independently of the production converter, so a byte-order bug there cannot hide itself.
        await ExecuteAsync(root,
            $"INSERT INTO clinical_care_patient_ref (patient_uuid, pid, source) VALUES (UNHEX('{uuid:N}'), {pid}, 'event')");
    }

    /// <summary>Inserts a patient into <b>legacy's</b> <c>patient_data</c>, as another system's registration would (for the backfill test).</summary>
    public async Task SeedLegacyPatientAsync(Guid uuid, long pid)
    {
        await using var root = new MySqlConnection(RootConnectionString);
        await root.OpenAsync();
        await ExecuteAsync(root,
            $"INSERT INTO patient_data (uuid, pid, fname, lname) VALUES (UNHEX('{uuid:N}'), {pid}, 'Katherine', 'Johnson')");
    }

    /// <summary>Runs a scalar query as root, for assertions.</summary>
    public async Task<T?> RootScalarAsync<T>(string sql)
    {
        await using var root = new MySqlConnection(RootConnectionString);
        await root.OpenAsync();
        await using var command = root.CreateCommand();
        command.CommandText = sql;
        var value = await command.ExecuteScalarAsync();
        return value is null or DBNull ? default : (T)Convert.ChangeType(value, Nullable.GetUnderlyingType(typeof(T)) ?? typeof(T));
    }

    /// <summary>Runs a non-query as root, for arranging test state.</summary>
    public async Task RootExecuteAsync(string sql)
    {
        await using var root = new MySqlConnection(RootConnectionString);
        await root.OpenAsync();
        await ExecuteAsync(root, sql);
    }

    private static async Task ExecuteAsync(MySqlConnection connection, string sql)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = sql;
        await command.ExecuteNonQueryAsync();
    }

    private static string ExtractCreateTable(string snapshot, string table)
    {
        var match = Regex.Match(snapshot, $@"CREATE TABLE `{Regex.Escape(table)}` \(.*?\n\) ENGINE[^\n]*;", RegexOptions.Singleline);
        return match.Success
            ? match.Value
            : throw new InvalidOperationException($"snapshots/db.sql has no CREATE TABLE for `{table}`.");
    }
}

[CollectionDefinition("ClinicalCareContainer")]
public class ClinicalCareContainerCollection : ICollectionFixture<ClinicalCareContainerFixture>;
