using System.Net;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using NepalHub.Api.Data;
using NepalHub.Api.Data.Entities;
using NepalHub.Api.Features.Badges;
using NepalHub.Api.Features.Quiz;

namespace NepalHub.Tests;

/// <summary>
/// The JSON shape the badges endpoint returns.
/// </summary>
internal static class Badges
{
    internal sealed record BadgeView(
        string Code,
        string Name,
        string Description,
        string Icon,
        bool Awarded,
        DateTime? AwardedAt,
        int Current,
        int Threshold);

    internal sealed record ListView(IReadOnlyList<BadgeView> Badges, int AwardedCount);

    /// <summary>Counts the award rows behind one account, so idempotency is asserted on
    /// the table rather than only on what the response happened to show.</summary>
    internal static async Task<int> AwardRowCountAsync(IServiceProvider services, Guid userId)
    {
        using var scope = services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        return await dbContext.UserBadges.AsNoTracking().CountAsync(award => award.UserId == userId);
    }

    /// <summary>The account behind an email, for the assertions that need its id.</summary>
    internal static async Task<Guid> UserIdAsync(IServiceProvider services, string email)
    {
        using var scope = services.CreateScope();
        var userManager = scope.ServiceProvider
            .GetRequiredService<UserManager<ApplicationUser>>();

        var user = await userManager.FindByEmailAsync(email);
        Assert.NotNull(user);

        return user!.Id;
    }
}

/// <summary>
/// Badges over the account page's own progression: what a finished quiz grants, what an
/// anonymous play grants, what a streak takes to earn, and that none of it pays XP.
///
/// Every test that plays a quiz creates its own account, because one attempt per user per
/// day is a database constraint and a shared account would collide. The streak test walks
/// the clock forward a day at a time for the same reason.
/// </summary>
public class BadgeAwardTests(NepalHubApiFactory factory) : IClassFixture<NepalHubApiFactory>
{
    private const string SubmitPath = "/api/quiz/daily/submit";
    private const string StatsPath = "/api/quiz/stats";
    private const string BadgesPath = "/api/account/badges";

    private const string FirstQuiz = "first-quiz";
    private const string SevenDayStreak = "seven-day-streak";
    private const string Enthusiast = "quiz-enthusiast";

    /// <summary>Plays today's quiz as the signed-in user on <paramref name="api"/>.</summary>
    private static async Task<Quizzes.ResultView> PlayTodayAsync(AuthApiClient api, IServiceProvider services)
    {
        var quiz = await api.GetAsync<Quizzes.DailyView>("/api/quiz/daily");
        var answers = await Quizzes.ReadAnswersAsync(services, quiz.Id);

        using var submitted = await api.PostAsync(SubmitPath, new { answers });
        submitted.EnsureSuccessStatusCode();

        return await api.GetAsync<Quizzes.ResultView>("/api/quiz/daily/result");
    }

    private static async Task<Badges.ListView> ReadBadgesAsync(AuthApiClient api) =>
        await api.GetAsync<Badges.ListView>(BadgesPath);

    private static async Task<IReadOnlyList<string>> AwardedCodesAsync(AuthApiClient api)
    {
        var badges = await ReadBadgesAsync(api);

        return badges.Badges.Where(badge => badge.Awarded).Select(badge => badge.Code).ToList();
    }

    // ------------------------------------------------------------------ the endpoint

    [Fact]
    public async Task Badges_are_not_readable_without_an_account()
    {
        using var client = factory.CreateClient();

        using var response = await client.GetAsync(BadgesPath);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Every_badge_is_listed_before_any_is_earned()
    {
        var client = factory.CreateSignedInClientAsync(TestData.UniqueEmail("badge"));
        var api = new AuthApiClient(client);

        var badges = await ReadBadgesAsync(api);

        Assert.Equal(
            [FirstQuiz, SevenDayStreak, Enthusiast],
            badges.Badges.Select(badge => badge.Code));
        Assert.Equal(0, badges.AwardedCount);
        Assert.All(badges.Badges, badge => Assert.False(badge.Awarded));
        Assert.All(badges.Badges, badge => Assert.Null(badge.AwardedAt));
    }

    [Fact]
    public async Task A_locked_badge_carries_the_progress_towards_it()
    {
        var client = factory.CreateSignedInClientAsync(TestData.UniqueEmail("badge"));
        var api = new AuthApiClient(client);

        await PlayTodayAsync(api, factory.Services);

        var badges = await ReadBadgesAsync(api);
        var enthusiast = badges.Badges.Single(badge => badge.Code == Enthusiast);

        Assert.False(enthusiast.Awarded);
        Assert.Equal(1, enthusiast.Current);
        Assert.Equal(10, enthusiast.Threshold);
    }

    // ------------------------------------------------------------------ awarding

    [Fact]
    public async Task A_finished_quiz_awards_the_first_quiz_badge()
    {
        var client = factory.CreateSignedInClientAsync(TestData.UniqueEmail("badge"));
        var api = new AuthApiClient(client);

        await PlayTodayAsync(api, factory.Services);

        Assert.Equal([FirstQuiz], await AwardedCodesAsync(api));
    }

    [Fact]
    public async Task An_anonymous_play_awards_nothing()
    {
        var api = new AuthApiClient(factory.CreateClient());

        var quiz = await api.GetAsync<Quizzes.DailyView>("/api/quiz/daily");
        var answers = await Quizzes.ReadAnswersAsync(factory.Services, quiz.Id);
        using var submitted = await api.PostAsync(SubmitPath, new { answers });
        submitted.EnsureSuccessStatusCode();

        // The same play through an account does award, so the anonymous path is what is
        // being ruled out here rather than the quiz itself failing to grade.
        var signedIn = new AuthApiClient(factory.CreateSignedInClientAsync(TestData.UniqueEmail("badge")));
        await PlayTodayAsync(signedIn, factory.Services);

        Assert.Equal([FirstQuiz], await AwardedCodesAsync(signedIn));
    }

    [Fact]
    public async Task A_second_play_on_the_same_day_awards_nothing_extra()
    {
        var client = factory.CreateSignedInClientAsync(TestData.UniqueEmail("badge"));
        var api = new AuthApiClient(client);

        await PlayTodayAsync(api, factory.Services);

        var quiz = await api.GetAsync<Quizzes.DailyView>("/api/quiz/daily");
        var answers = await Quizzes.ReadAnswersAsync(factory.Services, quiz.Id);
        using var repeated = await api.PostAsync(SubmitPath, new { answers });

        Assert.Equal(HttpStatusCode.Conflict, repeated.StatusCode);
        Assert.Equal([FirstQuiz], await AwardedCodesAsync(api));
    }

    [Fact]
    public async Task Reading_the_badges_repeatedly_never_awards_a_second_row()
    {
        var email = TestData.UniqueEmail("badge");
        var api = new AuthApiClient(factory.CreateSignedInClientAsync(email));

        await PlayTodayAsync(api, factory.Services);

        var userId = await Badges.UserIdAsync(factory.Services, email);

        // Reads are the one thing an impatient client does twice, and a second row for a
        // badge already held is the exact failure the unique constraint exists to stop.
        await ReadBadgesAsync(api);
        await ReadBadgesAsync(api);
        await ReadBadgesAsync(api);

        Assert.Equal(1, await Badges.AwardRowCountAsync(factory.Services, userId));
        Assert.Equal([FirstQuiz], await AwardedCodesAsync(api));
    }

    [Fact]
    public async Task A_badge_never_pays_extra_xp()
    {
        var client = factory.CreateSignedInClientAsync(TestData.UniqueEmail("badge"));
        var api = new AuthApiClient(client);

        var result = await PlayTodayAsync(api, factory.Services);
        var stats = await api.GetAsync<Quizzes.StatsView>(StatsPath);

        // Exactly the XP the one attempt earned, with nothing added for the badge it won.
        Assert.Equal(result.Progression.XpAwarded, stats.TotalXp);
    }

    [Fact]
    public async Task A_badge_is_awarded_with_the_time_of_the_quiz_that_earned_it()
    {
        var client = factory.CreateSignedInClientAsync(TestData.UniqueEmail("badge"));
        var api = new AuthApiClient(client);

        var result = await PlayTodayAsync(api, factory.Services);
        var first = (await ReadBadgesAsync(api)).Badges.Single(badge => badge.Code == FirstQuiz);

        Assert.True(first.Awarded);
        Assert.NotNull(first.AwardedAt);
        Assert.Equal(result.CompletedAt, first.AwardedAt!.Value);
    }
}

/// <summary>
/// Badges that take more than one quiz, so the clock is pinned and moved in whole days.
/// Without that the streak test would need a week of real time.
/// </summary>
public class BadgeStreakTests : IAsyncLifetime
{
    private const string SubmitPath = "/api/quiz/daily/submit";
    private const string BadgesPath = "/api/account/badges";

    private readonly FixedClockApiFactory _factory = FixedClockApiFactory.NearNow();

    /// <summary>"Today" as the pinned clock sees it, anchored on the real instant so the
    /// authentication cookie stays valid.</summary>
    private DateTimeOffset Day => DateTimeOffset.UtcNow;

    public Task InitializeAsync() => Task.CompletedTask;

    public async Task DisposeAsync()
    {
        _factory.Dispose();
        await Task.CompletedTask;
    }

    private async Task<Badges.ListView> PlayAndReadAsync(AuthApiClient api)
    {
        var quiz = await api.GetAsync<Quizzes.DailyView>("/api/quiz/daily");
        var answers = await Quizzes.ReadAnswersAsync(_factory.Services, quiz.Id);

        using var submitted = await api.PostAsync(SubmitPath, new { answers });
        submitted.EnsureSuccessStatusCode();

        return await api.GetAsync<Badges.ListView>(BadgesPath);
    }

    [Fact]
    public async Task Seven_days_in_a_row_awards_the_streak_badge_on_the_seventh_day()
    {
        var api = new AuthApiClient(_factory.CreateSignedInClientAsync(TestData.UniqueEmail("streak")));

        // Six days earns the first badge but not the streak.
        for (var day = 0; day < 6; day++)
        {
            _factory.Clock.Set(Day.AddDays(day));
            var badges = await PlayAndReadAsync(api);

            Assert.DoesNotContain("seven-day-streak", badges.Badges.Where(b => b.Awarded).Select(b => b.Code));
        }

        _factory.Clock.Set(Day.AddDays(6));
        var onSeventh = await PlayAndReadAsync(api);

        var streak = onSeventh.Badges.Single(badge => badge.Code == "seven-day-streak");
        Assert.True(streak.Awarded);
        Assert.NotNull(streak.AwardedAt);
        Assert.Equal(7, streak.Current);
    }

    [Fact]
    public async Task A_missed_day_never_awards_the_streak_badge()
    {
        var api = new AuthApiClient(_factory.CreateSignedInClientAsync(TestData.UniqueEmail("streak")));

        // Six consecutive days, then a gap, then a seventh play. Seven plays overall, but
        // never seven in a row, which is exactly the claim the badge rests on.
        for (var day = 0; day < 6; day++)
        {
            _factory.Clock.Set(Day.AddDays(day));
            await PlayAndReadAsync(api);
        }

        _factory.Clock.Set(Day.AddDays(8));
        var afterGap = await PlayAndReadAsync(api);

        var streak = afterGap.Badges.Single(badge => badge.Code == "seven-day-streak");
        Assert.False(streak.Awarded);
        Assert.Equal(1, streak.Current);
    }

    [Fact]
    public async Task The_tenth_quiz_awards_the_enthusiast_badge()
    {
        var email = TestData.UniqueEmail("enthusiast");
        var api = new AuthApiClient(_factory.CreateSignedInClientAsync(email));

        // Nine quizzes is one short, so the badge must still be locked at the end of this.
        for (var day = 0; day < 9; day++)
        {
            _factory.Clock.Set(Day.AddDays(day));
            var badges = await PlayAndReadAsync(api);

            var locked = badges.Badges.Single(badge => badge.Code == "quiz-enthusiast");
            Assert.False(locked.Awarded);
            Assert.Equal(day + 1, locked.Current);
            Assert.Equal(10, locked.Threshold);
        }

        // The tenth play is the one that earns it, and it has to be played: reading the
        // list a tenth time would only re-read nine committed attempts.
        _factory.Clock.Set(Day.AddDays(9));
        var onTenth = await PlayAndReadAsync(api);

        var enthusiast = onTenth.Badges.Single(badge => badge.Code == "quiz-enthusiast");
        Assert.True(enthusiast.Awarded);
        Assert.Equal(10, enthusiast.Current);
        Assert.Equal(10, enthusiast.Threshold);

        // Ten quizzes in a row also clears the seven-day streak, so this account has now
        // earned all three. Each must be exactly one row: the count is the guarantee that
        // awarding twice was not merely invisible in the response but never happened.
        var userId = await Badges.UserIdAsync(_factory.Services, email);
        Assert.Equal(3, await Badges.AwardRowCountAsync(_factory.Services, userId));
    }
}
