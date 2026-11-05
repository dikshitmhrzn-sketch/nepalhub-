namespace NepalHub.Api.Data.Entities;

/// <summary>Where a content claim sits in the poster's decision.</summary>
public enum ContentClaimStatus
{
    /// <summary>Filed; the post is hidden and the poster has not decided yet.</summary>
    Pending,

    /// <summary>The poster acknowledged the claimant; the post stays hidden.</summary>
    Accepted,

    /// <summary>The poster turned the claim down; the post is public again.</summary>
    Released,
}

/// <summary>
/// A signed-in person claiming a published job, offer or event the moment they see it.
///
/// <para>
/// Unlike <see cref="BusinessClaim"/>, no staff decide: filing hides the row from the public
/// in the same SaveChanges and hands the claimant's details to whoever created the post
/// (<c>CreatedByUserId</c>). The poster then accepts — the row stays hidden — or releases it
/// back into circulation. One active claim per row is checked in the service, because MySQL
/// has no partial unique index and a released claimant must be able to try again.
/// </para>
///
/// <para>
/// Exactly one of <see cref="JobId"/>, <see cref="EventId"/> and <see cref="DealId"/> is set,
/// matching <see cref="Kind"/>; the service refuses anything else. Each foreign key cascades,
/// so deleting a draft removes its claim instead of leaving a row pointing at nothing.
/// </para>
/// </summary>
public sealed class ContentClaim
{
    public Guid Id { get; set; }

    public SubmissionKind Kind { get; set; }

    public Guid? JobId { get; set; }

    public Job? Job { get; set; }

    public Guid? EventId { get; set; }

    public Event? Event { get; set; }

    public Guid? DealId { get; set; }

    public Deal? Deal { get; set; }

    /// <summary>The person claiming, recorded even if they lose the account later.</summary>
    public Guid ClaimantUserId { get; set; }

    public ApplicationUser? Claimant { get; set; }

    /// <summary>The claimant's own words about why they want it, capped at 500.</summary>
    public string? Note { get; set; }

    public ContentClaimStatus Status { get; set; }

    public DateTime CreatedAt { get; set; }

    /// <summary>The instant the poster accepted or released, in UTC like every other timestamp.</summary>
    public DateTime? ReviewedAt { get; set; }

    /// <summary>What the poster recorded when deciding, for whoever reads the claim later.</summary>
    public string? ReviewNote { get; set; }
}
