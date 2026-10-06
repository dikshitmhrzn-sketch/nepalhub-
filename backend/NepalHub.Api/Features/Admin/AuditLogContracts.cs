using NepalHub.Api.Common;

namespace NepalHub.Api.Features.Admin;

/// <summary>One line of the audit log as the admin screen reads it.</summary>
public sealed record AuditLogResponse(
    Guid Id,
    string Action,
    string TargetType,
    string TargetId,
    string? AdminName,
    string? Before,
    string? After,
    string? Ip,
    DateTime CreatedAt);

/// <summary>
/// The action names the log records, written down once so the writer and any future
/// reader agree on the spelling. Moderation actions live here; user administration adds
/// its own when it lands.
/// </summary>
public static class AuditActions
{
    public const string ReportResolved = "report.resolved";

    public const string ReportDismissed = "report.dismissed";

    public const string UserActivated = "user.activated";

    public const string UserDeactivated = "user.deactivated";

    public const string UserRoleGranted = "user.role_granted";

    public const string UserRoleRevoked = "user.role_revoked";

    public const string BusinessClaimApproved = "business.claim_approved";

    public const string BusinessClaimRejected = "business.claim_rejected";

    public const string BusinessVerificationApproved = "business.verification_approved";

    public const string BusinessVerificationRejected = "business.verification_rejected";

    public const string SubscriptionGranted = "subscription.granted";

    public const string SubscriptionExtended = "subscription.extended";

    public const string SubscriptionCancelled = "subscription.cancelled";

    public const string SubscriptionRequestApproved = "subscription_request.approved";

    public const string SubscriptionRequestRejected = "subscription_request.rejected";

    /// <summary>
    /// A provider confirmed money. The actor is the buyer, not an administrator: no
    /// decision was made by staff, and the entry exists so the queue a payment settles
    /// can be tied back to who paid for it.
    /// </summary>
    public const string PaymentSucceeded = "payment.succeeded";

    /// <summary>A refund was confirmed by the provider, wherever the money was returned from.</summary>
    public const string PaymentRefunded = "payment.refunded";
}

/// <summary>
/// The audit trail's write side, plus the admin-only read.
///
/// <para>
/// <see cref="Record"/> appends to the caller's own <c>AppDbContext</c> and does not save.
/// That is the whole contract: the row and the change it describes are written by the same
/// SaveChanges, so a half-applied action cannot leave a log entry for something that did
/// not happen, and a rolled-back one leaves no trace at all.
/// </para>
/// </summary>
public interface IAuditLog
{
    /// <summary>
    /// Appends one entry. <paramref name="before"/> and <paramref name="after"/> are
    /// serialized to JSON as they stand, so the reader sees what the row was and what it
    /// became without this table needing a schema per subject.
    /// </summary>
    void Record(
        Guid adminUserId,
        string action,
        string targetType,
        string targetId,
        object? before = null,
        object? after = null,
        string? ip = null);

    /// <summary>One page of the trail, newest first, optionally narrowed to one action or subject.</summary>
    Task<PagedResult<AuditLogResponse>> ListAsync(
        PageRequest paging,
        string? action,
        string? targetType,
        CancellationToken cancellationToken);
}
