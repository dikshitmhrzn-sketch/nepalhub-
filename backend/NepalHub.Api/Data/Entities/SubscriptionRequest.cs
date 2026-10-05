namespace NepalHub.Api.Data.Entities;

/// <summary>Where the request sits in the admin queue.</summary>
public enum SubscriptionRequestStatus
{
    Pending,
    Approved,
    Rejected,
}

/// <summary>
/// An owner asking staff to grant or extend a plan for their listing.
///
/// <para>
/// In this phase the request is the whole checkout: there is no payment provider, so a
/// person asks and an admin decides, and approving it writes the subscription in the
/// same SaveChanges as the queue row. When a real provider arrives its webhook resolves
/// into this same queue, so approval logic is written once rather than twice.
/// </para>
/// </summary>
public sealed class SubscriptionRequest
{
    public Guid Id { get; set; }

    public Guid BusinessId { get; set; }

    public Business? Business { get; set; }

    public Guid PlanId { get; set; }

    public Plan? Plan { get; set; }

    public Guid RequesterUserId { get; set; }

    public ApplicationUser? Requester { get; set; }

    /// <summary>What the owner wants done, in their own words. Capped at 500.</summary>
    public string? Note { get; set; }

    public SubscriptionRequestStatus Status { get; set; }

    public DateTime CreatedAt { get; set; }

    public Guid? ReviewedByUserId { get; set; }

    public ApplicationUser? ReviewedBy { get; set; }

    public DateTime? ReviewedAt { get; set; }

    /// <summary>What staff recorded when deciding, for whoever reads the request later.</summary>
    public string? ReviewNote { get; set; }
}
