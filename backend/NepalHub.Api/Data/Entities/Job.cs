namespace NepalHub.Api.Data.Entities;

/// <summary>
/// One job posting. A single table, because every posting is searched, filtered, paged and
/// expired identically; a separate table per category or employment type would mean a copy of
/// every query and a client that has to guess which one to ask.
/// <para>
/// This phase is staff-published copy only. There is no employer account, no application
/// row and no CV storage: <see cref="ApplicationUrl"/> is a validated link to wherever the
/// employer wants applicants to go, which keeps account creation, file scanning and
/// application state out of a board whose job is to be readable.
/// </para>
/// </summary>
public sealed class Job
{
    public Guid Id { get; set; }

    /// <summary>
    /// The public URL segment. Lowercase words joined by single hyphens and unique. It may be
    /// corrected while the row is a draft, and is frozen once published, because after that
    /// the slug is a live address that applicants and search engines have recorded.
    /// </summary>
    public string Slug { get; set; } = string.Empty;

    public string Title { get; set; } = string.Empty;

    /// <summary>The employer, named the way a reader would say it out loud.</summary>
    public string Organization { get; set; } = string.Empty;

    /// <summary>
    /// The full posting, stored and rendered as plain text. It is never treated as markup: the
    /// frontend renders it as text so an author's angle brackets cannot become elements.
    /// </summary>
    public string Description { get; set; } = string.Empty;

    public JobCategory Category { get; set; }

    public EmploymentType EmploymentType { get; set; }

    public WorkMode WorkMode { get; set; }

    /// <summary>
    /// Where the job is, as a district. The province is read through the district rather than
    /// stored beside it, because two columns naming the same place can disagree and a filter
    /// that contradicts its own row is worse than one fewer column.
    /// <para>
    /// Nullable because a remote posting has no location. An on-site or hybrid row without a
    /// district is allowed too: staff know cases the seven provinces do not cover, and the
    /// service rejects it rather than inventing a district.
    /// </para>
    /// </summary>
    public Guid? DistrictId { get; set; }

    public District? District { get; set; }

    /// <summary>
    /// Where to apply. Always an absolute https URL with a real host; shape-checked on the way
    /// in and never fetched by the server, so a posting cannot be used to make it call out to
    /// anything.
    /// </summary>
    public string ApplicationUrl { get; set; } = string.Empty;

    /// <summary>
    /// The last day an applicant can act on this posting. Required rather than nullable so the
    /// deadline sort has no null-ordering case, and read against the current Nepal date so a
    /// posting closes at the same moment for every reader.
    /// </summary>
    public DateOnly ApplicationDeadline { get; set; }

    public bool IsPublished { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime UpdatedAt { get; set; }

    public Guid? CreatedByUserId { get; set; }

    public ApplicationUser? CreatedByUser { get; set; }
}
