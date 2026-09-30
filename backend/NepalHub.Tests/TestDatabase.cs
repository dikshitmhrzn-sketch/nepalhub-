using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using MySqlConnector;

namespace NepalHub.Tests;

/// <summary>
/// Resolves the connection string the tests run against and makes sure that
/// database is migrated before any test host starts.
///
/// The tests use a real MySQL server (there is no in-memory provider) because the
/// schema, the collation, the seed data and the Identity stores all have to behave
/// exactly as they do in development.
///
/// Resolution order for the connection string:
///   1. ConnectionStrings__Test    (environment variable)
///   2. ConnectionStrings__Default (environment variable), with the database swapped
///   3. the API's dotnet user-secrets entry, with the database swapped
/// </summary>
internal static class TestDatabase
{
    private const string DevelopmentDatabaseName = "nepalhub";
    private const string TestDatabaseName = "nepalhub_test";
    private const string UserSecretsId = "nepalhub-api-e9b53b9a-0881-465f-abf3-bd9699e0a3cd";

    private static readonly SemaphoreSlim Gate = new(1, 1);
    private static bool _isReady;

    internal static string ConnectionString { get; } = Resolve();

    /// <summary>
    /// Points the application host at the test database and migrates it. This must
    /// run before the first <c>CreateClient()</c>, because the application seeds the
    /// Identity roles while the host starts.
    /// </summary>
    internal static async Task PrepareAsync(int authRateLimitPermitLimit)
    {
        Environment.SetEnvironmentVariable("ConnectionStrings__Default", ConnectionString);
        Environment.SetEnvironmentVariable(
            "Auth__RateLimit__PermitLimit",
            authRateLimitPermitLimit.ToString());

        if (_isReady)
        {
            return;
        }

        await Gate.WaitAsync();
        try
        {
            if (_isReady)
            {
                return;
            }

            var builder = new MySqlConnectionStringBuilder(ConnectionString);

            var masterConnectionString = new MySqlConnectionStringBuilder(builder.ConnectionString)
            {
                Database = string.Empty,
            }.ConnectionString;

            await using (var master = new MySqlConnection(masterConnectionString))
            {
                await master.OpenAsync();
                await using var create = master.CreateCommand();
                create.CommandText = $"CREATE DATABASE IF NOT EXISTS `{builder.Database}` "
                    + "CHARACTER SET utf8mb4 COLLATE utf8mb4_0900_ai_ci;";
                await create.ExecuteNonQueryAsync();
            }

            var options = new DbContextOptionsBuilder<NepalHub.Api.Data.AppDbContext>()
                .UseMySql(
                    ConnectionString,
                    ServerVersion.Parse("8.4.0-mysql"),
                    mySql => mySql.MigrationsAssembly(typeof(NepalHub.Api.Data.AppDbContext).Assembly.FullName))
                .UseSnakeCaseNamingConvention()
                .Options;

            await using var dbContext = new NepalHub.Api.Data.AppDbContext(options);
            await dbContext.Database.MigrateAsync();

            _isReady = true;
        }
        finally
        {
            Gate.Release();
        }
    }

    private static string Resolve()
    {
        var fromEnvironment = Environment.GetEnvironmentVariable("ConnectionStrings__Test");
        if (!string.IsNullOrWhiteSpace(fromEnvironment))
        {
            return new MySqlConnectionStringBuilder(fromEnvironment).ConnectionString;
        }

        var fromDefaultEnvironment = Environment.GetEnvironmentVariable("ConnectionStrings__Default");
        if (!string.IsNullOrWhiteSpace(fromDefaultEnvironment))
        {
            return WithTestDatabase(fromDefaultEnvironment);
        }

        var fromUserSecrets = ReadUserSecrets();
        if (!string.IsNullOrWhiteSpace(fromUserSecrets))
        {
            return WithTestDatabase(fromUserSecrets);
        }

        throw new InvalidOperationException(
            "The test database connection string could not be resolved. Set ConnectionStrings__Test, "
            + "or ConnectionStrings__Default, or run "
            + "'dotnet user-secrets set \"ConnectionStrings:Default\" <value>' in backend/NepalHub.Api.");
    }

    private static string WithTestDatabase(string connectionString)
    {
        var builder = new MySqlConnectionStringBuilder(connectionString);

        if (string.Equals(builder.Database, DevelopmentDatabaseName, StringComparison.OrdinalIgnoreCase))
        {
            builder.Database = TestDatabaseName;
        }

        return builder.ConnectionString;
    }

    private static string? ReadUserSecrets()
    {
        var path = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "Microsoft",
            "UserSecrets",
            UserSecretsId,
            "secrets.json");

        if (!File.Exists(path))
        {
            return null;
        }

        using var document = JsonDocument.Parse(File.ReadAllText(path));

        return document.RootElement.TryGetProperty("ConnectionStrings:Default", out var value)
            ? value.GetString()
            : null;
    }
}
