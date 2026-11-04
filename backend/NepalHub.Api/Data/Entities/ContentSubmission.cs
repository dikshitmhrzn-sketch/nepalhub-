namespace NepalHub.Api.Data.Entities;

/// <summary>Which kind of listing a submission is asking to publish.</summary>
public enum SubmissionKind
{
    Job,
    Event,
    Deal,
}

/// <summary>Where a submission sits in the queue staff work through.</summary>
public enum SubmissionStatus
{
    Pending,
    Approved,
    Rejected,
}

/// <summary>
/// A member's request that staff check a draft they wrote and publish it.
///
/// <para>
/// This is the business-verification pattern applied to content: submitting creates the real
/// row — a <see cref="Job"/>, <see cref="Event"/> or <see cref="Deal"/> with
/// <c>IsPublished = false</c> — and this record points at it. Approval flips the draft live,
/// rejection leaves it unpublished so the author can correct it and ask again, and the public
/// never sees either state because every public read filters on <c>IsPublished</c> first.
/// </para>
///
/// <para>
/// Exactly one of <see cref="JobId"/>, <see cref="EventId"/> and <see cref="DealId"/> is set,
/// matching <see cref="Kind"/>; the service refuses anything else rather than letting the
/// database guess. Each foreign key cascades, so deleting a draft removes its submission
/// instead of leaving a queue row pointing at nothing.
/// </para>
///
/// <para>
/// No unique index on the item: a rejected author who corrected their draft should be able to
/// ask again, and a database that refused the second row would give no reason at all. The
/// one-open-submission rule is checked in the service, where the refusal can be named.
/// </para>
/// </summary>
public sealed class ContentSubmission
{
    public Guid Id { get; set; }

    public SubmissionKind Kind { get; set; }

    public Guid? JobId { get; set; }

    public Job? Job { get; set; }

    public Guid? EventId { get; set; }

    public Event? Event { get; set; }

    public Guid? DealId { get; set; }

    public Deal? Deal { get; set; }

    /// <summary>The author who asked, recorded even if they lose the account later.</summary>
    public Guid SubmittedByUserId { get; set; }

    public ApplicationUser? SubmittedBy { get; set; }

    /// <summary>The author's own words about the draft, capped at 500.</summary>
    public string? Note { get; set; }

    public SubmissionStatus Status { get; set; }

    public DateTime CreatedAt { get; set; }

    public Guid? ReviewedByUserId { get; set; }

    public ApplicationUser? ReviewedBy { get; set; }

    /// <summary>The instant staff decided, in UTC like every other written timestamp.</summary>
    public DateTime? ReviewedAt { get; set; }

    /// <summary>What staff recorded when deciding, for whoever reads the submission later.</summary>
    public string? ReviewNote { get; set; }
}
