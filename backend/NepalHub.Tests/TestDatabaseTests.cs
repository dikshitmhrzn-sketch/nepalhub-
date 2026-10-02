namespace NepalHub.Tests;

/// <summary>
/// The guard that stands between a mistyped connection string and the development database.
/// </summary>
/// <remarks>
/// <para>
/// These tests take no <see cref="IClassFixture{TFixture}"/> and touch no service, and that is the
/// entire design. Taking <see cref="NepalHubApiFactory"/> is what calls
/// <see cref="TestDatabase.PrepareAsync"/>, which is what opens a connection and issues the
/// <c>DROP DATABASE</c>. A test suite whose only safe way to check the drop guard is to perform the
/// drop is not testing it, so <see cref="TestDatabase.EnsureResettable"/> was made a pure function of
/// its arguments and is called here directly. Nothing below reads or writes any database: the
/// <c>nepalhub</c> cases below are refused by string comparison before any SQL is built, which is
/// exactly why they can be asserted without a server.
/// </para>
/// <para>
/// The names are compared against the constants the guard actually uses rather than against literals
/// typed here, so a rename cannot leave this file quietly passing against a name that is no longer
/// guarded.
/// </para>
/// </remarks>
public sealed class TestDatabaseTests
{
    // ------------------------------------------------------------- refusals

    [Theory]
    [InlineData(TestDatabase.DevelopmentDatabaseName, false)]
    [InlineData(TestDatabase.DevelopmentDatabaseName, true)]
    [InlineData("NEPALHUB", true)]
    [InlineData("NepalHub", false)]
    public void The_development_database_is_refused_even_when_it_was_named_explicitly(
        string databaseName,
        bool namedExplicitly)
    {
        // The second argument being true is the case that matters. An explicit
        // ConnectionStrings__Test is trusted everywhere else, so if it were trusted here too, the one
        // way to point the suite at the development database would be the one way to lose it.
        var error = Assert.Throws<InvalidOperationException>(
            () => TestDatabase.EnsureResettable(databaseName, namedExplicitly));

        Assert.Contains(TestDatabase.DevelopmentDatabaseName, error.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void A_refusal_names_the_database_that_tripped_it()
    {
        // An operator reading a failed test run needs to know which name was wrong, and the
        // development database is the one worth being unmissable about.
        var error = Assert.Throws<InvalidOperationException>(
            () => TestDatabase.EnsureResettable(TestDatabase.DevelopmentDatabaseName, true));

        Assert.Contains("will not reset", error.Message, StringComparison.Ordinal);
        Assert.Contains(TestDatabase.DevelopmentDatabaseName, error.Message, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(" nepalhub")]
    [InlineData("nepalhub ")]
    [InlineData("nepal hub")]
    public void A_name_with_surrounding_or_embedded_spaces_is_refused_before_it_is_compared(
        string databaseName)
    {
        // Whitespace is caught by the character rule rather than by trimming and matching, so
        // " nepalhub" cannot be read as the development database with a typo around it.
        var error = Assert.Throws<InvalidOperationException>(
            () => TestDatabase.EnsureResettable(databaseName, true));

        Assert.Contains("character", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void A_name_carrying_a_backtick_is_refused_rather_than_quoted_into_a_statement()
    {
        // The identifier reaches this method interpolated into a DROP/CREATE, so a name that could
        // close the quoting has to be rejected on the way in.
        var injected = TestDatabase.DevelopmentDatabaseName + "`; DROP DATABASE `other";

        var error = Assert.Throws<InvalidOperationException>(
            () => TestDatabase.EnsureResettable(injected, true));

        Assert.Contains("character", error.Message, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public void A_connection_string_naming_no_database_is_refused(string? databaseName)
    {
        var error = Assert.Throws<InvalidOperationException>(
            () => TestDatabase.EnsureResettable(databaseName, false));

        Assert.Contains("names no database", error.Message, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("nepal")]
    [InlineData("nepalhub_prod")]
    [InlineData("nepalhub_testing")]
    [InlineData("nepalhub_test_backup")]
    public void A_substituted_name_that_is_not_the_test_database_is_refused(string databaseName)
    {
        // These arrived by swapping the database in a dev connection string rather than by anyone
        // choosing them, so nothing vouches for them and the guard declines to guess.
        var error = Assert.Throws<InvalidOperationException>(
            () => TestDatabase.EnsureResettable(databaseName, false));

        Assert.Contains(TestDatabase.TestDatabaseName, error.Message, StringComparison.Ordinal);
    }

    // ----------------------------------------------------------- acceptances

    [Theory]
    [InlineData(TestDatabase.TestDatabaseName, false)]
    [InlineData(TestDatabase.TestDatabaseName, true)]
    [InlineData("NEPALHUB_TEST", true)]
    public void The_test_database_is_accepted(string databaseName, bool namedExplicitly)
    {
        // The ordinary case, with and without an explicit connection string. A guard that refused
        // these would be a guard nobody could switch off, so they are asserted as loudly as the
        // refusals are.
        TestDatabase.EnsureResettable(databaseName, namedExplicitly);
    }

    [Fact]
    public void Any_other_database_is_accepted_when_it_was_named_explicitly()
    {
        // Somebody who typed a scratch database name by hand knows what they meant.
        TestDatabase.EnsureResettable("nepalhub_scratch", true);
    }
}