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
                UNIQUE KEY pid (pid),
                UNIQUE KEY uuid (uuid),
                KEY id (id)
            );
            """;
        await createPatientData.ExecuteNonQueryAsync();

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
