using MySqlConnector;

namespace OpenEmrNext.AuthFacade.UnitTests.Api;

/// <summary>
/// Proves the two branches in <c>AddOpenIddictClient</c> (ADR 0043) really are two different stores, not
/// just two connection strings to the same provider: every other test leaves <c>AuthFacadeDb</c> unset and
/// so only ever exercises the SQLite fallback (all 60-plus of them starting cleanly already proves that
/// side). This test configures it and expects a MySqlConnector-specific failure, since nothing is
/// listening on the port used — the opposite of what a SQLite misconfiguration would ever throw.
/// </summary>
public class AuthFacadeDbTests
{
    [Fact]
    public async Task Configuring_AuthFacadeDb_switches_the_store_from_sqlite_to_mysql()
    {
        var overrides = new Dictionary<string, string?>
        {
            // 127.0.0.1 on a port nothing listens on: an immediate connection-refused, not a slow DNS
            // timeout, so this fails fast if the branch is ever wired up wrong.
            ["AuthFacadeDb:Host"] = "127.0.0.1",
            ["AuthFacadeDb:Port"] = "1",
            ["AuthFacadeDb:Database"] = "authfacade",
            ["AuthFacadeDb:User"] = "authfacade",
            ["AuthFacadeDb:Password"] = "unused"
        };

        var exception = await Record.ExceptionAsync(() => TestApi.StartAsync(overrides));

        Assert.NotNull(exception);
        Assert.IsType<MySqlException>(exception.GetBaseException());
    }
}
