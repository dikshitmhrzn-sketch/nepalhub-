using System.ComponentModel.DataAnnotations;
using NepalHub.Api.Common;
using NepalHub.Api.Data.Entities;

namespace NepalHub.Api.Features.Businesses;

/// <summary>
/// Optional status parsing for the verification queue, kept to the same shape as the claim
/// queue's parser: a blank or "all" means no filter, and an unrecognised value is refused
/// rather than defaulted, so a typo cannot quietly return the whole table.
/// </summary>
internal static class VerificationFilters
{
    internal static bool TryParseStatus(string? raw, out BusinessVerificationStatus? status, out string field)
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

        if (Enum.TryParse<BusinessVerificationStatus>(trimmed, ignoreCase: true, out var parsed)
            && Enum.IsDefined(parsed))
        {
            status = parsed;

            return true;
        }

        return false;
    }
}

/// <summary>
/// One verification request as the owner sees it, from the account page's list and from
/// the editor's panel. The business name rides along so a row of requests reads as listings
/// rather than as bare ids.
/// </summary>
public sealed record VerificationResponse(
    Guid Id,
    Guid BusinessId,
    string Slug,
    string BusinessName,
    BusinessVerificationStatus Status,
    string? Note,
    DateTime CreatedAt,
    DateTime? ReviewedAt,
    string? ReviewNote);

/// <summary>One row of the staff queue: the listing, who registered it, and where it stands.</summary>
public sealed record AdminVerificationSummaryResponse(
    Guid Id,
    Guid BusinessId,
    string Slug,
    string BusinessName,
    string OwnerName,
    string? OwnerEmail,
    BusinessVerificationStatus Status,
    string? Note,
    DateTime CreatedAt,
    DateTime? ReviewedAt,
    string? ReviewNote);

/// <summary>The owner's own words when asking, optional and capped like a claim's evidence.</summary>
public sealed class SubmitVerificationRequest
{
    [StringLength(500, ErrorMessage = "The note must be 500 characters or fewer.")]
    public string? Note { get; init; }
}

/// <summary>The note staff attach when deciding. Shared by approve and reject.</summary>
public sealed class CloseVerificationRequest
{
    [StringLength(500, ErrorMessage = "The note must be 500 characters or fewer.")]
    public string? ReviewNote { get; init; }
}

public enum VerificationSubmitStatus
{
    Created,

    /// <summary>No listing at that address belongs to this caller.</summary>
    NotFound,

    /// <summary>A request for this listing is still waiting in the queue.</summary>
    AlreadyPending,

    /// <summary>The listing is already live, so there is nothing left for staff to approve.</summary>
    AlreadyPublished,
}

public enum VerificationReviewStatus
{
    Approved,
    Rejected,

    /// <summary>No request with that id.</summary>
    NotFound,

    /// <summary>It was approved or rejected already; the queue does not re-decide.</summary>
    AlreadyReviewed,

    /// <summary>The listing fails a publish rule, so approving it would send out a row staff would not stand behind.</summary>
    NotPublishable,

    /// <summary>The identity store refused the role grant, so nothing at all was applied.</summary>
    Failed,
}

public sealed record VerificationSubmitOutcome(
    VerificationSubmitStatus Status,
    VerificationResponse? Verification = null);

public sealed record VerificationReviewOutcome(
    VerificationReviewStatus Status,
    AdminVerificationSummaryResponse? Verification = null,
    IReadOnlyList<FieldError>? Errors = null);
