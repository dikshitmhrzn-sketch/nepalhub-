namespace NepalHub.Api.Data.Entities;

/// <summary>
/// Something happening at a date and a place that a reader can plan around. One table, because
/// every event is searched, filtered, paged and expired identically, and a single instance with a
/// start and an end needs no children.
///
/// <para>
/// No recurrence table. A multi-day festival is one row with a wide date range, because the calendar
/// this phase draws is a plain list of start times: if a series later needs per-date sessions, that
/// is a table added when a real need for it exists, not a generalisation made now for it.
/// </para>
/// </summary>
public sealed class Event
{
    public Guid Id { get; set; }

    /// <summary>
    /// The public URL segment. Lowercase words joined by single hyphens and unique. It may be
    /// corrected while the row is a draft, and is frozen once published, because after that the slug
    /// is a live address people and search engines have recorded.
    /// </summary>
    public string Slug { get; set; } = string.Empty;

    public string Title { get; set; } = string.Empty;

    /// <summary>
    /// One line for the listing cards. The description is the full text, and carrying that on every
    /// row of a page of events would make the list several times larger for content the reader has
    /// not asked for.
    /// </summary>
    public string? Summary { get; set; }

    /// <summary>
    /// The event in full, stored and rendered as plain text. It is never treated as markup: the
    /// frontend renders it as text so a staff member's angle brackets cannot become elements.
    /// </summary>
    public string Description { get; set; } = string.Empty;

    public EventCategory Category { get; set; }

    /// <summary>
    /// Whoever is running the event, named in the row rather than linked to a table. An organiser is
    /// free text here because most of them are a school, a club or a one-off committee that will
    /// never be listed anywhere else, and a nullable link that is usually empty would only force
    /// staff to decide what to do with the other rows.
    /// </summary>
    public string? Organiser { get; set; }

    /// <summary>
    /// The business running or hosting this, when it belongs to one. Nullable and not required to be
    /// published, and set null on delete for the same reason as on <see cref="Deal.BusinessId"/>: an
    /// event readers have already planned around must not disappear because a listing was retracted.
    /// </summary>
    public Guid? BusinessId { get; set; }

    public Business? Business { get; set; }

    /// <summary>
    /// Where the event happens, as a district, read through to the province rather than stored
    /// beside it so the two can never disagree.
    /// <para>
    /// Required, unlike on a deal. A deal can be national and still be useful; an event with no
    /// location is not an event a reader can attend.
    /// </para>
    /// </summary>
    public Guid DistrictId { get; set; }

    public District? District { get; set; }

    /// <summary>
    /// The building, ground or hall. Free text because "Pokhara Stadium, Lakeside" and "the ground
    /// behind Jana Higher Secondary School" are both real answers and neither needs a table.
    /// </summary>
    public string? Venue { get; set; }

    /// <summary>
    /// When it starts, on the Nepal clock.
    /// <para>
    /// A timestamp and not a date, which is where this differs from a deal. A deal that ends at
    /// midnight has ended; an event that starts at seven in the evening has not, and a date-only
    /// column would drop it from the calendar during the afternoon it was due to happen.
    /// </para>
    /// </summary>
    public DateTime StartsAt { get; set; }

    /// <summary>
    /// When it finishes, on the Nepal clock, inclusive. Required rather than nullable: an event with
    /// no end cannot be aged off the public calendar, and "it is happening right now" is derived from
    /// this column. Compared against the current Nepal time, so an event ends at the same moment for
    /// every reader in the country.
    /// </summary>
    public DateTime EndsAt { get; set; }

    /// <summary>
    /// Where to register, when registration is needed. Always an absolute https URL with a real host;
    /// shape-checked on the way in and never fetched by the server.
    /// </summary>
    public string? RegistrationUrl { get; set; }

    /// <summary>
    /// How many places the organiser has, when they are counting. Null means the event does
    /// not count seats — most events need no more than a link — so an event with no capacity
    /// accepts bookings until it starts without anyone having to invent a number first.
    /// </summary>
    public int? Capacity { get; set; }

    /// <summary>
    /// Places already taken, maintained by an atomic increment rather than by counting
    /// bookings. The capacity check, the attendee list's total and the reader's "spots left"
    /// all read this one column, so two of them can never disagree about a sell-out.
    /// </summary>
    public int RegistrationCount { get; set; }

    /// <summary>
    /// Whether readers can see this row. Retracting an event is always allowed; deleting a published
    /// one is not, so the record of what was public is not simply lost.
    /// </summary>
    public bool IsPublished { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime UpdatedAt { get; set; }

    public Guid? CreatedByUserId { get; set; }

    public ApplicationUser? CreatedByUser { get; set; }
}