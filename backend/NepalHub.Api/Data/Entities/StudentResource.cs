namespace NepalHub.Api.Data.Entities;

/// <summary>
/// One entry in the student resource catalogue: a note, a past question paper, or a
/// scholarship. All three live in one row because they share every column that matters for
/// browsing — slug, title, summary, body, subject, level, visibility and provenance — and
/// differ only in a handful of optional attributes that are cleared when they do not apply
/// to the row's <see cref="Type"/>.
///
/// This phase hosts no files, so a paper is a link to wherever its publisher put the PDF
/// rather than an upload. That is a deliberate boundary: file storage, scanning and access
/// control are a much larger problem than writing study notes.
/// </summary>
public sealed class StudentResource
{
    public Guid Id { get; set; }

    /// <summary>
    /// The public URL segment. Lowercase words joined by single hyphens, unique, and never
    /// changed after the row is created, because a changing URL breaks every link a student
    /// or a search engine has already recorded.
    /// </summary>
    public string Slug { get; set; } = string.Empty;

    public string Title { get; set; } = string.Empty;

    /// <summary>The one-line description shown on a catalogue card.</summary>
    public string Summary { get; set; } = string.Empty;

    /// <summary>
    /// The main content, stored and rendered as plain text. It is never treated as markup:
    /// the frontend renders it as text so an author's angle brackets cannot become elements.
    /// </summary>
    public string Body { get; set; } = string.Empty;

    public ResourceType Type { get; set; }

    public ResourceCategory Category { get; set; }

    public EducationLevel Level { get; set; }

    /// <summary>
    /// Where the underlying material lives, for a paper or a scholarship application. Always
    /// an absolute https URL with a real host; the server never fetches it.
    /// </summary>
    public string? ExternalUrl { get; set; }

    /// <summary>
    /// The year a question paper belongs to, held as a Bikram Sambat year (2081, not 2024)
    /// because that is how the paper is labelled and how a student looks for it.
    /// </summary>
    public int? AcademicYear { get; set; }

    /// <summary>Who offers a scholarship.</summary>
    public string? Provider { get; set; }

    /// <summary>How much a scholarship awards, in rupees.</summary>
    public decimal? AwardAmountNpr { get; set; }

    /// <summary>Who is eligible for a scholarship, in prose.</summary>
    public string? Eligibility { get; set; }

    /// <summary>
    /// The last day a resource is any use to a student, if it has one. Read against the
    /// current Nepal date, so a deadline turns over at midnight in Kathmandu.
    /// </summary>
    public DateOnly? ExpiresOn { get; set; }

    public bool IsPublished { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime UpdatedAt { get; set; }

    public Guid? CreatedByUserId { get; set; }

    public ApplicationUser? CreatedByUser { get; set; }
}
