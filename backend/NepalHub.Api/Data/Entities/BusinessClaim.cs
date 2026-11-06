namespace NepalHub.Api.Data.Entities;

/// <summary>Where the claim sits in the poster's decision (same lifecycle as content claims).</summary>
public enum BusinessClaimStatus
{
    Pending,
    Accepted,
    Released,
}

/// <summary>
/// A signed-in person asserting that a staff-published listing is theirs.
///
/// <para>
/// The directory starts as staff-written copy, so ownership is asserted rather than
/// assigned: somebody who recognises their own business claims it, the poster (or legacy
/// staff approve) decides, and acceptance writes <see cref="Business.OwnerUserId"/> and
/// hides the listing. A pending claim leaves the listing public.
/// </para>
///
/// <para>
/// No unique index on (business, claimant): a released claimant who corrects their
/// evidence should be able to try again, and a database that refuses the second row
/// would give no reason at all. The one-active-claim rule is checked in the service,
/// where the refusal can be named to the person.
/// </para>
/// </summary>
public sealed class BusinessClaim
{
    public Guid Id { get; set; }

    public Guid BusinessId { get; set; }

    public Business? Business { get; set; }

    public Guid ClaimantUserId { get; set; }

    public ApplicationUser? Claimant { get; set; }

    /// <summary>The claimant's own words: how they are connected to the listing, capped at 500.</summary>
    public string? Evidence { get; set; }

    public BusinessClaimStatus Status { get; set; }

    public DateTime CreatedAt { get; set; }

    public Guid? ReviewedByUserId { get; set; }

    public ApplicationUser? ReviewedBy { get; set; }

    /// <summary>The instant the poster or staff decided, in UTC like every other written timestamp.</summary>
    public DateTime? ReviewedAt { get; set; }

    /// <summary>What the decider recorded, for whoever reads the claim later.</summary>
    public string? ReviewNote { get; set; }
}
