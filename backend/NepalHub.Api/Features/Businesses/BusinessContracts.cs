using System.ComponentModel.DataAnnotations;
using NepalHub.Api.Common;
using NepalHub.Api.Data.Entities;
using NepalHub.Api.Features.Ownership;

namespace NepalHub.Api.Features.Businesses;

internal static class BusinessSlug
{
    /// <summary>Lowercase words separated by single hyphens.</summary>
    internal const string Pattern = "^[a-z0-9]+(?:-[a-z0-9]+)*$";
}

/// <summary>
/// Optional enum parsing, shared by the public and staff controllers.
///
/// It lives here rather than being copied into each controller because the job module has the same
/// helper, and a filter that silently matches nothing is a bug a visitor reports as "your search
/// is broken".
/// </summary>
internal static class BusinessFilters
{
    /// <summary>
    /// Parses an optional enum argument. A blank value means "no filter" or "the default", which is
    /// what both an absent and an empty query-string argument mean to a caller, and an
    /// unrecognised value is refused rather than defaulted so a typo cannot quietly widen a result
    /// set.
    /// </summary>
    internal static bool TryParseEnum<T>(string? raw, string name, out T? value, out string field)
        where T : struct, Enum
    {
        field = name;

        if (string.IsNullOrWhiteSpace(raw))
        {
            value = null;

            return true;
        }

        if (Enum.TryParse<T>(raw.Trim(), ignoreCase: true, out var parsed) && Enum.IsDefined(parsed))
        {
            value = parsed;

            return true;
        }

        value = null;

        return false;
    }

    /// <summary>
    /// Parses an optional boolean. Unlike an enum, an unrecognised value is refused rather than
    /// treated as false, because "verified" and "not verified" are different questions and a
    /// silently-defaulted filter would answer the wrong one.
    /// </summary>
    internal static bool TryParseBool(string? raw, string name, out bool? value, out string field)
    {
        field = name;

        if (string.IsNullOrWhiteSpace(raw))
        {
            value = null;

            return true;
        }

        if (bool.TryParse(raw.Trim(), out var parsed))
        {
            value = parsed;

            return true;
        }

        value = null;

        return false;
    }
}

/// <summary>How the public directory is ordered. Anything else is refused rather than defaulted.</summary>
public enum BusinessSort
{
    /// <summary>Alphabetical by name, which is how a person scans a list of places.</summary>
    Name = 0,

    Newest = 1,
}

/// <summary>Which slice of the directory the staff list is showing.</summary>
public enum BusinessAdminFilter
{
    All = 0,
    Published = 1,
    Draft = 2,
}

/// <summary>
/// One row on a business card.
///
/// Note what is missing: the description. A card is a name, a category and a place, and carrying
/// the full text on every row of a page would make the list several times larger for content the
/// reader has not asked for. The detail endpoint is where it comes from, and the list stays a
/// single SQL projection as a result.
/// <para>
/// <see cref="IsOpenNow"/> is derived from the opening hours and the current Nepal time on every
/// read and stored nowhere, because "open now" is not a property staff set: it stops being true
/// at half past nine on its own, and a stored flag would be wrong by lunchtime.
/// </para>
/// </summary>
public sealed record BusinessSummaryResponse(
    Guid Id,
    string Slug,
    string Name,
    BusinessCategory Category,
    string DistrictName,
    string ProvinceName,
    string? Address,
    string? Phone,
    bool IsVerified,
    bool? IsOpenNow,
    DateTime CreatedAt);

/// <summary>One weekday's hours. A closed day carries no times.</summary>
public sealed record BusinessHoursResponse(
    DayOfWeek DayOfWeek,
    bool IsClosed,
    TimeOnly? OpensAt,
    TimeOnly? ClosesAt);

/// <summary>One business in full, for a reader who has opened it.</summary>
/// <remarks>
/// <see cref="IsOwner"/> is the one per-visitor field: whether the caller manages this
/// listing, so the page can offer the owner's editor instead of a claim button. Derived by
/// comparing the loaded owner id against the caller and stored nowhere, because two readers
/// of one row must not disagree about who owns it.
/// </remarks>
public sealed record BusinessDetailResponse(
    Guid Id,
    string Slug,
    string Name,
    string Description,
    BusinessCategory Category,
    string DistrictName,
    string ProvinceName,
    string? Address,
    string? Phone,
    string? Email,
    string? Website,
    bool IsVerified,
    bool? IsOpenNow,
    IReadOnlyList<BusinessHoursResponse> OpeningHours,
    DateTime UpdatedAt,
    bool IsOwner);

/// <summary>
/// The staff list row. Drafts are included here, which is the entire difference from the public
/// list, and the description is not carried so the list stays a single projection.
/// </summary>
public sealed record BusinessAdminSummaryResponse(
    Guid Id,
    string Slug,
    string Name,
    BusinessCategory Category,
    string DistrictName,
    bool IsPublished,
    bool IsVerified,
    DateTime UpdatedAt,
    string? CreatedByEmail);

/// <summary>
/// The staff view of one row, which also carries the reasons a draft cannot be published yet.
/// <see cref="PublishBlockers"/> is produced by the same validation the publish path uses, so the
/// editor can say what is missing instead of leaving staff to guess and discover it on save.
/// </summary>
public sealed record BusinessAdminResponse(
    Guid Id,
    string Slug,
    string Name,
    string Description,
    BusinessCategory Category,
    string DistrictCode,
    string DistrictName,
    string ProvinceCode,
    string ProvinceName,
    string? Address,
    string? Phone,
    string? Email,
    string? Website,
    bool IsPublished,
    bool IsVerified,
    DateTime? VerifiedAt,
    IReadOnlyList<BusinessHoursResponse> OpeningHours,
    DateTime CreatedAt,
    DateTime UpdatedAt,
    string? CreatedByEmail,
    IReadOnlyList<string> PublishBlockers);

/// <summary>One weekday of hours as the staff form sends it.</summary>
public sealed class SaveBusinessHoursRequest
{
    [Range(0, 6, ErrorMessage = "Choose a day between Sunday and Saturday.")]
    public DayOfWeek DayOfWeek { get; init; }

    public bool IsClosed { get; init; }

    public TimeOnly? OpensAt { get; init; }

    public TimeOnly? ClosesAt { get; init; }
}

/// <summary>Create and update share one shape, so the staff form sends the same body for both.</summary>
public sealed class SaveBusinessRequest
{
    [Required(ErrorMessage = "A slug is required.")]
    [StringLength(160, MinimumLength = 2, ErrorMessage = "The slug must be 2 to 160 characters.")]
    [RegularExpression(
        BusinessSlug.Pattern,
        ErrorMessage = "Use lowercase words separated by single hyphens, for example newa-momo-house-lalitpur.")]
    public string Slug { get; init; } = string.Empty;

    [Required(ErrorMessage = "A business name is required.")]
    [StringLength(160, MinimumLength = 2, ErrorMessage = "The name must be 2 to 160 characters.")]
    public string Name { get; init; } = string.Empty;

    // The minimum length is deliberately absent and the maximum is here only to stop an absurd
    // value reaching a column. A draft with a name and nothing else is a legitimate thing for staff
    // to save; the minimum is enforced on the way to publishing instead, where it belongs.
    [StringLength(20000, ErrorMessage = "The description must be 20000 characters or fewer.")]
    public string Description { get; init; } = string.Empty;

    [EnumDataType(typeof(BusinessCategory), ErrorMessage = "Choose a valid category.")]
    public BusinessCategory Category { get; init; }

    /// <summary>
    /// A district reference code such as <c>bagmati-kathmandu</c>. A code rather than an id so the
    /// staff form posts the same thing the public reference endpoints hand out, and so a listing
    /// cannot be saved against a district that does not exist. Required, because a directory of
    /// local businesses with no location cannot be browsed.
    /// </summary>
    [Required(ErrorMessage = "Choose the district this business is in.")]
    [StringLength(80, ErrorMessage = "The district code must be 80 characters or fewer.")]
    public string? DistrictCode { get; init; }

    [StringLength(240, ErrorMessage = "The address must be 240 characters or fewer.")]
    public string? Address { get; init; }

    [StringLength(20, ErrorMessage = "The phone number must be 20 characters or fewer.")]
    public string? Phone { get; init; }

    [StringLength(254, ErrorMessage = "The email address must be 254 characters or fewer.")]
    public string? Email { get; init; }

    // The value itself is checked by ExternalUrlPolicy rather than by a RegularExpression,
    // because "is this a safe absolute https URL with a real host" is a parsing question rather
    // than a pattern question, and an approximation of it as a regex is how javascript: links get
    // through.
    [StringLength(500, ErrorMessage = "The website must be 500 characters or fewer.")]
    public string? Website { get; init; }

    public bool IsPublished { get; init; }

    public bool IsVerified { get; init; }

    /// <summary>
    /// At most seven rows. The service replaces the whole set on save, so there is no partial
    /// update to get out of step, and the composite primary key guarantees the database never holds
    /// two rows for the same day.
    /// </summary>
    public IReadOnlyList<SaveBusinessHoursRequest> OpeningHours { get; init; } = [];
}

/// <summary>
/// What an owner sends to register a listing of their own. The same fields as the staff
/// save minus the two flags, because publish and verify are decisions this request is not
/// allowed to express at all: a body that carries <c>isPublished</c> and is then ignored
/// teaches a client to trust a field the server never reads.
///
/// <para>
/// Everything else is held to the staff rules — the same slug pattern, district list, phone
/// shape and opening-week rules — so a listing that arrives through registration is
/// identical to one staff created, and the publish blockers are the only difference between
/// a draft and a live row.
/// </para>
/// </summary>
public sealed class RegisterBusinessRequest
{
    [Required(ErrorMessage = "A slug is required.")]
    [StringLength(160, MinimumLength = 2, ErrorMessage = "The slug must be 2 to 160 characters.")]
    [RegularExpression(
        BusinessSlug.Pattern,
        ErrorMessage = "Use lowercase words separated by single hyphens, for example newa-momo-house-lalitpur.")]
    public string Slug { get; init; } = string.Empty;

    [Required(ErrorMessage = "A business name is required.")]
    [StringLength(160, MinimumLength = 2, ErrorMessage = "The name must be 2 to 160 characters.")]
    public string Name { get; init; } = string.Empty;

    [StringLength(20000, ErrorMessage = "The description must be 20000 characters or fewer.")]
    public string Description { get; init; } = string.Empty;

    [EnumDataType(typeof(BusinessCategory), ErrorMessage = "Choose a valid category.")]
    public BusinessCategory Category { get; init; }

    [Required(ErrorMessage = "Choose the district this business is in.")]
    [StringLength(80, ErrorMessage = "The district code must be 80 characters or fewer.")]
    public string? DistrictCode { get; init; }

    [StringLength(240, ErrorMessage = "The address must be 240 characters or fewer.")]
    public string? Address { get; init; }

    [StringLength(20, ErrorMessage = "The phone number must be 20 characters or fewer.")]
    public string? Phone { get; init; }

    [StringLength(254, ErrorMessage = "The email address must be 254 characters or fewer.")]
    public string? Email { get; init; }

    [StringLength(500, ErrorMessage = "The website must be 500 characters or fewer.")]
    public string? Website { get; init; }

    public IReadOnlyList<SaveBusinessHoursRequest> OpeningHours { get; init; } = [];

    /// <summary>The registration as the staff rules already understand it: a draft, never live.</summary>
    internal SaveBusinessRequest AsDraft() => new()
    {
        Slug = Slug,
        Name = Name,
        Description = Description,
        Category = Category,
        DistrictCode = DistrictCode,
        Address = Address,
        Phone = Phone,
        Email = Email,
        Website = Website,
        IsPublished = false,
        IsVerified = false,
        OpeningHours = OpeningHours,
    };
}

/// <summary>How an owner's registration ended.</summary>
public enum RegisterStatus
{
    Created,

    /// <summary>Shape or content rules were not met, reported as field errors.</summary>
    Invalid,

    /// <summary>Another row already owns that slug.</summary>
    SlugTaken,
}

public sealed record RegisterOutcome(
    RegisterStatus Status,
    OwnerBusinessResponse? Business = null,
    IReadOnlyList<FieldError>? FieldErrors = null,
    string? Message = null);
