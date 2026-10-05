using System.ComponentModel.DataAnnotations;
using NepalHub.Api.Data.Entities;

namespace NepalHub.Api.Features.Moderation;

/// <summary>
/// Filing one report.
///
/// The target and the reason are nullable and marked required because of what happens to a
/// missing enum otherwise: without <c>[Required]</c> an absent property binds to its first
/// member, and a request carrying neither would silently file a spam report against a job
/// nobody named.
/// </summary>
public sealed class CreateReportRequest
{
    [Required(ErrorMessage = "A target type is required.")]
    public ReportTargetType? TargetType { get; set; }

    [Required(ErrorMessage = "A target is required.")]
    public Guid? TargetId { get; set; }

    [Required(ErrorMessage = "A reason is required.")]
    public ReportReason? Reason { get; set; }

    [StringLength(500, ErrorMessage = "The note must be 500 characters or fewer.")]
    public string? Note { get; set; }
}

/// <summary>What staff record when they close a report, if anything.</summary>
public sealed class CloseReportRequest
{
    [StringLength(500, ErrorMessage = "The resolution note must be 500 characters or fewer.")]
    public string? ResolutionNote { get; set; }
}

/// <summary>
/// The report as its reporter sees it: the pointer, the reason, and where it sits in the
/// queue. Staff-only fields such as who resolved it are not part of this shape.
/// </summary>
public sealed record ReportResponse(
    Guid Id,
    ReportTargetType TargetType,
    Guid TargetId,
    ReportReason Reason,
    ReportStatus Status,
    DateTime CreatedAt);

/// <summary>
/// One row of the staff queue, carrying enough of the target to recognise it without a
/// second request: the title and slug are read from the module's own table when the target
/// still exists, and are null when it does not, because a report outlives its listing by
/// design.
/// </summary>
public sealed record ReportAdminSummaryResponse(
    Guid Id,
    ReportTargetType TargetType,
    Guid TargetId,
    string? TargetTitle,
    string? TargetSlug,
    string ReporterName,
    ReportReason Reason,
    string? Note,
    ReportStatus Status,
    DateTime CreatedAt,
    string? ResolvedByName,
    DateTime? ResolvedAt,
    string? ResolutionNote);

/// <summary>
/// Maps a stored report to what its reporter is allowed to see. The staff queue has its
/// own shape, so this deliberately returns nothing that only staff should know.
/// </summary>
internal static class ReportViews
{
    internal static ReportResponse Of(Report report) => new(
        report.Id,
        report.TargetType,
        report.TargetId,
        report.Reason,
        report.Status,
        report.CreatedAt);
}

/// <summary>
/// Parses the queue's status filter, following the module filters' rule: a blank value
/// means no filter, and an unrecognised value is refused rather than defaulted, because
/// "show me the open ones" quietly answering with everything is the wrong answer.
/// </summary>
internal static class ReportFilters
{
    internal static bool TryParseStatus(string? raw, out ReportStatus? status, out string field)
    {
        field = nameof(status).ToLowerInvariant();
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

        if (Enum.TryParse<ReportStatus>(trimmed, ignoreCase: true, out var parsed) && Enum.IsDefined(parsed))
        {
            status = parsed;

            return true;
        }

        return false;
    }
}
