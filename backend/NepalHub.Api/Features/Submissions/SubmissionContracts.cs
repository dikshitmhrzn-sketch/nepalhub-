using System.ComponentModel.DataAnnotations;
using NepalHub.Api.Common;
using NepalHub.Api.Data.Entities;
using NepalHub.Api.Features.Deals;
using NepalHub.Api.Features.Events;
using NepalHub.Api.Features.Jobs;

namespace NepalHub.Api.Features.Submissions;

/// <summary>
/// Optional status and kind parsing for the submission queue, kept to the same shape as
/// the verification queue's parser: a blank or "all" means no filter, and an unrecognised
/// value is refused rather than defaulted, so a typo cannot quietly return the whole table.
/// </summary>
internal static class SubmissionFilters
{
    internal static bool TryParseStatus(string? raw, out SubmissionStatus? status, out string field)
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

        if (Enum.TryParse<SubmissionStatus>(trimmed, ignoreCase: true, out var parsed)
            && Enum.IsDefined(parsed))
        {
            status = parsed;

            return true;
        }

        return false;
    }

    internal static bool TryParseKind(string? raw, out SubmissionKind? kind, out string field)
    {
        field = "kind";
        kind = null;

        if (string.IsNullOrWhiteSpace(raw))
        {
            return true;
        }

        var trimmed = raw.Trim();

        if (trimmed.Equals("all", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        if (Enum.TryParse<SubmissionKind>(trimmed, ignoreCase: true, out var parsed)
            && Enum.IsDefined(parsed))
        {
            kind = parsed;

            return true;
        }

        return false;
    }
}

/// <summary>
/// One submission as the author sees it, from the submit screen's history list. The item's
/// slug and title ride along so a list reads as drafts rather than as bare ids, and
/// <see cref="IsPublished"/> says whether the draft has already gone live — which is what
/// stops a directly-published draft from reading as a rejection that never happened.
/// </summary>
public sealed record SubmissionResponse(
    Guid Id,
    SubmissionKind Kind,
    Guid ItemId,
    string Slug,
    string Title,
    bool IsPublished,
    SubmissionStatus Status,
    string? Note,
    DateTime CreatedAt,
    DateTime? ReviewedAt,
    string? ReviewNote);

/// <summary>One row of the staff queue: the draft, who wrote it, and where it stands.</summary>
public sealed record AdminSubmissionSummaryResponse(
    Guid Id,
    SubmissionKind Kind,
    Guid ItemId,
    string Slug,
    string Title,
    bool IsPublished,
    string SubmitterName,
    string? SubmitterEmail,
    SubmissionStatus Status,
    string? Note,
    DateTime CreatedAt,
    DateTime? ReviewedAt,
    string? ReviewNote);

/// <summary>
/// One submission as its author opens it to correct: the queue status beside the draft's
/// own body, so the editor that appears is prefilled with what was actually sent rather
/// than with what somebody remembers sending. Exactly one of the three content payloads
/// is set, matching the kind, and the others are null so a wrong-kind body can never be
/// built from this response by accident.
/// </summary>
public sealed record SubmissionDetailResponse(
    SubmissionResponse Submission,
    SaveJobRequest? Job,
    SaveEventRequest? Event,
    SaveDealRequest? Deal);

/// <summary>A job draft with the author's own words, optional and capped like a claim's evidence.</summary>
public sealed class SubmitJobRequest
{
    public SaveJobRequest Content { get; init; } = new();

    [StringLength(500, ErrorMessage = "The note must be 500 characters or fewer.")]
    public string? Note { get; init; }
}

/// <summary>An event draft with the author's own words.</summary>
public sealed class SubmitEventRequest
{
    public SaveEventRequest Content { get; init; } = new();

    [StringLength(500, ErrorMessage = "The note must be 500 characters or fewer.")]
    public string? Note { get; init; }
}

/// <summary>An offer draft with the author's own words.</summary>
public sealed class SubmitDealRequest
{
    public SaveDealRequest Content { get; init; } = new();

    [StringLength(500, ErrorMessage = "The note must be 500 characters or fewer.")]
    public string? Note { get; init; }
}

/// <summary>
/// The author's edit to a draft that has not been approved. The kind names which content
/// block carries the body and must match the submission it is aimed at, so a mismatched
/// pair is refused rather than one of the two being silently ignored. The note is replaced
/// wholesale like every other field, because a PUT here means the whole draft as it now stands.
/// </summary>
public sealed class UpdateSubmissionRequest
{
    [EnumDataType(typeof(SubmissionKind), ErrorMessage = "Choose a valid submission kind.")]
    public SubmissionKind Kind { get; init; }

    public SaveJobRequest? Job { get; init; }

    public SaveEventRequest? Event { get; init; }

    public SaveDealRequest? Deal { get; init; }

    [StringLength(500, ErrorMessage = "The note must be 500 characters or fewer.")]
    public string? Note { get; init; }
}

/// <summary>The note staff attach when deciding. Shared by approve and reject.</summary>
public sealed class CloseSubmissionRequest
{
    [StringLength(500, ErrorMessage = "The note must be 500 characters or fewer.")]
    public string? ReviewNote { get; init; }
}

public enum SubmissionSubmitStatus
{
    Created,

    /// <summary>Shape or content rules were not met, so no draft row was written.</summary>
    Invalid,

    /// <summary>Another row already owns that slug.</summary>
    SlugTaken,
}

public enum SubmissionEditStatus
{
    Updated,

    /// <summary>No submission at that id belongs to this caller.</summary>
    NotFound,

    /// <summary>Shape or content rules were not met.</summary>
    Invalid,

    /// <summary>Another row already owns that slug.</summary>
    SlugTaken,

    /// <summary>The body's kind does not match the submission it was aimed at.</summary>
    KindMismatch,

    /// <summary>The submission was approved, or its draft went live, so it is closed to the author.</summary>
    Closed,
}

public enum SubmissionResubmitStatus
{
    Resubmitted,

    /// <summary>No submission at that id belongs to this caller.</summary>
    NotFound,

    /// <summary>Only a turned-down submission can be sent again.</summary>
    NotRejected,
}

public enum SubmissionReviewStatus
{
    Approved,
    Rejected,

    /// <summary>No submission with that id.</summary>
    NotFound,

    /// <summary>It was approved or rejected already; the queue does not re-decide.</summary>
    AlreadyReviewed,

    /// <summary>The draft fails a publish rule, so approving it would send out a row staff would not stand behind.</summary>
    NotPublishable,

    /// <summary>The draft already went live out of band, so turning it down would contradict the editor.</summary>
    AlreadyPublished,
}

public sealed record SubmissionSubmitOutcome(
    SubmissionSubmitStatus Status,
    SubmissionResponse? Submission = null,
    IReadOnlyList<FieldError>? FieldErrors = null,
    string? Message = null);

public sealed record SubmissionEditOutcome(
    SubmissionEditStatus Status,
    SubmissionResponse? Submission = null,
    IReadOnlyList<FieldError>? FieldErrors = null,
    string? Message = null);

public sealed record SubmissionResubmitOutcome(
    SubmissionResubmitStatus Status,
    SubmissionResponse? Submission = null);

public sealed record SubmissionReviewOutcome(
    SubmissionReviewStatus Status,
    AdminSubmissionSummaryResponse? Submission = null,
    IReadOnlyList<FieldError>? Errors = null);
