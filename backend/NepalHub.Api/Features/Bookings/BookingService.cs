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

        await dbContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        return new CancelOutcome(CancelBookingStatus.Cancelled, ToResponse(booking, booking.Event!));
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
    private static bool IsDuplicateKey(DbUpdateException exception) =>
        exception.InnerException is MySqlException { Number: 1062 };
}
