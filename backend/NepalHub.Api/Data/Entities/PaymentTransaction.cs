namespace NepalHub.Api.Data.Entities;

/// <summary>
/// What became of one checkout attempt. Every transition is written by the settlement
/// engine against a row that was Pending, so a replayed provider answer finds nothing to
/// move and the entitlement behind it can only be granted once.
/// </summary>
public enum PaymentStatus
{
    /// <summary>Checkout begun, the provider has not confirmed anything yet.</summary>
    Pending,

    /// <summary>Confirmed paid. The entitlement it bought has been granted.</summary>
    Succeeded,

    /// <summary>The provider says it never completed. Nothing was taken.</summary>
    Failed,

    /// <summary>
    /// Paid, but a refund is owed — the buyer cancelled, the plan could not be granted,
    /// or an admin cancelled the entitlement. Money moves in the provider's own portal;
    /// this row is what remembers it is due.
    /// </summary>
    RefundDue,

    /// <summary>The provider has confirmed the money went back.</summary>
    Refunded,
}

/// <summary>What one payment was for. Two things can be bought; each settles differently.</summary>
public enum PaymentKind
{
    Subscription,
    EventTicket,
}

/// <summary>
/// One checkout against the payment provider for one thing.
///
/// <para>
/// The row is created before the buyer is sent away and holds the amount that was quoted,
/// so settlement compares what the provider reports against what we actually asked for
/// rather than against whatever the price happens to be afterwards. No card or wallet
/// credential ever reaches this table: the provider collects those on its own pages and
/// this row only ever learns the reference both sides can recognise.
/// </para>
///
/// <para>
/// One row stays open at a time per thing being bought, enforced in the service — MySQL
/// has no partial unique index, the same argument that keeps one owner per listing.
/// </para>
/// </summary>
public sealed class PaymentTransaction
{
    public Guid Id { get; set; }

    /// <summary>
    /// Our transaction uuid, sent to the provider and read back on every callback and
    /// status check. Opaque, unique, and the handle every other feature settles through.
    /// </summary>
    public string Reference { get; set; } = string.Empty;

    public Guid UserId { get; set; }

    public ApplicationUser? User { get; set; }

    public PaymentKind Kind { get; set; }

    /// <summary>The total quoted to the buyer, in whole rupees, fixed at checkout.</summary>
    public int AmountNpr { get; set; }

    public string Description { get; set; } = string.Empty;

    public PaymentStatus Status { get; set; }

    /// <summary>Why a row stopped, when it stopped for a reason we can name.</summary>
    public string? FailureCode { get; set; }

    public string Provider { get; set; } = "esewa";

    /// <summary>The provider's own reference for a confirmed transaction, once there is one.</summary>
    public string? ProviderReference { get; set; }

    /// <summary>The plan request this settles, for a subscription checkout.</summary>
    public Guid? SubscriptionRequestId { get; set; }

    public SubscriptionRequest? SubscriptionRequest { get; set; }

    /// <summary>The place this holds, for a ticket checkout. Set while the payment is open.</summary>
    public Guid? EventBookingId { get; set; }

    public EventBooking? EventBooking { get; set; }

    /// <summary>The address the checkout was started from, for the questions refunds raise.</summary>
    public string? Ip { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime UpdatedAt { get; set; }

    /// <summary>When the row left Pending for the last time.</summary>
    public DateTime? SettledAt { get; set; }

    public DateTime? RefundedAt { get; set; }
}
