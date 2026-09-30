namespace NepalHub.Api.Data.Entities;

/// <summary>
/// One day's quiz, materialised on the first request for that date. The row is what makes
/// a daily quiz genuinely daily: once the ten questions are chosen they stop moving, so a
/// question unpublished at 14:00 cannot change the set for someone who already played,
/// and a finished attempt can always be replayed for review.
/// </summary>
public sealed class DailyQuiz
{
    public Guid Id { get; set; }

    /// <summary>The Nepal-time date this quiz belongs to. Unique.</summary>
    public DateOnly QuizDate { get; set; }

    /// <summary>Denormalised so the catalogue of dates does not have to count the join rows.</summary>
    public int QuestionCount { get; set; }

    public DateTime CreatedAt { get; set; }

    public List<DailyQuizQuestion> Questions { get; set; } = [];
}
