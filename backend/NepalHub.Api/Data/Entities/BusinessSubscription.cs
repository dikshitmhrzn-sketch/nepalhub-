namespace NepalHub.Api.Data.Entities;

/// <summary>
/// What staff recorded about the subscription. Whether it is live is never read from
/// here: entitlement is derived from the dates against today, so a row that has run out
/// needs no job to flip it.
/// </summary>
public enum BusinessSubscriptionStatus
{
    Active,
    Cancelled,
}

/// <summary>
/// One granted run of a plan over a business, with an inclusive date window.
///
/// <para>
/// A business has at most one row that can be live at a time, enforced in the service:
/// buying again extends the current window rather than stacking a second one underneath
/// it, because two overlapping rows would make "is this listing featured" depend on
/// which of them the query happened to find.
/// </para>
///
/// <para>
/// Cancelled rows are kept, not deleted, so the history of what a listing paid for
/// survives the cancellation.
/// </para>
/// </summary>
public sealed class BusinessSubscription
{
    public Guid Id { get; set; }

    public Guid BusinessId { get; set; }

    public Business? Business { get; set; }

    public Guid PlanId { get; set; }

    public Plan? Plan { get; set; }

    /// <summary>The first day the subscription counts, in Nepal.</summary>
    public DateOnly StartsOn { get; set; }

    /// <summary>The last day it counts, inclusive. Extending moves this forward.</summary>
    public DateOnly EndsOn { get; set; }

    public BusinessSubscriptionStatus Status { get; set; }

    public DateTime CreatedAt { get; set; }

    /// <summary>Who granted it — the admin who approved it, or the requester themselves for free.</summary>
    public Guid? GrantedByUserId { get; set; }

    public ApplicationUser? GrantedByUser { get; set; }

    /// <summary>The request this was approved from, when there was one. Null for a direct grant.</summary>
    public Guid? SubscriptionRequestId { get; set; }

    public SubscriptionRequest? SubscriptionRequest { get; set; }

    public DateTime? CancelledAt { get; set; }
}
