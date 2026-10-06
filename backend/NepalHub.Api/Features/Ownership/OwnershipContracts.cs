using System.ComponentModel.DataAnnotations;
using NepalHub.Api.Common;
using NepalHub.Api.Data.Entities;
using NepalHub.Api.Features.Businesses;

namespace NepalHub.Api.Features.Ownership;

/// <summary>
/// Optional claim-status parsing for the staff queue, kept to the same shape as the report
/// queue's parser: a blank or "all" means no filter, and an unrecognised value is refused
/// rather than defaulted, so a typo cannot quietly return the whole table.
/// </summary>
internal static class ClaimFilters
{
    internal static bool TryParseStatus(string? raw, out BusinessClaimStatus? status, out string field)
    {
        field = "status";
        status = null;

        if (string.IsNullOrWhiteSpace(raw))
        {
            return true;
        }

        var trimmed = raw.Trim();

        if (trimmed.Equals("all", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        if (Enum.TryParse<BusinessClaimStatus>(trimmed, ignoreCase: true, out var parsed) && Enum.IsDefined(parsed))
        {
            status = parsed;

            return true;
        }

        return false;
    }
}

/// <summary>
/// One claim as the person who filed it sees it, from the account page's list. The business
/// name rides along so a row of claims reads as listings rather than as bare ids.
/// </summary>
public sealed record ClaimResponse(
    Guid Id,
    Guid BusinessId,
    string Slug,
    string BusinessName,
    BusinessClaimStatus Status,
    string? Evidence,
    DateTime CreatedAt,
    DateTime? ReviewedAt,
    string? ReviewNote);

/// <summary>
/// What a claim says: how the claimant is connected to the listing, in their own words.
/// Optional, because "I am the owner" is a legitimate claim and staff decide on the evidence
/// either way.
/// </summary>
public sealed class ClaimBusinessRequest
{
    [StringLength(500, ErrorMessage = "The note must be 500 characters or fewer.")]
    public string? Evidence { get; init; }
}

/// <summary>One row of the staff queue: the listing, who claims it, and where it stands.</summary>
public sealed record AdminClaimSummaryResponse(
    Guid Id,
    Guid BusinessId,
    string Slug,
    string BusinessName,
    string ClaimantName,
    string? ClaimantEmail,
    BusinessClaimStatus Status,
    string? Evidence,
    DateTime CreatedAt,
    DateTime? ReviewedAt,
    string? ReviewNote);

/// <summary>The note staff attach when deciding. Shared by approve and reject.</summary>
public sealed class CloseClaimRequest
{
    [StringLength(500, ErrorMessage = "The note must be 500 characters or fewer.")]
    public string? ReviewNote { get; init; }
}

/// <summary>The account page's ownership screen: the listings this person manages, every claim they have filed, and every verification they have asked for.</summary>
public sealed record MyBusinessesResponse(
    IReadOnlyList<MyBusinessSummary> Owned,
    IReadOnlyList<ClaimResponse> Claims,
    IReadOnlyList<VerificationResponse> Verifications);

/// <summary>How the owner's save ended.</summary>
public enum OwnerEditStatus
{
    Updated,

    /// <summary>No listing at that address belongs to this caller.</summary>
    NotFound,

    /// <summary>Shape or content rules were not met, reported as field errors.</summary>
    Invalid,

    /// <summary>Another row already owns the slug this draft was renamed to.</summary>
    SlugTaken,
}

public sealed record OwnerSaveOutcome(
    OwnerEditStatus Status,
    OwnerBusinessResponse? Business = null,
    IReadOnlyList<FieldError>? FieldErrors = null,
    string? Message = null);

/// <summary>One managed listing. The description is left out for the same reason the public card leaves it out.</summary>
public sealed record MyBusinessSummary(
    Guid Id,
    string Slug,
    string Name,
    BusinessCategory Category,
    string DistrictName,
    bool IsPublished,
    bool IsVerified);

/// <summary>
/// One owned listing in the shape the owner's editor sends back: every field the owner may
/// touch, plus the ones they may only read. Publish and verify state are shown read-only,
/// because those are staff decisions the owner cannot change from this screen, and
/// <c>Verification</c> is where their own request stands if they have filed one.
/// </summary>
public sealed record OwnerBusinessResponse(
    Guid Id,
    string Slug,
    string Name,
    string Description,
    BusinessCategory Category,
    string DistrictCode,
    string DistrictName,
    string? Address,
    string? Phone,
    string? Email,
    string? Website,
    bool IsPublished,
    bool IsVerified,
    IReadOnlyList<BusinessHoursResponse> OpeningHours,
    DateTime UpdatedAt,
    VerificationResponse? Verification);

/// <summary>
/// The owner's editor body. The identity fields — slug, name, category and district — are
/// here only for a listing that is not published: a draft's owner can correct a typo they
/// made while registering, and once the row is live its address and its facts are frozen to
/// staff, exactly as the staff editor freezes them. The publish flag and the verify badge are
/// absent entirely, because those are staff decisions an owner may never express.
/// </summary>
public sealed class SaveOwnerBusinessRequest
{
    /// <summary>Leave null to keep the slug as it is.</summary>
    [StringLength(160, MinimumLength = 2, ErrorMessage = "The slug must be 2 to 160 characters.")]
    [RegularExpression(
        BusinessSlug.Pattern,
        ErrorMessage = "Use lowercase words separated by single hyphens, for example newa-momo-house-lalitpur.")]
    public string? Slug { get; init; }

    /// <summary>Leave null to keep the name as it is.</summary>
    [StringLength(160, MinimumLength = 2, ErrorMessage = "The name must be 2 to 160 characters.")]
    public string? Name { get; init; }

    /// <summary>Leave null to keep the category as it is.</summary>
    [EnumDataType(typeof(BusinessCategory), ErrorMessage = "Choose a valid category.")]
    public BusinessCategory? Category { get; init; }

    /// <summary>A district reference code such as <c>bagmati-kathmandu</c>. Leave null to keep the district as it is.</summary>
    [StringLength(80, ErrorMessage = "The district code must be 80 characters or fewer.")]
    public string? DistrictCode { get; init; }

    [StringLength(20000, ErrorMessage = "The description must be 20000 characters or fewer.")]
    public string Description { get; init; } = string.Empty;

    [StringLength(240, ErrorMessage = "The address must be 240 characters or fewer.")]
    public string? Address { get; init; }

    [StringLength(20, ErrorMessage = "The phone number must be 20 characters or fewer.")]
    public string? Phone { get; init; }

    [StringLength(254, ErrorMessage = "The email address must be 254 characters or fewer.")]
    public string? Email { get; init; }

    [StringLength(500, ErrorMessage = "The website must be 500 characters or fewer.")]
    public string? Website { get; init; }

    /// <summary>
    /// The whole week, exactly as the staff editor sends it: the service replaces the set
    /// rather than diffing it, so a partial week would silently erase the missing days.
    /// </summary>
    public IReadOnlyList<SaveBusinessHoursRequest> OpeningHours { get; init; } = [];
}
