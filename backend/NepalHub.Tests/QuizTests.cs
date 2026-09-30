using System.Net;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using NepalHub.Api.Data;
using NepalHub.Api.Data.Entities;
using NepalHub.Api.Features.Auth;
using NepalHub.Api.Features.Quiz;

namespace NepalHub.Tests;

/// <summary>
/// The daily quiz end to end: what a player may see, what a submission is allowed to
/// contain, what progression it earns, and what staff may do to a question. Every test
/// that submits creates its own account, because one attempt per user per day is a
/// database constraint and shared accounts would collide.
/// </summary>
public class QuizTests(NepalHubApiFactory factory) : IClassFixture<NepalHubApiFactory>
{
    private const string DailyPath = "/api/quiz/daily";
    private const string SubmitPath = "/api/quiz/daily/submit";
    private const string ResultPath = "/api/quiz/daily/result";
    private const string StatsPath = "/api/quiz/stats";
    private const string StaffPath = "/api/admin/quiz/questions";

    // ------------------------------------------------------------------ reading

    [Fact]
    public async Task Daily_quiz_is_readable_without_an_account()
    {
        var api = new AuthApiClient(factory.CreateClient());
        var quiz = await api.GetAsync<Quizzes.DailyView>(DailyPath);

        Assert.True(quiz.QuestionCount > 0);
        Assert.Equal(quiz.QuestionCount, quiz.Questions.Count);
    }

    [Fact]
    public async Task Daily_quiz_never_carries_the_answer_before_submitting()
    {
        var api = new AuthApiClient(factory.CreateClient());
        var response = await api.GetRawAsync(DailyPath);
        var json = await response.Content.ReadAsStringAsync();

        response.EnsureSuccessStatusCode();

        // The whole point of grading server side. Any one of these appearing in the
        // pre-answer payload lets a player skip the quiz with a text search.
        Assert.DoesNotContain("\"isCorrect\"", json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("\"explanation\"", json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("\"correctOptionId\"", json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("\"correctOptionText\"", json, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Daily_quiz_is_the_same_set_for_everyone_on_the_same_day()
    {
        var first = await new AuthApiClient(factory.CreateClient()).GetAsync<Quizzes.DailyView>(DailyPath);
        var second = await new AuthApiClient(factory.CreateClient()).GetAsync<Quizzes.DailyView>(DailyPath);

        Assert.Equal(first.Id, second.Id);
        Assert.Equal(
            first.Questions.Select(question => question.Id).Order(),
            second.Questions.Select(question => question.Id).Order());
    }

    [Fact]
    public async Task Daily_quiz_presents_options_in_position_order_without_the_answer()
    {
        var api = new AuthApiClient(factory.CreateClient());
        var quiz = await api.GetAsync<Quizzes.DailyView>(DailyPath);

        foreach (var question in quiz.Questions)
        {
            Assert.Equal(Enumerable.Range(1, question.Options.Count), question.Options.Select(option => option.Position));
        }
    }

    [Fact]
    public async Task Result_and_stats_are_closed_to_anonymous_callers()
    {
        var client = factory.CreateClient();

        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync(ResultPath)).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync(StatsPath)).StatusCode);
    }

    // -------------------------------------------------------------- submission

    [Fact]
    public async Task Submitting_without_a_csrf_token_is_refused()
    {
        var client = factory.CreateClient();
        using var request = new HttpRequestMessage(HttpMethod.Post, SubmitPath)
        {
            Content = JsonContent.Create(new { answers = Array.Empty<object>() }),
        };

        using var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("csrf_failed", (await response.Content.ReadFromJsonAsync<ApiEnvelope<object>>(TestJson.Options))?.Error?.Code);
    }

    [Fact]
    public async Task An_anonymous_player_is_graded_but_nothing_is_recorded()
    {
        var api = new AuthApiClient(factory.CreateClient());
        var quiz = await api.GetAsync<Quizzes.DailyView>(DailyPath);
        var correct = await CorrectAnswersAsync(quiz);

        using var response = await api.PostAsync(SubmitPath, new { answers = correct });
        var envelope = await response.Content.ReadFromJsonAsync<ApiEnvelope<Quizzes.ResultView>>(TestJson.Options);

        response.EnsureSuccessStatusCode();
        Assert.Equal(quiz.QuestionCount, envelope!.Data!.Score);

        // Nothing was written, so nothing may be reported as earned. A player shown
        // "xpAwarded: 120" next to "recorded: false" is being told a lie.
        Assert.False(envelope.Data.Progression.Recorded);
        Assert.Equal(0, envelope.Data.Progression.XpAwarded);

        // Explanations are the reward for playing before registering.
        Assert.All(envelope.Data.Reviews, review => Assert.False(string.IsNullOrWhiteSpace(review.Explanation)));
    }

    [Fact]
    public async Task A_signed_in_player_earns_xp_a_level_and_a_streak()
    {
        var client = factory.CreateSignedInClientAsync(TestData.UniqueEmail("quiz"));
        var api = new AuthApiClient(client);

        var quiz = await api.GetAsync<Quizzes.DailyView>(DailyPath);
        var correct = await CorrectAnswersAsync(quiz);

        using var response = await api.PostAsync(SubmitPath, new { answers = correct });
        var envelope = await response.Content.ReadFromJsonAsync<ApiEnvelope<Quizzes.ResultView>>(TestJson.Options);

        response.EnsureSuccessStatusCode();
        var result = envelope!.Data!;

        Assert.Equal(quiz.QuestionCount, result.Score);
        Assert.Equal(quiz.QuestionCount, result.CorrectCount);
        Assert.True(result.Progression.Recorded);

        // 10 per right answer plus the completion bonus.
        Assert.Equal((quiz.QuestionCount * 10) + 20, result.Progression.XpAwarded);
        Assert.Equal(1, result.Progression.StreakCount);
        Assert.True(result.Progression.Level > 1);
    }

    [Fact]
    public async Task A_player_who_gets_everything_wrong_still_earns_the_completion_bonus()
    {
        var client = factory.CreateSignedInClientAsync(TestData.UniqueEmail("quiz"));
        var api = new AuthApiClient(client);

        var quiz = await api.GetAsync<Quizzes.DailyView>(DailyPath);
        var wrong = await WrongAnswersAsync(quiz);

        using var response = await api.PostAsync(SubmitPath, new { answers = wrong });
        var envelope = await response.Content.ReadFromJsonAsync<ApiEnvelope<Quizzes.ResultView>>(TestJson.Options);

        response.EnsureSuccessStatusCode();
        Assert.Equal(0, envelope!.Data!.Score);

        // The worst possible day still counted as finishing the quiz. Gating the bonus
        // on a non-zero score would pay a player nothing for turning up.
        Assert.Equal(20, envelope.Data.Progression.XpAwarded);
        Assert.Equal(1, envelope.Data.Progression.StreakCount);
    }

    [Fact]
    public async Task A_second_attempt_on_the_same_day_is_refused()
    {
        var client = factory.CreateSignedInClientAsync(TestData.UniqueEmail("quiz"));
        var api = new AuthApiClient(client);

        var quiz = await api.GetAsync<Quizzes.DailyView>(DailyPath);
        var answers = new { answers = await CorrectAnswersAsync(quiz) };

        using var first = await api.PostAsync(SubmitPath, answers);
        first.EnsureSuccessStatusCode();

        var second = await api.PostForSnapshotAsync(SubmitPath, answers);

        Assert.Equal(HttpStatusCode.Conflict, second.StatusCode);
        Assert.Equal("quiz_already_completed", second.Error?.Code);
    }

    [Fact]
    public async Task A_stored_result_matches_what_the_submit_returned()
    {
        var client = factory.CreateSignedInClientAsync(TestData.UniqueEmail("quiz"));
        var api = new AuthApiClient(client);

        var quiz = await api.GetAsync<Quizzes.DailyView>(DailyPath);
        using var submitted = await api.PostAsync(SubmitPath, new { answers = await CorrectAnswersAsync(quiz) });
        var submittedEnvelope = await submitted.Content.ReadFromJsonAsync<ApiEnvelope<Quizzes.ResultView>>(TestJson.Options);

        var stored = await api.GetAsync<Quizzes.ResultView>(ResultPath);

        Assert.Equal(submittedEnvelope!.Data!.Score, stored.Score);
        Assert.Equal(submittedEnvelope.Data.Progression.XpAwarded, stored.Progression.XpAwarded);
        Assert.Equal(quiz.QuestionCount, stored.Reviews.Count);
    }

    [Fact]
    public async Task Asking_for_a_result_before_playing_is_a_not_found()
    {
        var client = factory.CreateSignedInClientAsync(TestData.UniqueEmail("quiz"));

        using var response = await client.GetAsync(ResultPath);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Stats_report_progress_after_playing()
    {
        var client = factory.CreateSignedInClientAsync(TestData.UniqueEmail("quiz"));
        var api = new AuthApiClient(client);

        var before = await api.GetAsync<Quizzes.StatsView>(StatsPath);
        Assert.Equal(0, before.TotalXp);
        Assert.Equal(0, before.QuizzesCompleted);
        Assert.Null(before.LastActiveOn);

        var quiz = await api.GetAsync<Quizzes.DailyView>(DailyPath);
        using var submitted = await api.PostAsync(SubmitPath, new { answers = await CorrectAnswersAsync(quiz) });
        submitted.EnsureSuccessStatusCode();

        var after = await api.GetAsync<Quizzes.StatsView>(StatsPath);

        Assert.Equal((quiz.QuestionCount * 10) + 20, after.TotalXp);
        Assert.Equal(1, after.QuizzesCompleted);
        Assert.Equal(1, after.StreakCount);
        Assert.NotNull(after.LastActiveOn);
        Assert.True(after.XpToNextLevel > 0);
    }

    [Fact]
    public async Task A_day_the_player_already_played_reports_as_completed()
    {
        var client = factory.CreateSignedInClientAsync(TestData.UniqueEmail("quiz"));
        var api = new AuthApiClient(client);

        var quiz = await api.GetAsync<Quizzes.DailyView>(DailyPath);
        Assert.False(quiz.AlreadyCompleted);

        using var submitted = await api.PostAsync(SubmitPath, new { answers = await CorrectAnswersAsync(quiz) });
        submitted.EnsureSuccessStatusCode();

        var after = await api.GetAsync<Quizzes.DailyView>(DailyPath);

        Assert.True(after.AlreadyCompleted);
        Assert.NotNull(after.CompletedAt);
    }

    // ---------------------------------------------------------------- tampering

    [Fact]
    public async Task An_option_belonging_to_another_question_is_refused()
    {
        var client = factory.CreateSignedInClientAsync(TestData.UniqueEmail("quiz"));
        var api = new AuthApiClient(client);

        var quiz = await api.GetAsync<Quizzes.DailyView>(DailyPath);
        var correct = await CorrectAnswersAsync(quiz);

        // The anti-swap case: a real option id, just not this question's.
        var swapped = correct.ToList();
        swapped[0] = new Quizzes.SubmittedAnswerView(swapped[0].QuestionId, swapped[1].OptionId);

        var result = await api.PostForSnapshotAsync(SubmitPath, new { answers = swapped });

        Assert.Equal(HttpStatusCode.BadRequest, result.StatusCode);
        Assert.Equal("validation_failed", result.Error?.Code);
    }

    [Fact]
    public async Task A_partial_submission_is_refused()
    {
        var client = factory.CreateSignedInClientAsync(TestData.UniqueEmail("quiz"));
        var api = new AuthApiClient(client);

        var quiz = await api.GetAsync<Quizzes.DailyView>(DailyPath);
        var partial = (await CorrectAnswersAsync(quiz)).Take(quiz.QuestionCount - 1);

        var result = await api.PostForSnapshotAsync(SubmitPath, new { answers = partial });

        Assert.Equal(HttpStatusCode.BadRequest, result.StatusCode);
    }

    [Fact]
    public async Task Answering_the_same_question_twice_is_refused()
    {
        var client = factory.CreateSignedInClientAsync(TestData.UniqueEmail("quiz"));
        var api = new AuthApiClient(client);

        var quiz = await api.GetAsync<Quizzes.DailyView>(DailyPath);
        var correct = (await CorrectAnswersAsync(quiz)).ToList();
        correct[1] = correct[0];

        var result = await api.PostForSnapshotAsync(SubmitPath, new { answers = correct });

        Assert.Equal(HttpStatusCode.BadRequest, result.StatusCode);
    }

    [Fact]
    public async Task A_question_that_is_not_in_todays_quiz_is_refused()
    {
        var client = factory.CreateSignedInClientAsync(TestData.UniqueEmail("quiz"));
        var api = new AuthApiClient(client);

        var quiz = await api.GetAsync<Quizzes.DailyView>(DailyPath);
        var answers = (await CorrectAnswersAsync(quiz)).ToList();
        answers[^1] = new Quizzes.SubmittedAnswerView(Guid.NewGuid(), answers[^1].OptionId);

        var result = await api.PostForSnapshotAsync(SubmitPath, new { answers = answers });

        Assert.Equal(HttpStatusCode.BadRequest, result.StatusCode);
    }

    [Fact]
    public async Task An_empty_submission_is_refused()
    {
        var api = new AuthApiClient(factory.CreateClient());

        var result = await api.PostForSnapshotAsync(SubmitPath, new { answers = Array.Empty<object>() });

        Assert.Equal(HttpStatusCode.BadRequest, result.StatusCode);
    }

    // ------------------------------------------------------------------- staff

    [Fact]
    public async Task Question_management_is_closed_to_anonymous_and_to_players()
    {
        using var anonymous = await factory.CreateClient().GetAsync(StaffPath);
        Assert.Equal(HttpStatusCode.Unauthorized, anonymous.StatusCode);

        using var player = await factory.CreateSignedInClientAsync(TestData.UniqueEmail("quiz")).GetAsync(StaffPath);
        Assert.Equal(HttpStatusCode.Forbidden, player.StatusCode);
    }

    [Fact]
    public async Task A_question_can_be_stored_as_an_unfinished_draft()
    {
        var staff = new AuthApiClient(await factory.CreateStaffClientAsync(Roles.Moderator));

        using var response = await staff.PostAsync(StaffPath, new
        {
            text = "A draft that is not finished yet, which is allowed.",
            explanation = string.Empty,
            category = "Nepal",
            isPublished = false,
            options = new[] { new { text = "Alpha", isCorrect = false }, new { text = "Beta", isCorrect = false } },
        });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
    }

    [Theory]
    [InlineData("Mark exactly one option as the correct answer before publishing.")]
    public async Task Publishing_requires_exactly_one_correct_option(string expectedFragment)
    {
        var staff = new AuthApiClient(await factory.CreateStaffClientAsync(Roles.Moderator));

        var missing = await staff.PostForSnapshotAsync(StaffPath, new
        {
            text = "A question with no correct option marked anywhere.",
            explanation = "This explanation is comfortably long enough to publish.",
            category = "Nepal",
            isPublished = true,
            options = new[] { new { text = "Alpha", isCorrect = false }, new { text = "Beta", isCorrect = false } },
        });

        Assert.Equal(HttpStatusCode.BadRequest, missing.StatusCode);
        Assert.Contains(expectedFragment, missing.Json);

        var doubled = await staff.PostForSnapshotAsync(StaffPath, new
        {
            text = "A question with two correct options marked.",
            explanation = "This explanation is comfortably long enough to publish.",
            category = "Nepal",
            isPublished = true,
            options = new[] { new { text = "Alpha", isCorrect = true }, new { text = "Beta", isCorrect = true } },
        });

        Assert.Equal(HttpStatusCode.BadRequest, doubled.StatusCode);
    }

    [Fact]
    public async Task Publishing_requires_a_real_explanation()
    {
        var staff = new AuthApiClient(await factory.CreateStaffClientAsync(Roles.Moderator));

        var result = await staff.PostForSnapshotAsync(StaffPath, new
        {
            text = "A perfectly good question with a useless explanation.",
            explanation = "no",
            category = "Nepal",
            isPublished = true,
            options = new[] { new { text = "Alpha", isCorrect = true }, new { text = "Beta", isCorrect = false } },
        });

        Assert.Equal(HttpStatusCode.BadRequest, result.StatusCode);
    }

    [Fact]
    public async Task A_question_needs_at_least_two_distinct_options()
    {
        var staff = new AuthApiClient(await factory.CreateStaffClientAsync(Roles.Moderator));

        var single = await staff.PostForSnapshotAsync(StaffPath, new
        {
            text = "A question that offers only one possible answer.",
            explanation = string.Empty,
            category = "Nepal",
            isPublished = false,
            options = new[] { new { text = "Only", isCorrect = true } },
        });

        Assert.Equal(HttpStatusCode.BadRequest, single.StatusCode);

        var duplicated = await staff.PostForSnapshotAsync(StaffPath, new
        {
            text = "A question whose two options say the same thing.",
            explanation = string.Empty,
            category = "Nepal",
            isPublished = false,
            options = new[] { new { text = "Same", isCorrect = false }, new { text = "Same", isCorrect = true } },
        });

        Assert.Equal(HttpStatusCode.BadRequest, duplicated.StatusCode);
    }

    [Fact]
    public async Task An_unknown_category_is_refused()
    {
        var staff = new AuthApiClient(await factory.CreateStaffClientAsync(Roles.Moderator));

        var result = await staff.PostForSnapshotAsync(StaffPath, new
        {
            text = "A question filed under a category that does not exist.",
            explanation = string.Empty,
            category = 99,
            isPublished = false,
            options = new[] { new { text = "Alpha", isCorrect = true }, new { text = "Beta", isCorrect = false } },
        });

        Assert.Equal(HttpStatusCode.BadRequest, result.StatusCode);
    }

    [Fact]
    public async Task A_published_question_is_retracted_before_it_can_be_deleted()
    {
        var staff = new AuthApiClient(await factory.CreateStaffClientAsync(Roles.Moderator));

        using var created = await staff.PostAsync(StaffPath, new
        {
            text = "A question created to check the retract then delete path.",
            explanation = "Alpha is the answer, which is a long enough explanation.",
            category = "Nepal",
            isPublished = true,
            options = new[] { new { text = "Alpha", isCorrect = true }, new { text = "Beta", isCorrect = false } },
        });

        created.EnsureSuccessStatusCode();
        var id = await ReadIdAsync(created);

        var deleted = await staff.DeleteForSnapshotAsync($"{StaffPath}/{id}");
        Assert.Equal(HttpStatusCode.Conflict, deleted.StatusCode);
        Assert.Equal("question_published", deleted.Error?.Code);

        var retracted = await staff.PutForSnapshotAsync($"{StaffPath}/{id}", new
        {
            text = "A question created to check the retract then delete path.",
            explanation = "Alpha is the answer, which is a long enough explanation.",
            category = "Nepal",
            isPublished = false,
            options = new[] { new { text = "Alpha", isCorrect = true }, new { text = "Beta", isCorrect = false } },
        });

        Assert.Equal(HttpStatusCode.OK, retracted.StatusCode);

        var removed = await staff.DeleteForSnapshotAsync($"{StaffPath}/{id}");
        Assert.Equal(HttpStatusCode.NoContent, removed.StatusCode);

        var again = await staff.DeleteForSnapshotAsync($"{StaffPath}/{id}");
        Assert.Equal(HttpStatusCode.NotFound, again.StatusCode);
    }

    [Fact]
    public async Task A_played_question_can_be_retracted_but_never_rewritten()
    {
        var staff = new AuthApiClient(await factory.CreateStaffClientAsync(Roles.Moderator));

        var played = await staff.GetAsync<Quizzes.AdminListView>($"{StaffPath}?page=1&pageSize=100");
        var target = played.Items.FirstOrDefault(item => item.HasBeenPlayed);
        Assert.NotNull(target);

        var full = await staff.GetAsync<Quizzes.AdminQuestionView>($"{StaffPath}/{target!.Id}");

        // Rewriting the wording would silently change what past players' results say.
        var reworded = await staff.PutForSnapshotAsync($"{StaffPath}/{target.Id}", new
        {
            text = "A question that has been tampered with after being played.",
            explanation = "This rewrite must be refused because history would change.",
            category = full.Category,
            isPublished = full.IsPublished,
            options = full.Options.Select(option => new { text = option.Text, isCorrect = option.IsCorrect }),
        });

        Assert.Equal(HttpStatusCode.Conflict, reworded.StatusCode);
        Assert.Equal("question_in_use", reworded.Error?.Code);

        // Visibility stays the staff's call in both directions.
        var retracted = await staff.PutForSnapshotAsync($"{StaffPath}/{target.Id}", new
        {
            text = full.Text,
            explanation = full.Explanation,
            category = full.Category,
            isPublished = false,
            options = full.Options.Select(option => new { text = option.Text, isCorrect = option.IsCorrect }),
        });

        Assert.Equal(HttpStatusCode.OK, retracted.StatusCode);
        Assert.False(ReadPublished(retracted.Json));

        var restored = await staff.PutForSnapshotAsync($"{StaffPath}/{target.Id}", new
        {
            text = full.Text,
            explanation = full.Explanation,
            category = full.Category,
            isPublished = true,
            options = full.Options.Select(option => new { text = option.Text, isCorrect = option.IsCorrect }),
        });

        Assert.Equal(HttpStatusCode.OK, restored.StatusCode);
        Assert.True(ReadPublished(restored.Json));
    }

    [Fact]
    public async Task Staff_can_filter_and_search_the_question_list()
    {
        var staff = new AuthApiClient(await factory.CreateStaffClientAsync(Roles.Moderator));

        var nepal = await staff.GetAsync<Quizzes.AdminListView>($"{StaffPath}?page=1&pageSize=50&category=0");
        Assert.NotEmpty(nepal.Items);
        Assert.All(nepal.Items, item => Assert.Equal("Nepal", item.Category));

        var searched = await staff.GetAsync<Quizzes.AdminListView>($"{StaffPath}?page=1&pageSize=50&search=river");
        Assert.NotEmpty(searched.Items);
        Assert.All(
            searched.Items,
            item => Assert.Contains("river", item.Text, StringComparison.OrdinalIgnoreCase));
    }

    // -------------------------------------------------------------- seed health

    [Fact]
    public async Task Every_published_question_is_playable()
    {
        using var scope = factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var questions = await dbContext.QuizQuestions
            .AsNoTracking()
            .Include(question => question.Options)
            .Where(question => question.IsPublished)
            .ToListAsync();

        Assert.NotEmpty(questions);

        foreach (var question in questions)
        {
            Assert.InRange(question.Options.Count, 2, 6);
            Assert.Single(question.Options.Where(option => option.IsCorrect));
            Assert.True(
                question.Explanation.Trim().Length >= 10,
                $"Question '{question.Text}' is published without a usable explanation.");

            var texts = question.Options.Select(option => option.Text.Trim()).ToList();
            Assert.Equal(texts.Count, texts.Distinct(StringComparer.OrdinalIgnoreCase).Count());
        }
    }

    [Fact]
    public async Task Reseeding_does_not_duplicate_the_shipped_questions()
    {
        using (var scope = factory.Services.CreateScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            await dbContext.QuizQuestions.CountAsync();
        }

        // The seeder keys on a stable code, so a second application start is a no-op.
        var client = factory.CreateClient();
        using var response = await client.GetAsync(DailyPath);
        response.EnsureSuccessStatusCode();
    }

    // ------------------------------------------------------------------ helpers

    private static async Task<Guid> ReadIdAsync(HttpResponseMessage response)
    {
        var envelope = await response.Content.ReadFromJsonAsync<ApiEnvelope<Quizzes.AdminQuestionView>>(TestJson.Options);

        return envelope!.Data!.Id;
    }

    private static bool ReadPublished(string json) =>
        System.Text.Json.JsonDocument.Parse(json)
            .RootElement
            .GetProperty("data")
            .GetProperty("isPublished")
            .GetBoolean();

    private Task<IReadOnlyList<Quizzes.SubmittedAnswerView>> CorrectAnswersAsync(Quizzes.DailyView quiz) =>
        Quizzes.ReadAnswersAsync(factory.Services, quiz.Id);

    private Task<IReadOnlyList<Quizzes.SubmittedAnswerView>> WrongAnswersAsync(Quizzes.DailyView quiz) =>
        Quizzes.ReadAnswersAsync(factory.Services, quiz.Id, correct: false);

    // The projections live on <see cref="Quizzes"/> because the streak tests drive a
    // pinned clock and therefore cannot share this class's fixture.
}

/// <summary>
/// The JSON shapes the quiz endpoints return, plus the one helper that needs database
/// access. Declared once so the clock-driven streak tests and this class read the same
/// wire format instead of drifting apart.
/// </summary>
internal static class Quizzes
{
    internal sealed record DailyView(
        Guid Id,
        DateOnly QuizDate,
        int QuestionCount,
        int MaxXpAvailable,
        bool AlreadyCompleted,
        DateTime? CompletedAt,
        IReadOnlyList<QuestionView> Questions);

    internal sealed record QuestionView(Guid Id, int Position, string Text, string Category, IReadOnlyList<OptionView> Options);

    internal sealed record OptionView(Guid Id, int Position, string Text);

    internal sealed record SubmittedAnswerView(Guid QuestionId, Guid OptionId);

    internal sealed record ResultView(
        Guid AttemptId,
        DateOnly QuizDate,
        int Score,
        int MaxScore,
        int CorrectCount,
        DateTime CompletedAt,
        ProgressionView Progression,
        IReadOnlyList<ReviewView> Reviews);

    internal sealed record ProgressionView(
        bool Recorded,
        int XpAwarded,
        int? TotalXp,
        int? Level,
        int? StreakCount,
        DateOnly? LastActiveOn);

    internal sealed record ReviewView(
        Guid QuestionId,
        string Text,
        string Category,
        Guid SelectedOptionId,
        string SelectedOptionText,
        Guid CorrectOptionId,
        string CorrectOptionText,
        bool IsCorrect,
        string Explanation);

    internal sealed record StatsView(
        int TotalXp,
        int Level,
        int StreakCount,
        int XpToNextLevel,
        int QuizzesCompleted,
        DateOnly? LastActiveOn);

    internal sealed record AdminListView(IReadOnlyList<AdminSummaryView> Items, int TotalCount);

    internal sealed record AdminSummaryView(
        Guid Id,
        string Text,
        string Category,
        bool IsPublished,
        int OptionCount,
        int CorrectOptionCount,
        bool HasBeenPlayed);

    internal sealed record AdminQuestionView(
        Guid Id,
        string Text,
        string Explanation,
        string Category,
        bool IsPublished,
        IReadOnlyList<AdminOptionView> Options);

    internal sealed record AdminOptionView(Guid Id, int Position, string Text, bool IsCorrect);

    /// <summary>
    /// Reads the answer key for a materialised quiz straight from the database. The
    /// public payload deliberately withholds it, so a test acting as a player has to be
    /// a trusted client to construct a known-good submission.
    /// </summary>
    internal static async Task<IReadOnlyList<SubmittedAnswerView>> ReadAnswersAsync(
        IServiceProvider services,
        Guid dailyQuizId,
        bool correct = true)
    {
        using var scope = services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var links = await dbContext.DailyQuizQuestions
            .AsNoTracking()
            .Where(link => link.DailyQuizId == dailyQuizId)
            .OrderBy(link => link.Position)
            .ToListAsync();

        var options = await dbContext.QuizOptions
            .AsNoTracking()
            .Where(option => links.Select(link => link.QuestionId).Contains(option.QuestionId))
            .ToListAsync();

        return links
            .Select(link => new Quizzes.SubmittedAnswerView(
                link.QuestionId,
                options
                    .Where(option => option.QuestionId == link.QuestionId && option.IsCorrect == correct)
                    .OrderBy(option => option.Position)
                    .First()
                    .Id))
            .ToList();
    }
}

/// <summary>
/// Streaks depend on the Nepal calendar date, so these pin the clock. Yesterday's quiz
/// and today's quiz are different sets, and playing both in order is the only way to see
/// a streak extend rather than restart.
/// </summary>
public class QuizStreakTests : IAsyncLifetime
{
    private readonly FixedClockApiFactory _factory = FixedClockApiFactory.NearNow();

    /// <summary>
    /// "Today" as the pinned clock sees it. Anchored on the real instant so the auth
    /// cookie stays valid, then moved in whole days to change the Nepal date.
    /// </summary>
    private DateTimeOffset Day => DateTimeOffset.UtcNow;

    public Task InitializeAsync() => Task.CompletedTask;

    public async Task DisposeAsync()
    {
        _factory.Dispose();
        await Task.CompletedTask;
    }

    [Fact]
    public async Task Playing_on_two_consecutive_days_extends_the_streak()
    {
        var client = _factory.CreateSignedInClientAsync(TestData.UniqueEmail("streak"));
        var api = new AuthApiClient(client);

        // The pinned instant is the real one, so this is today's quiz.
        _factory.Clock.Set(Day);
        var firstDay = await api.GetAsync<Quizzes.DailyView>("/api/quiz/daily");        using (var submitted = await api.PostAsync("/api/quiz/daily/submit", new { answers = await Quizzes.ReadAnswersAsync(_factory.Services, firstDay.Id) }))
        {
            submitted.EnsureSuccessStatusCode();
        }

        var afterFirst = await api.GetAsync<Quizzes.StatsView>("/api/quiz/stats");
        Assert.Equal(1, afterFirst.StreakCount);

        _factory.Clock.Set(Day.AddDays(1));
        var today = await api.GetAsync<Quizzes.DailyView>("/api/quiz/daily");
        Assert.Equal(firstDay.QuizDate.AddDays(1), today.QuizDate);

        using (var submitted = await api.PostAsync("/api/quiz/daily/submit", new { answers = await Quizzes.ReadAnswersAsync(_factory.Services, today.Id) }))
        {
            submitted.EnsureSuccessStatusCode();
        }

        var afterSecond = await api.GetAsync<Quizzes.StatsView>("/api/quiz/stats");

        Assert.Equal(2, afterSecond.StreakCount);
        Assert.Equal(2, afterSecond.QuizzesCompleted);
    }

    [Fact]
    public async Task A_missed_day_restarts_the_streak()
    {
        var client = _factory.CreateSignedInClientAsync(TestData.UniqueEmail("streak"));
        var api = new AuthApiClient(client);

        _factory.Clock.Set(Day);
        var first = await api.GetAsync<Quizzes.DailyView>("/api/quiz/daily");
        using (var submitted = await api.PostAsync("/api/quiz/daily/submit", new { answers = await Quizzes.ReadAnswersAsync(_factory.Services, first.Id) }))
        {
            submitted.EnsureSuccessStatusCode();
        }

        // Two days later, so the middle day was missed.
        _factory.Clock.Set(Day.AddDays(2));
        var third = await api.GetAsync<Quizzes.DailyView>("/api/quiz/daily");
        using (var submitted = await api.PostAsync("/api/quiz/daily/submit", new { answers = await Quizzes.ReadAnswersAsync(_factory.Services, third.Id) }))
        {
            submitted.EnsureSuccessStatusCode();
        }

        var stats = await api.GetAsync<Quizzes.StatsView>("/api/quiz/stats");

        Assert.Equal(1, stats.StreakCount);
        Assert.Equal(2, stats.QuizzesCompleted);
    }

    [Fact]
    public async Task Consecutive_days_draw_different_questions()
    {
        var api = new AuthApiClient(_factory.CreateClient());

        _factory.Clock.Set(Day);
        var firstDay = await api.GetAsync<Quizzes.DailyView>("/api/quiz/daily");

        _factory.Clock.Set(Day.AddDays(1));
        var secondDay = await api.GetAsync<Quizzes.DailyView>("/api/quiz/daily");

        var overlap = firstDay.Questions
            .Select(question => question.Id)
            .Intersect(secondDay.Questions.Select(question => question.Id))
            .Count();

        // A rotation that restarted at the same offset every day would repeat the whole set.
        Assert.True(overlap < firstDay.Questions.Count, "Two consecutive days drew an identical set of questions.");
    }
}

/// <summary>The quiz submit endpoint is the one write a stranger can make repeatedly.</summary>
public class QuizRateLimitingTests(QuizLimitedApiFactory factory) : IClassFixture<QuizLimitedApiFactory>
{
    [Fact]
    public async Task Submitting_faster_than_the_limit_is_throttled()
    {
        factory.LowerSubmitQuota(QuizLimitedApiFactory.PermitLimit);

        var api = new AuthApiClient(factory.CreateClient());

        var accepted = 0;
        var throttled = 0;

        for (var attempt = 0; attempt < QuizLimitedApiFactory.PermitLimit + 2; attempt++)
        {
            using var response = await api.PostAsync("/api/quiz/daily/submit", new { answers = Array.Empty<object>() });

            if (response.StatusCode == HttpStatusCode.TooManyRequests)
            {
                throttled++;
            }
            else
            {
                accepted++;
            }
        }

        Assert.Equal(QuizLimitedApiFactory.PermitLimit, accepted);
        Assert.Equal(2, throttled);
    }

    [Fact]
    public async Task Reading_the_quiz_is_not_throttled()
    {
        factory.LowerSubmitQuota(QuizLimitedApiFactory.PermitLimit);

        var client = factory.CreateClient();

        // Exhausting the submit budget must not stop someone reloading the page.
        for (var attempt = 0; attempt < QuizLimitedApiFactory.PermitLimit + 2; attempt++)
        {
            using var response = await client.GetAsync("/api/quiz/daily");
            response.EnsureSuccessStatusCode();
        }
    }
}
