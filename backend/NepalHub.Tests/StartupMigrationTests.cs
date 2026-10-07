using System.Net;
using Microsoft.EntityFrameworkCore;
using MySqlConnector;
using NepalHub.Api.Data;

namespace NepalHub.Tests;

/// <summary>
/// The deploy step nobody remembers at 2 a.m.: the API applies its own pending migrations before
/// anything reads the schema. Proven by taking away the footprint of the last applied migration
/// (its index and its history row) and booting a host, which has to put both back.
/// </summary>
public class StartupMigrationTests
{
    private const string PendingMigrationId = "20261005042034_AddQuizAttemptCompletedAtIndex";
    private const string IndexName = "ix_quiz_attempts_completed_at";

    /// <summary>
    /// The index-only migration is the thing to take away, not a table: dropping it destroys no
    /// rows, and the suite shares this database, so a repair that lost data would make every test
    /// after this one answer for it. The finally clause re-applies whatever state the database is
    /// left in, so a failed assertion here cannot poison the tests that follow either.
    /// </summary>
    [Fact]
    public async Task A_host_starting_with_a_pending_migration_applies_it_before_anything_reads_the_schema()
    {
        try
        {
            await ExecuteAsync($"DROP INDEX `{IndexName}` ON quiz_attempts;");
            await ExecuteAsync(
                "DELETE FROM `__EFMigrationsHistory` WHERE `migration_id` = '" + PendingMigrationId + "';");
            Assert.False(await IndexExistsAsync());

            using var factory = new NepalHubApiFactory();
            using var client = factory.CreateClient();

            using var response = await client.GetAsync("/api/health");

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            Assert.True(
                await IndexExistsAsync(),
                "the host booted without applying the migration that was pending in its database");
        }
        finally
        {
            await RepairAsync();
        }
    }

    private static async Task ExecuteAsync(string sql)
    {
        await using var connection = new MySqlConnection(TestDatabase.ConnectionString);
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = sql;
        await command.ExecuteNonQueryAsync();
    }

    private static async Task<bool> IndexExistsAsync()
    {
        await using var connection = new MySqlConnection(TestDatabase.ConnectionString);
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText =
            "SELECT COUNT(*) FROM information_schema.statistics "
            + "WHERE table_schema = DATABASE() AND index_name = @indexName;";
        command.Parameters.AddWithValue("@indexName", IndexName);
        return Convert.ToInt64(await command.ExecuteScalarAsync()) > 0;
    }

    /// <summary>
    /// Runs the real migrations rather than re-stating the index DDL, so this repair cannot drift
    /// from what the migration itself would do. A no-op when the host already restored the schema.
    /// </summary>
    private static async Task RepairAsync()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseMySql(
                TestDatabase.ConnectionString,
                ServerVersion.Parse("8.4.0-mysql"),
                mySql => mySql.MigrationsAssembly(typeof(AppDbContext).Assembly.FullName))
            .UseSnakeCaseNamingConvention()
            .Options;

        await using var dbContext = new AppDbContext(options);
        await dbContext.Database.MigrateAsync();
    }
}
