namespace NepalHub.Api.Data.Entities;

/// <summary>
/// What staff and the attendee see on the booking. Cancelling toggles the row rather
/// than removing it, so the record of who had a place survives a cancellation.
/// </summary>
public enum EventBookingStatus
{
    Confirmed,
    Cancelled,

    /// <summary>
    /// A place held while its payment is still open. The seat is latched the moment
    /// checkout starts so the provider can never confirm a purchase of something that was
    /// sold out in the meantime, and it is released if the payment fails.
    /// </summary>
    Pending,
}

/// <summary>
/// One person holding a place at one event.
///
/// <para>
/// The unique (event, attendee) index means a person has one row per event for all
/// time: booking again after a cancellation reactivates that row instead of adding a
/// second, so a place can never be held twice by the same account even from two tabs
/// racing each other.
/// </para>
///
/// <para>
/// Whether a place is actually available is decided by an atomic increment against
/// <see cref="Event.RegistrationCount"/>, not by counting rows here — the counter on the
/// event is what the capacity check and the reader's "12 spots left" both read, and two
/// queries that disagree about availability is how an event sells out twice.
/// </para>
/// </summary>
public sealed class EventBooking
{
    public Guid Id { get; set; }

    public Guid EventId { get; set; }

    public Event? Event { get; set; }

    public Guid AttendeeUserId { get; set; }

    public ApplicationUser? Attendee { get; set; }

    /// <summary>Places held. One to four in this phase; the column is a number for later.</summary>
    public int Quantity { get; set; }

    public EventBookingStatus Status { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime? CancelledAt { get; set; }
}
