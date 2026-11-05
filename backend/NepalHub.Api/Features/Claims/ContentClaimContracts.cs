using System.ComponentModel.DataAnnotations;
using NepalHub.Api.Common;
using NepalHub.Api.Data.Entities;

namespace NepalHub.Api.Features.Claims;

/// <summary>How a content claim ended when the poster decided.</summary>
public enum ContentClaimDecisionStatus
{
    Accepted,
    Released,

    /// <summary>No claim with that id for this poster.</summary>
    NotFound,

    /// <summary>The claim is already settled; the decision does not re-run.</summary>
    AlreadyReviewed,
}

public sealed record ContentClaimDecisionOutcome(
    ContentClaimDecisionStatus Status,
    PostedContentClaimResponse? Claim = null);

/// <summary>
/// What a claimant sees on the account page: the posting, where the claim stands, and the
/// poster's note when there is one. Enough to recognise the row without loading descriptions.
/// </summary>
public sealed record MyContentClaimResponse(
    Guid Id,
    SubmissionKind Kind,
    string Slug,
    string Title,
    ContentClaimStatus Status,
    string? Note,
    DateTime CreatedAt,
    DateTime? ReviewedAt,
    string? ReviewNote);

/// <summary>
/// What the poster sees for a claim on a post of theirs: the claimant's name and email so
/// they can reach them, plus the same status and note the claimant sees.
/// </summary>
public sealed record PostedContentClaimResponse(
    Guid Id,
    SubmissionKind Kind,
    string Slug,
    string Title,
    string ClaimantName,
    string? ClaimantEmail,
    ContentClaimStatus Status,
    string? Note,
    DateTime CreatedAt,
    DateTime? ReviewedAt,
    string? ReviewNote);

/// <summary>The claimant's optional words about why they want the post.</summary>
public sealed class FileContentClaimRequest
{
    [StringLength(500, ErrorMessage = "The note must be 500 characters or fewer.")]
    public string? Note { get; init; }
}

/// <summary>The poster's optional note when accepting or releasing.</summary>
public sealed class ReviewContentClaimRequest
{
    [StringLength(500, ErrorMessage = "The note must be 500 characters or fewer.")]
    public string? ReviewNote { get; init; }
}

/// <summary>How filing a content claim ended.</summary>
public enum FileContentClaimStatus
{
    Created,

    /// <summary>No published, open post at that address of that kind.</summary>
    NotFound,

    /// <summary>Another person already holds an active claim on this post.</summary>
    AlreadyTaken,

    /// <summary>This person already has an active claim on this post.</summary>
    AlreadyClaimed,

    /// <summary>The post has no creator, so there is nobody to hand the claim to.</summary>
    NoPoster,
}

public sealed record FileContentClaimOutcome(
    FileContentClaimStatus Status,
    MyContentClaimResponse? Claim = null);

/// <summary>The account page's two claim boxes in one read.</summary>
public sealed record ContentClaimsOverview(
    IReadOnlyList<MyContentClaimResponse> Mine,
    IReadOnlyList<PostedContentClaimResponse> OnMyPosts);
