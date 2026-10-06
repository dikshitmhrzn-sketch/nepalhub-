using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using NepalHub.Api.Common;
using NepalHub.Api.Data;
using NepalHub.Api.Data.Entities;
using NepalHub.Api.Features.Admin;
using NepalHub.Api.Features.Auth;
using NepalHub.Api.Features.Quiz;

namespace NepalHub.Api.Features.Businesses;

/// <summary>
/// Registration's other half: the owner asks, staff decide, and the listing goes live.
///
/// <para>
/// A request grants nothing on its own — not the badge and not the public address — so the
/// queue is the same shape as the claim queue it sits beside, and for the same reason: the
/// thing being decided is a fact about a listing that readers will act on, which is
/// moderation work rather than administration.
/// </para>
///
/// <para>
/// Approving is one statement with two halves: the row satisfies the publish rules, and
/// staff stand behind it. The rules are read from <see cref="BusinessService"/>'s single
/// definition rather than restated here, so an approval can never publish a listing the
/// staff editor would have refused, and the approval writes the badge, the live flag, the
/// review and the audit entry in one SaveChanges — no state in between those facts exists.
/// </para>
/// </summary>
public sealed class BusinessVerificationService(
    AppDbContext dbContext,
    IAuditLog auditLog,
    UserManager<ApplicationUser> userManager,
    INepalDateProvider dates)
{
    // ---------------------------------------------------------------- member side

    /// <summary>
    /// Opens a request for one listing this caller owns. The lookup is scoped to ownership,
    /// so somebody else's slug answers the same 404 as one that never existed and this
    /// controller cannot be used to discover which addresses are real.
    /// </summary>
    internal async Task<VerificationSubmitOutcome> SubmitAsync(
        Guid ownerUserId,
        string slug,
        string? note,
        CancellationToken cancellationToken)
    {
        var business = await dbContext.Businesses
            .FirstOrDefaultAsync(
                item => item.Slug == slug && item.OwnerUserId == ownerUserId,
                cancellationToken);

        if (business is null)
        {
            return new VerificationSubmitOutcome(VerificationSubmitStatus.NotFound);
        }

        if (business.IsPublished)
        {
            return new VerificationSubmitOutcome(VerificationSubmitStatus.AlreadyPublished);
        }

        var open = await dbContext.BusinessVerificationRequests
            .AnyAsync(
                item => item.BusinessId == business.Id && item.Status == BusinessVerificationStatus.Pending,
                cancellationToken);

        if (open)
        {
            return new VerificationSubmitOutcome(VerificationSubmitStatus.AlreadyPending);
        }

        var request = new BusinessVerificationRequest
        {
            Id = Guid.NewGuid(),
            BusinessId = business.Id,
            SubmittedByUserId = ownerUserId,
            Note = Blank(note),
            Status = BusinessVerificationStatus.Pending,
        };

        dbContext.BusinessVerificationRequests.Add(request);
        await dbContext.SaveChangesAsync(cancellationToken);

        return new VerificationSubmitOutcome(
            VerificationSubmitStatus.Created,
            new VerificationResponse(
                request.Id,
                request.BusinessId,
                business.Slug,
                business.Name,
                request.Status,
                request.Note,
                request.CreatedAt,
                request.ReviewedAt,
                request.ReviewNote));
    }

    // ----------------------------------------------------------------- staff side

    /// <summary>
    /// The queue. Pending work sorts first — the enum is stored as its name, so the column
    /// itself would order "Approved" ahead of "Pending" alphabetically — and newest first
    /// within each band, which is the order the report and claim queues already teach.
    /// </summary>
    internal async Task<PagedResult<AdminVerificationSummaryResponse>> ListForAdminAsync(
        PageRequest paging,
        BusinessVerificationStatus? status,
        CancellationToken cancellationToken)
    {
        var matching =
            from request in dbContext.BusinessVerificationRequests
            join owner in dbContext.Users on request.SubmittedByUserId equals owner.Id
            join business in dbContext.Businesses on request.BusinessId equals business.Id
            where status == null || request.Status == status
            orderby (request.Status == BusinessVerificationStatus.Pending ? 0 : 1) ascending,
                    request.CreatedAt descending
            select new
            {
                request.Id,
                request.BusinessId,
                Slug = business.Slug,
                BusinessName = business.Name,
                OwnerName = owner.FullName,
                OwnerEmail = owner.Email,
                request.Status,
                request.Note,
                request.CreatedAt,
                request.ReviewedAt,
                request.ReviewNote,
            };

        var total = await matching.CountAsync(cancellationToken);
        var clamped = paging.ClampTo(total);

        var rows = await matching
            .Skip(clamped.Skip)
            .Take(clamped.PageSize)
            .ToListAsync(cancellationToken);

        var items = rows
            .Select(row => new AdminVerificationSummaryResponse(
                row.Id,
                row.BusinessId,
                row.Slug,
                row.BusinessName,
                row.OwnerName,
                row.OwnerEmail,
                row.Status,
                row.Note,
                row.CreatedAt,
                row.ReviewedAt,
                row.ReviewNote))
            .ToList();

        return new PagedResult<AdminVerificationSummaryResponse>(items, clamped.Page, clamped.PageSize, total);
    }

    /// <summary>
    /// Approves a request: the listing satisfies the publish rules, so it gains the badge
    /// and goes live, the owner gains the business role, and the decision is written to the
    /// audit log — all by one SaveChanges. A listing that no longer passes the rules answers
    /// 409 with the field errors rather than publishing something staff would retract.
    /// </summary>
    internal async Task<VerificationReviewOutcome> ApproveAsync(
        Guid requestId,
        string? reviewNote,
        Guid staffUserId,
        string? ip,
        CancellationToken cancellationToken)
    {
        var request = await LoadForReviewAsync(requestId, cancellationToken);

        if (request is null)
        {
            return new VerificationReviewOutcome(VerificationReviewStatus.NotFound);
        }

        if (request.Status != BusinessVerificationStatus.Pending)
        {
            return new VerificationReviewOutcome(VerificationReviewStatus.AlreadyReviewed, View(request));
        }

        var business = request.Business!;
        var blockers = BusinessService.CollectPublishBlockersFor(business);

        if (blockers.Count > 0)
        {
            return new VerificationReviewOutcome(
                VerificationReviewStatus.NotPublishable,
                View(request),
                blockers);
        }

        var reviewedAt = dates.UtcNow;

        request.Status = BusinessVerificationStatus.Approved;
        request.ReviewedByUserId = staffUserId;
        request.ReviewedAt = reviewedAt;
        request.ReviewNote = Blank(reviewNote);

        business.IsVerified = true;
        business.VerifiedAt = reviewedAt;
        business.IsPublished = true;

        // The audit entry is written before the role is granted, not after, so the identity
        // store's save — which shares this request's DbContext — flushes the request, the
        // publication, the badge and the log line in a single transaction. A failure in the
        // grant then rolls all of it back rather than leaving a published listing with no
        // record of who approved it.
        var owner = request.SubmittedBy!;
        var heldBefore = (await userManager.GetRolesAsync(owner))
            .Contains(Roles.Business, StringComparer.Ordinal);

        auditLog.Record(
            staffUserId,
            AuditActions.BusinessVerificationApproved,
            "business_verification",
            request.Id.ToString(),
            before: new { status = nameof(BusinessVerificationStatus.Pending), published = false },
            after: new
            {
                status = nameof(BusinessVerificationStatus.Approved),
                published = true,
                verified = true,
                roleGranted = !heldBefore,
            },
            ip);

        if (!heldBefore && await GrantBusinessRoleAsync(owner) is null)
        {
            return new VerificationReviewOutcome(VerificationReviewStatus.Failed, View(request));
        }

        await dbContext.SaveChangesAsync(cancellationToken);

        return new VerificationReviewOutcome(VerificationReviewStatus.Approved, View(request));
    }

    /// <summary>Turns the request down; the listing stays a draft and keeps no badge.</summary>
    internal async Task<VerificationReviewOutcome> RejectAsync(
        Guid requestId,
        string? reviewNote,
        Guid staffUserId,
        string? ip,
        CancellationToken cancellationToken)
    {
        var request = await LoadForReviewAsync(requestId, cancellationToken);

        if (request is null)
        {
            return new VerificationReviewOutcome(VerificationReviewStatus.NotFound);
        }

        if (request.Status != BusinessVerificationStatus.Pending)
        {
            return new VerificationReviewOutcome(VerificationReviewStatus.AlreadyReviewed, View(request));
        }

        request.Status = BusinessVerificationStatus.Rejected;
        request.ReviewedByUserId = staffUserId;
        request.ReviewedAt = dates.UtcNow;
        request.ReviewNote = Blank(reviewNote);

        auditLog.Record(
            staffUserId,
            AuditActions.BusinessVerificationRejected,
            "business_verification",
            request.Id.ToString(),
            before: new { status = nameof(BusinessVerificationStatus.Pending) },
            after: new { status = nameof(BusinessVerificationStatus.Rejected), note = request.ReviewNote },
            ip);

        await dbContext.SaveChangesAsync(cancellationToken);

        return new VerificationReviewOutcome(VerificationReviewStatus.Rejected, View(request));
    }

    // ------------------------------------------------------------------ internals

    private async Task<BusinessVerificationRequest?> LoadForReviewAsync(Guid requestId, CancellationToken cancellationToken) =>
        await dbContext.BusinessVerificationRequests
            .Include(request => request.Business)
            .Include(request => request.SubmittedBy)
            .FirstOrDefaultAsync(request => request.Id == requestId, cancellationToken);

    /// <summary>
    /// Adds the business role when the account does not hold it yet. Null means the identity
    /// store refused, which is the one outcome that must stop the approval: an owner who
    /// cannot reach the owner screen is worse than a request left in the queue. The role
    /// already being present is success, not failure.
    /// </summary>
    private async Task<bool?> GrantBusinessRoleAsync(ApplicationUser owner)
    {
        var currentRoles = await userManager.GetRolesAsync(owner);

        if (currentRoles.Contains(Roles.Business, StringComparer.Ordinal))
        {
            return true;
        }

        var result = await userManager.AddToRoleAsync(owner, Roles.Business);

        return result.Succeeded ? true : null;
    }

    private static AdminVerificationSummaryResponse View(BusinessVerificationRequest request) => new(
        request.Id,
        request.BusinessId,
        request.Business?.Slug ?? string.Empty,
        request.Business?.Name ?? string.Empty,
        request.SubmittedBy?.FullName ?? string.Empty,
        request.SubmittedBy?.Email,
        request.Status,
        request.Note,
        request.CreatedAt,
        request.ReviewedAt,
        request.ReviewNote);

    private static string? Blank(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
