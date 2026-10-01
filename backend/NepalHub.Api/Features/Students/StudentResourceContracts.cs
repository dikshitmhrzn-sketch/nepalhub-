using System.ComponentModel.DataAnnotations;
using NepalHub.Api.Data.Entities;

namespace NepalHub.Api.Features.Students;

internal static class ResourceSlug
{
    /// <summary>Lowercase words separated by single hyphens.</summary>
    internal const string Pattern = "^[a-z0-9]+(?:-[a-z0-9]+)*$";
}

/// <summary>How the catalogue is ordered. Anything else is refused rather than defaulted.</summary>
public enum ResourceSort
{
    Newest = 0,
    Expiring = 1,
    Title = 2,
}

/// <summary>
/// One row on a catalogue card.
///
/// Note what is missing: the body. A card is one line of prose, and carrying the full text on
/// every row of a page would make the list several times larger for content the reader has not
/// asked for. The detail endpoint is where the body comes from, and the list is a single SQL
/// projection as a result.
/// </summary>
public sealed record ResourceSummaryResponse(
    Guid Id,
    string Slug,
    ResourceType Type,
    ResourceCategory Category,
    EducationLevel Level,
    string Title,
    string Summary,
    bool HasExternalUrl,
    DateOnly? ExpiresOn,
    DateTime UpdatedAt);

/// <summary>One resource in full, for a reader who has opened it.</summary>
public sealed record ResourceDetailResponse(
    Guid Id,
    string Slug,
    ResourceType Type,
    ResourceCategory Category,
    EducationLevel Level,
    string Title,
    string Summary,
    string Body,
    string? ExternalUrl,
    int? AcademicYear,
    string? Provider,
    decimal? AwardAmountNpr,
    string? Eligibility,
    DateOnly? ExpiresOn,
    bool IsExpired,
    DateTime UpdatedAt);

/// <summary>
/// The staff list row. Drafts and expired rows are both included here, which is the entire
/// difference from the public list, and neither the body nor the eligibility text is carried so
/// the list stays a single projection.
/// </summary>
public sealed record ResourceAdminSummaryResponse(
    Guid Id,
    string Slug,
    ResourceType Type,
    ResourceCategory Category,
    EducationLevel Level,
    string Title,
    bool IsPublished,
    bool IsExpired,
    DateOnly? ExpiresOn,
    DateTime UpdatedAt,
    string? CreatedByEmail);

/// <summary>
/// The staff view of one row, which also carries the reasons a draft cannot be published yet.
/// <see cref="PublishBlockers"/> is produced by the same validation the publish path uses, so the
/// editor can say what is missing instead of leaving staff to guess and discover it on save.
/// </summary>
public sealed record ResourceAdminResponse(
    Guid Id,
    string Slug,
    ResourceType Type,
    ResourceCategory Category,
    EducationLevel Level,
    string Title,
    string Summary,
    string Body,
    string? ExternalUrl,
    int? AcademicYear,
    string? Provider,
    decimal? AwardAmountNpr,
    string? Eligibility,
    DateOnly? ExpiresOn,
    bool IsExpired,
    bool IsPublished,
    DateTime CreatedAt,
    DateTime UpdatedAt,
    string? CreatedByEmail,
    IReadOnlyList<string> PublishBlockers);

/// <summary>Create and update share one shape, so the staff form sends the same body for both.</summary>
public sealed class SaveResourceRequest
{
    [Required(ErrorMessage = "A slug is required.")]
    [StringLength(160, MinimumLength = 2, ErrorMessage = "The slug must be 2 to 160 characters.")]
    [RegularExpression(
        ResourceSlug.Pattern,
        ErrorMessage = "Use lowercase words separated by single hyphens, for example quadratic-equations-grade-9.")]
    public string Slug { get; init; } = string.Empty;

    [Required(ErrorMessage = "A title is required.")]
    [StringLength(200, MinimumLength = 5, ErrorMessage = "The title must be 5 to 200 characters.")]
    public string Title { get; init; } = string.Empty;

    // The minimum lengths on these two are deliberately absent, and the maximums are only here
    // to stop an absurd value reaching a column. A draft with a title and nothing else is a
    // legitimate thing for staff to save, and the summary and body minimums are enforced on the
    // way to publishing instead, where they belong. This is the same split the quiz uses for its
    // explanation field.
    [StringLength(300, ErrorMessage = "The summary must be 300 characters or fewer.")]
    public string Summary { get; init; } = string.Empty;

    [StringLength(20000, ErrorMessage = "The body must be 20000 characters or fewer.")]
    public string Body { get; init; } = string.Empty;

    [EnumDataType(typeof(ResourceType), ErrorMessage = "Choose a valid resource type.")]
    public ResourceType Type { get; init; }

    [EnumDataType(typeof(ResourceCategory), ErrorMessage = "Choose a valid category.")]
    public ResourceCategory Category { get; init; }

    [EnumDataType(typeof(EducationLevel), ErrorMessage = "Choose a valid education level.")]
    public EducationLevel Level { get; init; }

    // The value itself is checked by ExternalUrlPolicy rather than by a RegularExpression,
    // because "is this a safe absolute https URL with a real host" is a parsing question rather
    // than a pattern question, and an approximation of it as a regex is how javascript: links
    // get through.
    [StringLength(500, ErrorMessage = "The link must be 500 characters or fewer.")]
    public string? ExternalUrl { get; init; }

    [Range(2000, 2100, ErrorMessage = "The academic year must be between 2000 and 2100.")]
    public int? AcademicYear { get; init; }

    [StringLength(160, ErrorMessage = "The provider must be 160 characters or fewer.")]
    public string? Provider { get; init; }

    [Range(0, 100_000_000, ErrorMessage = "The award amount must be zero or more.")]
    public decimal? AwardAmountNpr { get; init; }

    [StringLength(500, ErrorMessage = "The eligibility text must be 500 characters or fewer.")]
    public string? Eligibility { get; init; }

    public DateOnly? ExpiresOn { get; init; }

    public bool IsPublished { get; init; }
}
