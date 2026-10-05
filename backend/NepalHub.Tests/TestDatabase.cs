using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using MySqlConnector;

namespace NepalHub.Tests;

/// <summary>
/// Resolves the connection string the tests run against and rebuilds that database from
/// scratch before any test host starts.
///
/// The tests use a real MySQL server (there is no in-memory provider) because the
/// schema, the collation, the seed data and the Identity stores all have to behave
/// exactly as they do in development.
///
/// Resolution order for the connection string:
///   1. ConnectionStrings__Test    (environment variable)
///   2. ConnectionStrings__Default (environment variable), with the database swapped
///   3. the API's dotnet user-secrets entry, with the database swapped
///
/// <para>
/// The database is dropped and recreated once per test run, and then migrated and seeded by the
/// application as it starts. Without that, every run leaves its own rows behind — the suite creates
/// published offers, listings and events that readers can see — so the catalogue grows without bound
/// and a test that reads the public list eventually stops seeing the rows it seeded. That is not
/// hypothetical: the coverage assertions in the offers and calendar tests read the first page, and
/// past a hundred published rows the seeded ones are pushed off the end of it. Rebuilding also
/// re-dates the seed set, which is inserted once and never re-dated, so its relative windows stay
/// valid however long the database has been in use.
/// </para>
///
/// <para>
/// The reset is guarded, because the failure it would cause is not recoverable from inside this
/// project. <see cref="DevelopmentDatabaseName"/> is refused outright however it was arrived at —
/// including through an explicitly mis-set ConnectionStrings__Test — and anything other than
/// <see cref="TestDatabaseName"/> has to have been named by hand. Set
/// <c>NepalHubTests__KeepData=1</c> to skip the reset and run against the database as it stands.
/// </para>
/// </summary>
internal static class TestDatabase
{
    // Internal rather than private so the guard tests can assert against these names instead of
    // repeating them as literals, which is the only way they could drift out of agreement with the
    // names actually guarded.
    internal const string DevelopmentDatabaseName = "nepalhub";
    internal const string TestDatabaseName = "nepalhub_test";
    private const string UserSecretsId = "nepalhub-api-e9b53b9a-0881-465f-abf3-bd9699e0a3cd";

    private const string KeepDataVariableName = "NepalHubTests__KeepData";

    /// <summary>
    /// Read before <see cref="ConnectionString"/>, because it decides whether the database that
    /// resolves is one a human picked on purpose. A name that arrived by substitution is held to
    /// <see cref="TestDatabaseName"/>; an explicit one is taken on trust.
    /// </summary>
    private static readonly bool TestDatabaseWasNamedExplicitly = HasExplicitConnectionString();

    private static readonly SemaphoreSlim Gate = new(1, 1);
    private static bool _isReady;

    internal static string ConnectionString { get; } = Resolve();

    /// <summary>
    /// Whether to leave the database exactly as it is and only migrate it. For debugging one test
    /// against rows that are already there; the suite's own results are not trustworthy with it set,
    /// which is the point.
    /// </summary>
    private static bool KeepExistingData =>
        Environment.GetEnvironmentVariable(KeepDataVariableName) is { } value
        && (value == "1" || (bool.TryParse(value, out var keep) && keep));

    /// <summary>
    /// Points the application host at the test database, rebuilds it and migrates it. This must
    /// run before the first <c>CreateClient()</c>, because the application seeds the
    /// Identity roles while the host starts.
    ///
    /// The rebuild happens once per process rather than once per fixture, so a fixture created part
    /// way through the suite does not pull the rows out from under the classes that ran before it.
    /// </summary>
    internal static async Task PrepareAsync(
        int authRateLimitPermitLimit,
        int calculateRateLimitPermitLimit = 10_000,
        int quizRateLimitPermitLimit = 10_000,
        int searchRateLimitPermitLimit = 10_000,
        int reportRateLimitPermitLimit = 10_000,
        int logoutRateLimitPermitLimit = 10_000,
        int claimRateLimitPermitLimit = 10_000,
        int bookingRateLimitPermitLimit = 10_000)
    {
        Environment.SetEnvironmentVariable("ConnectionStrings__Default", ConnectionString);
        Environment.SetEnvironmentVariable(
            "Auth__RateLimit__PermitLimit",
            authRateLimitPermitLimit.ToString());
        Environment.SetEnvironmentVariable(
            "Calculate__RateLimit__PermitLimit",
            calculateRateLimitPermitLimit.ToString());
        Environment.SetEnvironmentVariable(
            "Quiz__RateLimit__PermitLimit",
            quizRateLimitPermitLimit.ToString());
        Environment.SetEnvironmentVariable(
            "Search__RateLimit__PermitLimit",
            searchRateLimitPermitLimit.ToString());
        Environment.SetEnvironmentVariable(
            "Report__RateLimit__PermitLimit",
            reportRateLimitPermitLimit.ToString());
        Environment.SetEnvironmentVariable(
            "Logout__RateLimit__PermitLimit",
            logoutRateLimitPermitLimit.ToString());
        Environment.SetEnvironmentVariable(
            "Claim__RateLimit__PermitLimit",
            claimRateLimitPermitLimit.ToString());
        Environment.SetEnvironmentVariable(
            "Booking__RateLimit__PermitLimit",
            bookingRateLimitPermitLimit.ToString());

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

                if (KeepExistingData)
                {
                    await using var create = master.CreateCommand();
                    create.CommandText = $"CREATE DATABASE IF NOT EXISTS `{builder.Database}` "
                        + "CHARACTER SET utf8mb4 COLLATE utf8mb4_0900_ai_ci;";
                    await create.ExecuteNonQueryAsync();
                }
                else
                {
                    EnsureResettable(builder.Database, TestDatabaseWasNamedExplicitly);

                    // Dropped rather than emptied row by row: an empty database needs no foreign key
                    // ordering to be reasoned about, and no table added by a later migration can be
                    // forgotten here. The migrations immediately below restore the schema and the
                    // province and district reference data they insert, and the seeders restore the
                    // content, so the database the suite starts from is the one a fresh install has.
                    await using var drop = master.CreateCommand();
                    drop.CommandText = $"DROP DATABASE IF EXISTS `{builder.Database}`;";
                    await drop.ExecuteNonQueryAsync();

                    await using var create = master.CreateCommand();
                    create.CommandText = $"CREATE DATABASE `{builder.Database}` "
                        + "CHARACTER SET utf8mb4 COLLATE utf8mb4_0900_ai_ci;";
                    await create.ExecuteNonQueryAsync();
                }
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

    private static bool HasExplicitConnectionString() =>
        !string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("ConnectionStrings__Test"));

    /// <summary>
    /// Refuses to reset anything that is not unmistakably a test database, before the name is put
    /// into a statement. The order of the checks matters: the character test comes first, so a name
    /// carrying a backtick is rejected outright rather than being compared and then quoted.
    /// </summary>
    /// <remarks>
    /// Internal, and a function of its arguments only, for one reason: this is the code standing
    /// between a mistyped connection string and an unrecoverable development database, and testing
    /// it against a live server is not a thing anyone should be willing to do. Taking
    /// <paramref name="namedExplicitly"/> as a parameter rather than reading the static field keeps
    /// the whole decision reachable from a test that opens no connection at all.
    /// </remarks>
    internal static void EnsureResettable(string? databaseName, bool namedExplicitly)
    {
        if (string.IsNullOrWhiteSpace(databaseName))
        {
            throw new InvalidOperationException(
                "The test connection string names no database, so there is nothing the tests can "
                + "safely reset. Set ConnectionStrings__Test to a database of its own.");
        }

        if (databaseName.Any(character => !char.IsAsciiLetterOrDigit(character) && character != '_'))
        {
            throw new InvalidOperationException(
                $"The test database name '{databaseName}' contains a character that cannot appear in "
                + "a schema name, so the reset was refused rather than quoting it into a statement.");
        }

        if (string.Equals(databaseName, DevelopmentDatabaseName, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(
                $"The tests resolved to the development database '{databaseName}', and will not reset "
                + $"it. Point ConnectionStrings__Test at a separate database, or leave it unset so "
                + $"{TestDatabaseName} is used.");
        }

        if (!namedExplicitly
            && !string.Equals(databaseName, TestDatabaseName, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(
                $"The tests resolved to '{databaseName}', which is neither {TestDatabaseName} nor a "
                + "database named explicitly through ConnectionStrings__Test, so the reset was "
                + "refused rather than guessed at.");
        }
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
