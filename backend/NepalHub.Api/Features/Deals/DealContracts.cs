using System.ComponentModel.DataAnnotations;
using NepalHub.Api.Data.Entities;

namespace NepalHub.Api.Features.Deals;

/// <summary>How the public offers page is ordered. Anything else is refused rather than defaulted.</summary>
public enum DealSort
{
    /// <summary>
    /// Soonest end date first, which is what a reader wants from an offers page: the deals about to
    /// expire are the ones worth acting on today.
    /// </summary>
    EndingSoon = 0,

    /// <summary>Most recently added first, for readers who want to see what is new.</summary>
    Newest = 1,
}

/// <summary>Which slice of the offers list the staff screen is showing.</summary>
public enum DealAdminFilter
{
    All = 0,
    Published = 1,
    Draft = 2,
}

/// <summary>
/// One row on an offer card.
///
/// Note what is missing: the description. A card is the offer, the business and how long it has left,
/// and carrying the full terms on every row of a page would make the list several times larger for
/// content the reader has not asked for. The detail endpoint is where it comes from.
///
/// <para>
/// <see cref="BusinessName"/> is the business's current name when one is linked, and blank otherwise.
/// It is carried here so a card needs no second request, and so an offer still reads correctly after
/// its business has been retracted — the foreign key is set null on delete for exactly that case.
/// </para>
/// </summary>
public sealed record DealSummaryResponse(
    Guid Id,
    string Slug,
    string Title,
    string? Summary,
    string? DiscountText,
    string? BusinessName,
    string? BusinessSlug,
    string DistrictName,
    string ProvinceName,
    DateOnly StartsOn,
    DateOnly EndsOn,
    int DaysRemaining,
    DateTime CreatedAt);

/// <summary>One offer in full, for a reader who has opened it.</summary>
public sealed record DealDetailResponse(
    Guid Id,
    string Slug,
    string Title,
    string? Summary,
    string Description,
    string? DiscountText,
    string? BusinessName,
    string? BusinessSlug,
    string? DistrictName,
    string? ProvinceName,
    string? RedemptionUrl,
    DateOnly StartsOn,
    DateOnly EndsOn,
    int DaysRemaining,
    bool IsRunning,
    DateTime UpdatedAt);

/// <summary>
/// The staff list row. Drafts and expired offers are both included here, which is the entire
/// difference from the public list. An expired offer stays in the staff board rather than vanishing,
/// because a staff member looking at "why is this not showing" needs to see it and needs to see that
/// it ended rather than that it vanished.
/// </summary>
public sealed record DealAdminSummaryResponse(
    Guid Id,
    string Slug,
    string Title,
    string? DistrictName,
    string? BusinessName,
    bool IsPublished,
    bool IsExpired,
    DateOnly EndsOn,
    DateTime UpdatedAt,
    string? CreatedByEmail);

/// <summary>
/// The staff view of one row, which also carries the reasons a draft cannot be published yet.
/// <see cref="PublishBlockers"/> comes from the same validation the publish path enforces, so the
/// editor can say what is missing instead of leaving staff to find out on save.
/// </summary>
public sealed record DealAdminResponse(
    Guid Id,
    string Slug,
    string Title,
    string? Summary,
    string Description,
    string? DiscountText,
    string? BusinessSlug,
    string? DistrictCode,
    string? DistrictName,
    string? ProvinceCode,
    string? ProvinceName,
    string? RedemptionUrl,
    DateOnly StartsOn,
    DateOnly EndsOn,
    bool IsPublished,
    bool IsExpired,
    DateTime CreatedAt,
    DateTime UpdatedAt,
    string? CreatedByEmail,
    IReadOnlyList<string> PublishBlockers);

/// <summary>Create and update share one shape, so the staff form sends the same body for both.</summary>
public sealed class SaveDealRequest
{
    [Required(ErrorMessage = "A slug is required.")]
    [StringLength(160, MinimumLength = 2, ErrorMessage = "The slug must be 2 to 160 characters.")]
    [RegularExpression(
        DealSlug.Pattern,
        ErrorMessage = "Use lowercase words separated by single hyphens, for example free-chiya-with-thakali-set.")]
    public string Slug { get; init; } = string.Empty;

    [Required(ErrorMessage = "An offer title is required.")]
    [StringLength(200, MinimumLength = 2, ErrorMessage = "The title must be 2 to 200 characters.")]
    public string Title { get; init; } = string.Empty;

    // The minimum is deliberately absent and the maximum is here only to stop an absurd value
    // reaching a column. A draft with a title and nothing else is a legitimate thing to save; the
    // minimum is enforced on the way to publishing instead, where it belongs.
    [StringLength(400, ErrorMessage = "The summary must be 400 characters or fewer.")]
    public string? Summary { get; init; }

    [StringLength(20000, ErrorMessage = "The description must be 20000 characters or fewer.")]
    public string Description { get; init; } = string.Empty;

    /// <summary>
    /// The offer in the business's own words. Required when publishing but not while drafting, which
    /// is why the minimum and the presence check live in the publish rules rather than here.
    /// </summary>
    [StringLength(120, ErrorMessage = "The offer must be 120 characters or fewer.")]
    public string? DiscountText { get; init; }

    /// <summary>
    /// The slug of a listed business this offer belongs to. Optional, and deliberately not required to
    /// be published — most offers exist before the business is listed here, and demanding a published
    /// business would block the only order in which staff can work: write the business, then its
    /// offers.
    /// </summary>
    [StringLength(160, ErrorMessage = "The business slug must be 160 characters or fewer.")]
    public string? BusinessSlug { get; init; }

    /// <summary>
    /// A district reference code such as <c>bagmati-kathmandu</c>. A code rather than an id so the
    /// staff form posts the same thing the public reference endpoints hand out. Optional here,
    /// because an offer can genuinely be nationwide — that is the case that separates an offer from a
    /// business listing.
    /// </summary>
    [StringLength(80, ErrorMessage = "The district code must be 80 characters or fewer.")]
    public string? DistrictCode { get; init; }

    // Checked by ExternalUrlPolicy rather than a RegularExpression, because "is this a safe absolute
    // https URL with a real host" is a parsing question, and an approximation of it as a regex is how
    // javascript: links get through.
    [StringLength(500, ErrorMessage = "The redemption link must be 500 characters or fewer.")]
    public string? RedemptionUrl { get; init; }

    [Required(ErrorMessage = "Choose the day the offer starts.")]
    public DateOnly? StartsOn { get; init; }

    [Required(ErrorMessage = "Choose the day the offer ends.")]
    public DateOnly? EndsOn { get; init; }

    public bool IsPublished { get; init; }
}

/// <summary>
/// A business an offer can be attached to, for the staff editor's picker. Slug and name rather than an
/// id, because the choice being made is "this business" and nobody picks a business by GUID.
/// </summary>
public sealed record DealBusinessOptionResponse(string Slug, string Name);

/// <summary>Lowercase words separated by single hyphens, shared by the deals save path.</summary>
internal static class DealSlug
{
    internal const string Pattern = "^[a-z0-9]+(?:-[a-z0-9]+)*$";
}