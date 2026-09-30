namespace NepalHub.Api.Data.Entities;

public sealed class QuizOption
{
    public Guid Id { get; set; }

    public Guid QuestionId { get; set; }

    public QuizQuestion? Question { get; set; }

    /// <summary>Display order within the question, 1-based and contiguous.</summary>
    public int Position { get; set; }

    public string Text { get; set; } = string.Empty;

    /// <summary>
    /// The single right answer. Exactly one option per question may have this set, which
    /// the service enforces on write because MySQL has no partial unique index to do it
    /// for us. Never exposed on a public response record.
    /// </summary>
    public bool IsCorrect { get; set; }
}
