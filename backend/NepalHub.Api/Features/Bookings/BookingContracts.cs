using System.ComponentModel.DataAnnotations;
using NepalHub.Api.Data.Entities;

namespace NepalHub.Api.Features.Bookings;

/// <summary>
/// Taking places at an event. The body is optional — an empty one means one place — so the
/// plainest possible request is a reader who simply wants a seat.
/// </summary>
public sealed class BookEventRequest
{
    /// <summary>
    /// One to four places. The bound is enforced as a field error rather than by rejecting
    /// the request wholesale, so the stepper on the page can say what went wrong.
    /// </summary>
    [Range(1, 4, ErrorMessage = "You can book one to four places at a time.")]
    public int Quantity { get; init; } = 1;
}

/// <summary>One booking as the attendee's screen shows it.</summary>
public sealed record BookingResponse(
    Guid Id,
    Guid EventId,
    string EventSlug,
    string Title,
    DateTime StartsAt,
    int Quantity,
    EventBookingStatus Status,
    DateTime CreatedAt,
    DateTime? CancelledAt);

/// <summary>One attendee on the staff list.</summary>
public sealed record AdminBookingSummaryResponse(
    Guid Id,
    string AttendeeName,
    string? AttendeeEmail,
    int Quantity,
    EventBookingStatus Status,
    DateTime CreatedAt,
    DateTime? CancelledAt);

/// <summary>
/// Who is coming to one event: the live counter the reader sees plus the rows behind it,
/// cancelled rows included, because "someone cancelled" is a question staff ask as often as
/// "who is coming".
/// </summary>
public sealed record EventAttendanceResponse(
    Guid EventId,
    string Slug,
    string Title,
    DateTime StartsAt,
    int? Capacity,
    int RegistrationCount,
    IReadOnlyList<AdminBookingSummaryResponse> Bookings);
