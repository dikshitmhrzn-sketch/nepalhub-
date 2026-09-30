namespace NepalHub.Api.Data.Entities;

/// <summary>
/// Pins a question to a day's quiz. This is the table that makes "you answered a question
/// that was not in your quiz" structurally impossible: an attempt answer references a
/// question that has to appear here first.
/// </summary>
public sealed class DailyQuizQuestion
{
    public Guid DailyQuizId { get; set; }

    public DailyQuiz? DailyQuiz { get; set; }

    public Guid QuestionId { get; set; }

    public QuizQuestion? Question { get; set; }

    /// <summary>1-based position within the day's quiz.</summary>
    public int Position { get; set; }
}
