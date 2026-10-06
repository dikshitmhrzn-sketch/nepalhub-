using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using MySqlConnector;
using NepalHub.Api.Common;
using NepalHub.Api.Data;
using NepalHub.Api.Data.Entities;
using NepalHub.Api.Features.Quiz;

namespace NepalHub.Api.Features.Bookings;

public enum BookStatus
{
    Booked,
    NotFound,
    EventStarted,
    AlreadyBooked,
    Full,

    /// <summary>The event has a price, so a place is bought rather than simply taken.</summary>
    PaymentRequired,
}

public enum CancelBookingStatus
{
    Cancelled,
    NotFound,
    EventStarted,
}

public sealed record BookOutcome(BookStatus Status, BookingResponse? Booking = null);

public sealed record CancelOutcome(CancelBookingStatus Status, BookingResponse? Booking = null);

/// <summary>
/// Taking and releasing places at an event.
///
/// <para>
/// Availability lives entirely on <c>events.registration_count</c>, and the check that takes a
/// place is one conditional UPDATE — the count only rises when the row still has room, so two
/// readers racing for the last place produce one winner rather than two confirmations. The
/// booking row is the record of who holds what; the counter is the truth about how many are
/// held. Counting rows instead would let a cancelled row and a fresh one disagree about the
/// same seat.
/// </para>
///
/// <para>
/// No audit line is written for any of this, unlike the claim and subscription paths. The
/// audit log records what staff and administrators did to other people's things; a booking is
/// a person acting on their own account, and the row itself — with its status and its
/// cancellation timestamp — is the complete record of that.
/// </para>
/// </summary>
public sealed class BookingService(
    AppDbContext dbContext,
    INepalDateProvider dates,
    ILogger<BookingService> logger)
{
    /// <summary>Places one person may hold at a time, mirrored by the request's [Range].</summary>
    internal const int MaxQuantity = 4;

    internal async Task<BookOutcome> BookAsync(
        Guid attendeeUserId,
        string slug,
        int quantity,
        CancellationToken cancellationToken)
    {
        var now = dates.NepalNow;

        // The same published, unfinished scope the calendar reads, so booking a draft or a
        // lapsed link answers the 404 the rest of the site gives rather than admitting the
        // event exists.
        var calendarEvent = await PublicContentScope.Events(dbContext.Events, now)
            .Where(single => single.Slug == slug)
            .FirstOrDefaultAsync(cancellationToken);

        if (calendarEvent is null)
        {
            return new BookOutcome(BookStatus.NotFound);
        }

        // Bookings close the moment the event begins. An event already under way is still on
        // the calendar — a reader may open its page during it — but taking a place at
        // something that has started is a correction ticket, not a booking.
        if (calendarEvent.StartsAt <= now)
        {
            return new BookOutcome(BookStatus.EventStarted);
        }

        // Priced events are bought, not booked. Answering here rather than in the
        // controller keeps the rule beside the latch it guards: the only way past this
        // line is the payment path, which holds the place the same way and confirms it
        // when the provider says the money arrived.
        if (calendarEvent.TicketPrice > 0)
        {
            return new BookOutcome(BookStatus.PaymentRequired);
        }

        var existing = await dbContext.EventBookings
            .Where(booking => booking.EventId == calendarEvent.Id
                && booking.AttendeeUserId == attendeeUserId)
            .FirstOrDefaultAsync(cancellationToken);

        if (existing is { Status: EventBookingStatus.Confirmed })
        {
            return new BookOutcome(BookStatus.AlreadyBooked, ToResponse(existing, calendarEvent));
        }

        await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);

        // The latch: ask whether a place is free and take it in one statement. The quantity
        // rides in the same arithmetic, so a request for four either claims four or claims
        // none — a partial claim would need a release path the reader would never see.
        var claimed = await dbContext.Events
            .Where(single => single.Id == calendarEvent.Id
                && (single.Capacity == null
                    || single.RegistrationCount + quantity <= single.Capacity))
            .ExecuteUpdateAsync(
                setters => setters.SetProperty(
                    single => single.RegistrationCount,
                    single => single.RegistrationCount + quantity),
                cancellationToken);

        if (claimed == 0)
        {
            await transaction.RollbackAsync(cancellationToken);
            return new BookOutcome(BookStatus.Full);
        }

        if (existing is null)
        {
            existing = new EventBooking
            {
                Id = Guid.NewGuid(),
                EventId = calendarEvent.Id,
                AttendeeUserId = attendeeUserId,
                Quantity = quantity,
                Status = EventBookingStatus.Confirmed,
                CreatedAt = now,
            };

            dbContext.EventBookings.Add(existing);
        }
        else
        {
            // Reactivation: the unique row from a cancelled booking is the booking again. The
            // timestamp moves with it, because the row records the place as it stands now —
            // who held it last spring is history the cancellation timestamp already keeps.
            existing.Quantity = quantity;
            existing.Status = EventBookingStatus.Confirmed;
            existing.CancelledAt = null;
            existing.CreatedAt = now;
        }

        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        }
        catch (DbUpdateException exception) when (IsDuplicateKey(exception))
        {
            // Two tabs of the same account passed the latch together, so the counter was
            // raised twice for one seat. Rolling back takes that extra place back with it,
            // and losing this race is an expected outcome, not a fault.
            await transaction.RollbackAsync(cancellationToken);
            Detach(existing!);

            logger.LogInformation(
                "A duplicate booking attempt for user {UserId} on event {EventId} was refused.",
                attendeeUserId,
                calendarEvent.Id);

            return new BookOutcome(BookStatus.AlreadyBooked);
        }

        return new BookOutcome(BookStatus.Booked, ToResponse(existing!, calendarEvent));
    }

    internal async Task<CancelOutcome> CancelAsync(
        Guid attendeeUserId,
        string slug,
        CancellationToken cancellationToken)
    {
        var now = dates.NepalNow;

        // Only a confirmed booking is cancellable; a row already cancelled answers the same
        // 404 as one that never existed, so a double-submit cannot report a second success.
        var booking = await dbContext.EventBookings
            .Where(candidate => candidate.AttendeeUserId == attendeeUserId
                && candidate.Status == EventBookingStatus.Confirmed
                && candidate.Event!.Slug == slug)
            .Include(candidate => candidate.Event)
            .FirstOrDefaultAsync(cancellationToken);

        if (booking is null)
        {
            return new CancelOutcome(CancelBookingStatus.NotFound);
        }

        if (booking.Event!.StartsAt <= now)
        {
            return new CancelOutcome(CancelBookingStatus.EventStarted);
        }

        await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);

        // The place goes back with the same guarded arithmetic it was taken with, floored at
        // zero: if the counter was ever wound down elsewhere, cancelling must not drive it
        // negative and make a full event look half empty.
        await dbContext.Events
            .Where(single => single.Id == booking.EventId)
            .ExecuteUpdateAsync(
                setters => setters.SetProperty(
                    single => single.RegistrationCount,
                    single => single.RegistrationCount > booking.Quantity
                        ? single.RegistrationCount - booking.Quantity
                        : 0),
                cancellationToken);

        booking.Status = EventBookingStatus.Cancelled;
        booking.CancelledAt = now;

        // A place that cost something and is now given up is money owed back. The refund
        // itself happens in the provider's own portal — eSewa has no refund API — so the
        // payment row is flipped to say it is due, which is what the admin list reads to
        // know a refund is outstanding.
        var payment = await dbContext.PaymentTransactions
            .Where(candidate => candidate.EventBookingId == booking.Id
                && candidate.Status == PaymentStatus.Succeeded)
            .FirstOrDefaultAsync(cancellationToken);

        if (payment is not null)
        {
            payment.Status = PaymentStatus.RefundDue;
            payment.FailureCode = "booking_cancelled";
            payment.UpdatedAt = now;
        }

        await dbContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        return new CancelOutcome(CancelBookingStatus.Cancelled, ToResponse(booking, booking.Event!));
    }

    // ------------------------------------------------------ the paid path's primitives

    /// <summary>
    /// Takes places for a checkout that has not been paid for yet: the same latch as
    /// <see cref="BookAsync"/>, writing a row that stands at <see cref="EventBookingStatus.Pending"/>
    /// instead of a confirmed one. The seat is held before the provider is asked because a
    /// provider that confirms a purchase of something sold out in the meantime has no way
    /// to give the money back — eSewa's refunds are a manual act in a merchant portal.
    ///
    /// <para>
    /// The caller owns the transaction, wrapping this and the payment row it belongs to so
    /// the latch, the booking and the payment commit as one thing. A duplicate-key failure
    /// is left to surface to that caller, which is what can roll the latch back.
    /// </para>
    /// </summary>
    internal async Task<BookOutcome> HoldPlaceAsync(
        Guid attendeeUserId,
        string slug,
        int quantity,
        CancellationToken cancellationToken)
    {
        var now = dates.NepalNow;

        var calendarEvent = await PublicContentScope.Events(dbContext.Events, now)
            .Where(single => single.Slug == slug)
            .FirstOrDefaultAsync(cancellationToken);

        if (calendarEvent is null)
        {
            return new BookOutcome(BookStatus.NotFound);
        }

        if (calendarEvent.StartsAt <= now)
        {
            return new BookOutcome(BookStatus.EventStarted);
        }

        var existing = await dbContext.EventBookings
            .Where(booking => booking.EventId == calendarEvent.Id
                && booking.AttendeeUserId == attendeeUserId)
            .FirstOrDefaultAsync(cancellationToken);

        // A row already standing means something is already held for this person: a
        // confirmed place, or a seat this same buyer's open checkout is holding. Neither
        // may be taken again, and neither may raise the counter twice.
        if (existing is { Status: EventBookingStatus.Confirmed or EventBookingStatus.Pending })
        {
            return new BookOutcome(BookStatus.AlreadyBooked, ToResponse(existing, calendarEvent));
        }

        var claimed = await dbContext.Events
            .Where(single => single.Id == calendarEvent.Id
                && (single.Capacity == null
                    || single.RegistrationCount + quantity <= single.Capacity))
            .ExecuteUpdateAsync(
                setters => setters.SetProperty(
                    single => single.RegistrationCount,
                    single => single.RegistrationCount + quantity),
                cancellationToken);

        if (claimed == 0)
        {
            return new BookOutcome(BookStatus.Full);
        }

        if (existing is null)
        {
            existing = new EventBooking
            {
                Id = Guid.NewGuid(),
                EventId = calendarEvent.Id,
                AttendeeUserId = attendeeUserId,
                Quantity = quantity,
                Status = EventBookingStatus.Pending,
                CreatedAt = now,
            };

            dbContext.EventBookings.Add(existing);
        }
        else
        {
            // A cancelled row coming back as a held one: same bookkeeping as rebooking,
            // except the status says the place is waiting on a payment rather than held.
            existing.Quantity = quantity;
            existing.Status = EventBookingStatus.Pending;
            existing.CancelledAt = null;
            existing.CreatedAt = now;
        }

        return new BookOutcome(BookStatus.Booked, ToResponse(existing, calendarEvent));
    }

    /// <summary>
    /// Turns a held place into a confirmed one once the provider has confirmed the money.
    /// The seat was latched at checkout, so this only moves the status — no counter is
    /// touched, because the place was counted when it was taken.
    /// </summary>
    internal async Task ConfirmPlaceAsync(Guid bookingId, CancellationToken cancellationToken)
    {
        var booking = await dbContext.EventBookings
            .Where(candidate => candidate.Id == bookingId)
            .FirstOrDefaultAsync(cancellationToken);

        if (booking is null || booking.Status == EventBookingStatus.Cancelled)
        {
            return;
        }

        booking.Status = EventBookingStatus.Confirmed;
        booking.CancelledAt = null;
    }

    /// <summary>
    /// Gives a place back — used when a payment fails, when an abandoned checkout is
    /// reconciled, and when a paid place is refunded. The counter returns with the same
    /// guarded, floored arithmetic cancelling uses, and a row already cancelled changes
    /// nothing, so releasing twice cannot take two seats away.
    /// </summary>
    internal async Task ReleasePlaceAsync(Guid bookingId, DateTime now, CancellationToken cancellationToken)
    {
        var booking = await dbContext.EventBookings
            .Where(candidate => candidate.Id == bookingId)
            .FirstOrDefaultAsync(cancellationToken);

        if (booking is null || booking.Status == EventBookingStatus.Cancelled)
        {
            return;
        }

        await dbContext.Events
            .Where(single => single.Id == booking.EventId)
            .ExecuteUpdateAsync(
                setters => setters.SetProperty(
                    single => single.RegistrationCount,
                    single => single.RegistrationCount > booking.Quantity
                        ? single.RegistrationCount - booking.Quantity
                        : 0),
                cancellationToken);

        booking.Status = EventBookingStatus.Cancelled;
        booking.CancelledAt = now;
    }

    /// <summary>
    /// The staff side of one event: who holds a place, cancelled rows and all. Staff see
    /// drafts and finished events too, so this deliberately does not go through the public
    /// scope — an event nobody can book still has an attendee list somebody is asking about.
    /// </summary>
    internal async Task<EventAttendanceResponse?> ListAttendanceAsync(
        Guid eventId,
        CancellationToken cancellationToken)
    {
        var calendarEvent = await dbContext.Events
            .AsNoTracking()
            .Where(single => single.Id == eventId)
            .FirstOrDefaultAsync(cancellationToken);

        if (calendarEvent is null)
        {
            return null;
        }

        var bookings = await dbContext.EventBookings
            .Include(booking => booking.Attendee)
            .Where(booking => booking.EventId == eventId)
            .OrderByDescending(booking => booking.CreatedAt)
            .ThenBy(booking => booking.Id)
            .ToListAsync(cancellationToken);

        var rows = bookings
            .Select(booking => new AdminBookingSummaryResponse(
                booking.Id,
                booking.Attendee?.UserName ?? "Removed account",
                booking.Attendee?.Email,
                booking.Quantity,
                booking.Status,
                booking.CreatedAt,
                booking.CancelledAt))
            .ToList();

        return new EventAttendanceResponse(
            calendarEvent.Id,
            calendarEvent.Slug,
            calendarEvent.Title,
            calendarEvent.StartsAt,
            calendarEvent.Capacity,
            calendarEvent.RegistrationCount,
            rows);
    }

    private static BookingResponse ToResponse(EventBooking booking, Event calendarEvent) => new(
        booking.Id,
        booking.EventId,
        calendarEvent.Slug,
        calendarEvent.Title,
        calendarEvent.StartsAt,
        booking.Quantity,
        booking.Status,
        booking.CreatedAt,
        booking.CancelledAt);

    private void Detach(EventBooking booking) =>
        dbContext.Entry(booking).State = EntityState.Detached;

    /// <summary>MySQL error 1062 is ER_DUP_ENTRY, matching the other write paths.</summary>
    internal static bool IsDuplicateKey(DbUpdateException exception) =>
        exception.InnerException is MySqlException { Number: 1062 };
}
