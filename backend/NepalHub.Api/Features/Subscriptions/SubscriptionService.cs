using Microsoft.EntityFrameworkCore;
using NepalHub.Api.Common;
using NepalHub.Api.Data;
using NepalHub.Api.Data.Entities;
using NepalHub.Api.Features.Admin;
using NepalHub.Api.Features.Quiz;

namespace NepalHub.Api.Features.Subscriptions;

public enum SubscribeStatus
{
    /// <summary>A window was opened for the listing.</summary>
    Granted,

    /// <summary>An existing window was rolled forward by the plan's period.</summary>
    Extended,

    /// <summary>The owner's ask for a paid plan is waiting in the admin queue.</summary>
    Requested,

    Cancelled,

    /// <summary>No listing at that address belongs to this caller.</summary>
    NotFound,

    /// <summary>The owner already has a request waiting for a decision.</summary>
    AlreadyPending,

    /// <summary>No active plan row of that tier exists, so there is nothing to grant.</summary>
    PlanUnavailable,

    /// <summary>The listing has no active subscription to cancel.</summary>
    NotActive,
}

public enum RequestDecisionStatus
{
    Approved,
    Rejected,

    NotFound,

    /// <summary>It was decided already; the queue does not re-decide.</summary>
    AlreadyReviewed,

    /// <summary>The plan was deactivated between the request and the decision.</summary>
    PlanUnavailable,
}

public sealed record SubscribeOutcome(SubscribeStatus Status, SubscriptionStateResponse? State = null);

public sealed record RequestDecisionOutcome(
    RequestDecisionStatus Status,
    AdminSubscriptionRequestResponse? Request = null);

/// <summary>How a paid settlement resolved against the request queue.</summary>
public enum PaidSettleStatus
{
    /// <summary>The window opened or rolled forward, exactly as an approval would have done.</summary>
    Granted,

    NotFound,

    /// <summary>Staff decided it while the money was still in flight, so there is nothing to grant.</summary>
    AlreadyReviewed,

    /// <summary>The plan was deactivated between the checkout and the confirmation.</summary>
    PlanUnavailable,
}

/// <summary>
/// What a settlement granted, so the caller can record one audit line naming both the
/// payment and the window it produced.
/// </summary>
internal sealed record PaidSettleOutcome(
    PaidSettleStatus Status,
    Guid? SubscriptionId = null,
    bool Extended = false);

/// <summary>What one grant did, for the caller to record in the audit log.</summary>
internal sealed record GrantResult(
    Guid SubscriptionId,
    bool Extended,
    object? Before,
    object After);

/// <summary>
/// Plans, entitlement and the queues that decide them.
///
/// <para>
/// Entitlement is derived at read: a subscription counts while today's Nepal date falls in
/// its window and it has not been cancelled, so nothing expires in the background and no job
/// can fall behind. Every path that opens or moves a window — the owner's free grant, a
/// direct admin grant, an approved request — runs the same arithmetic through
/// <see cref="SubscriptionRules"/>, because three copies of "how long does this last" would
/// eventually answer differently.
/// </para>
///
/// <para>
/// One live window per listing, enforced here rather than by the schema: a business gains at
/// most one row with status Active, and buying again rolls that row's end date forward
/// instead of stacking a second window underneath it. Two overlapping rows would make "is
/// this listing featured" depend on which one a query happened to find.
/// </para>
/// </summary>
public sealed class SubscriptionService(
    AppDbContext dbContext,
    IAuditLog auditLog,
    INepalDateProvider dates)
{
    // ------------------------------------------------------------------ public reads

    internal async Task<IReadOnlyList<PlanResponse>> ListPlansAsync(CancellationToken cancellationToken) =>
        await dbContext.Plans
            .AsNoTracking()
            .Where(plan => plan.IsActive)
            .OrderBy(plan => plan.PriceNpr)
            .ThenBy(plan => plan.Name)
            .Select(plan => new PlanResponse(
                plan.Id,
                plan.Tier,
                plan.Name,
                plan.Description,
                plan.PeriodDays,
                plan.PriceNpr))
            .ToListAsync(cancellationToken);

    // -------------------------------------------------------------- owner read and ask

    /// <summary>
    /// One owned listing's standing with the plans. Scoped by ownership in the query itself,
    /// so somebody else's listing answers the same null as an address that never existed.
    /// </summary>
    internal async Task<SubscriptionStateResponse?> FindStateAsync(
        string slug,
        Guid ownerUserId,
        CancellationToken cancellationToken)
    {
        var business = await dbContext.Businesses
            .Where(item => item.Slug == slug && item.OwnerUserId == ownerUserId)
            .Select(item => new { item.Id, item.Slug })
            .FirstOrDefaultAsync(cancellationToken);

        return business is null
            ? null
            : await StateAsync(business.Id, business.Slug, cancellationToken);
    }

    /// <summary>
    /// The owner asks for a plan. Free is granted on the spot because there is nothing to
    /// decide and nothing to charge; featured waits in the admin queue, because until a
    /// payment provider exists the decision is a person's. Either way the caller gets the
    /// same standing back, so the screen renders one shape.
    /// </summary>
    internal async Task<SubscribeOutcome> SubscribeAsync(
        Guid ownerUserId,
        string slug,
        CreateSubscriptionRequest request,
        string? ip,
        CancellationToken cancellationToken)
    {
        var business = await dbContext.Businesses
            .FirstOrDefaultAsync(
                item => item.Slug == slug && item.OwnerUserId == ownerUserId,
                cancellationToken);

        if (business is null)
        {
            return new SubscribeOutcome(SubscribeStatus.NotFound);
        }

        if (request.Tier == PlanTier.Free)
        {
            var grant = await ApplyGrantAsync(business, request.Tier, ownerUserId, requestId: null, cancellationToken);

            if (grant is null)
            {
                return new SubscribeOutcome(SubscribeStatus.PlanUnavailable);
            }

            RecordGrant(ownerUserId, grant, ip);
            await dbContext.SaveChangesAsync(cancellationToken);

            return new SubscribeOutcome(
                grant.Extended ? SubscribeStatus.Extended : SubscribeStatus.Granted,
                await StateAsync(business.Id, business.Slug, cancellationToken));
        }

        var requestedPlan = await dbContext.Plans
            .FirstOrDefaultAsync(plan => plan.Tier == request.Tier && plan.IsActive, cancellationToken);

        if (requestedPlan is null)
        {
            return new SubscribeOutcome(SubscribeStatus.PlanUnavailable);
        }

        // One question at a time per listing. A second request while the first is open would
        // be two rows asking for the same thing, and the person deciding the first would not
        // know the second existed.
        var hasPending = await dbContext.SubscriptionRequests.AnyAsync(
            item => item.BusinessId == business.Id && item.Status == SubscriptionRequestStatus.Pending,
            cancellationToken);

        if (hasPending)
        {
            return new SubscribeOutcome(SubscribeStatus.AlreadyPending);
        }

        dbContext.SubscriptionRequests.Add(new SubscriptionRequest
        {
            Id = Guid.NewGuid(),
            BusinessId = business.Id,
            PlanId = requestedPlan.Id,
            RequesterUserId = ownerUserId,
            Note = Blank(request.Note),
            Status = SubscriptionRequestStatus.Pending,
            CreatedAt = dates.UtcNow,
        });

        await dbContext.SaveChangesAsync(cancellationToken);

        return new SubscribeOutcome(
            SubscribeStatus.Requested,
            await StateAsync(business.Id, business.Slug, cancellationToken));
    }

    // -------------------------------------------------------------------- staff writes

    /// <summary>
    /// Grants or extends a plan for a listing directly, from the admin side — the path for
    /// comp cases, offline payments and anything a webhook will do later.
    /// </summary>
    internal async Task<SubscribeOutcome> GrantAsync(
        Guid adminUserId,
        GrantSubscriptionRequest request,
        string? ip,
        CancellationToken cancellationToken)
    {
        var business = await dbContext.Businesses
            .FirstOrDefaultAsync(item => item.Id == request.BusinessId, cancellationToken);

        if (business is null)
        {
            return new SubscribeOutcome(SubscribeStatus.NotFound);
        }

        var grant = await ApplyGrantAsync(business, request.Tier, adminUserId, requestId: null, cancellationToken);

        if (grant is null)
        {
            return new SubscribeOutcome(SubscribeStatus.PlanUnavailable);
        }

        RecordGrant(adminUserId, grant, ip);
        await dbContext.SaveChangesAsync(cancellationToken);

        return new SubscribeOutcome(
            grant.Extended ? SubscribeStatus.Extended : SubscribeStatus.Granted,
            await StateAsync(business.Id, business.Slug, cancellationToken));
    }

    /// <summary>Stops a listing's window at the moment it is cancelled. The row is kept.</summary>
    internal async Task<SubscribeOutcome> CancelAsync(
        Guid adminUserId,
        Guid businessId,
        string? ip,
        CancellationToken cancellationToken)
    {
        var business = await dbContext.Businesses
            .FirstOrDefaultAsync(item => item.Id == businessId, cancellationToken);

        if (business is null)
        {
            return new SubscribeOutcome(SubscribeStatus.NotFound);
        }

        var subscription = await ActiveSubscriptionAsync(businessId, cancellationToken);

        if (subscription is null)
        {
            return new SubscribeOutcome(SubscribeStatus.NotActive);
        }

        var before = new
        {
            status = nameof(BusinessSubscriptionStatus.Active),
            tier = subscription.Plan?.Tier.ToString(),
            endsOn = subscription.EndsOn,
        };

        subscription.Status = BusinessSubscriptionStatus.Cancelled;
        subscription.CancelledAt = dates.UtcNow;

        // The window this row held may have been bought rather than granted, and money
        // that bought a placement staff have now stopped is money owed back. The refund
        // happens in the provider's own portal, so what is recorded here is that one is
        // due — the payment row is what the admin list reads to see it outstanding.
        var payment = subscription.SubscriptionRequestId is { } requestId
            ? await dbContext.PaymentTransactions
                .Where(candidate => candidate.SubscriptionRequestId == requestId
                    && candidate.Status == PaymentStatus.Succeeded)
                .FirstOrDefaultAsync(cancellationToken)
            : null;

        if (payment is not null)
        {
            payment.Status = PaymentStatus.RefundDue;
            payment.FailureCode = "subscription_cancelled";
            payment.UpdatedAt = dates.UtcNow;
        }

        auditLog.Record(
            adminUserId,
            AuditActions.SubscriptionCancelled,
            "business_subscription",
            subscription.Id.ToString(),
            before,
            new { status = nameof(BusinessSubscriptionStatus.Cancelled), cancelledAt = subscription.CancelledAt },
            ip);

        await dbContext.SaveChangesAsync(cancellationToken);

        return new SubscribeOutcome(
            SubscribeStatus.Cancelled,
            await StateAsync(business.Id, business.Slug, cancellationToken));
    }

    // ------------------------------------------------------------------ the request queue

    internal async Task<PagedResult<AdminSubscriptionRequestResponse>> ListRequestsForAdminAsync(
        PageRequest paging,
        SubscriptionRequestStatus? status,
        CancellationToken cancellationToken)
    {
        var matching =
            from request in dbContext.SubscriptionRequests
            join requester in dbContext.Users on request.RequesterUserId equals requester.Id
            join business in dbContext.Businesses on request.BusinessId equals business.Id
            join plan in dbContext.Plans on request.PlanId equals plan.Id
            where status == null || request.Status == status
            orderby (request.Status == SubscriptionRequestStatus.Pending ? 0 : 1) ascending,
                    request.CreatedAt descending
            select new
            {
                request.Id,
                request.BusinessId,
                Slug = business.Slug,
                BusinessName = business.Name,
                plan.Tier,
                RequesterName = requester.FullName,
                RequesterEmail = requester.Email,
                request.Note,
                request.Status,
                request.CreatedAt,
                request.ReviewedAt,
                request.ReviewNote,
            };

        var total = await matching.CountAsync(cancellationToken);
        var clamped = paging.ClampTo(total);

        var rows = await matching
            .Skip(clamped.Skip)
            .Take(clamped.PageSize)
            .ToListAsync(cancellationToken);

        var items = rows
            .Select(row => new AdminSubscriptionRequestResponse(
                row.Id,
                row.BusinessId,
                row.Slug,
                row.BusinessName,
                row.Tier,
                row.RequesterName,
                row.RequesterEmail,
                row.Note,
                row.Status,
                row.CreatedAt,
                row.ReviewedAt,
                row.ReviewNote))
            .ToList();

        return new PagedResult<AdminSubscriptionRequestResponse>(items, clamped.Page, clamped.PageSize, total);
    }

    /// <summary>
    /// Approves a request: the window opens or rolls forward and the request is settled, both
    /// by one SaveChanges, with a single audit entry that records the decision and the window
    /// it produced. A plan deactivated since the request is refused rather than granted anyway
    /// — deactivating is how staff stop a plan being handed out.
    /// </summary>
    internal async Task<RequestDecisionOutcome> ApproveRequestAsync(
        Guid requestId,
        string? reviewNote,
        Guid adminUserId,
        string? ip,
        CancellationToken cancellationToken)
    {
        var request = await dbContext.SubscriptionRequests
            .Include(item => item.Business)
            .Include(item => item.Plan)
            .FirstOrDefaultAsync(item => item.Id == requestId, cancellationToken);

        if (request is null)
        {
            return new RequestDecisionOutcome(RequestDecisionStatus.NotFound);
        }

        if (request.Status != SubscriptionRequestStatus.Pending)
        {
            return new RequestDecisionOutcome(
                RequestDecisionStatus.AlreadyReviewed,
                await RequestViewAsync(request, cancellationToken));
        }

        var grant = await ApplyGrantAsync(
            request.Business!,
            request.Plan!.Tier,
            adminUserId,
            requestId: request.Id,
            cancellationToken);

        if (grant is null)
        {
            return new RequestDecisionOutcome(RequestDecisionStatus.PlanUnavailable);
        }

        request.Status = SubscriptionRequestStatus.Approved;
        request.ReviewedByUserId = adminUserId;
        request.ReviewedAt = dates.UtcNow;
        request.ReviewNote = Blank(reviewNote);

        auditLog.Record(
            adminUserId,
            AuditActions.SubscriptionRequestApproved,
            "subscription_request",
            request.Id.ToString(),
            before: new { status = nameof(SubscriptionRequestStatus.Pending) },
            after: new
            {
                status = nameof(SubscriptionRequestStatus.Approved),
                businessId = request.BusinessId,
                grant.SubscriptionId,
                grant.Extended,
            },
            ip);

        await dbContext.SaveChangesAsync(cancellationToken);

        return new RequestDecisionOutcome(
            RequestDecisionStatus.Approved,
            await RequestViewAsync(request, cancellationToken));
    }

    internal async Task<RequestDecisionOutcome> RejectRequestAsync(
        Guid requestId,
        string? reviewNote,
        Guid adminUserId,
        string? ip,
        CancellationToken cancellationToken)
    {
        var request = await dbContext.SubscriptionRequests
            .Include(item => item.Business)
            .Include(item => item.Plan)
            .FirstOrDefaultAsync(item => item.Id == requestId, cancellationToken);

        if (request is null)
        {
            return new RequestDecisionOutcome(RequestDecisionStatus.NotFound);
        }

        if (request.Status != SubscriptionRequestStatus.Pending)
        {
            return new RequestDecisionOutcome(
                RequestDecisionStatus.AlreadyReviewed,
                await RequestViewAsync(request, cancellationToken));
        }

        request.Status = SubscriptionRequestStatus.Rejected;
        request.ReviewedByUserId = adminUserId;
        request.ReviewedAt = dates.UtcNow;
        request.ReviewNote = Blank(reviewNote);

        auditLog.Record(
            adminUserId,
            AuditActions.SubscriptionRequestRejected,
            "subscription_request",
            request.Id.ToString(),
            before: new { status = nameof(SubscriptionRequestStatus.Pending) },
            after: new { status = nameof(SubscriptionRequestStatus.Rejected), note = request.ReviewNote },
            ip);

        await dbContext.SaveChangesAsync(cancellationToken);

        return new RequestDecisionOutcome(
            RequestDecisionStatus.Rejected,
            await RequestViewAsync(request, cancellationToken));
    }

    /// <summary>
    /// Settles a paid checkout into the same queue an admin approval would have gone
    /// through: the grant runs the one <c>ApplyGrantAsync</c> every other path uses, the
    /// request is closed as approved with nobody's name on it — no person clicked, the
    /// provider confirmed — and the note carries the reference so the queue can be tied
    /// back to the payment that closed it.
    ///
    /// <para>
    /// Nothing is saved here and no audit line is written: the payment engine that called
    /// this owns the transaction, the payment row and the single <c>payment.succeeded</c>
    /// entry, so the grant and the money that bought it commit as one thing.
    /// </para>
    /// </summary>
    internal async Task<PaidSettleOutcome> SettlePaidRequestAsync(
        Guid requestId,
        string reference,
        Guid payerUserId,
        CancellationToken cancellationToken)
    {
        var request = await dbContext.SubscriptionRequests
            .Include(item => item.Business)
            .Include(item => item.Plan)
            .FirstOrDefaultAsync(item => item.Id == requestId, cancellationToken);

        if (request is null)
        {
            return new PaidSettleOutcome(PaidSettleStatus.NotFound);
        }

        if (request.Status != SubscriptionRequestStatus.Pending)
        {
            return new PaidSettleOutcome(PaidSettleStatus.AlreadyReviewed);
        }

        var grant = await ApplyGrantAsync(
            request.Business!,
            request.Plan!.Tier,
            payerUserId,
            requestId: request.Id,
            cancellationToken);

        if (grant is null)
        {
            return new PaidSettleOutcome(PaidSettleStatus.PlanUnavailable);
        }

        request.Status = SubscriptionRequestStatus.Approved;
        request.ReviewedByUserId = null;
        request.ReviewedAt = dates.UtcNow;
        request.ReviewNote = $"Paid online (reference {reference}).";

        return new PaidSettleOutcome(
            PaidSettleStatus.Granted,
            grant.SubscriptionId,
            grant.Extended);
    }

    // ------------------------------------------------------------------ internals

    /// <summary>
    /// The one row that can currently be live for a listing, if any. Ordered by end date so
    /// that even if history somehow holds two Active rows the furthest-reaching one wins, and
    /// the plan loaded with it because the tier is part of every view of the state.
    /// </summary>
    private async Task<BusinessSubscription?> ActiveSubscriptionAsync(Guid businessId, CancellationToken cancellationToken) =>
        await dbContext.BusinessSubscriptions
            .Where(subscription => subscription.BusinessId == businessId
                && subscription.Status == BusinessSubscriptionStatus.Active)
            .Include(subscription => subscription.Plan)
            .OrderByDescending(subscription => subscription.EndsOn)
            .FirstOrDefaultAsync(cancellationToken);

    private async Task<SubscriptionStateResponse> StateAsync(Guid businessId, string slug, CancellationToken cancellationToken)
    {
        var subscription = await ActiveSubscriptionAsync(businessId, cancellationToken);
        var live = subscription is not null && SubscriptionRules.IsLive(subscription, dates.Today);

        var pendingRequestId = await dbContext.SubscriptionRequests
            .Where(request => request.BusinessId == businessId
                && request.Status == SubscriptionRequestStatus.Pending)
            .OrderByDescending(request => request.CreatedAt)
            .Select(request => (Guid?)request.Id)
            .FirstOrDefaultAsync(cancellationToken);

        return new SubscriptionStateResponse(
            businessId,
            slug,
            subscription?.Plan?.Tier,
            subscription?.Plan?.Name,
            subscription?.StartsOn,
            subscription?.EndsOn,
            live,
            pendingRequestId);
    }

    /// <summary>
    /// Opens a window or rolls the current one forward by the plan's period, by the shared
    /// extension rule. Null means no active plan of that tier exists, which is the one thing
    /// that stops a grant — the caller records nothing and nothing changes. The returned
    /// before/after pair is captured ahead of the mutation so the audit log can show the
    /// window as it was.
    /// </summary>
    private async Task<GrantResult?> ApplyGrantAsync(
        Business business,
        PlanTier tier,
        Guid actorUserId,
        Guid? requestId,
        CancellationToken cancellationToken)
    {
        var plan = await dbContext.Plans
            .FirstOrDefaultAsync(
                item => item.Tier == tier && item.IsActive,
                cancellationToken);

        if (plan is null)
        {
            return null;
        }

        var today = dates.Today;
        var existing = await ActiveSubscriptionAsync(business.Id, cancellationToken);

        if (existing is null)
        {
            var subscription = new BusinessSubscription
            {
                Id = Guid.NewGuid(),
                BusinessId = business.Id,
                PlanId = plan.Id,
                StartsOn = today,
                EndsOn = SubscriptionRules.ExtendTo(null, today, plan.PeriodDays),
                Status = BusinessSubscriptionStatus.Active,
                CreatedAt = dates.UtcNow,
                GrantedByUserId = actorUserId,
                SubscriptionRequestId = requestId,
            };

            dbContext.BusinessSubscriptions.Add(subscription);

            return new GrantResult(
                subscription.Id,
                Extended: false,
                Before: null,
                After: new { tier = tier.ToString(), startsOn = subscription.StartsOn, endsOn = subscription.EndsOn });
        }

        var before = new { tier = existing.Plan?.Tier.ToString(), endsOn = existing.EndsOn };
        var previousEnd = existing.EndsOn;

        existing.EndsOn = SubscriptionRules.ExtendTo(previousEnd, today, plan.PeriodDays);
        existing.PlanId = plan.Id;
        existing.GrantedByUserId = actorUserId;

        if (requestId is not null)
        {
            existing.SubscriptionRequestId = requestId;
        }

        // The navigation is pointed at the new plan explicitly rather than left to fix-up:
        // this row's plan just changed, and the response built straight after this save must
        // report the tier that was granted rather than the one loaded before it.
        dbContext.Entry(existing).Reference(subscription => subscription.Plan).CurrentValue = plan;

        return new GrantResult(
            existing.Id,
            Extended: true,
            Before: before,
            After: new { tier = tier.ToString(), endsOn = existing.EndsOn });
    }

    /// <summary>Records a grant or an extension, naming the action by what actually happened.</summary>
    private void RecordGrant(Guid actorUserId, GrantResult grant, string? ip) =>
        auditLog.Record(
            actorUserId,
            grant.Extended ? AuditActions.SubscriptionExtended : AuditActions.SubscriptionGranted,
            "business_subscription",
            grant.SubscriptionId.ToString(),
            grant.Before,
            grant.After,
            ip);

    private async Task<AdminSubscriptionRequestResponse> RequestViewAsync(
        SubscriptionRequest request,
        CancellationToken cancellationToken)
    {
        var requester = await dbContext.Users
            .AsNoTracking()
            .Where(user => user.Id == request.RequesterUserId)
            .Select(user => new { user.FullName, user.Email })
            .FirstOrDefaultAsync(cancellationToken);

        var business = await dbContext.Businesses
            .AsNoTracking()
            .Where(item => item.Id == request.BusinessId)
            .Select(item => new { item.Slug, item.Name })
            .FirstOrDefaultAsync(cancellationToken);

        var tier = request.Plan?.Tier
            ?? await dbContext.Plans
                .AsNoTracking()
                .Where(plan => plan.Id == request.PlanId)
                .Select(plan => (PlanTier?)plan.Tier)
                .FirstOrDefaultAsync(cancellationToken)
            ?? PlanTier.Free;

        return new AdminSubscriptionRequestResponse(
            request.Id,
            request.BusinessId,
            business?.Slug ?? string.Empty,
            business?.Name ?? string.Empty,
            tier,
            requester?.FullName ?? string.Empty,
            requester?.Email,
            request.Note,
            request.Status,
            request.CreatedAt,
            request.ReviewedAt,
            request.ReviewNote);
    }

    private static string? Blank(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
