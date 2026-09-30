namespace NepalHub.Api.Data.Entities;

/// <summary>
/// What one person picked on one question, and whether it was right. <see cref="IsCorrect"/>
/// is computed server-side from the stored option and is never read from the request, so a
/// caller cannot talk the server into marking its own answer right.
/// </summary>
public sealed class QuizAttemptAnswer
{
    public Guid AttemptId { get; set; }

    public QuizAttempt? Attempt { get; set; }

    public Guid QuestionId { get; set; }

    public QuizQuestion? Question { get; set; }

    public Guid SelectedOptionId { get; set; }

    public QuizOption? SelectedOption { get; set; }

    public bool IsCorrect { get; set; }
}
