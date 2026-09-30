using System.ComponentModel.DataAnnotations;
using NepalHub.Api.Data.Entities;

namespace NepalHub.Api.Features.Quiz;

/// <summary>
/// One answer choice as the public sees it. Note what is missing: there is no
/// <c>IsCorrect</c> and no explanation. Those two fields are the whole quiz, and they
/// only ever leave the database inside a review, which is built server-side after a
/// submission has been graded.
/// </summary>
public sealed record QuizOptionResponse(Guid Id, int Position, string Text);

/// <summary>A question as the public sees it, before anything has been answered.</summary>
public sealed record QuizQuestionResponse(
    Guid Id,
    int Position,
    string Text,
    QuizCategory Category,
    IReadOnlyList<QuizOptionResponse> Options);

/// <summary>Today's quiz. <c>AlreadyCompleted</c> is only meaningful to a signed-in caller.</summary>
public sealed record DailyQuizResponse(
    Guid Id,
    DateOnly QuizDate,
    int QuestionCount,
    int MaxXpAvailable,
    bool AlreadyCompleted,
    DateTime? CompletedAt,
    IReadOnlyList<QuizQuestionResponse> Questions);

public sealed class SubmittedAnswer
{
    public Guid QuestionId { get; init; }

    public Guid OptionId { get; init; }
}

/// <summary>
/// The whole quiz in one body. There is no per-answer endpoint on purpose: an attempt is
/// created atomically, so there is no half-finished state to resume and no way to revise
/// an answer after the fact.
/// </summary>
public sealed class SubmitQuizRequest
{
    public IReadOnlyList<SubmittedAnswer>? Answers { get; init; }
}

/// <summary>One graded question, with the right answer and the reason behind it.</summary>
public sealed record QuizAnswerReview(
    Guid QuestionId,
    string Text,
    QuizCategory Category,
    Guid SelectedOptionId,
    string SelectedOptionText,
    Guid CorrectOptionId,
    string CorrectOptionText,
    bool IsCorrect,
    string Explanation);

/// <summary>
/// The progression earned by an attempt. <c>Recorded</c> is false for an anonymous
/// visitor, who still sees their score and explanations but accrues nothing, so
/// <see cref="TotalXp"/> and friends are null rather than a misleading zero.
/// </summary>
public sealed record QuizProgressionResponse(
    bool Recorded,
    int XpAwarded,
    int? TotalXp,
    int? Level,
    int? StreakCount,
    DateOnly? LastActiveOn);

/// <summary>Shared by the submit and the read-back result, so one component renders both.</summary>
public sealed record QuizResultResponse(
    Guid AttemptId,
    DateOnly QuizDate,
    int Score,
    int MaxScore,
    int CorrectCount,
    DateTime CompletedAt,
    QuizProgressionResponse Progression,
    IReadOnlyList<QuizAnswerReview> Reviews);

public sealed record QuizStatsResponse(
    int TotalXp,
    int Level,
    int StreakCount,
    int XpToNextLevel,
    int QuizzesCompleted,
    DateOnly? LastActiveOn);

/// <summary>
/// One option in a staff save. <c>IsCorrect</c> is the only place the answer is ever
/// accepted, and the service requires exactly one of these per question.
/// </summary>
public sealed class SaveQuizOption
{
    [Required(ErrorMessage = "Every option needs some text.")]
    [StringLength(300, MinimumLength = 1, ErrorMessage = "An option must be 1 to 300 characters.")]
    public string Text { get; init; } = string.Empty;

    public bool IsCorrect { get; init; }
}

/// <summary>
/// The staff list row. It omits the options and the explanation so the list stays a
/// single SQL projection; the edit form fetches the full record by id, exactly as the
/// calculator list does.
/// </summary>
public sealed record QuizQuestionAdminSummaryResponse(
    Guid Id,
    string Text,
    QuizCategory Category,
    bool IsPublished,
    int OptionCount,
    int CorrectOptionCount,
    bool HasBeenPlayed,
    DateTime UpdatedAt,
    string? CreatedByEmail);

/// <summary>The full staff record, which is the only shape that ever carries the answer.</summary>
public sealed record QuizQuestionAdminResponse(
    Guid Id,
    string Text,
    string Explanation,
    QuizCategory Category,
    bool IsPublished,
    DateTime CreatedAt,
    DateTime UpdatedAt,
    string? CreatedByEmail,
    IReadOnlyList<SaveQuizOption> Options);

/// <summary>Create and update share one shape, so the staff form sends the same body.</summary>
public sealed class SaveQuizQuestionRequest
{
    [Required(ErrorMessage = "The question text is required.")]
    [StringLength(600, MinimumLength = 10, ErrorMessage = "The question must be 10 to 600 characters.")]
    public string Text { get; init; } = string.Empty;

    // Deliberately no [Required] and no minimum length here. An explanation is only
    // mandatory when the question is published, which is a rule the service can apply but
    // model binding cannot, because binding does not know the intended IsPublished value
    // while an empty draft is still being typed. The service enforces 10 characters on the
    // way to publishing; this only stops an absurdly long one reaching the column.
    [StringLength(1500, ErrorMessage = "The explanation must be 1500 characters or fewer.")]
    public string Explanation { get; init; } = string.Empty;

    [EnumDataType(typeof(QuizCategory), ErrorMessage = "Choose a valid category.")]
    public QuizCategory Category { get; init; }

    public bool IsPublished { get; init; }

    public IReadOnlyList<SaveQuizOption>? Options { get; init; }
}
