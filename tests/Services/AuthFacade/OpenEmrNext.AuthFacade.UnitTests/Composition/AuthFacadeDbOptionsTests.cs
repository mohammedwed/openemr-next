using OpenEmrNext.AuthFacade.Composition;

namespace OpenEmrNext.AuthFacade.UnitTests.Composition;

/// <summary>
/// Tests <see cref="AuthFacadeDbOptions.IsConfigured"/> (ADR 0043), the switch between the real MariaDB
/// store and the SQLite fallback — <see cref="OpenIddictClientEndpointTests"/> and every other test that
/// starts a <see cref="Api.TestApi"/> already exercise the fallback itself, since none of them set an
/// <c>AuthFacadeDb</c> section.
/// </summary>
public class AuthFacadeDbOptionsTests
{
    [Fact]
    public void Not_configured_when_every_field_is_left_at_its_default()
    {
        Assert.False(new AuthFacadeDbOptions().IsConfigured);
    }

    [Fact]
    public void Configured_once_host_database_and_user_are_all_set()
    {
        var options = new AuthFacadeDbOptions { Host = "mysql", Database = "authfacade", User = "authfacade" };
        Assert.True(options.IsConfigured);
    }

    [Theory]
    [InlineData("", "authfacade", "authfacade")]
    [InlineData("mysql", "", "authfacade")]
    [InlineData("mysql", "authfacade", "")]
    public void Not_configured_when_any_one_of_host_database_or_user_is_missing(string host, string database, string user)
    {
        var options = new AuthFacadeDbOptions { Host = host, Database = database, User = user };
        Assert.False(options.IsConfigured);
    }

    [Fact]
    public void Password_alone_does_not_make_it_configured()
    {
        // Mirrors the real failure mode this guards against: a .env with AUTHFACADE_DB_PASSWORD set but
        // Host/Database/User left blank must still fall back to SQLite, not attempt a connection with an
        // empty hostname.
        Assert.False(new AuthFacadeDbOptions { Password = "secret" }.IsConfigured);
    }
}
