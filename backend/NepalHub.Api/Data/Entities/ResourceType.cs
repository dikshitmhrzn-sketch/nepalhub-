namespace NepalHub.Api.Data.Entities;

/// <summary>
/// Which of the three kinds of study resource a row holds. Stored as an int rather than a
/// MySQL ENUM, and serialised to the API as its name so the client never has to know the
/// ordinal values.
///
/// The three types share title, summary, body, slug, publish state and provenance, so they
/// live in one table. What separates them is four small attributes: a question paper adds
/// an academic year and always needs an external link, and a scholarship adds a provider, a
/// deadline and optionally an award amount. A table per type would have tripled the service
/// and DTO surface to express those nullable columns.
/// </summary>
public enum ResourceType
{
    /// <summary>A written summary or explanation of a topic. Carries no extra attributes.</summary>
    Note = 0,

    /// <summary>
    /// A past exam paper. Requires an academic year and an external link, because this phase
    /// hosts no files: the paper itself lives wherever its publisher put it.
    /// </summary>
    QuestionPaper = 1,

    /// <summary>
    /// A funding opportunity. Requires a provider and a deadline, and the deadline hides the
    /// row from the public catalogue once it has passed.
    /// </summary>
    Scholarship = 2,
}
