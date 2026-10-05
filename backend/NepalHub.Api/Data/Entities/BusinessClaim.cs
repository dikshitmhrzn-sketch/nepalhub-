namespace NepalHub.Api.Data.Entities;

/// <summary>Where the claim sits in the queue staff work through.</summary>
public enum BusinessClaimStatus
{
    Pending,
    Approved,
    Rejected,
}

/// <summary>
/// A signed-in person asserting that a staff-published listing is theirs.
///
/// <para>
/// The directory starts as staff-written copy, so ownership is asserted rather than
/// assigned: somebody who recognises their own business claims it, a moderator decides,
/// and approval writes <see cref="Business.OwnerUserId"/>. Nothing about the row is
/// taken on trust before that decision — a claim grants no access of its own.
/// </para>
///
/// <para>
/// No unique index on (business, claimant): a rejected claimant who corrects their
/// evidence should be able to try again, and a database that refuses the second row
/// would give no reason at all. The one-pending-claim rule is checked in the service,
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

    /// <summary>The instant staff decided, in UTC like every other written timestamp.</summary>
    public DateTime? ReviewedAt { get; set; }

    /// <summary>What staff recorded when deciding, for whoever reads the claim later.</summary>
    public string? ReviewNote { get; set; }
}
