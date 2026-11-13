using Microsoft.EntityFrameworkCore;
using MySqlConnector;
using OpenEmrNext.PatientManagement.Infrastructure;
using Testcontainers.MariaDb;
using Xunit;

namespace OpenEmrNext.PatientManagement.IntegrationTests;

/// <summary>
/// A throwaway MariaDB container per test run (ADR 0050), not the shared Compose stack -- these tests
/// write directly to a legacy table (ADR 0049) and don't depend on anyone having run
/// <c>docker compose up</c> first. The schema here mirrors <c>patient_data</c>'s real shape from
/// <c>snapshots/db.sql</c>, deliberately including the anti-pattern <c>docs/m2-schema-inventory.md</c>
/// found: no <c>PRIMARY KEY</c>, just <c>AUTO_INCREMENT</c> on a non-unique <c>KEY</c>. A nicer test
/// schema would prove nothing about whether this service's mapping actually works against the real
/// table.
/// </summary>
public sealed class PatientDataContainerFixture : IAsyncLifetime
{
    private const string Username = "patient_management_test";
    private const string Password = "patient_management_test_password";
    private const string DatabaseName = "openemr_test";

    // Same image this repo's own Compose stack runs (.env.example's MARIADB_IMAGE) -- testing against
    // a different default version would prove less about what this service actually runs against.
    // Credentials are fixed explicitly (not Testcontainers' own generated defaults) so the
    // WebApplicationFactory-based endpoint tests can configure PatientManagementDb:* directly, without
    // needing to parse them back out of a connection string.
    private readonly MariaDbContainer _container = new MariaDbBuilder()
        .WithImage("mariadb:12.3.3")
        .WithUsername(Username)
        .WithPassword(Password)
        .WithDatabase(DatabaseName)
        .Build();

    public string ConnectionString => _container.GetConnectionString();
    public string Hostname => _container.Hostname;
    public ushort Port => _container.GetMappedPublicPort(3306);
    public string Database => DatabaseName;
    public string User => Username;
    public string DbPassword => Password;

    public async Task InitializeAsync()
    {
        await _container.StartAsync();

        await using var connection = new MySqlConnection(ConnectionString);
        await connection.OpenAsync();

        await using var createPatientData = connection.CreateCommand();
        createPatientData.CommandText = """
            CREATE TABLE patient_data (
                id bigint(20) NOT NULL AUTO_INCREMENT,
                uuid binary(16) DEFAULT NULL,
                fname varchar(255) NOT NULL DEFAULT '',
                lname varchar(255) NOT NULL DEFAULT '',
                DOB date DEFAULT NULL,
                sex varchar(255) NOT NULL DEFAULT '',
                email varchar(255) NOT NULL DEFAULT '',
                email_direct varchar(255) NOT NULL DEFAULT '',
                phone_home varchar(255) NOT NULL DEFAULT '',
                phone_biz varchar(255) NOT NULL DEFAULT '',
                phone_cell varchar(255) NOT NULL DEFAULT '',
                dupscore int(11) NOT NULL DEFAULT -9,
                pubpid varchar(255) NOT NULL DEFAULT '',
                pid bigint(20) NOT NULL DEFAULT 0,
                date datetime DEFAULT NULL,
                regdate datetime DEFAULT NULL,
                created_by bigint(20) DEFAULT NULL,
                updated_by bigint(20) DEFAULT NULL,
                completed_ad varchar(3) NOT NULL DEFAULT 'NO',
                ad_reviewed datetime DEFAULT NULL,
                advance_directive_user_authenticator bigint(20) DEFAULT NULL,
                title varchar(255) NOT NULL DEFAULT '',
                mname varchar(255) NOT NULL DEFAULT '',
                suffix tinytext,
                street varchar(255) NOT NULL DEFAULT '',
                street_line_2 tinytext,
                city varchar(255) NOT NULL DEFAULT '',
                state varchar(255) NOT NULL DEFAULT '',
                postal_code varchar(255) NOT NULL DEFAULT '',
                country_code varchar(255) NOT NULL DEFAULT '',
                ss varchar(255) NOT NULL DEFAULT '',
                language varchar(255) NOT NULL DEFAULT '',
                providerID int(11) DEFAULT NULL,
                race varchar(255) NOT NULL DEFAULT '',
                ethnicity varchar(255) NOT NULL DEFAULT '',
                gender_identity text,
                sex_identified text,
                tribal_affiliations text,
                interpreter_needed text,
                deceased_date datetime DEFAULT NULL,
                UNIQUE KEY pid (pid),
                UNIQUE KEY uuid (uuid),
                KEY id (id)
            );
            """;
        await createPatientData.ExecuteNonQueryAsync();

        await using var createUsers = connection.CreateCommand();
        createUsers.CommandText = """
            CREATE TABLE users (
                id bigint(20) NOT NULL AUTO_INCREMENT PRIMARY KEY,
                uuid binary(16) DEFAULT NULL
            );
            """;
        await createUsers.ExecuteNonQueryAsync();

        // The handful of list_options rows the FHIR Patient mapping actually resolves against,
        // trimmed from the real seed data in sql/7_0_3-to-7_0_4_upgrade.sql and sql/database.sql --
        // not invented, so a test asserting on a specific code/display is asserting against the real
        // values an actual install would also resolve.
        await using var createListOptions = connection.CreateCommand();
        createListOptions.CommandText = """
            CREATE TABLE list_options (
                list_id varchar(100) NOT NULL DEFAULT '',
                option_id varchar(100) NOT NULL DEFAULT '',
                title varchar(255) NOT NULL DEFAULT '',
                notes text,
                codes varchar(255) NOT NULL DEFAULT '',
                PRIMARY KEY (list_id, option_id)
            );
            """;
        await createListOptions.ExecuteNonQueryAsync();

        await using var seedListOptions = connection.CreateCommand();
        seedListOptions.CommandText = """
            INSERT INTO list_options (list_id, option_id, title, notes, codes) VALUES
                ('race', 'white', 'White', '2106-3', ''),
                ('ethnicity', 'hispanic', 'Hispanic or Latino', '2135-2', ''),
                ('ethnicity', 'decline_to_specify', 'Declined To Specify', NULL, ''),
                ('administrative_sex', 'Male', 'Male', NULL, 'SNOMED-CT:248152002'),
                ('administrative_sex', 'Female', 'Female', NULL, 'SNOMED-CT:248153007'),
                ('yes_no_unknown', 'yes', 'Yes', NULL, 'SNOMED-CT:373066001'),
                ('yes_no_unknown', 'no', 'No', NULL, 'SNOMED-CT:373067005'),
                ('tribal_affiliations', 'navajo_nation', 'Navajo Nation (AZ/NM/UT)', NULL, '170'),
                ('language', 'english', 'English', 'en', '');
            """;
        await seedListOptions.ExecuteNonQueryAsync();

        await using var createInsuranceData = connection.CreateCommand();
        createInsuranceData.CommandText = """
            CREATE TABLE insurance_data (
                id bigint(20) NOT NULL AUTO_INCREMENT,
                uuid binary(16) DEFAULT NULL,
                type enum('primary','secondary','tertiary') DEFAULT NULL,
                provider varchar(255) DEFAULT NULL,
                plan_name varchar(255) DEFAULT NULL,
                policy_number varchar(255) DEFAULT NULL,
                group_number varchar(255) DEFAULT NULL,
                date date DEFAULT NULL,
                pid bigint(20) NOT NULL DEFAULT 0,
                date_end date DEFAULT NULL,
                PRIMARY KEY (id),
                UNIQUE KEY uuid (uuid),
                UNIQUE KEY pid_type_date (pid, type, date)
            );
            """;
        await createInsuranceData.ExecuteNonQueryAsync();

        // The three tables advance-directive document storage writes into (ADR 0049 -- no new table),
        // trimmed to the columns this service's own reads/writes ever touch, but matching the real
        // shape where it matters: `documents.id` is NOT AUTO_INCREMENT (it's allocated from
        // `sequences`, same as legacy), and `sequences` really does hold exactly one row.
        await using var createDocuments = connection.CreateCommand();
        createDocuments.CommandText = """
            CREATE TABLE documents (
                id int(11) NOT NULL,
                uuid binary(16) DEFAULT NULL,
                type enum('file_url','blob','web_url') DEFAULT NULL,
                size int(11) DEFAULT NULL,
                date datetime DEFAULT NULL,
                url varchar(255) DEFAULT NULL,
                mimetype varchar(255) DEFAULT NULL,
                owner int(11) DEFAULT NULL,
                revision timestamp NOT NULL DEFAULT CURRENT_TIMESTAMP,
                foreign_id bigint(20) DEFAULT NULL,
                docdate date DEFAULT NULL,
                hash varchar(255) DEFAULT NULL,
                name varchar(255) DEFAULT NULL,
                drive_uuid binary(16) DEFAULT NULL,
                storagemethod tinyint(4) NOT NULL DEFAULT 0,
                path_depth tinyint DEFAULT 1,
                encounter_id bigint(20) NOT NULL DEFAULT 0,
                deleted tinyint(1) NOT NULL DEFAULT 0,
                PRIMARY KEY (id),
                UNIQUE KEY drive_uuid (drive_uuid),
                UNIQUE KEY uuid (uuid),
                KEY foreign_id (foreign_id)
            );
            """;
        await createDocuments.ExecuteNonQueryAsync();

        await using var createCategories = connection.CreateCommand();
        createCategories.CommandText = """
            CREATE TABLE categories (
                id int(11) NOT NULL AUTO_INCREMENT,
                name varchar(255) DEFAULT NULL,
                parent int(11) NOT NULL DEFAULT 0,
                codes varchar(255) NOT NULL DEFAULT '',
                PRIMARY KEY (id)
            );
            """;
        await createCategories.ExecuteNonQueryAsync();

        // Same id and seed data as a real fresh legacy install (sql/database.sql).
        await using var seedCategories = connection.CreateCommand();
        seedCategories.CommandText = "INSERT INTO categories (id, name, parent, codes) VALUES (6, 'Advance Directive', 1, 'LOINC:42348-3')";
        await seedCategories.ExecuteNonQueryAsync();

        await using var createCategoriesToDocuments = connection.CreateCommand();
        createCategoriesToDocuments.CommandText = """
            CREATE TABLE categories_to_documents (
                category_id int(11) NOT NULL DEFAULT 0,
                document_id int(11) NOT NULL DEFAULT 0,
                PRIMARY KEY (category_id, document_id)
            );
            """;
        await createCategoriesToDocuments.ExecuteNonQueryAsync();

        await using var createSequences = connection.CreateCommand();
        createSequences.CommandText = "CREATE TABLE sequences (id int(11) unsigned NOT NULL DEFAULT 0)";
        await createSequences.ExecuteNonQueryAsync();

        await using var seedSequences = connection.CreateCommand();
        seedSequences.CommandText = "INSERT INTO sequences VALUES (1)";
        await seedSequences.ExecuteNonQueryAsync();

        await using var createOutbox = connection.CreateCommand();
        createOutbox.CommandText = """
            CREATE TABLE patient_management_outbox (
                id bigint(20) NOT NULL AUTO_INCREMENT PRIMARY KEY,
                event_type varchar(255) NOT NULL,
                payload longtext NOT NULL,
                occurred_at datetime NOT NULL,
                published_at datetime DEFAULT NULL
            );
            """;
        await createOutbox.ExecuteNonQueryAsync();
    }

    public async Task DisposeAsync() => await _container.DisposeAsync();

    public PatientManagementDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<PatientManagementDbContext>()
            .UseMySql(ConnectionString, new MariaDbServerVersion(new Version(12, 3, 3)))
            .Options;
        return new PatientManagementDbContext(options);
    }
}

[CollectionDefinition("PatientDataContainer")]
public class PatientDataContainerCollection : ICollectionFixture<PatientDataContainerFixture>;
