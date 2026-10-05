using Microsoft.EntityFrameworkCore;
using MySqlConnector;
using NepalHub.Api.Common;
using NepalHub.Api.Data;
using NepalHub.Api.Data.Entities;
using NepalHub.Api.Features.Badges;

namespace NepalHub.Api.Features.Quiz;

/// <summary>Tuning that is safe to change without a deploy, bound from the <c>Quiz</c> config section.</summary>
public sealed class QuizSettings
{
    public const string SectionName = "Quiz";

    /// <summary>How many questions one daily quiz holds.</summary>
    public int QuestionsPerQuiz { get; set; } = 10;

    public int XpPerCorrectAnswer { get; set; } = 10;

    public int CompletionBonusXp { get; set; } = 20;

    public int RateLimitPermitLimit { get; set; } = 20;

    public int RateLimitWindowSeconds { get; set; } = 60;
}

/// <summary>
/// The submit quota, as a mutable holder rather than a value copied into the rate limiter
/// at startup. The partitioner reads these properties on every lookup, so the effective
/// limit follows the holder instead of freezing whatever the configuration said when the
/// host was built.
/// </summary>
public sealed class QuizRateLimitOptions
{
    public QuizRateLimitOptions(QuizSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);

        PermitLimit = Math.Max(1, settings.RateLimitPermitLimit);
        Window = TimeSpan.FromSeconds(Math.Max(1, settings.RateLimitWindowSeconds));
    }

    public int PermitLimit { get; set; }

    public TimeSpan Window { get; set; }
}

public enum DailyQuizStatus
{
    Success,
    NotAvailable,
}

public enum SubmitStatus
{
    Success,
    NotAvailable,
    Invalid,
    AlreadyCompleted,
}

public enum QuizSaveStatus
{
    Created,
    Updated,
    NotFound,
    Invalid,

    /// <summary>
    /// The question has already been played, so its wording and options are part of somebody's
    /// permanent result. Retracting it is allowed; rewriting it is not.
    /// </summary>
    InUse,
}

public enum QuizDeleteStatus
{
    Deleted,
    NotFound,
    Published,
    InUse,
}

public sealed record DailyQuizOutcome(
    DailyQuizStatus Status,
    DailyQuizResponse? Quiz = null,
    string? Message = null);

public sealed record SubmitOutcome(
    SubmitStatus Status,
    QuizResultResponse? Result = null,
    IReadOnlyList<FieldError>? FieldErrors = null,
    string? Message = null);

public sealed record QuizSaveOutcome(
    QuizSaveStatus Status,
    QuizQuestionAdminResponse? Question = null,
    IReadOnlyList<FieldError>? FieldErrors = null,
    string? Message = null);

/// <summary>
/// All quiz rules: materialising the day, grading a submission, awarding progression and
/// the staff write paths. The controllers only map these outcomes onto HTTP, exactly as
/// <c>CalculatorService</c> does for calculators.
/// </summary>
public sealed class QuizService(
    AppDbContext dbContext,
    INepalDateProvider dates,
    QuizSettings settings,
    BadgeAwardService badgeAwards,
    ILogger<QuizService> logger)
{
    private const int MinOptions = 2;
    private const int MaxOptions = 6;

    private int QuestionsPerQuiz => Math.Max(1, settings.QuestionsPerQuiz);

    /// <summary>
    /// The ceiling for one quiz is derived from the questions it actually contains. A day
    /// can legitimately come up short of the configured size when the published pool is
    /// small, and telling the player a perfect score is worth XP that cannot be earned is
    /// a lie the UI would happily render.
    /// </summary>
    private int MaxXpFor(int questionCount) =>
        (Math.Max(0, questionCount) * settings.XpPerCorrectAnswer) + settings.CompletionBonusXp;

    // ---------------------------------------------------------------- public read

    /// <summary>
    /// Today's quiz, materialised on first use. <paramref name="userId"/> is null for an
    /// anonymous caller, who sees the questions but never has anything recorded.
    /// </summary>
    internal async Task<DailyQuizOutcome> GetDailyQuizAsync(Guid? userId, CancellationToken cancellationToken)
    {
        var quiz = await FindOrMaterialiseAsync(dates.Today, cancellationToken);

        if (quiz is null)
        {
            return new DailyQuizOutcome(
                DailyQuizStatus.NotAvailable,
                Message: "There is no quiz available today. Please check back tomorrow.");
        }

        // Answered only with a query scoped to the signed-in user's own attempt, so one
        // visitor can never learn anything about somebody else's day.
        var attempt = userId is { } id
            ? await FindAttemptAsync(id, quiz.Id, cancellationToken)
            : null;

        return new DailyQuizOutcome(DailyQuizStatus.Success, ToDailyQuizResponse(quiz, attempt));
    }

    internal async Task<QuizResultResponse?> GetResultAsync(Guid userId, CancellationToken cancellationToken)
    {
        var quiz = await FindForDateAsync(dates.Today, cancellationToken);
        if (quiz is null)
        {
            return null;
        }

        var attempt = await FindAttemptAsync(userId, quiz.Id, cancellationToken);

        return attempt is null ? null : await ToResultAsync(attempt, cancellationToken);
    }

    internal async Task<QuizStatsResponse> GetStatsAsync(Guid userId, CancellationToken cancellationToken)
    {
        var user = await LoadUserAsync(userId, cancellationToken, tracking: false);

        return new QuizStatsResponse(
            user.Xp,
            QuizProgression.LevelForXp(user.Xp),
            user.StreakCount,
            QuizProgression.XpToNextLevel(user.Xp),
            await dbContext.QuizAttempts.CountAsync(attempt => attempt.UserId == userId, cancellationToken),
            user.LastActiveOn);
    }

    // ------------------------------------------------------------------- grading

    /// <summary>
    /// Grades a whole submitted quiz. Nothing about correctness is taken from the request:
    /// the score is recomputed from the stored options, so a caller cannot talk the server
    /// into marking its own answer right.
    /// </summary>
    internal async Task<SubmitOutcome> SubmitAsync(
        SubmitQuizRequest request,
        Guid? userId,
        CancellationToken cancellationToken)
    {
        var today = dates.Today;
        var quiz = await FindOrMaterialiseAsync(today, cancellationToken);

        if (quiz is null)
        {
            return new SubmitOutcome(SubmitStatus.NotAvailable);
        }

        if (userId is { } playerId)
        {
            // Checked before grading, so "you already played" is the same answer whatever
            // body the second request happens to carry.
            if (await FindAttemptAsync(playerId, quiz.Id, cancellationToken) is not null)
            {
                return new SubmitOutcome(
                    SubmitStatus.AlreadyCompleted,
                    Message: "You have already played today's quiz.");
            }
        }

        var graded = Grade(quiz, request.Answers);
        if (graded.FieldErrors.Count > 0)
        {
            return new SubmitOutcome(SubmitStatus.Invalid, FieldErrors: graded.FieldErrors);
        }

        var xp = QuizProgression.XpForAttempt(
            graded.CorrectCount,
            settings.XpPerCorrectAnswer,
            settings.CompletionBonusXp);

        if (userId is not { } player)
        {
            // An anonymous visitor gets the score and the explanations, which is what makes
            // trying the quiz worth doing before creating an account, but nothing is stored.
            // The XP fields are reported as zero rather than as the score the same answers
            // would have earned: "xpAwarded: 120" next to "recorded: false" would have the UI
            // congratulate a visitor on points they do not have and cannot spend.
            return new SubmitOutcome(
                SubmitStatus.Success,
                new QuizResultResponse(
                    Guid.NewGuid(),
                    quiz.QuizDate,
                    graded.Score,
                    quiz.QuestionCount,
                    graded.CorrectCount,
                    dates.UtcNow,
                    new QuizProgressionResponse(false, 0, null, null, null, null),
                    graded.Reviews));
        }

        var attempt = new QuizAttempt
        {
            Id = Guid.NewGuid(),
            UserId = player,
            DailyQuizId = quiz.Id,
            Score = graded.Score,
            MaxScore = quiz.QuestionCount,
            XpAwarded = xp,
            CompletedAt = dates.UtcNow,
        };

        foreach (var review in graded.Reviews)
        {
            attempt.Answers.Add(new QuizAttemptAnswer
            {
                AttemptId = attempt.Id,
                QuestionId = review.QuestionId,
                SelectedOptionId = review.SelectedOptionId,
                IsCorrect = review.IsCorrect,
            });
        }

        var user = await LoadUserAsync(player, cancellationToken, tracking: true);

        // The XP, the level and the streak move in the same transaction as the attempt
        // insert, so a half-awarded completion cannot survive a failure.
        await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);

        user.Xp += xp;
        user.Level = QuizProgression.LevelForXp(user.Xp);
        user.StreakCount = QuizProgression.StreakAfter(user.LastActiveOn, today, user.StreakCount);
        user.LastActiveOn = today;
        user.UpdatedAt = dates.UtcNow;

        dbContext.QuizAttempts.Add(attempt);

        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        }
        catch (DbUpdateException exception) when (IsDuplicateKey(exception))
        {
            // The unique constraint on (user_id, daily_quiz_id) is the real guard against a
            // second daily completion. Losing that race is an expected outcome, not a fault.
            await transaction.RollbackAsync(cancellationToken);
            DetachAttempt(attempt);

            logger.LogInformation(exception, "A duplicate daily quiz attempt was refused for user {UserId}.", player);

            return new SubmitOutcome(
                SubmitStatus.AlreadyCompleted,
                Message: "You have already played today's quiz.");
        }

        // The quiz is durable now, so whatever it earned can be recorded. This runs after
        // the commit rather than inside the transaction on purpose: the duplicate-key
        // handler above catches every MySQL 1062 and reports it as a repeated quiz, so a
        // badge insert sharing that save could be reported as a quiz the player never
        // retried. See BadgeAwardService for the longer reasoning.
        await GrantEarnedBadgesAsync(player, user.StreakCount, attempt.CompletedAt, cancellationToken);

        return new SubmitOutcome(
            SubmitStatus.Success,
            new QuizResultResponse(
                attempt.Id,
                quiz.QuizDate,
                graded.Score,
                quiz.QuestionCount,
                graded.CorrectCount,
                attempt.CompletedAt,
                new QuizProgressionResponse(true, xp, user.Xp, user.Level, user.StreakCount, user.LastActiveOn),
                graded.Reviews));
    }

    /// <summary>
    /// Counts the person's finished quizzes from the committed rows and hands the totals to
    /// the badge service. Counting rows beats keeping a tally on the user, which would be a
    /// second place for the quiz count to live and could disagree with the attempts.
    /// </summary>
    private async Task GrantEarnedBadgesAsync(
        Guid player,
        int streakCount,
        DateTime awardedAt,
        CancellationToken cancellationToken)
    {
        var quizzesCompleted = await dbContext.QuizAttempts
            .AsNoTracking()
            .CountAsync(attempt => attempt.UserId == player, cancellationToken);

        await badgeAwards.GrantEarnedBadgesAsync(
            player,
            quizzesCompleted,
            streakCount,
            awardedAt,
            cancellationToken);
    }

    // ---------------------------------------------------------------- staff read

    internal async Task<PagedResult<QuizQuestionAdminSummaryResponse>> ListForAdminAsync(
        PageRequest paging,
        string? search,
        QuizCategory? category,
        CancellationToken cancellationToken)
    {
        var query = dbContext.QuizQuestions.AsNoTracking();

        if (category is { } selected)
        {
            query = query.Where(question => question.Category == selected);
        }

        if (!string.IsNullOrWhiteSpace(search))
        {
            var pattern = $"%{EscapeLike(search.Trim())}%";
            query = query.Where(question => EF.Functions.Like(question.Text, pattern, LikeEscape));
        }

        var totalCount = await query.CountAsync(cancellationToken);
        var paged = paging.ClampTo(totalCount);

        // The option counts are aggregated as correlated subqueries rather than by loading
        // every option, so a thousand-question pool costs what a ten-question one does.
        var summaries = await query
            .OrderByDescending(question => question.UpdatedAt)
            .ThenBy(question => question.Id)
            .Skip(paged.Skip)
            .Take(paged.PageSize)
            .Select(question => new
            {
                question.Id,
                question.Text,
                question.Category,
                question.IsPublished,
                question.UpdatedAt,
                OptionCount = question.Options.Count,
                CorrectOptionCount = question.Options.Count(option => option.IsCorrect),
                Author = question.CreatedByUser == null ? null : question.CreatedByUser.Email,
            })
            .ToListAsync(cancellationToken);

        if (summaries.Count == 0)
        {
            return new PagedResult<QuizQuestionAdminSummaryResponse>([], paged.Page, paged.PageSize, totalCount);
        }

        var ids = summaries.Select(summary => summary.Id).ToList();
        var played = await dbContext.QuizAttemptAnswers
            .AsNoTracking()
            .Where(answer => ids.Contains(answer.QuestionId))
            .Select(answer => answer.QuestionId)
            .Distinct()
            .ToListAsync(cancellationToken);

        var playedSet = played.ToHashSet();
        var items = summaries
            .Select(summary => new QuizQuestionAdminSummaryResponse(
                summary.Id,
                summary.Text,
                summary.Category,
                summary.IsPublished,
                summary.OptionCount,
                summary.CorrectOptionCount,
                playedSet.Contains(summary.Id),
                summary.UpdatedAt,
                summary.Author))
            .ToList();

        return new PagedResult<QuizQuestionAdminSummaryResponse>(items, paged.Page, paged.PageSize, totalCount);
    }

    internal async Task<QuizQuestionAdminResponse?> FindForAdminAsync(Guid id, CancellationToken cancellationToken)
    {
        var question = await dbContext.QuizQuestions
            .AsNoTracking()
            .Include(item => item.Options)
            .FirstOrDefaultAsync(item => item.Id == id, cancellationToken);

        if (question is null)
        {
            return null;
        }

        var author = question.CreatedByUserId is null
            ? null
            : await dbContext.Users
                .AsNoTracking()
                .Where(user => user.Id == question.CreatedByUserId)
                .Select(user => user.Email)
                .FirstOrDefaultAsync(cancellationToken);

        return new QuizQuestionAdminResponse(
            question.Id,
            question.Text,
            question.Explanation,
            question.Category,
            question.IsPublished,
            question.CreatedAt,
            question.UpdatedAt,
            author,
            question.Options
                .OrderBy(option => option.Position)
                .Select(option => new SaveQuizOption { Text = option.Text, IsCorrect = option.IsCorrect })
                .ToList());
    }

    // -------------------------------------------------------------- staff writes

    internal async Task<QuizSaveOutcome> CreateAsync(
        SaveQuizQuestionRequest request,
        Guid? createdByUserId,
        CancellationToken cancellationToken)
    {
        var errors = ValidateDefinition(request);
        if (errors.Count > 0)
        {
            return new QuizSaveOutcome(QuizSaveStatus.Invalid, FieldErrors: errors);
        }

        var question = new QuizQuestion
        {
            Id = Guid.NewGuid(),
            Text = request.Text.Trim(),
            Explanation = request.Explanation.Trim(),
            Category = request.Category,
            IsPublished = request.IsPublished,
            CreatedByUserId = createdByUserId,
        };

        AddOptions(question, request.Options!);

        dbContext.QuizQuestions.Add(question);
        await dbContext.SaveChangesAsync(cancellationToken);

        return new QuizSaveOutcome(QuizSaveStatus.Created, await FindForAdminAsync(question.Id, cancellationToken));
    }

    internal async Task<QuizSaveOutcome> UpdateAsync(
        Guid id,
        SaveQuizQuestionRequest request,
        CancellationToken cancellationToken)
    {
        var question = await dbContext.QuizQuestions
            .Include(item => item.Options)
            .FirstOrDefaultAsync(item => item.Id == id, cancellationToken);

        if (question is null)
        {
            return new QuizSaveOutcome(QuizSaveStatus.NotFound);
        }

        // A question that has already been played is frozen. Attempt rows point at the exact
        // option the player chose, and a result page re-reads the question to show the
        // explanation, so rewriting the wording or reshuffling the options would silently
        // rewrite history and would trip the restrict foreign key on the chosen option.
        // Visibility is still the staff's call, in both directions: retracting is how a bad
        // question is pulled out of future days, and restoring it is how a false alarm is undone.
        if (await HasBeenPlayedAsync(id, cancellationToken))
        {
            var contentIsUnchanged = question.Text.Trim() == request.Text.Trim()
                && question.Explanation.Trim() == request.Explanation.Trim()
                && question.Category == request.Category
                && OptionsMatch(question.Options, request.Options);

            if (!contentIsUnchanged)
            {
                return new QuizSaveOutcome(
                    QuizSaveStatus.InUse,
                    Message: "This question has already been played, so only publishing or retracting it is allowed.");
            }

            question.IsPublished = request.IsPublished;
            await dbContext.SaveChangesAsync(cancellationToken);

            return new QuizSaveOutcome(QuizSaveStatus.Updated, await FindForAdminAsync(id, cancellationToken));
        }

        var errors = ValidateDefinition(request);
        if (errors.Count > 0)
        {
            return new QuizSaveOutcome(QuizSaveStatus.Invalid, FieldErrors: errors);
        }

        question.Text = request.Text.Trim();
        question.Explanation = request.Explanation.Trim();
        question.Category = request.Category;
        question.IsPublished = request.IsPublished;

        // Options are replaced wholesale rather than diffed. A question is small, a staff
        // save is a rare event, and rewriting the set is the only way to guarantee the
        // "exactly one correct option" invariant after a reorder.
        dbContext.QuizOptions.RemoveRange(question.Options);
        question.Options.Clear();
        AddOptions(question, request.Options!);

        await dbContext.SaveChangesAsync(cancellationToken);

        return new QuizSaveOutcome(QuizSaveStatus.Updated, await FindForAdminAsync(id, cancellationToken));
    }

    internal async Task<QuizDeleteStatus> DeleteAsync(Guid id, CancellationToken cancellationToken)
    {
        var question = await dbContext.QuizQuestions
            .FirstOrDefaultAsync(item => item.Id == id, cancellationToken);

        if (question is null)
        {
            return QuizDeleteStatus.NotFound;
        }

        if (question.IsPublished)
        {
            return QuizDeleteStatus.Published;
        }

        // A played question is part of somebody's attempt history and review, so it is
        // retracted rather than removed even while it sits as a draft.
        if (await HasBeenPlayedAsync(id, cancellationToken))
        {
            return QuizDeleteStatus.InUse;
        }

        dbContext.QuizQuestions.Remove(question);
        await dbContext.SaveChangesAsync(cancellationToken);

        return QuizDeleteStatus.Deleted;
    }

    /// <summary>
    /// A question counts as played once it has been drawn into a day's quiz, whether or not
    /// anyone has submitted that day yet. Freezing at selection time rather than at first
    /// answer is what keeps a day's result set self-consistent for the whole day.
    /// </summary>
    private async Task<bool> HasBeenPlayedAsync(Guid questionId, CancellationToken cancellationToken) =>
        await dbContext.DailyQuizQuestions.AnyAsync(link => link.QuestionId == questionId, cancellationToken)
        || await dbContext.QuizAttemptAnswers.AnyAsync(answer => answer.QuestionId == questionId, cancellationToken);

    /// <summary>
    /// Position-independent comparison of an incoming option set against the stored one, so
    /// a retraction can be told apart from a reword. Order is part of the stored value and
    /// is compared too, because that is what the player saw.
    /// </summary>
    private static bool OptionsMatch(
        IReadOnlyCollection<QuizOption> stored,
        IReadOnlyList<SaveQuizOption>? incoming)
    {
        if (incoming is null || incoming.Count != stored.Count)
        {
            return false;
        }

        var ordered = stored.OrderBy(option => option.Position).ToList();

        for (var index = 0; index < ordered.Count; index++)
        {
            if (ordered[index].Text.Trim() != incoming[index].Text.Trim()
                || ordered[index].IsCorrect != incoming[index].IsCorrect)
            {
                return false;
            }
        }

        return true;
    }

    // ----------------------------------------------------------- materialisation

    /// <summary>A published question as the rotation sees it: an id and a category only.</summary>
    private sealed record QuestionCandidate(Guid Id, QuizCategory Category);

    private Task<DailyQuiz?> FindForDateAsync(DateOnly date, CancellationToken cancellationToken) =>
        dbContext.DailyQuizzes
            .AsNoTracking()
            .Include(quiz => quiz.Questions)
            .ThenInclude(link => link.Question)
            .ThenInclude(question => question!.Options)
            .FirstOrDefaultAsync(quiz => quiz.QuizDate == date, cancellationToken);

    private async Task<DailyQuiz?> FindOrMaterialiseAsync(DateOnly date, CancellationToken cancellationToken)
    {
        var existing = await FindForDateAsync(date, cancellationToken);
        if (existing is not null)
        {
            return existing;
        }

        var candidates = await dbContext.QuizQuestions
            .AsNoTracking()
            .Where(question => question.IsPublished)
            .OrderBy(question => question.Id)
            .Select(question => new QuestionCandidate(question.Id, question.Category))
            .ToListAsync(cancellationToken);

        if (candidates.Count == 0)
        {
            return null;
        }

        var chosen = SelectQuestions(candidates, date);
        if (chosen.Count == 0)
        {
            return null;
        }

        var quiz = new DailyQuiz
        {
            Id = Guid.NewGuid(),
            QuizDate = date,
            QuestionCount = chosen.Count,
        };

        for (var index = 0; index < chosen.Count; index++)
        {
            quiz.Questions.Add(new DailyQuizQuestion
            {
                DailyQuizId = quiz.Id,
                QuestionId = chosen[index],
                Position = index + 1,
            });
        }

        dbContext.DailyQuizzes.Add(quiz);

        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException exception) when (IsDuplicateKey(exception))
        {
            // Two visitors opened the first quiz of the day at the same instant. The
            // winner's row is the one that counts, so drop ours and read theirs.
            logger.LogInformation(exception, "A concurrent materialisation of {QuizDate} was resolved in favour of the other request.", date);
            DetachQuiz(quiz);
            return await FindForDateAsync(date, cancellationToken);
        }

        return await FindForDateAsync(date, cancellationToken);
    }

    /// <summary>
    /// MySQL error 1062 is ER_DUP_ENTRY. Both quiz write paths treat losing a race on a
    /// unique index as an ordinary outcome, so they must only swallow that one error:
    /// catching every <see cref="DbUpdateException"/> would report a dead connection or a
    /// truncated column as a friendly "someone else got there first".
    /// </summary>
    private static bool IsDuplicateKey(DbUpdateException exception) =>
        exception.InnerException is MySqlException { Number: 1062 };

    /// <summary>
    /// Picks the day's questions deterministically: the same date always yields the same
    /// set, and consecutive dates start at a different offset so the pool is not reshuffled
    /// into the same ten. Categories are interleaved so a single day is not entirely Nepal
    /// trivia, which is the failure mode of a plain "take the first ten" query.
    ///
    /// This reads the published id list into memory, which is comfortable for a pool in the
    /// hundreds. A pool in the tens of thousands would want the rotation expressed in SQL.
    /// </summary>
    private List<Guid> SelectQuestions(IReadOnlyList<QuestionCandidate> candidates, DateOnly date)
    {
        var buckets = new Dictionary<QuizCategory, List<Guid>>();
        foreach (var candidate in candidates)
        {
            if (!buckets.TryGetValue(candidate.Category, out var bucket))
            {
                bucket = [];
                buckets[candidate.Category] = bucket;
            }

            bucket.Add(candidate.Id);
        }

        var categories = buckets.Keys.OrderBy(category => (int)category).ToList();
        var cursors = categories.ToDictionary(
            category => category,
            category => (date.DayNumber * QuestionsPerQuiz) % buckets[category].Count);

        var selected = new List<Guid>();
        var alreadyPicked = new HashSet<Guid>();

        while (selected.Count < QuestionsPerQuiz)
        {
            var added = false;

            foreach (var category in categories)
            {
                if (selected.Count >= QuestionsPerQuiz)
                {
                    break;
                }

                var bucket = buckets[category];

                // The cursor is taken modulo the bucket size so a thin category is re-entered
                // from the top instead of running dry. Without the wrap, a five-question
                // category that started near its end contributed one question for the rest of
                // the day and the quiz came out visibly lopsided.
                var candidate = bucket[cursors[category] % bucket.Count];
                cursors[category] = (cursors[category] + 1) % bucket.Count;

                if (!alreadyPicked.Add(candidate))
                {
                    continue;
                }

                selected.Add(candidate);
                added = true;
            }

            // One full pass over every category produced nothing new, so the whole published
            // pool is already in the quiz. A short quiz is correct here; failing instead
            // would leave the day with no quiz at all.
            if (!added)
            {
                break;
            }
        }

        return selected;
    }

    // ------------------------------------------------------------------ grading

    private sealed record GradedQuiz(
        int Score,
        int CorrectCount,
        IReadOnlyList<QuizAnswerReview> Reviews,
        IReadOnlyList<FieldError> FieldErrors);

    private GradedQuiz Grade(DailyQuiz quiz, IReadOnlyList<SubmittedAnswer>? answers)
    {
        var questions = quiz.Questions
            .OrderBy(link => link.Position)
            .Select(link => link.Question)
            .OfType<QuizQuestion>()
            .ToList();

        var errors = new List<FieldError>();

        if (answers is null || answers.Count == 0)
        {
            errors.Add(new FieldError(
                "answers",
                "required",
                $"Answer all {questions.Count} questions before submitting."));

            return new GradedQuiz(0, 0, [], errors);
        }

        if (answers.Count != questions.Count)
        {
            errors.Add(new FieldError(
                "answers",
                "invalid",
                $"This quiz has {questions.Count} questions but {answers.Count} answers were sent."));

            return new GradedQuiz(0, 0, [], errors);
        }

        var byId = questions.ToDictionary(question => question.Id);
        var reviews = new List<QuizAnswerReview>(questions.Count);
        var seen = new HashSet<Guid>();

        foreach (var answer in answers)
        {
            if (!byId.TryGetValue(answer.QuestionId, out var question))
            {
                errors.Add(new FieldError(
                    answer.QuestionId.ToString(),
                    "unexpected",
                    "A question that is not part of this quiz was answered."));

                continue;
            }

            if (!seen.Add(answer.QuestionId))
            {
                errors.Add(new FieldError(
                    answer.QuestionId.ToString(),
                    "duplicate",
                    "The same question was answered more than once."));

                continue;
            }

            // The option is looked up inside its own question, so an id belonging to a
            // different question simply is not there. That is the whole anti-swap defence.
            var option = question.Options.FirstOrDefault(candidate => candidate.Id == answer.OptionId);
            if (option is null)
            {
                errors.Add(new FieldError(
                    answer.QuestionId.ToString(),
                    "out_of_range",
                    "The chosen answer is not one of that question's options."));

                continue;
            }

            var correct = question.Options.First(candidate => candidate.IsCorrect);
            reviews.Add(new QuizAnswerReview(
                question.Id,
                question.Text,
                question.Category,
                option.Id,
                option.Text,
                correct.Id,
                correct.Text,
                option.IsCorrect,
                question.Explanation));
        }

        // No partial grading is ever returned, so a rejected submission cannot be used to
        // learn which questions were right.
        if (errors.Count > 0)
        {
            return new GradedQuiz(0, 0, [], errors);
        }

        var correctCount = reviews.Count(review => review.IsCorrect);

        return new GradedQuiz(correctCount, correctCount, reviews, []);
    }

    // ---------------------------------------------------------- staff validation

    /// <summary>
    /// Shape checks always apply, because a draft with no options is not worth storing. The
    /// content checks that make a question playable apply only when publishing, which is the
    /// difference between "not finished yet" and "wrong". Requiring a correct answer on a
    /// draft would leave staff no way to save a half-written question.
    /// </summary>
    private IReadOnlyList<FieldError> ValidateDefinition(SaveQuizQuestionRequest request)
    {
        var errors = new List<FieldError>();
        var options = request.Options;

        if (options is null || options.Count < MinOptions)
        {
            errors.Add(new FieldError("options", "required", $"A question needs at least {MinOptions} options."));

            return errors;
        }

        if (options.Count > MaxOptions)
        {
            errors.Add(new FieldError("options", "too_many", $"A question can have at most {MaxOptions} options."));

            return errors;
        }

        if (options.Any(option => string.IsNullOrWhiteSpace(option.Text)))
        {
            errors.Add(new FieldError("options", "invalid", "Every option needs some text."));

            return errors;
        }

        var distinct = options
            .Select(option => option.Text.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Count();

        if (distinct != options.Count)
        {
            errors.Add(new FieldError("options", "invalid", "Two options have the same text."));

            return errors;
        }

        if (!request.IsPublished)
        {
            return errors;
        }

        // Everything below this point is about whether the question is playable, so it is
        // only checked on the way to being published.
        var correct = options.Count(option => option.IsCorrect);
        if (correct != 1)
        {
            // Refusing to publish an ambiguous question is the whole point of this check.
            errors.Add(new FieldError(
                "options",
                "invalid",
                correct == 0
                    ? "Mark exactly one option as the correct answer before publishing."
                    : $"Only one option can be correct, but {correct} are marked."));

            return errors;
        }

        if (request.Explanation.Trim().Length < 10)
        {
            errors.Add(new FieldError(
                "explanation",
                "required",
                "A published question needs an explanation, so a wrong answer can teach something."));
        }

        return errors;
    }

    private static void AddOptions(QuizQuestion question, IReadOnlyList<SaveQuizOption> options)
    {
        var position = 1;
        foreach (var option in options)
        {
            question.Options.Add(new QuizOption
            {
                Id = Guid.NewGuid(),
                QuestionId = question.Id,
                Position = position++,
                Text = option.Text.Trim(),
                IsCorrect = option.IsCorrect,
            });
        }
    }

    // ------------------------------------------------------------------- helpers

    private Task<QuizAttempt?> FindAttemptAsync(Guid userId, Guid dailyQuizId, CancellationToken cancellationToken) =>
        dbContext.QuizAttempts
            .AsNoTracking()
            .FirstOrDefaultAsync(
                attempt => attempt.UserId == userId && attempt.DailyQuizId == dailyQuizId,
                cancellationToken);

    private async Task<QuizResultResponse> ToResultAsync(QuizAttempt attempt, CancellationToken cancellationToken)
    {
        var answers = await dbContext.QuizAttemptAnswers
            .AsNoTracking()
            .Include(answer => answer.Question)
            .ThenInclude(question => question!.Options)
            .Include(answer => answer.SelectedOption)
            .Where(answer => answer.AttemptId == attempt.Id)
            .ToListAsync(cancellationToken);

        var byQuestion = answers.ToDictionary(answer => answer.QuestionId);
        var links = await dbContext.DailyQuizQuestions
            .AsNoTracking()
            .Where(link => link.DailyQuizId == attempt.DailyQuizId)
            .OrderBy(link => link.Position)
            .ToListAsync(cancellationToken);

        // The attempt row carries the score but not the date, and the navigation is not
        // loaded, so the date is read explicitly rather than through attempt.DailyQuiz.
        var quizDate = await dbContext.DailyQuizzes
            .AsNoTracking()
            .Where(quiz => quiz.Id == attempt.DailyQuizId)
            .Select(quiz => (DateOnly?)quiz.QuizDate)
            .FirstOrDefaultAsync(cancellationToken);

        var reviews = new List<QuizAnswerReview>(answers.Count);
        foreach (var link in links)
        {
            if (!byQuestion.TryGetValue(link.QuestionId, out var answer)
                || answer.Question is null
                || answer.SelectedOption is null)
            {
                continue;
            }

            var correct = answer.Question.Options.First(candidate => candidate.IsCorrect);
            reviews.Add(new QuizAnswerReview(
                answer.QuestionId,
                answer.Question.Text,
                answer.Question.Category,
                answer.SelectedOption.Id,
                answer.SelectedOption.Text,
                correct.Id,
                correct.Text,
                answer.IsCorrect,
                answer.Question.Explanation));
        }

        var user = await LoadUserAsync(attempt.UserId, cancellationToken, tracking: false);

        return new QuizResultResponse(
            attempt.Id,
            quizDate ?? dates.Today,
            attempt.Score,
            attempt.MaxScore,
            attempt.Score,
            attempt.CompletedAt,
            new QuizProgressionResponse(true, attempt.XpAwarded, user.Xp, user.Level, user.StreakCount, user.LastActiveOn),
            reviews);
    }

    private DailyQuizResponse ToDailyQuizResponse(DailyQuiz quiz, QuizAttempt? attempt) => new(
        quiz.Id,
        quiz.QuizDate,
        quiz.QuestionCount,
        MaxXpFor(quiz.QuestionCount),
        attempt is not null,
        attempt?.CompletedAt,
        quiz.Questions
            .OrderBy(link => link.Position)
            .Where(link => link.Question is not null)
            .Select(link => new QuizQuestionResponse(
                link.Question!.Id,
                link.Position,
                link.Question.Text,
                link.Question.Category,
                link.Question.Options
                    .OrderBy(option => option.Position)
                    .Select(option => new QuizOptionResponse(option.Id, option.Position, option.Text))
                    .ToList()))
            .ToList());

    private Task<ApplicationUser> LoadUserAsync(Guid userId, CancellationToken cancellationToken, bool tracking)
    {
        var query = tracking ? dbContext.Users : dbContext.Users.AsNoTracking();

        return query.FirstAsync(user => user.Id == userId, cancellationToken);
    }

    private void DetachAttempt(QuizAttempt attempt)
    {
        foreach (var answer in attempt.Answers)
        {
            dbContext.Entry(answer).State = EntityState.Detached;
        }

        dbContext.Entry(attempt).State = EntityState.Detached;
    }

    /// <summary>
    /// Detaching only the root is not enough. EF tracks the added join rows separately, so
    /// without clearing them the next SaveChanges in this request would try to insert the
    /// same question links a second time and fail on a key we already know about.
    /// </summary>
    private void DetachQuiz(DailyQuiz quiz)
    {
        foreach (var link in quiz.Questions)
        {
            dbContext.Entry(link).State = EntityState.Detached;
        }

        dbContext.Entry(quiz).State = EntityState.Detached;
    }

    private const string LikeEscape = "\\";

    private static string EscapeLike(string term) => term
        .Replace("\\", "\\\\", StringComparison.Ordinal)
        .Replace("%", "\\%", StringComparison.Ordinal)
        .Replace("_", "\\_", StringComparison.Ordinal);
}
