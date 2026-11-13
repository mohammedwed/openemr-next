using Microsoft.EntityFrameworkCore;
using MySqlConnector;
using OpenEmrNext.PatientManagement.Api.Persistence;
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
    // Same image this repo's own Compose stack runs (.env.example's MARIADB_IMAGE) -- testing against
    // a different default version would prove less about what this service actually runs against.
    private readonly MariaDbContainer _container = new MariaDbBuilder().WithImage("mariadb:12.3.3").Build();

    public string ConnectionString => _container.GetConnectionString();

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
