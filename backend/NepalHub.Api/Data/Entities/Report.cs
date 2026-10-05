namespace NepalHub.Api.Data.Entities;

/// <summary>
/// What a reader is reporting. The five public listing types — the content a signed-in
/// member can actually see and therefore complain about. The id is deliberately not a
/// foreign key: a polymorphic target cannot hold five foreign keys with one column, so the
/// target is validated against the right table through the same visibility rules the public
/// site uses, and a target that disappears keeps its report row as the record of what was
/// reported rather than cascading it away.
/// </summary>
public enum ReportTargetType
{
    Job,
    Business,
    Deal,
    Event,
    StudentResource,
}

/// <summary>
/// Why something was reported. A closed set, because a free-text reason cannot be counted,
/// filtered or trended in the queue it feeds.
/// </summary>
public enum ReportReason
{
    Spam,
    Misleading,
    Inappropriate,
    Broken,
    Other,
}

/// <summary>Where the report is in the queue.</summary>
public enum ReportStatus
{
    Open,
    Resolved,
    Dismissed,
}

/// <summary>
/// A signed-in member reporting one listing they can see.
///
/// <para>
/// The unique (reporter, target) index is what makes filing idempotent: a double-click, a
/// retried request or a second tab all end in one row and a 409 for the person to read,
/// rather than a queue that quietly fills with the same complaint. Someone who reports a
/// target again after staff have resolved the first report is refused too — the record of
/// what they thought is already in the queue, and a repeat of the same report adds nothing
/// a moderator can act on.
/// </para>
///
/// <para>
/// Nothing here stores content about the listing itself. The report is a pointer plus a
/// reason, so the row stays small and the moderation queue never holds a copy of the thing
/// it is complaining about.
/// </para>
/// </summary>
public sealed class Report
{
    public Guid Id { get; set; }

    public Guid ReporterUserId { get; set; }

    public ApplicationUser? Reporter { get; set; }

    public ReportTargetType TargetType { get; set; }

    public Guid TargetId { get; set; }

    public ReportReason Reason { get; set; }

    /// <summary>Optional detail in the reporter's own words, capped at 500 characters.</summary>
    public string? Note { get; set; }

    public ReportStatus Status { get; set; }

    public DateTime CreatedAt { get; set; }

    public Guid? ResolvedByUserId { get; set; }

    public ApplicationUser? ResolvedBy { get; set; }

    /// <summary>The instant the report left the queue, in UTC like every other written timestamp.</summary>
    public DateTime? ResolvedAt { get; set; }

    /// <summary>What staff recorded when closing it, for the moderator who reads it later.</summary>
    public string? ResolutionNote { get; set; }
}
