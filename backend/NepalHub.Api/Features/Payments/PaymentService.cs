using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using NepalHub.Api.Common;
using NepalHub.Api.Data;
using NepalHub.Api.Data.Entities;
using NepalHub.Api.Features.Admin;
using NepalHub.Api.Features.Bookings;
using NepalHub.Api.Features.Quiz;
using NepalHub.Api.Features.Subscriptions;

namespace NepalHub.Api.Features.Payments;

public enum CheckoutStatus
{
    /// <summary>A signed form to post to the provider; the buyer is ready to leave.</summary>
    Ready,

    /// <summary>No provider is configured, so there is nothing to send anybody to.</summary>
    NotConfigured,

    BusinessNotFound,
    PlanUnavailable,

    /// <summary>There is no price on this — free plans and free events are never a checkout.</summary>
    Free,

    EventNotFound,
    EventStarted,
    EventFull,
    AlreadyBooked,

    /// <summary>An open checkout turned out to have completed while the buyer was away.</summary>
    AlreadySettled,
}

public enum VerifyStatus
{
    /// <summary>The provider answered and the row now stands at its final state.</summary>
    Settled,

    /// <summary>Nothing confirmed yet: still in flight, unreachable, or about another amount.</summary>
    Pending,

    /// <summary>The signed payload was absent, malformed, or signed by somebody else.</summary>
    InvalidSignature,

    NotFound,
}

public sealed record CheckoutOutcome(
    CheckoutStatus Status,
    CheckoutResponse? Checkout = null,
    PaymentResponse? Payment = null);

public sealed record VerifyOutcome(VerifyStatus Status, PaymentResponse? Payment = null);

/// <summary>
/// Checkout and settlement: the one place that moves a payment row, and the only caller
/// of the provider adapter.
///
/// <para>
/// Nothing here trusts the buyer's browser. A signed return payload is verified and then
/// still answered by asking the provider directly, because the redirect is a message the
/// buyer's machine delivered and the status check is one the provider's servers did. The
/// amount we quoted rides along on that question, and a reply about a different total is
/// recorded rather than believed.
/// </para>
///
/// <para>
/// Settlement runs behind a conditional update — only a row still standing at Pending may
/// leave it — inside the transaction that also writes the entitlement. Two answers
/// arriving together, or a return page and an administrator clicking at the same moment,
/// therefore produce one grant and one audit line; the loser's update claims nothing and
/// rolls back.
/// </para>
///
/// <para>
/// No job sweeps anything. A checkout that has gone quiet is reconciled when somebody
/// next reads it, resumes it, or needs the seat it is holding, which is the same
/// derived-at-read rule the subscription windows already follow.
/// </para>
/// </summary>
public sealed class PaymentService(
    AppDbContext dbContext,
    SubscriptionService subscriptionService,
    BookingService bookingService,
    IPaymentGateway gateway,
    PaymentSettings settings,
    IAuditLog auditLog,
    INepalDateProvider dates,
    ILogger<PaymentService> logger)
{
    /// <summary>
    /// How long an open checkout may sit untouched before a read reconciles it. Longer
    /// than the provider's own session, so a buyer still on its pages is never raced by
    /// our own status check; short enough that an abandoned one releases its hold.
    /// </summary>
    private static readonly TimeSpan StaleAfter = TimeSpan.FromMinutes(5);

    /// <summary>How many abandoned holds one failed take-a-seat attempt will go and release.</summary>
    private const int MaxStaleReconciles = 5;

    // -------------------------------------------------------------------- checkout

    internal async Task<CheckoutOutcome> BeginCheckoutAsync(
        Guid userId,
        CreateCheckoutRequest request,
        string? ip,
        CancellationToken cancellationToken)
    {
        if (!settings.IsConfigured)
        {
            return new CheckoutOutcome(CheckoutStatus.NotConfigured);
        }

        return request.Kind == PaymentKind.Subscription
            ? await BeginSubscriptionCheckoutAsync(userId, request, ip, cancellationToken)
            : await BeginTicketCheckoutAsync(userId, request, ip, cancellationToken);
    }

    private async Task<CheckoutOutcome> BeginSubscriptionCheckoutAsync(
        Guid userId,
        CreateCheckoutRequest request,
        string? ip,
        CancellationToken cancellationToken)
    {
        // Scoped by ownership in the query, so somebody else's listing answers the same
        // 404 their own account screen would — a checkout must not confirm that a listing
        // exists to somebody who cannot buy it.
        var business = await dbContext.Businesses
            .Where(item => item.Slug == request.Slug && item.OwnerUserId == userId)
            .FirstOrDefaultAsync(cancellationToken);

        if (business is null)
        {
            return new CheckoutOutcome(CheckoutStatus.BusinessNotFound);
        }

        var tier = request.Tier ?? PlanTier.Featured;
        if (tier == PlanTier.Free)
        {
            return new CheckoutOutcome(CheckoutStatus.Free);
        }

        var plan = await dbContext.Plans
            .FirstOrDefaultAsync(item => item.Tier == tier && item.IsActive, cancellationToken);

        if (plan is null)
        {
            return new CheckoutOutcome(CheckoutStatus.PlanUnavailable);
        }

        if (plan.PriceNpr <= 0)
        {
            // Nothing to charge: the normal subscribe path grants it on the spot, and a
            // checkout button with no price behind it would be a lie.
            return new CheckoutOutcome(CheckoutStatus.Free);
        }

        // One question at a time per listing, the same rule SubscribeAsync works to. An
        // existing pending request is the one this payment will settle — creating a
        // second would leave the queue two rows for the same thing.
        var pendingRequest = await dbContext.SubscriptionRequests
            .Where(item => item.BusinessId == business.Id
                && item.Status == SubscriptionRequestStatus.Pending)
            .OrderByDescending(item => item.CreatedAt)
            .FirstOrDefaultAsync(cancellationToken);

        var openPayment = pendingRequest is null
            ? null
            : await FindOpenPaymentAsync(
                payment => payment.SubscriptionRequestId == pendingRequest.Id,
                cancellationToken);

        if (openPayment is not null)
        {
            var resumed = await TryResumeAsync(openPayment, cancellationToken);
            if (resumed is not null)
            {
                return resumed;
            }
        }

        pendingRequest ??= new SubscriptionRequest
        {
            Id = Guid.NewGuid(),
            BusinessId = business.Id,
            PlanId = plan.Id,
            RequesterUserId = userId,
            Status = SubscriptionRequestStatus.Pending,
            CreatedAt = dates.UtcNow,
        };

        var description = $"{plan.Name} plan for {business.Name}";

        return await CreateCheckoutAsync(
            userId,
            PaymentKind.Subscription,
            plan.PriceNpr,
            description,
            ip,
            pendingRequest,
            eventBookingId: null,
            cancellationToken);
    }

    private async Task<CheckoutOutcome> BeginTicketCheckoutAsync(
        Guid userId,
        CreateCheckoutRequest request,
        string? ip,
        CancellationToken cancellationToken)
    {
        var now = dates.NepalNow;

        var calendarEvent = await PublicContentScope.Events(dbContext.Events, now)
            .Where(single => single.Slug == request.Slug)
            .FirstOrDefaultAsync(cancellationToken);

        if (calendarEvent is null)
        {
            return new CheckoutOutcome(CheckoutStatus.EventNotFound);
        }

        if (calendarEvent.StartsAt <= now)
        {
            return new CheckoutOutcome(CheckoutStatus.EventStarted);
        }

        if (calendarEvent.TicketPrice <= 0)
        {
            return new CheckoutOutcome(CheckoutStatus.Free);
        }

        var quantity = Math.Clamp(request.Quantity, 1, BookingService.MaxQuantity);
        var amount = calendarEvent.TicketPrice * quantity;

        var existing = await dbContext.EventBookings
            .Where(booking => booking.EventId == calendarEvent.Id
                && booking.AttendeeUserId == userId)
            .FirstOrDefaultAsync(cancellationToken);

        if (existing is { Status: EventBookingStatus.Confirmed })
        {
            return new CheckoutOutcome(CheckoutStatus.AlreadyBooked);
        }

        if (existing is { Status: EventBookingStatus.Pending })
        {
            var heldPayment = await FindOpenPaymentAsync(
                payment => payment.EventBookingId == existing.Id,
                cancellationToken);

            if (heldPayment is not null)
            {
                var resumed = await TryResumeAsync(heldPayment, cancellationToken);
                if (resumed is not null)
                {
                    return resumed;
                }

                // Resuming turned out to be impossible because that checkout had already
                // finished one way or the other, so its own settle path has dealt with the
                // seat and this is a fresh start.
                dbContext.Entry(existing).State = EntityState.Detached;
                existing = null!;
            }
            else
            {
                // A held seat with nothing open behind it: a row that outlived its
                // payment. Release it before taking the place again, or the counter
                // would count the same seat twice. Its two writes share a transaction
                // so the counter cannot come back without the row saying why.
                await using var releaseTransaction =
                    await dbContext.Database.BeginTransactionAsync(cancellationToken);

                await bookingService.ReleasePlaceAsync(existing.Id, now, cancellationToken);
                await dbContext.SaveChangesAsync(cancellationToken);
                await releaseTransaction.CommitAsync(cancellationToken);

                existing = null!;
            }
        }

        for (var attempt = 0; attempt < 2; attempt++)
        {
            var outcome = await TryHoldTicketAsync(
                userId,
                calendarEvent,
                quantity,
                amount,
                ip,
                cancellationToken);

            if (outcome.Status == CheckoutStatus.EventFull && attempt == 0)
            {
                // The latch found no room. Some of it may be held by checkouts nobody
                // finished, so go and reconcile those and try once more — the only place
                // an abandoned hold is ever cleaned up, and only when it is in the way.
                await ReconcileStaleHoldsAsync(calendarEvent.Id, cancellationToken);
                continue;
            }

            return outcome;
        }

        return new CheckoutOutcome(CheckoutStatus.EventFull);
    }

    private async Task<CheckoutOutcome> TryHoldTicketAsync(
        Guid userId,
        Event calendarEvent,
        int quantity,
        int amount,
        string? ip,
        CancellationToken cancellationToken)
    {
        await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);

        var held = await bookingService.HoldPlaceAsync(userId, calendarEvent.Slug, quantity, cancellationToken);

        switch (held.Status)
        {
            case BookStatus.NotFound:
                await transaction.RollbackAsync(cancellationToken);
                return new CheckoutOutcome(CheckoutStatus.EventNotFound);

            case BookStatus.EventStarted:
                await transaction.RollbackAsync(cancellationToken);
                return new CheckoutOutcome(CheckoutStatus.EventStarted);

            case BookStatus.AlreadyBooked:
                await transaction.RollbackAsync(cancellationToken);
                return new CheckoutOutcome(CheckoutStatus.AlreadyBooked);

            case BookStatus.Full:
                await transaction.RollbackAsync(cancellationToken);
                return new CheckoutOutcome(CheckoutStatus.EventFull);
        }

        var description = $"{calendarEvent.Title} x{quantity}";

        var created = await CreateCheckoutAsync(
            userId,
            PaymentKind.EventTicket,
            amount,
            description,
            ip,
            subscriptionRequest: null,
            eventBookingId: held.Booking!.Id,
            cancellationToken);

        if (created.Status != CheckoutStatus.Ready)
        {
            await transaction.RollbackAsync(cancellationToken);
            return created;
        }

        await transaction.CommitAsync(cancellationToken);
        return created;
    }

    /// <summary>
    /// Writes the payment row — together with the request it will settle, when there was
    /// not one already — and signs the form that sends the buyer to the provider. One
    /// SaveChanges covers both rows, so there is never a payment pointing at a request
    /// that does not exist.
    /// </summary>
    private async Task<CheckoutOutcome> CreateCheckoutAsync(
        Guid userId,
        PaymentKind kind,
        int amount,
        string description,
        string? ip,
        SubscriptionRequest? subscriptionRequest,
        Guid? eventBookingId,
        CancellationToken cancellationToken)
    {
        var reference = Guid.NewGuid().ToString("N");

        var payment = new PaymentTransaction
        {
            Id = Guid.NewGuid(),
            Reference = reference,
            UserId = userId,
            Kind = kind,
            AmountNpr = amount,
            Description = description,
            Status = PaymentStatus.Pending,
            Provider = settings.Provider,
            SubscriptionRequestId = subscriptionRequest?.Id,
            EventBookingId = eventBookingId,
            Ip = ip,
            CreatedAt = dates.UtcNow,
            UpdatedAt = dates.UtcNow,
        };

        dbContext.PaymentTransactions.Add(payment);

        if (subscriptionRequest is not null && !dbContext.SubscriptionRequests.Contains(subscriptionRequest))
        {
            dbContext.SubscriptionRequests.Add(subscriptionRequest);
        }

        await dbContext.SaveChangesAsync(cancellationToken);

        var handshake = await gateway.BeginCheckoutAsync(
            new CheckoutIntent(reference, amount, description),
            cancellationToken);

        return new CheckoutOutcome(
            CheckoutStatus.Ready,
            new CheckoutResponse(
                handshake.Reference,
                handshake.CheckoutUrl.ToString(),
                handshake.Fields,
                PaymentStatus.Pending));
    }

    // ------------------------------------------------------------------ reconciliation

    internal async Task<VerifyOutcome> VerifyAsync(
        Guid userId,
        VerifyPaymentRequest request,
        string? ip,
        CancellationToken cancellationToken)
    {
        var payment = await dbContext.PaymentTransactions
            .FirstOrDefaultAsync(
                item => item.Reference == request.Reference && item.UserId == userId,
                cancellationToken);

        if (payment is null)
        {
            return new VerifyOutcome(VerifyStatus.NotFound);
        }

        // A signed payload, when the provider sent one, is checked first and rejected
        // before anything else is done with it. Its own verdict is still not believed:
        // the status check below is what settles, so a forger who cannot sign can gain
        // nothing and a genuine redirect merely arrives at the same question we ask.
        if (!string.IsNullOrWhiteSpace(request.Data))
        {
            var notice = await gateway.ReadNoticeAsync(
                request.Data,
                request.Signature ?? string.Empty,
                cancellationToken);

            if (notice is null || !string.Equals(notice.Reference, payment.Reference, StringComparison.Ordinal))
            {
                return new VerifyOutcome(VerifyStatus.InvalidSignature);
            }
        }

        if (payment.Status != PaymentStatus.Pending)
        {
            return new VerifyOutcome(VerifyStatus.Settled, ToResponse(payment));
        }

        return await ReconcileAsync(payment, userId, ip, cancellationToken);
    }

    /// <summary>
    /// One payment as it stands, reconciling it first when it has been left quiet long
    /// enough that the provider will certainly have finished with it by now.
    /// </summary>
    internal async Task<VerifyOutcome?> GetStateAsync(
        Guid userId,
        string reference,
        CancellationToken cancellationToken)
    {
        var payment = await dbContext.PaymentTransactions
            .FirstOrDefaultAsync(
                item => item.Reference == reference && item.UserId == userId,
                cancellationToken);

        if (payment is null)
        {
            return null;
        }

        var staleCutoff = dates.UtcNow - StaleAfter;

        if (payment.Status == PaymentStatus.Pending && payment.UpdatedAt <= staleCutoff)
        {
            return await ReconcileAsync(payment, userId, ip: null, cancellationToken);
        }

        return new VerifyOutcome(
            payment.Status == PaymentStatus.Pending ? VerifyStatus.Pending : VerifyStatus.Settled,
            ToResponse(payment));
    }

    /// <summary>
    /// Asks the provider what became of one open checkout and lets the answer settle it.
    /// Called by the return page, the "check again" button, an administrator checking by
    /// hand, and the resume path — one question, one place that acts on the answer.
    /// </summary>
    internal async Task<VerifyOutcome> ReconcileAsync(
        PaymentTransaction payment,
        Guid triggerUserId,
        string? ip,
        CancellationToken cancellationToken)
    {
        if (payment.Status == PaymentStatus.Failed)
        {
            // Terminal and never a refund: nothing was taken, so there is nothing to ask.
            return new VerifyOutcome(VerifyStatus.Settled, ToResponse(payment));
        }

        var report = await gateway.CheckStatusAsync(payment.Reference, payment.AmountNpr, cancellationToken);

        if (report is null)
        {
            // Unreachable or unanswerable. "We do not know" must never be recorded as
            // "it failed" — the row stays exactly as it was.
            return new VerifyOutcome(VerifyStatus.Pending, ToResponse(payment));
        }

        if (!string.Equals(report.Reference, payment.Reference, StringComparison.Ordinal)
            || report.TotalNpr != payment.AmountNpr)
        {
            // A reply about another transaction, or about a different total than we
            // quoted. Either way it is not an answer to this payment, so it is noted for
            // whoever looks at the row and believed about nothing.
            payment.FailureCode = "amount_mismatch";
            payment.UpdatedAt = dates.UtcNow;
            await dbContext.SaveChangesAsync(cancellationToken);

            logger.LogWarning(
                "Payment {Reference} got a status answer about {ReportReference} totalling {ReportTotal}; recorded and not settled.",
                payment.Reference,
                report.Reference,
                report.TotalNpr);

            return new VerifyOutcome(VerifyStatus.Pending, ToResponse(payment));
        }

        return await SettleAsync(payment, report.Kind, report.ProviderReference, triggerUserId, ip, cancellationToken);
    }

    /// <summary>
    /// Moves one payment to the state the provider just confirmed and writes everything
    /// that state implies — the seat, the entitlement, the audit line — in a single
    /// transaction guarded by the conditional update that decides who may move it.
    /// </summary>
    private async Task<VerifyOutcome> SettleAsync(
        PaymentTransaction payment,
        ProviderNoticeKind notice,
        string? providerReference,
        Guid triggerUserId,
        string? ip,
        CancellationToken cancellationToken)
    {
        if (notice == ProviderNoticeKind.Pending)
        {
            return new VerifyOutcome(VerifyStatus.Pending, ToResponse(payment));
        }

        var target = notice switch
        {
            ProviderNoticeKind.Succeeded => PaymentStatus.Succeeded,
            ProviderNoticeKind.Refunded => PaymentStatus.Refunded,
            _ => PaymentStatus.Failed,
        };

        var now = dates.UtcNow;
        var previousStatus = payment.Status;

        await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);

        // The latch. A refund may land on a row that is open, already owed back, or
        // settled-and-since-refunded through the portal; everything else may only move a
        // row that has not answered yet. Two settlements racing claim this once.
        var claimed = target == PaymentStatus.Refunded
            ? await dbContext.PaymentTransactions
                .Where(item => item.Id == payment.Id
                    && (item.Status == PaymentStatus.Pending
                        || item.Status == PaymentStatus.RefundDue
                        || item.Status == PaymentStatus.Succeeded))
                .ExecuteUpdateAsync(
                    setters => setters
                        .SetProperty(item => item.Status, PaymentStatus.Refunded)
                        .SetProperty(item => item.RefundedAt, now)
                        .SetProperty(item => item.UpdatedAt, now)
                        .SetProperty(item => item.ProviderReference, providerReference),
                    cancellationToken)
            : await dbContext.PaymentTransactions
                .Where(item => item.Id == payment.Id && item.Status == PaymentStatus.Pending)
                .ExecuteUpdateAsync(
                    setters => setters
                        .SetProperty(item => item.Status, target)
                        .SetProperty(item => item.SettledAt, now)
                        .SetProperty(item => item.UpdatedAt, now)
                        .SetProperty(item => item.ProviderReference, providerReference),
                    cancellationToken);

        if (claimed == 0)
        {
            await transaction.RollbackAsync(cancellationToken);
            await dbContext.Entry(payment).ReloadAsync(cancellationToken);

            return new VerifyOutcome(VerifyStatus.Settled, ToResponse(payment));
        }

        // Mirror the row the update just wrote, so the change tracker and the database
        // agree by the time anything else saves in this transaction.
        payment.Status = target;
        payment.UpdatedAt = now;
        payment.ProviderReference = providerReference ?? payment.ProviderReference;

        if (target == PaymentStatus.Refunded)
        {
            payment.RefundedAt = now;
        }
        else
        {
            payment.SettledAt = now;
        }

        var granted = target == PaymentStatus.Succeeded
            ? await ApplySucceededAsync(payment, triggerUserId, ip, cancellationToken)
            : target == PaymentStatus.Failed
                ? await ApplyFailedAsync(payment, now, cancellationToken)
                : await ApplyRefundedAsync(payment, previousStatus, triggerUserId, now, ip, cancellationToken);

        if (target == PaymentStatus.Succeeded && granted != PaymentStatus.Succeeded)
        {
            // The entitlement could not be granted — the plan went away, or staff had
            // already decided the request. The money exists, so the row records that it
            // is owed back rather than standing as a success nothing came of.
            payment.Status = granted;
        }

        await dbContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        return new VerifyOutcome(
            payment.Status == PaymentStatus.Pending ? VerifyStatus.Pending : VerifyStatus.Settled,
            ToResponse(payment));
    }

    /// <summary>
    /// What a confirmed payment buys. Returns the status the payment should stand at once
    /// the entitlement has been attempted — Succeeded when something was granted, and
    /// RefundDue when the money bought nothing and is therefore owed back.
    /// </summary>
    private async Task<PaymentStatus> ApplySucceededAsync(
        PaymentTransaction payment,
        Guid triggerUserId,
        string? ip,
        CancellationToken cancellationToken)
    {
        if (payment.Kind == PaymentKind.EventTicket)
        {
            if (payment.EventBookingId is { } bookingId)
            {
                await bookingService.ConfirmPlaceAsync(bookingId, cancellationToken);
            }

            auditLog.Record(
                payment.UserId,
                AuditActions.PaymentSucceeded,
                "payment_transactions",
                payment.Id.ToString(),
                before: new { status = nameof(PaymentStatus.Pending) },
                after: new
                {
                    status = nameof(PaymentStatus.Succeeded),
                    payment.Kind,
                    payment.AmountNpr,
                    payment.Reference,
                    payment.EventBookingId,
                },
                ip);

            return PaymentStatus.Succeeded;
        }

        if (payment.SubscriptionRequestId is not { } requestId)
        {
            return RefundDue(payment, "grant_failed");
        }

        var settled = await subscriptionService.SettlePaidRequestAsync(
            requestId,
            payment.Reference,
            payment.UserId,
            cancellationToken);

        switch (settled.Status)
        {
            case PaidSettleStatus.Granted:
                auditLog.Record(
                    payment.UserId,
                    AuditActions.PaymentSucceeded,
                    "payment_transactions",
                    payment.Id.ToString(),
                    before: new { status = nameof(PaymentStatus.Pending) },
                    after: new
                    {
                        status = nameof(PaymentStatus.Succeeded),
                        payment.Kind,
                        payment.AmountNpr,
                        payment.Reference,
                        subscriptionRequestId = requestId,
                        settled.SubscriptionId,
                        settled.Extended,
                    },
                    ip);

                return PaymentStatus.Succeeded;

            case PaidSettleStatus.AlreadyReviewed:
                // Staff granted it while the money was in flight. The placement exists
                // already, so this payment bought nothing and goes back to its maker.
                return RefundDue(payment, "not_needed");

            default:
                return RefundDue(payment, "grant_failed");
        }
    }

    private static PaymentStatus RefundDue(PaymentTransaction payment, string failureCode)
    {
        payment.FailureCode = failureCode;
        return PaymentStatus.RefundDue;
    }

    private async Task<PaymentStatus> ApplyFailedAsync(
        PaymentTransaction payment,
        DateTime now,
        CancellationToken cancellationToken)
    {
        payment.FailureCode ??= "provider_failed";

        // Nothing was taken, so nothing is owed: a ticket's hold goes back to the event
        // and the request, if any, simply stays in the queue where an administrator can
        // still decide it or the buyer can try again.
        if (payment.Kind == PaymentKind.EventTicket && payment.EventBookingId is { } bookingId)
        {
            await bookingService.ReleasePlaceAsync(bookingId, now, cancellationToken);
        }

        return PaymentStatus.Failed;
    }

    private async Task<PaymentStatus> ApplyRefundedAsync(
        PaymentTransaction payment,
        PaymentStatus previousStatus,
        Guid triggerUserId,
        DateTime now,
        string? ip,
        CancellationToken cancellationToken)
    {
        // A place that has been paid for and then refunded must go back to the event:
        // the money is gone, so the seat cannot stay held. Cancelling it earlier already
        // released the counter, and releasing a cancelled row changes nothing, so the
        // double path through here is harmless.
        if (payment.Kind == PaymentKind.EventTicket && payment.EventBookingId is { } bookingId)
        {
            await bookingService.ReleasePlaceAsync(bookingId, now, cancellationToken);
        }

        // A subscription window is not revoked automatically: windows roll forward by
        // max(end, today) + period, so working out what a refund reverses is a judgement
        // for the administrator who issued it, using the cancellation that already exists.
        auditLog.Record(
            triggerUserId,
            AuditActions.PaymentRefunded,
            "payment_transactions",
            payment.Id.ToString(),
            before: new { status = previousStatus.ToString() },
            after: new
            {
                status = nameof(PaymentStatus.Refunded),
                payment.Kind,
                payment.AmountNpr,
                payment.Reference,
            },
            ip);

        return PaymentStatus.Refunded;
    }

    /// <summary>
    /// Releases the seats held by checkouts nobody finished, so an abandoned payment
    /// cannot park itself on an event's capacity for ever. Only ever called when those
    /// holds are in the way, and bounded, because each one is a call to the provider.
    /// </summary>
    private async Task ReconcileStaleHoldsAsync(Guid eventId, CancellationToken cancellationToken)
    {
        var cutoff = dates.UtcNow - StaleAfter;

        var stale = await dbContext.PaymentTransactions
            .Where(item => item.Status == PaymentStatus.Pending
                && item.Kind == PaymentKind.EventTicket
                && item.EventBooking != null
                && item.EventBooking.EventId == eventId
                && item.CreatedAt <= cutoff)
            .OrderBy(item => item.CreatedAt)
            .Take(MaxStaleReconciles)
            .ToListAsync(cancellationToken);

        foreach (var payment in stale)
        {
            await ReconcileAsync(payment, payment.UserId, ip: null, cancellationToken);
        }
    }

    /// <summary>
    /// The sweep nobody has to remember: every checkout left Pending long enough that the
    /// provider has certainly finished with it is asked about, oldest first, so one that was
    /// nevertheless paid settles without the buyer or an administrator ever coming back.
    /// Bounded to <see cref="MaxStaleReconciles"/> per pass because each one is a call to the
    /// provider, and skipped whole when there is no provider to ask. Returns how many were
    /// examined, which is what the caller logs.
    /// </summary>
    internal async Task<int> ReconcileStaleAsync(CancellationToken cancellationToken)
    {
        if (!settings.IsConfigured)
        {
            return 0;
        }

        var cutoff = dates.UtcNow - StaleAfter;

        var stale = await dbContext.PaymentTransactions
            .Where(item => item.Status == PaymentStatus.Pending && item.UpdatedAt <= cutoff)
            .OrderBy(item => item.UpdatedAt)
            .Take(MaxStaleReconciles)
            .ToListAsync(cancellationToken);

        foreach (var payment in stale)
        {
            await ReconcileAsync(payment, payment.UserId, ip: null, cancellationToken);
        }

        return stale.Count;
    }

    /// <summary>
    /// What to do about a checkout that already exists: ask the provider whether it
    /// finished while the buyer was away, resume it if it did not, and stand out of the
    /// way if it did. Null means "carry on and start a fresh one".
    /// </summary>
    private async Task<CheckoutOutcome?> TryResumeAsync(
        PaymentTransaction open,
        CancellationToken cancellationToken)
    {
        var report = await gateway.CheckStatusAsync(open.Reference, open.AmountNpr, cancellationToken);

        if (report is null)
        {
            // No answer: the checkout is still the one in flight. Re-sign it, because
            // the signature and the provider's session are both short-lived, while the
            // reference — the thing both sides recognise — stays the same.
            return await ResumeAsync(open, cancellationToken);
        }

        if (!string.Equals(report.Reference, open.Reference, StringComparison.Ordinal)
            || report.TotalNpr != open.AmountNpr)
        {
            open.FailureCode = "amount_mismatch";
            open.UpdatedAt = dates.UtcNow;
            await dbContext.SaveChangesAsync(cancellationToken);

            return await ResumeAsync(open, cancellationToken);
        }

        switch (report.Kind)
        {
            case ProviderNoticeKind.Succeeded:
                await SettleAsync(open, report.Kind, report.ProviderReference, open.UserId, ip: null, cancellationToken);
                return new CheckoutOutcome(CheckoutStatus.AlreadySettled, Payment: ToResponse(open));

            case ProviderNoticeKind.Pending:
                return await ResumeAsync(open, cancellationToken);

            default:
                // Failed or refunded: that attempt is over and its consequences are
                // written, so this buyer is welcome to start again.
                await SettleAsync(open, report.Kind, report.ProviderReference, open.UserId, ip: null, cancellationToken);
                return null;
        }
    }

    private async Task<CheckoutOutcome> ResumeAsync(PaymentTransaction open, CancellationToken cancellationToken)
    {
        var handshake = await gateway.BeginCheckoutAsync(
            new CheckoutIntent(open.Reference, open.AmountNpr, open.Description),
            cancellationToken);

        return new CheckoutOutcome(
            CheckoutStatus.Ready,
            new CheckoutResponse(
                handshake.Reference,
                handshake.CheckoutUrl.ToString(),
                handshake.Fields,
                PaymentStatus.Pending));
    }

    // ------------------------------------------------------------------------ reads

    internal async Task<PagedResult<PaymentSummaryResponse>> ListForUserAsync(
        Guid userId,
        PageRequest paging,
        CancellationToken cancellationToken)
    {
        var matching = dbContext.PaymentTransactions
            .Where(item => item.UserId == userId)
            .OrderByDescending(item => item.CreatedAt)
            .ThenByDescending(item => item.Id);

        var total = await matching.CountAsync(cancellationToken);
        var clamped = paging.ClampTo(total);

        var rows = await matching
            .Skip(clamped.Skip)
            .Take(clamped.PageSize)
            .AsNoTracking()
            .ToListAsync(cancellationToken);

        return new PagedResult<PaymentSummaryResponse>(
            rows.Select(ToSummary).ToList(),
            clamped.Page,
            clamped.PageSize,
            total);
    }

    internal async Task<PagedResult<AdminPaymentResponse>> ListForAdminAsync(
        PageRequest paging,
        PaymentStatus? status,
        CancellationToken cancellationToken)
    {
        var matching =
            from payment in dbContext.PaymentTransactions
            join buyer in dbContext.Users on payment.UserId equals buyer.Id
            where status == null || payment.Status == status
            orderby payment.CreatedAt descending
            select new { payment, buyer.FullName, buyer.UserName, buyer.Email };

        var total = await matching.CountAsync(cancellationToken);
        var clamped = paging.ClampTo(total);

        var rows = await matching
            .Skip(clamped.Skip)
            .Take(clamped.PageSize)
            .ToListAsync(cancellationToken);

        var items = rows
            .Select(row => new AdminPaymentResponse(
                row.payment.Reference,
                row.payment.Kind,
                row.payment.Status,
                row.payment.AmountNpr,
                row.payment.Description,
                row.payment.FailureCode,
                row.payment.CreatedAt,
                row.payment.SettledAt,
                row.payment.RefundedAt,
                row.FullName ?? row.UserName ?? row.Email,
                row.Email))
            .ToList();

        return new PagedResult<AdminPaymentResponse>(items, clamped.Page, clamped.PageSize, total);
    }

    /// <summary>
    /// An administrator asking the provider directly — the move that discovers a refund
    /// somebody issued in the portal, and the answer to "has this actually gone through?".
    /// Anybody's payment may be checked: this is the back office, not the buyer's screen.
    /// </summary>
    internal async Task<VerifyOutcome> CheckStatusAsync(
        Guid adminUserId,
        string reference,
        string? ip,
        CancellationToken cancellationToken)
    {
        var payment = await dbContext.PaymentTransactions
            .FirstOrDefaultAsync(item => item.Reference == reference, cancellationToken);

        if (payment is null)
        {
            return new VerifyOutcome(VerifyStatus.NotFound);
        }

        return await ReconcileAsync(payment, adminUserId, ip, cancellationToken);
    }

    internal async Task<PaymentResponse?> FindForUserAsync(
        Guid userId,
        string reference,
        CancellationToken cancellationToken)
    {
        var payment = await dbContext.PaymentTransactions
            .AsNoTracking()
            .FirstOrDefaultAsync(
                item => item.Reference == reference && item.UserId == userId,
                cancellationToken);

        return payment is null ? null : ToResponse(payment);
    }

    private async Task<PaymentTransaction?> FindOpenPaymentAsync(
        System.Linq.Expressions.Expression<Func<PaymentTransaction, bool>> target,
        CancellationToken cancellationToken) =>
        await dbContext.PaymentTransactions
            .Where(target)
            .Where(item => item.Status == PaymentStatus.Pending)
            .OrderByDescending(item => item.CreatedAt)
            .FirstOrDefaultAsync(cancellationToken);

    private static PaymentResponse ToResponse(PaymentTransaction payment) => new(
        payment.Reference,
        payment.Kind,
        payment.Status,
        payment.AmountNpr,
        payment.Description,
        payment.FailureCode,
        payment.ProviderReference,
        payment.CreatedAt,
        payment.SettledAt,
        payment.RefundedAt);

    private static PaymentSummaryResponse ToSummary(PaymentTransaction payment) => new(
        payment.Reference,
        payment.Kind,
        payment.Status,
        payment.AmountNpr,
        payment.Description,
        payment.FailureCode,
        payment.CreatedAt,
        payment.SettledAt,
        payment.RefundedAt);
}
