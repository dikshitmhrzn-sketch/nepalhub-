namespace NepalHub.Api.Data.Entities;

/// <summary>
/// A finished run of one day's quiz. There is exactly one row per user per day, enforced
/// by a unique constraint, which is what stops XP being farmed by reloading the page.
/// There is deliberately no in-progress state: the whole quiz is submitted in one request,
/// so an attempt row only ever means "this person finished that day".
/// </summary>
public sealed class QuizAttempt
{
    public Guid Id { get; set; }

    public Guid UserId { get; set; }

    public ApplicationUser? User { get; set; }

    public Guid DailyQuizId { get; set; }

    public DailyQuiz? DailyQuiz { get; set; }

    public int Score { get; set; }

    public int MaxScore { get; set; }

    public int XpAwarded { get; set; }

    public DateTime CompletedAt { get; set; }

    public List<QuizAttemptAnswer> Answers { get; set; } = [];
}
