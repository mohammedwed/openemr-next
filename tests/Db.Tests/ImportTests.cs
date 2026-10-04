using MySqlConnector;

public class ImportTests : IClassFixture<DbFixture>
{
    private readonly DbFixture _fx;
    public ImportTests(DbFixture fx) => _fx = fx;

    private async Task<long> ScalarAsync(string sql)
    {
        await using var conn = new MySqlConnection(_fx.ConnectionString);
        await conn.OpenAsync();
        await using var cmd = new MySqlCommand(sql, conn);
        return Convert.ToInt64(await cmd.ExecuteScalarAsync());
    }

    [Fact]
    public async Task Schema_has_expected_table_count() =>
        Assert.True(await ScalarAsync(
            "SELECT COUNT(*) FROM information_schema.tables WHERE table_schema='openemr'") >= 200);

    [Theory]
    [InlineData("users")]
    [InlineData("globals")]
    [InlineData("list_options")]
    public async Task Seed_tables_are_not_empty(string table) =>
        Assert.True(await ScalarAsync($"SELECT COUNT(*) FROM `{table}`") > 0);
}