using System.ComponentModel.DataAnnotations;
using NepalHub.Api.Data.Entities;

namespace NepalHub.Api.Features.Events;

/// <summary>How the public calendar is ordered. Anything else is refused rather than defaulted.</summary>
public enum EventSort
{
    /// <summary>
    /// Soonest start first, which is what a calendar is for. An event already under way sorts ahead of
    /// next week's, because the reader who opens this page today is the one who can still get to it.
    /// </summary>
    Soonest = 0,

    /// <summary>Most recently added first.</summary>
    Newest = 1,
}

/// <summary>Which slice of the calendar the staff screen is showing.</summary>
public enum EventAdminFilter
{
    All = 0,
    Published = 1,
    Draft = 2,
}

/// <summary>
/// One row on an event card.
///
/// Note what is missing: the description, for the same reason the offers and business cards omit
/// theirs. A card is what, where, when and whether it is free, and a page of cards is read by
/// scanning rather than reading.
///
/// <para>
/// <see cref="IsOngoing"/> is derived from the current Nepal time on every read and stored nowhere.
/// "Happening now" is not a property staff set: it stops being true at the finishing time on its own,
/// and a stored flag would be wrong by the evening.
/// </para>
/// </summary>
public sealed record EventSummaryResponse(
    Guid Id,
    string Slug,
    string Title,
    string? Summary,
    EventCategory Category,
    string? Organiser,
    string? BusinessName,
    string DistrictName,
    string ProvinceName,
    string? Venue,
    DateTime StartsAt,
    DateTime EndsAt,
    bool IsOngoing,
    DateTime CreatedAt);

/// <summary>One event in full, for a reader who has opened it.</summary>
public sealed record EventDetailResponse(
    Guid Id,
    string Slug,
    string Title,
    string? Summary,
    string Description,
    EventCategory Category,
    string? Organiser,
    string? BusinessName,
    string? BusinessSlug,
    string DistrictName,
    string ProvinceName,
    string? Venue,
    DateTime StartsAt,
    DateTime EndsAt,
    bool IsOngoing,
    string? RegistrationUrl,

    /// <summary>Places this event can hold, or null for an event with no cap.</summary>
    int? Capacity,

    /// <summary>Places currently held. Derived from the same counter the booking path latches.</summary>
    int RegistrationCount,

    /// <summary>
    /// Spots left, or null when the event is uncapped. Never negative: a race the counter
    /// lost shows as zero, not as minus one, so the reader is never asked to divide by a
    /// number the page itself produced.
    /// </summary>
    int? RemainingSpots,

    /// <summary>
    /// What one place costs, in whole rupees. Zero is the ordinary case and means free —
    /// the reader is asked for money only when this is above zero, and the checkout path
    /// reads the same column, so the button and the charge cannot disagree.
    /// </summary>
    int TicketPrice,

    DateTime UpdatedAt);

/// <summary>
/// The staff list row. Drafts and finished events are both included, which is the entire difference
/// from the public list. A finished event stays in the staff board rather than disappearing, because
/// the question a staff member brings to this screen is usually "why is my event not showing", and the
/// answer is often that it ended.
/// </summary>
public sealed record EventAdminSummaryResponse(
    Guid Id,
    string Slug,
    string Title,
    EventCategory Category,
    string DistrictName,
    bool IsPublished,
    bool IsFinished,
    DateTime StartsAt,
    DateTime UpdatedAt,
    string? CreatedByEmail);

/// <summary>
/// The staff view of one row, which also carries the reasons a draft cannot be published yet.
/// <see cref="PublishBlockers"/> comes from the same validation the publish path enforces, so the
/// editor and the server cannot drift apart.
/// </summary>
public sealed record EventAdminResponse(
    Guid Id,
    string Slug,
    string Title,
    string? Summary,
    string Description,
    EventCategory Category,
    string? Organiser,
    string? BusinessSlug,
    string? DistrictCode,
    string? DistrictName,
    string? ProvinceCode,
    string? ProvinceName,
    string? Venue,
    DateTime StartsAt,
    DateTime EndsAt,
    string? RegistrationUrl,
    int? Capacity,
    int RegistrationCount,

    /// <summary>What one place costs in whole rupees; zero for the free events every event was before.</summary>
    int TicketPrice,

    bool IsPublished,
    bool IsFinished,
    DateTime CreatedAt,
    DateTime UpdatedAt,
    string? CreatedByEmail,
    IReadOnlyList<string> PublishBlockers);

/// <summary>Create and update share one shape, so the staff form sends the same body for both.</summary>
public sealed class SaveEventRequest
{
    [Required(ErrorMessage = "A slug is required.")]
    [StringLength(160, MinimumLength = 2, ErrorMessage = "The slug must be 2 to 160 characters.")]
    [RegularExpression(
        EventSlug.Pattern,
        ErrorMessage = "Use lowercase words separated by single hyphens, for example pokhara-paragliding-briefing.")]
    public string Slug { get; init; } = string.Empty;

    [Required(ErrorMessage = "An event title is required.")]
    [StringLength(200, MinimumLength = 2, ErrorMessage = "The title must be 2 to 200 characters.")]
    public string Title { get; init; } = string.Empty;

    // The minimum is deliberately absent and the maximum is here only to stop an absurd value reaching
    // a column. A draft with a title and nothing else is a legitimate thing to save; the minimum is
    // enforced on the way to publishing instead.
    [StringLength(400, ErrorMessage = "The summary must be 400 characters or fewer.")]
    public string? Summary { get; init; }

    [StringLength(20000, ErrorMessage = "The description must be 20000 characters or fewer.")]
    public string Description { get; init; } = string.Empty;

    [EnumDataType(typeof(EventCategory), ErrorMessage = "Choose a valid category.")]
    public EventCategory Category { get; init; }

    [StringLength(160, ErrorMessage = "The organiser must be 160 characters or fewer.")]
    public string? Organiser { get; init; }

    /// <summary>
    /// The slug of a listed business running or hosting this. Optional and not required to be
    /// published, for the same reason as on an offer: the event can be listed before the business is.
    /// </summary>
    [StringLength(160, ErrorMessage = "The business slug must be 160 characters or fewer.")]
    public string? BusinessSlug { get; init; }

    /// <summary>
    /// A district reference code. Required, unlike on an offer: a reader cannot attend something with
    /// no location, so an event with no district has nothing to browse by.
    /// </summary>
    [Required(ErrorMessage = "Choose the district this event is in.")]
    [StringLength(80, ErrorMessage = "The district code must be 80 characters or fewer.")]
    public string? DistrictCode { get; init; }

    [StringLength(240, ErrorMessage = "The venue must be 240 characters or fewer.")]
    public string? Venue { get; init; }

    /// <summary>
    /// Checked by ExternalUrlPolicy rather than a RegularExpression, because "is this a safe absolute
    /// https URL with a real host" is a parsing question, and an approximation of it as a regex is how
    /// javascript: links get through.
    /// </summary>
    [StringLength(500, ErrorMessage = "The registration link must be 500 characters or fewer.")]
    public string? RegistrationUrl { get; init; }

    /// <summary>
    /// How many places can be held, or null for an event with no cap. The booking latch reads
    /// this column directly, so "no capacity" means unlimited places rather than zero — and a
    /// zero would be an event nobody is allowed to attend, so it is refused rather than stored.
    /// </summary>
    [Range(1, 1_000_000, ErrorMessage = "Capacity must be at least one place. Leave it empty for an event with no cap.")]
    public int? Capacity { get; init; }

    /// <summary>
    /// What one place costs, in whole rupees. Zero — the default — keeps the event free and
    /// the booking path exactly as it was; anything above it puts a checkout in front of the
    /// booking button and holds the seat until the payment answers.
    /// </summary>
    [Range(0, 1_000_000, ErrorMessage = "The ticket price cannot be negative.")]
    public int TicketPrice { get; init; }

    [Required(ErrorMessage = "Choose when the event starts.")]
    public DateTime? StartsAt { get; init; }

    [Required(ErrorMessage = "Choose when the event ends.")]
    public DateTime? EndsAt { get; init; }

    public bool IsPublished { get; init; }
}

/// <summary>
/// A business an event can be attached to, for the staff editor's picker. Slug and name rather than an
/// id, because the choice being made is "this business" and nobody picks a business by GUID.
/// </summary>
public sealed record EventBusinessOptionResponse(string Slug, string Name);

/// <summary>Lowercase words separated by single hyphens, shared by the events save path.</summary>
internal static class EventSlug
{
    internal const string Pattern = "^[a-z0-9]+(?:-[a-z0-9]+)*$";
}