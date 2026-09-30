namespace NepalHub.Api.Data.Entities;

public sealed class QuizQuestion
{
    public Guid Id { get; set; }

    public string Text { get; set; } = string.Empty;

    /// <summary>
    /// Shown only after the whole quiz has been submitted. It is never part of a
    /// pre-answer response, so a visitor cannot read the answer out of the payload.
    /// </summary>
    public string Explanation { get; set; } = string.Empty;

    public QuizCategory Category { get; set; }

    public bool IsPublished { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime UpdatedAt { get; set; }

    public Guid? CreatedByUserId { get; set; }

    public ApplicationUser? CreatedByUser { get; set; }

    public List<QuizOption> Options { get; set; } = [];
}
