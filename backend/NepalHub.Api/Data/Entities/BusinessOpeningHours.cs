namespace NepalHub.Api.Data.Entities;

/// <summary>
/// One weekday's opening hours for a business. At most one row per day per business, which the
/// composite primary key enforces rather than trusting the service to check.
///
/// A child table rather than a JSON column, a text blob or seven nullable columns on the parent,
/// because all three eventually get a partial update that leaves one day out of step with the other
/// six. It also buys "is it open now" for free, which a single string cannot answer.
/// <para>
/// A day is either open (with an opening and a closing time) or marked closed. There is no third
/// state: an open day with no times is a mistake, and a closed day with times is a contradiction.
/// </para>
/// </summary>
public sealed class BusinessOpeningHours
{
    public Guid BusinessId { get; set; }

    public Business? Business { get; set; }

    /// <summary>
    /// The day this row describes, matching <see cref="System.DayOfWeek"/>: 0 is Sunday and 6 is
    /// Saturday. Stored as an int so the value is compact and the range is enforced by the service,
    /// which is also the only place that turns it into a label.
    /// </summary>
    public DayOfWeek DayOfWeek { get; set; }

    /// <summary>
    /// Whether the business does not open at all on this day. Needed because "closed" is not the
    /// absence of hours: a Sunday-only closure is a real schedule, and inferring it from missing
    /// rows would make a business that only lists its busy days look like it opens every day.
    /// </summary>
    public bool IsClosed { get; set; }

    /// <summary>When it opens, Nepal local time. Null on a closed day.</summary>
    public TimeOnly? OpensAt { get; set; }

    /// <summary>
    /// When it closes, Nepal local time. Null on a closed day.
    /// <para>
    /// Earlier than <see cref="OpensAt"/> is allowed and means the closing time falls on the
    /// following day: a bar that opens at 22:00 and empties at 02:00 is an ordinary Nepal
    /// schedule, and treating it as invalid would make the hours useless for exactly the
    /// businesses most likely to state them.
    /// </para>
    /// </summary>
    public TimeOnly? ClosesAt { get; set; }
}