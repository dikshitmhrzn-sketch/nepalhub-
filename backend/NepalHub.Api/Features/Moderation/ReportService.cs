using Microsoft.EntityFrameworkCore;
using MySqlConnector;
using NepalHub.Api.Common;
using NepalHub.Api.Data;
using NepalHub.Api.Data.Entities;
using NepalHub.Api.Features.Admin;
using NepalHub.Api.Features.Quiz;

namespace NepalHub.Api.Features.Moderation;

public enum ReportCreateStatus
{
    Created,
    TargetNotFound,
    AlreadyReported,
}

public sealed record ReportCreateOutcome(ReportCreateStatus Status, Report? Report);

public enum ReportCloseStatus
{
    Closed,
    NotFound,
    AlreadyClosed,
}

public sealed record ReportCloseOutcome(ReportCloseStatus Status, Report? Report);

/// <summary>
/// Filing reports and working the queue.
///
/// <para>
/// A report can only be filed against a target a member is actually allowed to see, which
/// is the same <see cref="PublicContentScope"/> rule the public pages use. A draft, an
/// expired offer or an id that never existed all answer the same way, so the endpoint
/// cannot be used to discover which listings exist but are not public.
/// </para>
///
/// <para>
/// Closing a report writes its audit entry into the same context and saves once: the log
/// row and the change it describes are one write, so neither can exist without the other.
/// </para>
/// </summary>
public sealed class ReportService(
    AppDbContext dbContext,
    INepalDateProvider dates,
    IAuditLog auditLog)
{
    public async Task<ReportCreateOutcome> CreateAsync(
        Guid reporterId,
        ReportTargetType targetType,
        Guid targetId,
        ReportReason reason,
        string? note,
        CancellationToken cancellationToken)
    {
        if (!await TargetExistsAsync(targetType, targetId, cancellationToken))
        {
            return new ReportCreateOutcome(ReportCreateStatus.TargetNotFound, null);
        }

        var alreadyReported = await dbContext.Reports.AnyAsync(
            report => report.ReporterUserId == reporterId
                && report.TargetType == targetType
                && report.TargetId == targetId,
            cancellationToken);

        if (alreadyReported)
        {
            return new ReportCreateOutcome(ReportCreateStatus.AlreadyReported, null);
        }

        var report = new Report
        {
            Id = Guid.NewGuid(),
            ReporterUserId = reporterId,
            TargetType = targetType,
            TargetId = targetId,
            Reason = reason,
            Note = TrimOrNull(note),
            Status = ReportStatus.Open,
        };

        dbContext.Reports.Add(report);

        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException exception) when (IsDuplicateKey(exception))
        {
            // Two tabs filed the same report at the same instant. The unique index
            // settled it, and the loser is told what the loser is always told.
            dbContext.Entry(report).State = EntityState.Detached;

            return new ReportCreateOutcome(ReportCreateStatus.AlreadyReported, null);
        }

        return new ReportCreateOutcome(ReportCreateStatus.Created, report);
    }

    /// <summary>
    /// One report, but only the caller's own: a member reading somebody else's report is
    /// answered with the same absence as an id that never existed.
    /// </summary>
    public Task<Report?> FindOwnAsync(
        Guid reportId,
        Guid reporterId,
        CancellationToken cancellationToken) =>
        dbContext.Reports
            .AsNoTracking()
            .FirstOrDefaultAsync(
                report => report.Id == reportId && report.ReporterUserId == reporterId,
                cancellationToken);

    /// <summary>
    /// The queue. Open work first, then newest, unless a status filter narrows it — at
    /// which point the status order is a constant and the sort is simply newest first.
    /// </summary>
    public async Task<PagedResult<ReportAdminSummaryResponse>> ListForAdminAsync(
        PageRequest paging,
        ReportStatus? status,
        CancellationToken cancellationToken)
    {
        var matching =
            from report in dbContext.Reports
            join reporter in dbContext.Users on report.ReporterUserId equals reporter.Id
            join resolver in dbContext.Users on report.ResolvedByUserId equals resolver.Id into resolving
            from resolver in resolving.DefaultIfEmpty()
            where status == null || report.Status == status
            // The enum is stored as its name, so the column itself cannot be sorted on:
            // alphabetically "Dismissed" would queue up ahead of "Open". The case puts
            // the open work first without pretending the storage order means anything.
            orderby (report.Status == ReportStatus.Open ? 0 : 1) ascending,
                    report.CreatedAt descending
            select new
            {
                report.Id,
                report.TargetType,
                report.TargetId,
                report.Reason,
                report.Note,
                report.Status,
                report.CreatedAt,
                report.ResolvedAt,
                report.ResolutionNote,
                ReporterName = reporter.FullName,
                ResolvedByName = resolver == null ? null : resolver.FullName,
            };

        var total = await matching.CountAsync(cancellationToken);
        var clamped = paging.ClampTo(total);

        var rows = (await matching
                .Skip(clamped.Skip)
                .Take(clamped.PageSize)
                .ToListAsync(cancellationToken))
            .Select(row => new QueueRow(
                row.Id,
                row.TargetType,
                row.TargetId,
                row.Reason,
                row.Note,
                row.Status,
                row.CreatedAt,
                row.ResolvedAt,
                row.ResolutionNote,
                row.ReporterName,
                row.ResolvedByName))
            .ToList();

        var targets = await LoadTargetsAsync(rows, cancellationToken);

        var items = rows
            .Select(row => new ReportAdminSummaryResponse(
                row.Id,
                row.TargetType,
                row.TargetId,
                targets.TryGetValue((row.TargetType, row.TargetId), out var target) ? target.Title : null,
                targets.TryGetValue((row.TargetType, row.TargetId), out var linked) ? linked.Slug : null,
                row.ReporterName,
                row.Reason,
                row.Note,
                row.Status,
                row.CreatedAt,
                row.ResolvedByName,
                row.ResolvedAt,
                row.ResolutionNote))
            .ToList();

        return new PagedResult<ReportAdminSummaryResponse>(items, clamped.Page, clamped.PageSize, total);
    }

    /// <summary>
    /// Closes an open report. A report that is already closed answers with a conflict
    /// rather than being overwritten, because the second moderator's note would silently
    /// replace the first one's record of what was decided.
    /// </summary>
    public async Task<ReportCloseOutcome> CloseAsync(
        Guid reportId,
        ReportStatus newStatus,
        string? resolutionNote,
        Guid staffId,
        string? ip,
        CancellationToken cancellationToken)
    {
        var report = await dbContext.Reports
            .FirstOrDefaultAsync(candidate => candidate.Id == reportId, cancellationToken);

        if (report is null)
        {
            return new ReportCloseOutcome(ReportCloseStatus.NotFound, null);
        }

        if (report.Status != ReportStatus.Open)
        {
            return new ReportCloseOutcome(ReportCloseStatus.AlreadyClosed, null);
        }

        var before = new { status = report.Status.ToString() };

        report.Status = newStatus;
        report.ResolvedByUserId = staffId;
        report.ResolvedAt = dates.UtcNow;
        report.ResolutionNote = TrimOrNull(resolutionNote);

        auditLog.Record(
            staffId,
            newStatus == ReportStatus.Resolved
                ? AuditActions.ReportResolved
                : AuditActions.ReportDismissed,
            "report",
            report.Id.ToString(),
            before,
            new { status = report.Status.ToString(), report.ResolutionNote },
            ip);

        await dbContext.SaveChangesAsync(cancellationToken);

        return new ReportCloseOutcome(ReportCloseStatus.Closed, report);
    }

    /// <summary>
    /// Whether the target exists and the reporter may see it. Every case goes through the
    /// module's own public scope rather than a bare id lookup, so the answer to "can this
    /// be reported" is the same as the answer to "can this be seen".
    /// </summary>
    private async Task<bool> TargetExistsAsync(
        ReportTargetType targetType,
        Guid targetId,
        CancellationToken cancellationToken) =>
        targetType switch
        {
            ReportTargetType.Job => await PublicContentScope
                .Jobs(dbContext.Jobs, dates.Today)
                .AnyAsync(job => job.Id == targetId, cancellationToken),

            ReportTargetType.Business => await PublicContentScope
                .Businesses(dbContext.Businesses)
                .AnyAsync(business => business.Id == targetId, cancellationToken),

            ReportTargetType.Deal => await PublicContentScope
                .Deals(dbContext.Deals, dates.Today)
                .AnyAsync(deal => deal.Id == targetId, cancellationToken),

            ReportTargetType.Event => await PublicContentScope
                .Events(dbContext.Events, dates.NepalNow)
                .AnyAsync(calendarEvent => calendarEvent.Id == targetId, cancellationToken),

            ReportTargetType.StudentResource => await PublicContentScope
                .StudentResources(dbContext.StudentResources, dates.Today)
                .AnyAsync(resource => resource.Id == targetId, cancellationToken),

            _ => false,
        };

    /// <summary>
    /// Titles and slugs for the targets on this page, one query per module type present
    /// and none at all for a type that is not. The queue can then show what it is talking
    /// about and link to it, while the report row itself stays the pointer it was meant
    /// to be.
    /// </summary>
    private async Task<Dictionary<(ReportTargetType Type, Guid Id), (string Title, string Slug)>>
        LoadTargetsAsync(IReadOnlyList<QueueRow> rows, CancellationToken cancellationToken)
    {
        var targets = new Dictionary<(ReportTargetType, Guid), (string, string)>();

        var jobIds = IdsFor(rows, ReportTargetType.Job);
        if (jobIds.Length > 0)
        {
            var jobs = await dbContext.Jobs
                .AsNoTracking()
                .Where(job => jobIds.Contains(job.Id))
                .Select(job => new { job.Id, job.Title, job.Slug })
                .ToListAsync(cancellationToken);

            foreach (var job in jobs)
            {
                targets[(ReportTargetType.Job, job.Id)] = (job.Title, job.Slug);
            }
        }

        var businessIds = IdsFor(rows, ReportTargetType.Business);
        if (businessIds.Length > 0)
        {
            var businesses = await dbContext.Businesses
                .AsNoTracking()
                .Where(business => businessIds.Contains(business.Id))
                .Select(business => new { business.Id, business.Name, business.Slug })
                .ToListAsync(cancellationToken);

            foreach (var business in businesses)
            {
                targets[(ReportTargetType.Business, business.Id)] = (business.Name, business.Slug);
            }
        }

        var dealIds = IdsFor(rows, ReportTargetType.Deal);
        if (dealIds.Length > 0)
        {
            var deals = await dbContext.Deals
                .AsNoTracking()
                .Where(deal => dealIds.Contains(deal.Id))
                .Select(deal => new { deal.Id, deal.Title, deal.Slug })
                .ToListAsync(cancellationToken);

            foreach (var deal in deals)
            {
                targets[(ReportTargetType.Deal, deal.Id)] = (deal.Title, deal.Slug);
            }
        }

        var eventIds = IdsFor(rows, ReportTargetType.Event);
        if (eventIds.Length > 0)
        {
            var events = await dbContext.Events
                .AsNoTracking()
                .Where(calendarEvent => eventIds.Contains(calendarEvent.Id))
                .Select(calendarEvent => new { calendarEvent.Id, calendarEvent.Title, calendarEvent.Slug })
                .ToListAsync(cancellationToken);

            foreach (var calendarEvent in events)
            {
                targets[(ReportTargetType.Event, calendarEvent.Id)] = (calendarEvent.Title, calendarEvent.Slug);
            }
        }

        var resourceIds = IdsFor(rows, ReportTargetType.StudentResource);
        if (resourceIds.Length > 0)
        {
            var resources = await dbContext.StudentResources
                .AsNoTracking()
                .Where(resource => resourceIds.Contains(resource.Id))
                .Select(resource => new { resource.Id, resource.Title, resource.Slug })
                .ToListAsync(cancellationToken);

            foreach (var resource in resources)
            {
                targets[(ReportTargetType.StudentResource, resource.Id)] = (resource.Title, resource.Slug);
            }
        }

        return targets;

        static Guid[] IdsFor(IReadOnlyList<QueueRow> rows, ReportTargetType targetType) =>
            rows
                .Where(row => row.TargetType == targetType)
                .Select(row => row.TargetId)
                .Distinct()
                .ToArray();
    }

    private static bool IsDuplicateKey(DbUpdateException exception) =>
        exception.InnerException is MySqlException { Number: 1062 };

    private static string? TrimOrNull(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    /// <summary>One queue row, materialised. The query above stays anonymous; this is the shape after it.</summary>
    private sealed record QueueRow(
        Guid Id,
        ReportTargetType TargetType,
        Guid TargetId,
        ReportReason Reason,
        string? Note,
        ReportStatus Status,
        DateTime CreatedAt,
        DateTime? ResolvedAt,
        string? ResolutionNote,
        string ReporterName,
        string? ResolvedByName);
}
