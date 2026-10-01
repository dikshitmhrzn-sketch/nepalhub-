using System.ComponentModel.DataAnnotations;
using NepalHub.Api.Data.Entities;

namespace NepalHub.Api.Features.Jobs;

internal static class JobSlug
{
    /// <summary>Lowercase words separated by single hyphens.</summary>
    internal const string Pattern = "^[a-z0-9]+(?:-[a-z0-9]+)*$";
}

/// <summary>
/// Query-string parsing shared by the public and staff controllers.
///
/// It lives here rather than being copied into each controller because the resource module has the
/// same helper in both of its files, and a board is exactly as easy to get wrong: a filter that
/// silently matches nothing is a bug a visitor reports as "your search is broken".
/// </summary>
internal static class JobFilters
{
    /// <summary>
    /// Parses an optional enum argument. A blank value means "no filter" or "the default", which
    /// is what both an absent and an empty query-string argument mean to a caller, and an
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
}

/// <summary>How the public board is ordered. Anything else is refused rather than defaulted.</summary>
public enum JobSort
{
    Newest = 0,
    Deadline = 1,
}

/// <summary>
/// What a reader needs to be told about a posting's deadline, beyond the date itself.
///
/// This is derived from the deadline and the current Nepal date on every read and stored nowhere,
/// because "closing soon" is not a property a staff member sets — it stops being true on its own
/// as the deadline approaches, and a stored flag would be wrong the following morning.
/// </summary>
public enum JobStatus
{
    /// <summary>More than the closing-soon window away.</summary>
    Open = 0,

    /// <summary>The deadline falls within the closing-soon window, so it needs saying once.</summary>
    ClosingSoon = 1,
}

/// <summary>Which slice of the board the staff list is showing.</summary>
public enum JobAdminFilter
{
    All = 0,
    Published = 1,
    Draft = 2,

    /// <summary>Published or not, but past its deadline. The only filter a public reader has no equivalent of.</summary>
    Expired = 3,
}

/// <summary>
/// One row on a job card.
///
/// Note what is missing: the description. A card is a title, an employer and a deadline, and
/// carrying the full posting text on every row of a page would make the list several times larger
/// for content the reader has not asked for. The detail endpoint is where it comes from, and the
/// list stays a single SQL projection as a result.
/// </summary>
public sealed record JobSummaryResponse(
    Guid Id,
    string Slug,
    string Title,
    string Organization,
    JobCategory Category,
    EmploymentType EmploymentType,
    WorkMode WorkMode,
    string? DistrictName,
    string? ProvinceName,
    DateOnly ApplicationDeadline,
    JobStatus Status,
    DateTime CreatedAt);

/// <summary>One posting in full, for a reader who has opened it.</summary>
public sealed record JobDetailResponse(
    Guid Id,
    string Slug,
    string Title,
    string Organization,
    string Description,
    JobCategory Category,
    EmploymentType EmploymentType,
    WorkMode WorkMode,
    string? DistrictName,
    string? ProvinceName,
    string ApplicationUrl,
    DateOnly ApplicationDeadline,
    JobStatus Status,
    DateTime UpdatedAt);

/// <summary>
/// The staff list row. Drafts and expired rows are both included here, which is the entire
/// difference from the public list, and the description is not carried so the list stays a single
/// projection.
/// </summary>
public sealed record JobAdminSummaryResponse(
    Guid Id,
    string Slug,
    string Title,
    string Organization,
    JobCategory Category,
    EmploymentType EmploymentType,
    WorkMode WorkMode,
    string? DistrictName,
    DateOnly ApplicationDeadline,
    bool IsPublished,
    bool IsExpired,
    DateTime UpdatedAt,
    string? CreatedByEmail);

/// <summary>
/// The staff view of one row, which also carries the reasons a draft cannot be published yet.
/// <see cref="PublishBlockers"/> is produced by the same validation the publish path uses, so the
/// editor can say what is missing instead of leaving staff to guess and discover it on save.
/// </summary>
public sealed record JobAdminResponse(
    Guid Id,
    string Slug,
    string Title,
    string Organization,
    string Description,
    JobCategory Category,
    EmploymentType EmploymentType,
    WorkMode WorkMode,
    string? DistrictCode,
    string? DistrictName,
    string? ProvinceCode,
    string? ProvinceName,
    string ApplicationUrl,
    DateOnly ApplicationDeadline,
    bool IsExpired,
    bool IsPublished,
    DateTime CreatedAt,
    DateTime UpdatedAt,
    string? CreatedByEmail,
    IReadOnlyList<string> PublishBlockers);

/// <summary>Create and update share one shape, so the staff form sends the same body for both.</summary>
public sealed class SaveJobRequest
{
    [Required(ErrorMessage = "A slug is required.")]
    [StringLength(160, MinimumLength = 2, ErrorMessage = "The slug must be 2 to 160 characters.")]
    [RegularExpression(
        JobSlug.Pattern,
        ErrorMessage = "Use lowercase words separated by single hyphens, for example junior-python-developer-remote.")]
    public string Slug { get; init; } = string.Empty;

    [Required(ErrorMessage = "A title is required.")]
    [StringLength(200, MinimumLength = 5, ErrorMessage = "The title must be 5 to 200 characters.")]
    public string Title { get; init; } = string.Empty;

    [Required(ErrorMessage = "An employer is required.")]
    [StringLength(160, MinimumLength = 2, ErrorMessage = "The employer must be 2 to 160 characters.")]
    public string Organization { get; init; } = string.Empty;

    // The minimum length is deliberately absent and the maximum is here only to stop an absurd
    // value reaching a column. A draft with a title and nothing else is a legitimate thing for
    // staff to save; the minimum is enforced on the way to publishing instead, where it belongs.
    [StringLength(20000, ErrorMessage = "The description must be 20000 characters or fewer.")]
    public string Description { get; init; } = string.Empty;

    [EnumDataType(typeof(JobCategory), ErrorMessage = "Choose a valid category.")]
    public JobCategory Category { get; init; }

    [EnumDataType(typeof(EmploymentType), ErrorMessage = "Choose a valid employment type.")]
    public EmploymentType EmploymentType { get; init; }

    [EnumDataType(typeof(WorkMode), ErrorMessage = "Choose a valid work mode.")]
    public WorkMode WorkMode { get; init; }

    /// <summary>
    /// A district reference code such as <c>bagmati-kathmandu</c>, or null for a posting with no
    /// fixed location. A code rather than an id so the staff form posts the same thing the public
    /// reference endpoints hand out, and so a posting cannot be saved against a district that does
    /// not exist. The province is read through it rather than accepted, so the two cannot disagree.
    /// </summary>
    [StringLength(80, ErrorMessage = "The district code must be 80 characters or fewer.")]
    public string? DistrictCode { get; init; }

    // The value itself is checked by ExternalUrlPolicy rather than by a RegularExpression,
    // because "is this a safe absolute https URL with a real host" is a parsing question rather
    // than a pattern question, and an approximation of it as a regex is how javascript: links get
    // through.
    [Required(ErrorMessage = "An application link is required.")]
    [StringLength(500, ErrorMessage = "The application link must be 500 characters or fewer.")]
    public string? ApplicationUrl { get; init; }

    public DateOnly ApplicationDeadline { get; init; }

    public bool IsPublished { get; init; }
}
