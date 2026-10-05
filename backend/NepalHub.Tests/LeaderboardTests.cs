using System.Net;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using NepalHub.Api.Data;
using NepalHub.Api.Data.Entities;

namespace NepalHub.Tests;

/// <summary>
/// The JSON shape the leaderboard returns, mirroring the API's paged envelope.
/// </summary>
internal static class Leaderboards
{
    internal sealed record EntryView(int Rank, string DisplayName, int Level, int Xp);

    internal sealed record PageView(
        IReadOnlyList<EntryView> Items,
        int Page,
        int PageSize,
        int TotalCount,
        int TotalPages,
        bool HasPrevious,
        bool HasNext);

    /// <summary>
    /// Creates an account straight in the table rather than through registration. The
    /// board is a public read, so no cookie, role or password is involved, and writing
    /// the row is the only way to pin the XP and the deactivation flag the ranking is
    /// being asked about.
    /// </summary>
    internal static async Task<Guid> SeedUserAsync(
        IServiceProvider services,
        string fullName,
        int xp,
        int level = 1,
        bool isActive = true)
    {
        using var scope = services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var email = TestData.UniqueEmail("board");
        var user = new ApplicationUser
        {
            Id = Guid.NewGuid(),
            UserName = email,
            Email = email,
            NormalizedUserName = email.ToUpperInvariant(),
            NormalizedEmail = email.ToUpperInvariant(),
            FullName = fullName,
            Xp = xp,
            Level = level,
            IsActive = isActive,
        };

        dbContext.Users.Add(user);
        await dbContext.SaveChangesAsync();

        return user.Id;
    }

    /// <summary>
    /// One finished attempt on a Nepal date, stamped with the UTC instant the period
    /// windows are compared against. The daily quiz row is reused when one already
    /// exists for the date, because one quiz per day is a unique constraint.
    /// </summary>
    internal static async Task SeedAttemptAsync(
        IServiceProvider services,
        Guid userId,
        DateOnly quizDate,
        DateTime completedAtUtc,
        int xpAwarded)
    {
        using var scope = services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var quiz = await dbContext.DailyQuizzes
            .SingleOrDefaultAsync(candidate => candidate.QuizDate == quizDate);

        if (quiz is null)
        {
            quiz = new DailyQuiz
            {
                Id = Guid.NewGuid(),
                QuizDate = quizDate,
                QuestionCount = 10,
            };

            dbContext.DailyQuizzes.Add(quiz);
        }

        dbContext.QuizAttempts.Add(new QuizAttempt
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            DailyQuizId = quiz.Id,
            Score = 1,
            MaxScore = 10,
            XpAwarded = xpAwarded,
            CompletedAt = completedAtUtc,
        });

        await dbContext.SaveChangesAsync();
    }

    internal static async Task<ApiEnvelope<PageView>> ReadAsync(HttpClient client, string query = "")
    {
        using var response = await client.GetAsync($"/api/leaderboard{query}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var envelope = await response.Content
            .ReadFromJsonAsync<ApiEnvelope<PageView>>(TestJson.Options);

        Assert.NotNull(envelope);
        Assert.True(envelope!.Success);
        Assert.NotNull(envelope.Data);

        return envelope;
    }

    /// <summary>
    /// The rows this test seeded, in rank order, so assertions are about this test's own
    /// accounts rather than about whichever accounts the rest of the suite happens to
    /// have created in the same shared database.
    /// </summary>
    internal static IReadOnlyList<EntryView> OfThese(PageView page, params string[] displayNames) =>
        page.Items.Where(item => displayNames.Contains(item.DisplayName)).ToList();

    /// <summary>
    /// Four accounts, four attempts, placed against the windows the pinned December
    /// clock implies. Asha and Dipak straddle the day boundary, Bikram straddles the
    /// week's, and Chhaya's attempt belongs to the week before. The display names are
    /// fixed and the board is seeded exactly once, because the database this suite runs
    /// against is shared by every test class.
    ///
    /// Lifetime XP sits in a band of its own: above the hundred-account cap board, which
    /// tops out near a thousand, and below the ranking class's six-figure leaders, so
    /// these four always appear on page one of the all-time board whichever test class
    /// happened to seed first.
    /// </summary>
    internal static async Task SeedPeriodBoardAsync(IServiceProvider services)
    {
        var asha = await SeedUserAsync(services, "Asha", 50_002);
        var bikram = await SeedUserAsync(services, "Bikram", 50_001);
        var chhaya = await SeedUserAsync(services, "Chhaya", 50_003);
        var dipak = await SeedUserAsync(services, "Dipak", 50_000);

        // 07:45 in Kathmandu on the pinned Wednesday: inside today, inside the week.
        await SeedAttemptAsync(
            services, asha, new DateOnly(2026, 12, 9),
            new DateTime(2026, 12, 9, 2, 0, 0, DateTimeKind.Utc), xpAwarded: 50);

        // 01:45 on Saturday in Kathmandu: the week's first day, still Friday evening
        // in UTC, so a row only a UTC-based boundary could fail to see.
        await SeedAttemptAsync(
            services, bikram, new DateOnly(2026, 12, 5),
            new DateTime(2026, 12, 4, 20, 0, 0, DateTimeKind.Utc), xpAwarded: 30);

        // Thursday of the week before last: outside both windows on any clock.
        await SeedAttemptAsync(
            services, chhaya, new DateOnly(2026, 12, 3),
            new DateTime(2026, 12, 3, 4, 0, 0, DateTimeKind.Utc), xpAwarded: 400);

        // 01:45 on Wednesday in Kathmandu, still Tuesday in UTC: the other half of the
        // day-boundary pair.
        await SeedAttemptAsync(
            services, dipak, new DateOnly(2026, 12, 9),
            new DateTime(2026, 12, 8, 20, 0, 0, DateTimeKind.Utc), xpAwarded: 20);
    }
}

/// <summary>
/// Reading the board as a visitor. Nothing here signs in: the whole point of the
/// leaderboard is that it works for somebody who has never made an account.
///
/// The seeded accounts carry deliberately high XP so they occupy the top of a board
/// whose lower ranks fill up with whatever the rest of the suite registers, and the
/// assertions are scoped to their display names for the same reason the other modules
/// scope their counts to a search term: this database is shared by every test class.
/// </summary>
public sealed class LeaderboardTests(NepalHubApiFactory factory) : IClassFixture<NepalHubApiFactory>
{
    private const string QueryPath = "/api/leaderboard";

    [Fact]
    public async Task The_board_reads_without_an_account()
    {
        using var client = factory.CreateClient();

        var page = await Leaderboards.ReadAsync(client);

        Assert.Equal(1, page.Data!.Page);
        Assert.True(page.Data.TotalCount >= 0);
        Assert.NotNull(page.Data.Items);
    }

    [Fact]
    public async Task The_top_is_ranked_by_lifetime_xp_and_deactivated_accounts_are_left_off()
    {
        using var client = factory.CreateClient();

        await Leaderboards.SeedUserAsync(factory.Services, "Leaderboard Gold", 90_000, level: 7);
        await Leaderboards.SeedUserAsync(factory.Services, "Leaderboard Silver", 80_000, level: 6);
        await Leaderboards.SeedUserAsync(factory.Services, "Leaderboard Bronze", 70_000, level: 5);

        // The highest XP on the board and still absent, which is the assertion that
        // deactivation is enforced by the query and not by a caller-supplied flag.
        await Leaderboards.SeedUserAsync(
            factory.Services, "Leaderboard Retired", 95_000, isActive: false);

        var page = await Leaderboards.ReadAsync(client, "?pageSize=100");

        var top = page.Data!.Items.Take(3).ToList();
        Assert.Equal(
            new[] { "Leaderboard Gold", "Leaderboard Silver", "Leaderboard Bronze" },
            top.Select(entry => entry.DisplayName));
        Assert.Equal(new[] { 1, 2, 3 }, top.Select(entry => entry.Rank));
        Assert.Equal(new[] { 7, 6, 5 }, top.Select(entry => entry.Level));
        Assert.DoesNotContain(page.Data.Items, entry => entry.DisplayName == "Leaderboard Retired");
    }

    [Fact]
    public async Task An_account_without_a_display_name_is_published_as_a_member()
    {
        using var client = factory.CreateClient();

        await Leaderboards.SeedUserAsync(factory.Services, string.Empty, 60_000);

        var page = await Leaderboards.ReadAsync(client, "?pageSize=100");

        Assert.Contains(page.Data!.Items, entry => entry.DisplayName == "Member");
    }

    [Fact]
    public async Task Ranks_continue_from_one_page_to_the_next()
    {
        using var client = factory.CreateClient();

        var firstPage = await Leaderboards.ReadAsync(client, "?page=1&pageSize=2");
        var secondPage = await Leaderboards.ReadAsync(client, "?page=2&pageSize=2");

        Assert.Equal(2, firstPage.Data!.Items.Count);
        Assert.Equal(new[] { 1, 2 }, firstPage.Data.Items.Select(entry => entry.Rank));
        Assert.True(firstPage.Data.HasNext);
        Assert.False(firstPage.Data.HasPrevious);

        Assert.Equal(2, secondPage.Data!.Items.Count);
        Assert.Equal(new[] { 3, 4 }, secondPage.Data.Items.Select(entry => entry.Rank));
        Assert.True(secondPage.Data.HasPrevious);
        Assert.Equal(firstPage.Data.TotalCount, secondPage.Data.TotalCount);
    }

    [Fact]
    public async Task The_board_publishes_at_most_a_hundred_entries()
    {
        using var client = factory.CreateClient();

        // A hundred and five accounts, so the cap is what stops the hundred-and-fifth
        // from being published rather than the account count happening to be small.
        for (var index = 0; index < 105; index++)
        {
            await Leaderboards.SeedUserAsync(factory.Services, $"Board Cap {index}", 1_000 + index);
        }

        var page = await Leaderboards.ReadAsync(client, "?page=1&pageSize=100");

        Assert.Equal(100, page.Data!.TotalCount);
        Assert.Equal(100, page.Data.Items.Count);
        Assert.Equal(1, page.Data.TotalPages);
        Assert.False(page.Data.HasNext);
        Assert.Equal(1, page.Data.Items[0].Rank);
        Assert.Equal(100, page.Data.Items[99].Rank);
    }

    [Fact]
    public async Task An_unknown_period_is_refused_and_all_is_accepted()
    {
        using var client = factory.CreateClient();

        using var refused = await client.GetAsync($"{QueryPath}?period=fortnight");
        Assert.Equal(HttpStatusCode.BadRequest, refused.StatusCode);

        var body = await refused.Content.ReadAsStringAsync();
        var snapshot = new ApiResponseSnapshot(refused.StatusCode, body);
        Assert.NotNull(snapshot.Error);
        Assert.Equal("period_invalid", snapshot.Error!.Code);
        Assert.Contains("\"field\":\"period\"", snapshot.Error.Details?.ToString() ?? string.Empty);

        var accepted = await Leaderboards.ReadAsync(client, "?period=all&pageSize=100");
        Assert.True(accepted.Data!.TotalCount >= 0);
    }
}

/// <summary>
/// One pinned clock and one seeded board for the whole period class. xUnit creates a
/// fresh test class instance per method, so the board cannot be seeded from a test
/// method: the names are fixed and the database is shared, and a second seeding would
/// publish two Ashas.
///
/// Wednesday 2026-12-09 12:00 UTC is 17:45 in Kathmandu, which fixes the windows before
/// a single row exists: today opened at 2026-12-08 18:15 UTC, and the Saturday-to-Friday
/// week opened at 2026-12-04 18:15 UTC. The pinned date is also months away from the
/// real one, because the suite's own quiz attempts land on or near today and would
/// otherwise join this board's windows.
/// </summary>
public sealed class LeaderboardWindowFixture : IAsyncLifetime
{
    public FixedClockApiFactory Factory { get; } =
        new(new DateTimeOffset(2026, 12, 9, 12, 0, 0, TimeSpan.Zero));

    public Task InitializeAsync() => Leaderboards.SeedPeriodBoardAsync(Factory.Services);

    public Task DisposeAsync()
    {
        Factory.Dispose();

        return Task.CompletedTask;
    }
}

/// <summary>
/// The period boards: today against the Nepal midnight, this week against the most
/// recent Saturday, and the default against lifetime XP. Two of the seeded attempts sit
/// between the Nepal and the UTC boundary on purpose, because a window computed on the
/// wrong side of the 05:45 offset would drop exactly those rows.
/// </summary>
public sealed class LeaderboardWindowTests(LeaderboardWindowFixture board)
    : IClassFixture<LeaderboardWindowFixture>
{
    [Fact]
    public async Task Today_counts_only_attempts_since_this_nepal_midnight()
    {
        using var client = board.Factory.CreateClient();
        var page = await Leaderboards.ReadAsync(client, "?period=today&pageSize=50");

        var mine = Leaderboards.OfThese(page.Data!, "Asha", "Bikram", "Chhaya", "Dipak");

        Assert.Equal(new[] { "Asha", "Dipak" }, mine.Select(entry => entry.DisplayName));
        Assert.Equal(new[] { 50, 20 }, mine.Select(entry => entry.Xp));
        Assert.Equal(new[] { 1, 2 }, mine.Select(entry => entry.Rank));
    }

    [Fact]
    public async Task The_week_runs_from_the_saturday_and_no_further_back()
    {
        using var client = board.Factory.CreateClient();
        var page = await Leaderboards.ReadAsync(client, "?period=week&pageSize=50");

        var mine = Leaderboards.OfThese(page.Data!, "Asha", "Bikram", "Chhaya", "Dipak");

        // Window XP decides the order: 50 from Wednesday, 30 from Saturday morning,
        // 20 from Wednesday's small hours, and last Thursday's 400 is a week too old.
        Assert.Equal(new[] { "Asha", "Bikram", "Dipak" }, mine.Select(entry => entry.DisplayName));
        Assert.Equal(new[] { 50, 30, 20 }, mine.Select(entry => entry.Xp));
    }

    [Fact]
    public async Task The_default_board_ranks_lifetime_xp_whatever_the_window_would_say()
    {
        using var client = board.Factory.CreateClient();
        var page = await Leaderboards.ReadAsync(client, "?pageSize=50");

        var mine = Leaderboards.OfThese(page.Data!, "Asha", "Bikram", "Chhaya", "Dipak");

        Assert.Equal(new[] { "Chhaya", "Asha", "Bikram", "Dipak" }, mine.Select(entry => entry.DisplayName));
        Assert.Equal(new[] { 50_003, 50_002, 50_001, 50_000 }, mine.Select(entry => entry.Xp));
    }
}
