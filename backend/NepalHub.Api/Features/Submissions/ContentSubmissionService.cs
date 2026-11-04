using Microsoft.EntityFrameworkCore;
using NepalHub.Api.Common;
using NepalHub.Api.Data;
using NepalHub.Api.Data.Entities;
using NepalHub.Api.Features.Admin;
using NepalHub.Api.Features.Deals;
using NepalHub.Api.Features.Events;
using NepalHub.Api.Features.Jobs;
using NepalHub.Api.Features.Quiz;

namespace NepalHub.Api.Features.Submissions;

/// <summary>
/// The member's half of the content queue: somebody signs in, writes a draft, and asks for
/// it to become public. This is the business-verification pattern applied to listings —
/// submitting creates the real row unpublished and opens one request against it, staff
/// approve or turn the request down, and only the approval flips the draft live.
///
/// <para>
/// The draft is created through the existing <see cref="JobService"/> /
/// <see cref="EventService"/> / <see cref="DealService"/> write paths with
/// <c>IsPublished</c> forced off, so a submission cannot smuggle a publish past the rules:
/// it earns exactly the validation a staff draft earns, and the publish rules are checked
/// once more — against the stored row, through each service's single definition — at the
/// moment staff approve.
/// </para>
///
/// <para>
/// Approval is one statement with two halves: the row satisfies the publish rules, and
/// staff stand behind it. Both halves commit with the review and the audit entry in one
/// SaveChanges, so no state in between exists where a listing is live with no record of
/// who approved it. There is no role to grant here — unlike a verification, content
/// carries no capability — so the audit line is the whole trail.
/// </para>
/// </summary>
public sealed class ContentSubmissionService(
    AppDbContext dbContext,
    IAuditLog auditLog,
    INepalDateProvider dates,
    JobService jobService,
    EventService eventService,
    DealService dealService)
{
    // ---------------------------------------------------------------- member side

    /// <summary>Opens a job submission: the draft row first, then the request against it.</summary>
    internal async Task<SubmissionSubmitOutcome> SubmitJobAsync(
        Guid authorUserId,
        SaveJobRequest content,
        string? note,
        CancellationToken cancellationToken)
    {
        var outcome = await jobService.CreateAsync(AsDraft(content), authorUserId, cancellationToken);

        if (outcome.Status == JobSaveStatus.Invalid)
        {
            return new SubmissionSubmitOutcome(
                SubmissionSubmitStatus.Invalid,
                FieldErrors: outcome.FieldErrors);
        }

        if (outcome.Status == JobSaveStatus.SlugTaken)
        {
            return new SubmissionSubmitOutcome(
                SubmissionSubmitStatus.SlugTaken,
                Message: outcome.Message);
        }

        var job = outcome.Job!;

        return await OpenAsync(
            SubmissionKind.Job,
            job.Id,
            authorUserId,
            note,
            job.Slug,
            job.Title,
            cancellationToken);
    }

    /// <summary>Opens an event submission: the draft row first, then the request against it.</summary>
    internal async Task<SubmissionSubmitOutcome> SubmitEventAsync(
        Guid authorUserId,
        SaveEventRequest content,
        string? note,
        CancellationToken cancellationToken)
    {
        var outcome = await eventService.CreateAsync(AsDraft(content), authorUserId, cancellationToken);

        if (outcome.Status == EventSaveStatus.Invalid)
        {
            return new SubmissionSubmitOutcome(
                SubmissionSubmitStatus.Invalid,
                FieldErrors: outcome.FieldErrors);
        }

        if (outcome.Status == EventSaveStatus.SlugTaken)
        {
            return new SubmissionSubmitOutcome(
                SubmissionSubmitStatus.SlugTaken,
                Message: outcome.Message);
        }

        var calendarEvent = outcome.Event!;

        return await OpenAsync(
            SubmissionKind.Event,
            calendarEvent.Id,
            authorUserId,
            note,
            calendarEvent.Slug,
            calendarEvent.Title,
            cancellationToken);
    }

    /// <summary>Opens an offer submission: the draft row first, then the request against it.</summary>
    internal async Task<SubmissionSubmitOutcome> SubmitDealAsync(
        Guid authorUserId,
        SaveDealRequest content,
        string? note,
        CancellationToken cancellationToken)
    {
        var outcome = await dealService.CreateAsync(AsDraft(content), authorUserId, cancellationToken);

        if (outcome.Status == DealSaveStatus.Invalid)
        {
            return new SubmissionSubmitOutcome(
                SubmissionSubmitStatus.Invalid,
                FieldErrors: outcome.FieldErrors);
        }

        if (outcome.Status == DealSaveStatus.SlugTaken)
        {
            return new SubmissionSubmitOutcome(
                SubmissionSubmitStatus.SlugTaken,
                Message: outcome.Message);
        }

        var deal = outcome.Deal!;

        return await OpenAsync(
            SubmissionKind.Deal,
            deal.Id,
            authorUserId,
            note,
            deal.Slug,
            deal.Title,
            cancellationToken);
    }

    /// <summary>
    /// Everything this person has sent in, newest open work first so the row they are
    /// waiting on is the one they see. Scoped to the caller, so somebody else's history
    /// answers as an empty list rather than as a window onto their drafts.
    /// </summary>
    internal async Task<IReadOnlyList<SubmissionResponse>> ListMineAsync(
        Guid authorUserId,
        CancellationToken cancellationToken)
    {
        var submissions = await dbContext.ContentSubmissions
            .AsNoTracking()
            .Where(submission => submission.SubmittedByUserId == authorUserId)
            .OrderByDescending(submission => submission.Status == SubmissionStatus.Pending ? 0 : 1)
            .ThenByDescending(submission => submission.CreatedAt)
            .ThenBy(submission => submission.Id)
            .ToListAsync(cancellationToken);

        var facts = await ReadItemFactsAsync(
            submissions.Select(submission => KeyOf(submission)).ToList(),
            cancellationToken);

        return submissions
            .Select(submission => View(submission, facts[KeyOf(submission)]))
            .ToList();
    }

    /// <summary>
    /// One submission with the draft body behind it, for the author's editor. Scoped to
    /// ownership like every other member read — somebody else's submission answers the same
    /// 404 as one that never existed — and projected through each service's own
    /// <c>ToSaveRequest</c>, so what the form shows and what the publish rules read are the
    /// same projection rather than two copies that could drift apart.
    /// </summary>
    internal async Task<SubmissionDetailResponse?> GetDetailAsync(
        Guid authorUserId,
        Guid submissionId,
        CancellationToken cancellationToken)
    {
        var submission = await dbContext.ContentSubmissions
            .AsNoTracking()
            .FirstOrDefaultAsync(
                item => item.Id == submissionId && item.SubmittedByUserId == authorUserId,
                cancellationToken);

        if (submission is null)
        {
            return null;
        }

        SaveJobRequest? job = null;
        SaveEventRequest? calendarEvent = null;
        SaveDealRequest? deal = null;

        switch (submission.Kind)
        {
            case SubmissionKind.Job:
            {
                var row = await dbContext.Jobs
                    .AsNoTracking()
                    .Include(item => item.District)
                    .FirstOrDefaultAsync(item => item.Id == submission.JobId, cancellationToken);

                job = row is null ? null : JobService.ToSaveRequest(row);

                break;
            }

            case SubmissionKind.Event:
            {
                var row = await dbContext.Events
                    .AsNoTracking()
                    .Include(item => item.District)
                    .Include(item => item.Business)
                    .FirstOrDefaultAsync(item => item.Id == submission.EventId, cancellationToken);

                calendarEvent = row is null ? null : EventService.ToSaveRequest(row);

                break;
            }

            default:
            {
                var row = await dbContext.Deals
                    .AsNoTracking()
                    .Include(item => item.District)
                    .Include(item => item.Business)
                    .FirstOrDefaultAsync(item => item.Id == submission.DealId, cancellationToken);

                deal = row is null ? null : DealService.ToSaveRequest(row);

                break;
            }
        }

        return new SubmissionDetailResponse(
            View(submission, await ReadFactsAsync(submission, cancellationToken)),
            job,
            calendarEvent,
            deal);
    }

    /// <summary>
    /// The author's edit to a draft that has not gone live. Scoped to ownership like every
    /// other member write, and closed once the submission is approved or the draft has gone
    /// live out of band — at that point the row belongs to the editor, not to the form.
    /// </summary>
    internal async Task<SubmissionEditOutcome> UpdateDraftAsync(
        Guid authorUserId,
        Guid submissionId,
        UpdateSubmissionRequest request,
        CancellationToken cancellationToken)
    {
        var submission = await dbContext.ContentSubmissions
            .FirstOrDefaultAsync(
                item => item.Id == submissionId && item.SubmittedByUserId == authorUserId,
                cancellationToken);

        if (submission is null)
        {
            return new SubmissionEditOutcome(SubmissionEditStatus.NotFound);
        }

        if (submission.Status == SubmissionStatus.Approved || await IsLiveAsync(submission, cancellationToken))
        {
            return new SubmissionEditOutcome(SubmissionEditStatus.Closed);
        }

        if (request.Kind != submission.Kind
            || (request.Kind, request.Job, request.Event, request.Deal) switch
            {
                (SubmissionKind.Job, not null, _, _) => false,
                (SubmissionKind.Event, _, not null, _) => false,
                (SubmissionKind.Deal, _, _, not null) => false,
                _ => true,
            })
        {
            return new SubmissionEditOutcome(
                SubmissionEditStatus.KindMismatch,
                FieldErrors:
                [
                    new FieldError(
                        "kind",
                        "invalid",
                        $"This submission is a {submission.Kind}, so that is the body it accepts."),
                ]);
        }

        switch (submission.Kind)
        {
            case SubmissionKind.Job:
            {
                var outcome = await jobService.UpdateAsync(
                    submission.JobId!.Value,
                    AsDraft(request.Job!),
                    cancellationToken);

                return outcome.Status switch
                {
                    JobSaveStatus.Updated => await EditUpdatedAsync(submission, request, outcome.Job!.Slug, outcome.Job.Title, outcome.Job.IsPublished, cancellationToken),
                    JobSaveStatus.Invalid => new SubmissionEditOutcome(SubmissionEditStatus.Invalid, FieldErrors: outcome.FieldErrors),
                    JobSaveStatus.SlugTaken => new SubmissionEditOutcome(SubmissionEditStatus.SlugTaken, Message: outcome.Message),
                    JobSaveStatus.SlugImmutable => new SubmissionEditOutcome(SubmissionEditStatus.Closed, Message: outcome.Message),
                    _ => new SubmissionEditOutcome(SubmissionEditStatus.NotFound),
                };
            }

            case SubmissionKind.Event:
            {
                var outcome = await eventService.UpdateAsync(
                    submission.EventId!.Value,
                    AsDraft(request.Event!),
                    cancellationToken);

                return outcome.Status switch
                {
                    EventSaveStatus.Updated => await EditUpdatedAsync(submission, request, outcome.Event!.Slug, outcome.Event.Title, outcome.Event.IsPublished, cancellationToken),
                    EventSaveStatus.Invalid => new SubmissionEditOutcome(SubmissionEditStatus.Invalid, FieldErrors: outcome.FieldErrors),
                    EventSaveStatus.SlugTaken => new SubmissionEditOutcome(SubmissionEditStatus.SlugTaken, Message: outcome.Message),
                    EventSaveStatus.SlugImmutable => new SubmissionEditOutcome(SubmissionEditStatus.Closed, Message: outcome.Message),
                    _ => new SubmissionEditOutcome(SubmissionEditStatus.NotFound),
                };
            }

            default:
            {
                var outcome = await dealService.UpdateAsync(
                    submission.DealId!.Value,
                    AsDraft(request.Deal!),
                    cancellationToken);

                return outcome.Status switch
                {
                    DealSaveStatus.Updated => await EditUpdatedAsync(submission, request, outcome.Deal!.Slug, outcome.Deal.Title, outcome.Deal.IsPublished, cancellationToken),
                    DealSaveStatus.Invalid => new SubmissionEditOutcome(SubmissionEditStatus.Invalid, FieldErrors: outcome.FieldErrors),
                    DealSaveStatus.SlugTaken => new SubmissionEditOutcome(SubmissionEditStatus.SlugTaken, Message: outcome.Message),
                    DealSaveStatus.SlugImmutable => new SubmissionEditOutcome(SubmissionEditStatus.Closed, Message: outcome.Message),
                    _ => new SubmissionEditOutcome(SubmissionEditStatus.NotFound),
                };
            }
        }
    }

    /// <summary>
    /// Sends a turned-down submission back into the queue. The review fields are cleared
    /// and the clock restarted, so the row re-enters the pending band as the new question
    /// it is rather than sinking under the date of the decision that has already been made.
    /// </summary>
    internal async Task<SubmissionResubmitOutcome> ResubmitAsync(
        Guid authorUserId,
        Guid submissionId,
        CancellationToken cancellationToken)
    {
        var submission = await dbContext.ContentSubmissions
            .FirstOrDefaultAsync(
                item => item.Id == submissionId && item.SubmittedByUserId == authorUserId,
                cancellationToken);

        if (submission is null)
        {
            return new SubmissionResubmitOutcome(SubmissionResubmitStatus.NotFound);
        }

        if (submission.Status != SubmissionStatus.Rejected)
        {
            return new SubmissionResubmitOutcome(SubmissionResubmitStatus.NotRejected);
        }

        submission.Status = SubmissionStatus.Pending;
        submission.CreatedAt = dates.UtcNow;
        submission.ReviewedByUserId = null;
        submission.ReviewedAt = null;
        submission.ReviewNote = null;

        await dbContext.SaveChangesAsync(cancellationToken);

        var facts = await ReadFactsAsync(submission, cancellationToken);

        return new SubmissionResubmitOutcome(
            SubmissionResubmitStatus.Resubmitted,
            View(submission, facts));
    }

    // ----------------------------------------------------------------- staff side

    /// <summary>
    /// The queue. Pending work sorts first — the enum is stored as its name, so the column
    /// itself would order "Approved" ahead of "Pending" alphabetically — and newest first
    /// within each band, which is the order the report, claim and verification queues
    /// already teach.
    /// </summary>
    internal async Task<PagedResult<AdminSubmissionSummaryResponse>> ListForAdminAsync(
        PageRequest paging,
        SubmissionStatus? status,
        SubmissionKind? kind,
        CancellationToken cancellationToken)
    {
        var matching =
            from submission in dbContext.ContentSubmissions
            join author in dbContext.Users on submission.SubmittedByUserId equals author.Id
            where status == null || submission.Status == status
            where kind == null || submission.Kind == kind
            orderby (submission.Status == SubmissionStatus.Pending ? 0 : 1) ascending,
                    submission.CreatedAt descending
            select new
            {
                submission.Id,
                submission.Kind,
                submission.JobId,
                submission.EventId,
                submission.DealId,
                submission.Status,
                submission.Note,
                submission.CreatedAt,
                submission.ReviewedAt,
                submission.ReviewNote,
                SubmitterName = author.FullName,
                SubmitterEmail = author.Email,
            };

        var total = await matching.CountAsync(cancellationToken);
        var clamped = paging.ClampTo(total);

        var rows = await matching
            .Skip(clamped.Skip)
            .Take(clamped.PageSize)
            .ToListAsync(cancellationToken);

        var keys = rows
            .Select(row => new ItemKey(row.Kind, row.JobId ?? row.EventId ?? row.DealId ?? Guid.Empty))
            .ToList();

        var facts = await ReadItemFactsAsync(keys, cancellationToken);

        var items = rows
            .Select(row =>
            {
                var key = new ItemKey(row.Kind, row.JobId ?? row.EventId ?? row.DealId ?? Guid.Empty);
                var item = facts[key];

                return new AdminSubmissionSummaryResponse(
                    row.Id,
                    row.Kind,
                    key.ItemId,
                    item.Slug,
                    item.Title,
                    item.IsPublished,
                    row.SubmitterName,
                    row.SubmitterEmail,
                    row.Status,
                    row.Note,
                    row.CreatedAt,
                    row.ReviewedAt,
                    row.ReviewNote);
            })
            .ToList();

        return new PagedResult<AdminSubmissionSummaryResponse>(items, clamped.Page, clamped.PageSize, total);
    }

    /// <summary>
    /// Approves a request: the draft satisfies the publish rules, so it goes live, and the
    /// decision is written to the audit log — all by one SaveChanges. A draft that no
    /// longer passes the rules answers 409 with the field errors rather than publishing
    /// something staff would retract. A draft that already went live through the editor is
    /// settled without a second publish, because the fact staff are standing behind it is
    /// the only thing the approval still has to record.
    /// </summary>
    internal async Task<SubmissionReviewOutcome> ApproveAsync(
        Guid submissionId,
        string? reviewNote,
        Guid staffUserId,
        string? ip,
        CancellationToken cancellationToken)
    {
        var submission = await LoadForReviewAsync(submissionId, cancellationToken);

        if (submission is null)
        {
            return new SubmissionReviewOutcome(SubmissionReviewStatus.NotFound);
        }

        if (submission.Status != SubmissionStatus.Pending)
        {
            return new SubmissionReviewOutcome(
                SubmissionReviewStatus.AlreadyReviewed,
                ViewAdmin(submission, await ReadFactsAsync(submission, cancellationToken)));
        }

        var alreadyLive = await IsLiveAsync(submission, cancellationToken);

        if (!alreadyLive)
        {
            var blockers = await CollectBlockersAsync(submission, cancellationToken);

            if (blockers.Count > 0)
            {
                return new SubmissionReviewOutcome(
                    SubmissionReviewStatus.NotPublishable,
                    ViewAdmin(submission, await ReadFactsAsync(submission, cancellationToken)),
                    blockers);
            }

            await PublishAsync(submission, cancellationToken);
        }

        submission.Status = SubmissionStatus.Approved;
        submission.ReviewedByUserId = staffUserId;
        submission.ReviewedAt = dates.UtcNow;
        submission.ReviewNote = Blank(reviewNote);

        auditLog.Record(
            staffUserId,
            AuditActions.SubmissionApproved,
            "content_submission",
            submission.Id.ToString(),
            before: new { status = nameof(SubmissionStatus.Pending), published = alreadyLive },
            after: new { status = nameof(SubmissionStatus.Approved), published = true },
            ip);

        await dbContext.SaveChangesAsync(cancellationToken);

        return new SubmissionReviewOutcome(
            SubmissionReviewStatus.Approved,
            ViewAdmin(submission, await ReadFactsAsync(submission, cancellationToken)));
    }

    /// <summary>
    /// Turns the request down; the draft stays unpublished so the author can correct it and
    /// ask again. A draft that already went live out of band answers 409 rather than
    /// recording a rejection beside a live row the editor published.
    /// </summary>
    internal async Task<SubmissionReviewOutcome> RejectAsync(
        Guid submissionId,
        string? reviewNote,
        Guid staffUserId,
        string? ip,
        CancellationToken cancellationToken)
    {
        var submission = await LoadForReviewAsync(submissionId, cancellationToken);

        if (submission is null)
        {
            return new SubmissionReviewOutcome(SubmissionReviewStatus.NotFound);
        }

        if (submission.Status != SubmissionStatus.Pending)
        {
            return new SubmissionReviewOutcome(
                SubmissionReviewStatus.AlreadyReviewed,
                ViewAdmin(submission, await ReadFactsAsync(submission, cancellationToken)));
        }

        if (await IsLiveAsync(submission, cancellationToken))
        {
            return new SubmissionReviewOutcome(
                SubmissionReviewStatus.AlreadyPublished,
                ViewAdmin(submission, await ReadFactsAsync(submission, cancellationToken)));
        }

        submission.Status = SubmissionStatus.Rejected;
        submission.ReviewedByUserId = staffUserId;
        submission.ReviewedAt = dates.UtcNow;
        submission.ReviewNote = Blank(reviewNote);

        auditLog.Record(
            staffUserId,
            AuditActions.SubmissionRejected,
            "content_submission",
            submission.Id.ToString(),
            before: new { status = nameof(SubmissionStatus.Pending) },
            after: new { status = nameof(SubmissionStatus.Rejected), note = submission.ReviewNote },
            ip);

        await dbContext.SaveChangesAsync(cancellationToken);

        return new SubmissionReviewOutcome(
            SubmissionReviewStatus.Rejected,
            ViewAdmin(submission, await ReadFactsAsync(submission, cancellationToken)));
    }

    // ------------------------------------------------------------------ internals

    /// <summary>
    /// Writes the request row against a draft that <c>CreateAsync</c> has already saved.
    /// Two saves rather than one transaction: the draft is written through its own service,
    /// which owns validation and the duplicate-slug race, and a draft that somehow lost its
    /// request would still be an ordinary staff-visible draft rather than a lost row —
    /// which is the safe direction for the failure to fall.
    /// </summary>
    private async Task<SubmissionSubmitOutcome> OpenAsync(
        SubmissionKind kind,
        Guid itemId,
        Guid authorUserId,
        string? note,
        string slug,
        string title,
        CancellationToken cancellationToken)
    {
        var submission = new ContentSubmission
        {
            Id = Guid.NewGuid(),
            Kind = kind,
            JobId = kind == SubmissionKind.Job ? itemId : null,
            EventId = kind == SubmissionKind.Event ? itemId : null,
            DealId = kind == SubmissionKind.Deal ? itemId : null,
            SubmittedByUserId = authorUserId,
            Note = Blank(note),
            Status = SubmissionStatus.Pending,
        };

        dbContext.ContentSubmissions.Add(submission);
        await dbContext.SaveChangesAsync(cancellationToken);

        var facts = new ItemFacts(slug, title, IsPublished: false);

        return new SubmissionSubmitOutcome(SubmissionSubmitStatus.Created, View(submission, facts));
    }

    private async Task<ContentSubmission?> LoadForReviewAsync(Guid submissionId, CancellationToken cancellationToken) =>
        await dbContext.ContentSubmissions
            .Include(submission => submission.SubmittedBy)
            .FirstOrDefaultAsync(submission => submission.Id == submissionId, cancellationToken);

    /// <summary>Whether the draft this request points at has already gone live.</summary>
    private async Task<bool> IsLiveAsync(ContentSubmission submission, CancellationToken cancellationToken) =>
        submission.Kind switch
        {
            SubmissionKind.Job => await dbContext.Jobs.AnyAsync(
                job => job.Id == submission.JobId && job.IsPublished, cancellationToken),
            SubmissionKind.Event => await dbContext.Events.AnyAsync(
                calendarEvent => calendarEvent.Id == submission.EventId && calendarEvent.IsPublished, cancellationToken),
            _ => await dbContext.Deals.AnyAsync(
                deal => deal.Id == submission.DealId && deal.IsPublished, cancellationToken),
        };

    /// <summary>
    /// The publish rules against the stored draft, read from the owning service's single
    /// definition so an approval can never publish a row the staff editor would refuse.
    /// </summary>
    private async Task<IReadOnlyList<FieldError>> CollectBlockersAsync(
        ContentSubmission submission,
        CancellationToken cancellationToken)
    {
        switch (submission.Kind)
        {
            case SubmissionKind.Job:
            {
                var job = await dbContext.Jobs
                    .FirstOrDefaultAsync(item => item.Id == submission.JobId, cancellationToken);

                return job is null
                    ? [new FieldError("content", "missing", "The draft this request points at no longer exists.")]
                    : jobService.CollectPublishBlockersFor(job);
            }

            case SubmissionKind.Event:
            {
                var calendarEvent = await dbContext.Events
                    .FirstOrDefaultAsync(item => item.Id == submission.EventId, cancellationToken);

                return calendarEvent is null
                    ? [new FieldError("content", "missing", "The draft this request points at no longer exists.")]
                    : eventService.CollectPublishBlockersFor(calendarEvent);
            }

            default:
            {
                var deal = await dbContext.Deals
                    .FirstOrDefaultAsync(item => item.Id == submission.DealId, cancellationToken);

                return deal is null
                    ? [new FieldError("content", "missing", "The draft this request points at no longer exists.")]
                    : dealService.CollectPublishBlockersFor(deal);
            }
        }
    }

    /// <summary>Flips the draft live. Loaded through the same context, so it commits with the review.</summary>
    private async Task PublishAsync(ContentSubmission submission, CancellationToken cancellationToken)
    {
        switch (submission.Kind)
        {
            case SubmissionKind.Job:
            {
                var job = await dbContext.Jobs
                    .FirstOrDefaultAsync(item => item.Id == submission.JobId, cancellationToken);

                if (job is not null)
                {
                    job.IsPublished = true;
                }

                break;
            }

            case SubmissionKind.Event:
            {
                var calendarEvent = await dbContext.Events
                    .FirstOrDefaultAsync(item => item.Id == submission.EventId, cancellationToken);

                if (calendarEvent is not null)
                {
                    calendarEvent.IsPublished = true;
                }

                break;
            }

            default:
            {
                var deal = await dbContext.Deals
                    .FirstOrDefaultAsync(item => item.Id == submission.DealId, cancellationToken);

                if (deal is not null)
                {
                    deal.IsPublished = true;
                }

                break;
            }
        }

        await Task.CompletedTask;
    }

    /// <summary>
    /// The saved edit: the author's note is replaced alongside the body it travels with,
    /// in the same SaveChanges as the queue row itself, so a note and a draft can never
    /// disagree about what was last sent. Only reached on a successful content update —
    /// a refused save leaves the note as it stood, the same as any other field.
    /// </summary>
    private async Task<SubmissionEditOutcome> EditUpdatedAsync(
        ContentSubmission submission,
        UpdateSubmissionRequest request,
        string slug,
        string title,
        bool isPublished,
        CancellationToken cancellationToken)
    {
        submission.Note = Blank(request.Note);
        await dbContext.SaveChangesAsync(cancellationToken);

        return new SubmissionEditOutcome(
            SubmissionEditStatus.Updated,
            View(submission, new ItemFacts(slug, title, isPublished)));
    }

    /// <summary>The item id a submission points at, which is exactly one of the three columns.</summary>
    private static Guid ItemIdOf(ContentSubmission submission) =>
        submission.JobId ?? submission.EventId ?? submission.DealId ?? Guid.Empty;

    private static SubmissionResponse View(ContentSubmission submission, ItemFacts facts) => new(
        submission.Id,
        submission.Kind,
        ItemIdOf(submission),
        facts.Slug,
        facts.Title,
        facts.IsPublished,
        submission.Status,
        submission.Note,
        submission.CreatedAt,
        submission.ReviewedAt,
        submission.ReviewNote);

    private static AdminSubmissionSummaryResponse ViewAdmin(ContentSubmission submission, ItemFacts facts) => new(
        submission.Id,
        submission.Kind,
        ItemIdOf(submission),
        facts.Slug,
        facts.Title,
        facts.IsPublished,
        submission.SubmittedBy?.FullName ?? string.Empty,
        submission.SubmittedBy?.Email,
        submission.Status,
        submission.Note,
        submission.CreatedAt,
        submission.ReviewedAt,
        submission.ReviewNote);

    private async Task<ItemFacts> ReadFactsAsync(ContentSubmission submission, CancellationToken cancellationToken)
    {
        var key = KeyOf(submission);
        var facts = await ReadItemFactsAsync([key], cancellationToken);

        return facts.TryGetValue(key, out var found)
            ? found
            : new ItemFacts(string.Empty, string.Empty, IsPublished: false);
    }

    /// <summary>
    /// Slug, title and published-ness for every draft a page of submissions points at, in
    /// one query per kind rather than one per row. The queue's page is at most a hundred
    /// rows, so three lookups beat three hundred.
    /// </summary>
    private async Task<Dictionary<ItemKey, ItemFacts>> ReadItemFactsAsync(
        IReadOnlyList<ItemKey> keys,
        CancellationToken cancellationToken)
    {
        var facts = new Dictionary<ItemKey, ItemFacts>();

        var jobIds = keys
            .Where(key => key.Kind == SubmissionKind.Job)
            .Select(key => key.ItemId)
            .Distinct()
            .ToList();

        if (jobIds.Count > 0)
        {
            var rows = await dbContext.Jobs
                .AsNoTracking()
                .Where(job => jobIds.Contains(job.Id))
                .Select(job => new { job.Id, job.Slug, job.Title, job.IsPublished })
                .ToListAsync(cancellationToken);

            foreach (var row in rows)
            {
                facts[new ItemKey(SubmissionKind.Job, row.Id)] = new ItemFacts(row.Slug, row.Title, row.IsPublished);
            }
        }

        var eventIds = keys
            .Where(key => key.Kind == SubmissionKind.Event)
            .Select(key => key.ItemId)
            .Distinct()
            .ToList();

        if (eventIds.Count > 0)
        {
            var rows = await dbContext.Events
                .AsNoTracking()
                .Where(calendarEvent => eventIds.Contains(calendarEvent.Id))
                .Select(calendarEvent => new { calendarEvent.Id, calendarEvent.Slug, calendarEvent.Title, calendarEvent.IsPublished })
                .ToListAsync(cancellationToken);

            foreach (var row in rows)
            {
                facts[new ItemKey(SubmissionKind.Event, row.Id)] = new ItemFacts(row.Slug, row.Title, row.IsPublished);
            }
        }

        var dealIds = keys
            .Where(key => key.Kind == SubmissionKind.Deal)
            .Select(key => key.ItemId)
            .Distinct()
            .ToList();

        if (dealIds.Count > 0)
        {
            var rows = await dbContext.Deals
                .AsNoTracking()
                .Where(deal => dealIds.Contains(deal.Id))
                .Select(deal => new { deal.Id, deal.Slug, deal.Title, deal.IsPublished })
                .ToListAsync(cancellationToken);

            foreach (var row in rows)
            {
                facts[new ItemKey(SubmissionKind.Deal, row.Id)] = new ItemFacts(row.Slug, row.Title, row.IsPublished);
            }
        }

        return facts;
    }

    private static ItemKey KeyOf(ContentSubmission submission) =>
        new(submission.Kind, ItemIdOf(submission));

    private static SaveJobRequest AsDraft(SaveJobRequest content) => new()
    {
        Slug = content.Slug,
        Title = content.Title,
        Organization = content.Organization,
        Description = content.Description,
        Category = content.Category,
        EmploymentType = content.EmploymentType,
        WorkMode = content.WorkMode,
        DistrictCode = content.DistrictCode,
        ApplicationUrl = content.ApplicationUrl,
        ApplicationDeadline = content.ApplicationDeadline,
        IsPublished = false,
    };

    private static SaveEventRequest AsDraft(SaveEventRequest content) => new()
    {
        Slug = content.Slug,
        Title = content.Title,
        Summary = content.Summary,
        Description = content.Description,
        Category = content.Category,
        Organiser = content.Organiser,
        BusinessSlug = content.BusinessSlug,
        DistrictCode = content.DistrictCode,
        Venue = content.Venue,
        RegistrationUrl = content.RegistrationUrl,
        Capacity = content.Capacity,
        TicketPrice = content.TicketPrice,
        StartsAt = content.StartsAt,
        EndsAt = content.EndsAt,
        IsPublished = false,
    };

    private static SaveDealRequest AsDraft(SaveDealRequest content) => new()
    {
        Slug = content.Slug,
        Title = content.Title,
        Summary = content.Summary,
        Description = content.Description,
        DiscountText = content.DiscountText,
        BusinessSlug = content.BusinessSlug,
        DistrictCode = content.DistrictCode,
        RedemptionUrl = content.RedemptionUrl,
        StartsOn = content.StartsOn,
        EndsOn = content.EndsOn,
        IsPublished = false,
    };

    private static string? Blank(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    /// <summary>What a submission's draft reads as, once looked up in its own table.</summary>
    private sealed record ItemFacts(string Slug, string Title, bool IsPublished);

    /// <summary>Which table and row a submission points at — the pair the lookup keys on.</summary>
    private sealed record ItemKey(SubmissionKind Kind, Guid ItemId);
}
