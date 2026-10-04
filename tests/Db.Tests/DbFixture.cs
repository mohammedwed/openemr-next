using MySqlConnector;
using Testcontainers.MariaDb;
using Xunit;

public sealed class DbFixture : IAsyncLifetime
{
    private readonly MariaDbContainer _db = new MariaDbBuilder("mariadb:12.3")
        .WithDatabase("openemr")
        .WithUsername("openemr")
        .WithPassword("test-password")
        .WithResourceMapping(
            new FileInfo(Path.Combine(AppContext.BaseDirectory, "baseline.sql")),
            "/docker-entrypoint-initdb.d/")
        .Build();

    public string ConnectionString => _db.GetConnectionString();

    public Task InitializeAsync() => _db.StartAsync();
    public Task DisposeAsync() => _db.DisposeAsync().AsTask();
}