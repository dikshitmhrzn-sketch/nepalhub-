using Microsoft.EntityFrameworkCore;
using NepalHub.Api.Common;
using NepalHub.Api.Data;
using NepalHub.Api.Data.Entities;
using NepalHub.Api.Features.Admin;
using NepalHub.Api.Features.Quiz;

namespace NepalHub.Api.Features.Claims;

/// <summary>
/// Instant claims on published jobs, offers and events — the staff-free twin of
/// <c>OwnershipService</c>. Filing hides the post from every public read in the same
/// SaveChanges (the <c>IsClaimed</c> flag <see cref="PublicContentScope"/> filters on) and
/// hands the claimant's details to whoever created the post. The poster accepts — the row
/// stays hidden — or releases it back into circulation.
///
/// <para>
/// One active claim per post is checked here, not by a unique index: MySQL has no partial
/// unique index, and a released claimant must be able to try again. The claim lookup for
/// filing deliberately does <em>not</em> use <see cref="PublicContentScope"/>'s claimed
/// exclusion, so a second claimant gets <see cref="FileContentClaimStatus.AlreadyTaken"/>
/// rather than a 404 that cannot tell them what happened.
/// </para>
/// </summary>
public sealed class ContentClaimService(
    AppDbContext dbContext,
    IAuditLog auditLog,
    INepalDateProvider dates)
{
    /// <summary>Files a claim on one published, unclaimed post and hides it immediately.</summary>
    internal async Task<FileContentClaimOutcome> FileClaimAsync(
        SubmissionKind kind,
        string slug,
        Guid claimantUserId,
        string? note,
        CancellationToken cancellationToken)
    {
        var item = await FindPublicAsync(kind, slug, cancellationToken);

        if (item is null)
        {
            return new FileContentClaimOutcome(FileContentClaimStatus.NotFound);
        }

        var itemId = item.Value.Id;
        var title = item.Value.Title;
        var posterUserId = item.Value.CreatedByUserId;

        if (posterUserId is null)
        {
            return new FileContentClaimOutcome(FileContentClaimStatus.NoPoster);
        }

        // MySQL has no partial unique index, so "one active claim per post" is checked here,
        // where the refusal can be named. Released claims do not block a fresh attempt.
        var activeClaims = await ActiveClaimsOnAsync(kind, itemId, cancellationToken);

        if (activeClaims is { Count: > 0 })
        {
            return new FileContentClaimOutcome(
                activeClaims.Any(claim => claim.ClaimantUserId == claimantUserId)
                    ? FileContentClaimStatus.AlreadyClaimed
                    : FileContentClaimStatus.AlreadyTaken);
        }

        var claim = new ContentClaim
        {
            Id = Guid.NewGuid(),
            Kind = kind,
            ClaimantUserId = claimantUserId,
            Note = Blank(note),
            Status = ContentClaimStatus.Pending,
            CreatedAt = dates.UtcNow,
        };

        AssignClaimId(claim, kind, itemId);

        SetClaimed(item.Value with { IsClaimed = true }, cancellationToken);

        dbContext.ContentClaims.Add(claim);

        auditLog.Record(
            claimantUserId,
            AuditActions.ContentClaimed,
            "ContentClaim",
            claim.Id.ToString(),
            after: new { kind, slug, title });

        await dbContext.SaveChangesAsync(cancellationToken);

        return new FileContentClaimOutcome(
            FileContentClaimStatus.Created,
            ToMine(claim, slug, title));
    }

    /// <summary>Every claim this person filed, newest first.</summary>
    internal async Task<IReadOnlyList<MyContentClaimResponse>> ListMineAsync(
        Guid claimantUserId,
        CancellationToken cancellationToken)
    {
        var claims = await dbContext.ContentClaims.AsNoTracking()
            .Include(claim => claim.Job)
            .Include(claim => claim.Event)
            .Include(claim => claim.Deal)
            .Where(claim => claim.ClaimantUserId == claimantUserId)
            .OrderByDescending(claim => claim.CreatedAt)
            .ToListAsync(cancellationToken);

        return claims.Select(claim => ToMine(claim, SlugOf(claim), TitleOf(claim))).ToList();
    }

    private static ClaimKind ToClaimKind(SubmissionKind kind) => kind switch
    {
        SubmissionKind.Job => ClaimKind.Job,
        SubmissionKind.Event => ClaimKind.Event,
        _ => ClaimKind.Deal,
    };

    /// <summary>Every claim on a post this person created, pending first, then newest.</summary>
    internal async Task<IReadOnlyList<PostedContentClaimResponse>> ListOnMyPostsAsync(
        Guid posterUserId,
        CancellationToken cancellationToken)
    {
        var claims = await dbContext.ContentClaims.AsNoTracking()
            .Include(claim => claim.Claimant)
            .Include(claim => claim.Job)
            .Include(claim => claim.Event)
            .Include(claim => claim.Deal)
            .Where(claim =>
                (claim.Job != null && claim.Job.CreatedByUserId == posterUserId)
                || (claim.Event != null && claim.Event.CreatedByUserId == posterUserId)
                || (claim.Deal != null && claim.Deal.CreatedByUserId == posterUserId))
            .OrderByDescending(claim => claim.Status == ContentClaimStatus.Pending)
            .ThenByDescending(claim => claim.CreatedAt)
            .ToListAsync(cancellationToken);

        return claims.Select(claim => ToPosted(claim, SlugOf(claim), TitleOf(claim))).ToList();
    }

    /// <summary>The poster acknowledges the claimant; the post stays hidden.</summary>
    internal async Task<ContentClaimDecisionOutcome> AcceptAsync(
        Guid claimId,
        Guid posterUserId,
        string? reviewNote,
        CancellationToken cancellationToken) =>
        await DecideAsync(claimId, posterUserId, ContentClaimStatus.Accepted, reviewNote, cancellationToken);

    /// <summary>The poster turns the claim down; the post is public again.</summary>
    internal async Task<ContentClaimDecisionOutcome> ReleaseAsync(
        Guid claimId,
        Guid posterUserId,
        string? reviewNote,
        CancellationToken cancellationToken) =>
        await DecideAsync(claimId, posterUserId, ContentClaimStatus.Released, reviewNote, cancellationToken);

    private async Task<ContentClaimDecisionOutcome> DecideAsync(
        Guid claimId,
        Guid posterUserId,
        ContentClaimStatus decision,
        string? reviewNote,
        CancellationToken cancellationToken)
    {
        var claim = await dbContext.ContentClaims.FirstOrDefaultAsync(
            item => item.Id == claimId,
            cancellationToken);

        if (claim is null || !await IsPosterAsync(claim, posterUserId, cancellationToken))
        {
            return new ContentClaimDecisionOutcome(ContentClaimDecisionStatus.NotFound);
        }

        if (claim.Status != ContentClaimStatus.Pending)
        {
            return new ContentClaimDecisionOutcome(ContentClaimDecisionStatus.AlreadyReviewed);
        }

        claim.Status = decision;
        claim.ReviewedAt = dates.UtcNow;
        claim.ReviewNote = Blank(reviewNote);

        // Release is the only path that unhides: accept keeps the post off the public pages.
        if (decision == ContentClaimStatus.Released)
        {
            await SetClaimedByIdAsync(claim, isClaimed: false, cancellationToken);
        }

        auditLog.Record(
            posterUserId,
            decision == ContentClaimStatus.Accepted
                ? AuditActions.ContentClaimAccepted
                : AuditActions.ContentClaimReleased,
            "ContentClaim",
            claim.Id.ToString(),
            after: new { status = decision.ToString(), note = claim.ReviewNote });

        await dbContext.SaveChangesAsync(cancellationToken);

        var slug = await SlugOfAsync(claim, cancellationToken);
        var title = await TitleOfAsync(claim, cancellationToken);

        return new ContentClaimDecisionOutcome(
            ContentClaimDecisionStatus.Accepted,
            ToPosted(claim, slug, title));
    }

    /// <summary>
    /// The claim's post, as the poster's decision needs it. Loads tracked so the accept or
    /// release and the claim row commit in one SaveChanges with their audit line.
    /// </summary>
    private async Task<bool> IsPosterAsync(
        ContentClaim claim,
        Guid posterUserId,
        CancellationToken cancellationToken)
    {
        var poster = claim.Kind switch
        {
            SubmissionKind.Job => await dbContext.Jobs.AsNoTracking()
                .Where(job => job.Id == claim.JobId)
                .Select(job => job.CreatedByUserId)
                .FirstOrDefaultAsync(cancellationToken),
            SubmissionKind.Event => await dbContext.Events.AsNoTracking()
                .Where(calendarEvent => calendarEvent.Id == claim.EventId)
                .Select(calendarEvent => calendarEvent.CreatedByUserId)
                .FirstOrDefaultAsync(cancellationToken),
            _ => await dbContext.Deals.AsNoTracking()
                .Where(deal => deal.Id == claim.DealId)
                .Select(deal => deal.CreatedByUserId)
                .FirstOrDefaultAsync(cancellationToken),
        };

        return poster == posterUserId;
    }

    private async Task SetClaimedByIdAsync(
        ContentClaim claim,
        bool isClaimed,
        CancellationToken cancellationToken)
    {
        switch (claim.Kind)
        {
            case SubmissionKind.Job:
                var job = await dbContext.Jobs.FirstAsync(
                    item => item.Id == claim.JobId,
                    cancellationToken);
                job.IsClaimed = isClaimed;
                break;

            case SubmissionKind.Event:
                var calendarEvent = await dbContext.Events.FirstAsync(
                    item => item.Id == claim.EventId,
                    cancellationToken);
                calendarEvent.IsClaimed = isClaimed;
                break;

            default:
                var deal = await dbContext.Deals.FirstAsync(
                    item => item.Id == claim.DealId,
                    cancellationToken);
                deal.IsClaimed = isClaimed;
                break;
        }
    }

    /// <summary>
    /// The post a claim would attach to: published, still open, and not already held by an
    /// active claim. Returns the identity fields the claim row and the hide flag need.
    /// </summary>
    private async Task<ClaimableItem?> FindPublicAsync(
        SubmissionKind kind,
        string slug,
        CancellationToken cancellationToken)
    {
        var today = dates.Today;
        var now = dates.UtcNow;

        switch (kind)
        {
            case SubmissionKind.Job:
                var job = await dbContext.Jobs.AsNoTracking()
                    .Where(item => item.Slug == slug
                        && item.IsPublished
                        && item.ApplicationDeadline >= today)
                    .Select(item => new { item.Id, item.Title, item.CreatedByUserId, item.IsClaimed })
                    .FirstOrDefaultAsync(cancellationToken);

                return job is null
                    ? null
                    : new ClaimableItem(SubmissionKind.Job, job.Id, job.Title, job.CreatedByUserId, job.IsClaimed);

            case SubmissionKind.Event:
                var calendarEvent = await dbContext.Events.AsNoTracking()
                    .Where(item => item.Slug == slug
                        && item.IsPublished
                        && item.EndsAt >= now)
                    .Select(item => new { item.Id, item.Title, item.CreatedByUserId, item.IsClaimed })
                    .FirstOrDefaultAsync(cancellationToken);

                return calendarEvent is null
                    ? null
                    : new ClaimableItem(SubmissionKind.Event, calendarEvent.Id, calendarEvent.Title, calendarEvent.CreatedByUserId, calendarEvent.IsClaimed);

            default:
                var deal = await dbContext.Deals.AsNoTracking()
                    .Where(item => item.Slug == slug
                        && item.IsPublished
                        && item.EndsOn >= today)
                    .Select(item => new { item.Id, item.Title, item.CreatedByUserId, item.IsClaimed })
                    .FirstOrDefaultAsync(cancellationToken);

                return deal is null
                    ? null
                    : new ClaimableItem(SubmissionKind.Deal, deal.Id, deal.Title, deal.CreatedByUserId, deal.IsClaimed);
        }
    }

    private void SetClaimed(ClaimableItem item, CancellationToken cancellationToken)
    {
        _ = cancellationToken;
        switch (item.Kind)
        {
            case SubmissionKind.Job:
                var job = dbContext.Jobs.First(entry => entry.Id == item.Id);
                job.IsClaimed = item.IsClaimed;
                break;

            case SubmissionKind.Event:
                var calendarEvent = dbContext.Events.First(entry => entry.Id == item.Id);
                calendarEvent.IsClaimed = item.IsClaimed;
                break;

            default:
                var deal = dbContext.Deals.First(entry => entry.Id == item.Id);
                deal.IsClaimed = item.IsClaimed;
                break;
        }
    }

    /// <summary>
    /// Active claims on one post, as a materialised list: the service needs both "anyone"
    /// and "this person" answers, and a method call inside the expression tree would not
    /// translate to SQL anyway.
    /// </summary>
    private async Task<List<ContentClaim>> ActiveClaimsOnAsync(
        SubmissionKind kind,
        Guid itemId,
        CancellationToken cancellationToken)
    {
        var query = dbContext.ContentClaims.AsNoTracking()
            .Where(claim => claim.Status == ContentClaimStatus.Pending
                || claim.Status == ContentClaimStatus.Accepted);

        query = kind switch
        {
            SubmissionKind.Job => query.Where(claim => claim.JobId == itemId),
            SubmissionKind.Event => query.Where(claim => claim.EventId == itemId),
            _ => query.Where(claim => claim.DealId == itemId),
        };

        return await query.ToListAsync(cancellationToken);
    }

    private static void AssignClaimId(ContentClaim claim, SubmissionKind kind, Guid itemId)
    {
        switch (kind)
        {
            case SubmissionKind.Job:
                claim.JobId = itemId;
                break;
            case SubmissionKind.Event:
                claim.EventId = itemId;
                break;
            default:
                claim.DealId = itemId;
                break;
        }
    }

    private static string SlugOf(ContentClaim claim) =>
        claim.Job?.Slug ?? claim.Event?.Slug ?? claim.Deal?.Slug ?? string.Empty;

    private static string TitleOf(ContentClaim claim) =>
        claim.Job?.Title ?? claim.Event?.Title ?? claim.Deal?.Title ?? string.Empty;

    private async Task<string> SlugOfAsync(ContentClaim claim, CancellationToken cancellationToken) =>
        claim.Kind switch
        {
            SubmissionKind.Job => await dbContext.Jobs.AsNoTracking()
                .Where(item => item.Id == claim.JobId)
                .Select(item => item.Slug)
                .FirstAsync(cancellationToken),
            SubmissionKind.Event => await dbContext.Events.AsNoTracking()
                .Where(item => item.Id == claim.EventId)
                .Select(item => item.Slug)
                .FirstAsync(cancellationToken),
            _ => await dbContext.Deals.AsNoTracking()
                .Where(item => item.Id == claim.DealId)
                .Select(item => item.Slug)
                .FirstAsync(cancellationToken),
        };

    private async Task<string> TitleOfAsync(ContentClaim claim, CancellationToken cancellationToken) =>
        claim.Kind switch
        {
            SubmissionKind.Job => await dbContext.Jobs.AsNoTracking()
                .Where(item => item.Id == claim.JobId)
                .Select(item => item.Title)
                .FirstAsync(cancellationToken),
            SubmissionKind.Event => await dbContext.Events.AsNoTracking()
                .Where(item => item.Id == claim.EventId)
                .Select(item => item.Title)
                .FirstAsync(cancellationToken),
            _ => await dbContext.Deals.AsNoTracking()
                .Where(item => item.Id == claim.DealId)
                .Select(item => item.Title)
                .FirstAsync(cancellationToken),
        };

    private static MyContentClaimResponse ToMine(ContentClaim claim, string slug, string title) =>
        new(
            claim.Id,
            ToClaimKind(claim.Kind),
            slug,
            title,
            claim.Status,
            claim.Note,
            claim.CreatedAt,
            claim.ReviewedAt,
            claim.ReviewNote);

    private static PostedContentClaimResponse ToPosted(
        ContentClaim claim,
        string slug,
        string title) =>
        new(
            claim.Id,
            ToClaimKind(claim.Kind),
            slug,
            title,
            claim.Claimant?.FullName
                ?? claim.Claimant?.UserName
                ?? claim.Claimant?.Email
                ?? "A member",
            claim.Claimant?.Email,
            claim.Status,
            claim.Note,
            claim.CreatedAt,
            claim.ReviewedAt,
            claim.ReviewNote);

    private static string? Blank(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private readonly record struct ClaimableItem(
        SubmissionKind Kind,
        Guid Id,
        string Title,
        Guid? CreatedByUserId,
        bool IsClaimed);
}
