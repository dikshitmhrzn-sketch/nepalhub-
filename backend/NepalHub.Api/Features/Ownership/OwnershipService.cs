using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using NepalHub.Api.Common;
using NepalHub.Api.Data;
using NepalHub.Api.Data.Entities;
using NepalHub.Api.Features.Admin;
using NepalHub.Api.Features.Auth;
using NepalHub.Api.Features.Businesses;
using NepalHub.Api.Features.Quiz;

namespace NepalHub.Api.Features.Ownership;

public enum ClaimSubmitStatus
{
    Created,

    /// <summary>No published listing at that address, so there is nothing to claim.</summary>
    NotFound,

    /// <summary>Someone already manages this listing.</summary>
    AlreadyOwned,

    /// <summary>The caller is already the owner.</summary>
    AlreadyOwner,

    /// <summary>The caller has a claim still waiting in the queue for this listing.</summary>
    AlreadyClaimed,
}

public enum ClaimReviewStatus
{
    Approved,
    Rejected,

    /// <summary>No claim with that id.</summary>
    NotFound,

    /// <summary>It was approved or rejected already; the queue does not re-decide.</summary>
    AlreadyReviewed,

    /// <summary>Somebody else became the owner between the queue rendering and this decision.</summary>
    BusinessAlreadyOwned,

    /// <summary>The identity store refused the role grant, so nothing at all was applied.</summary>
    Failed,
}

public sealed record ClaimSubmitOutcome(ClaimSubmitStatus Status, ClaimResponse? Claim = null);

public sealed record ClaimReviewOutcome(
    ClaimReviewStatus Status,
    AdminClaimSummaryResponse? Claim = null,
    IReadOnlyList<FieldError>? Errors = null);

/// <summary>
/// Ownership of a listing: claiming one as a member, and the staff queue that decides.
///
/// <para>
/// A claim grants nothing on its own. The listing only gains an owner when staff approve,
/// and the approval writes ownership, settles the rival claims, grants the business role and
/// appends the audit entry in one SaveChanges — so a half-applied approval cannot exist: no
/// owner without a role, no role without a log line, no log line for something rolled back.
/// </para>
///
/// <para>
/// The claim lookup is scoped to published listings, so this endpoint answers the same 404
/// for a draft and an unknown slug and cannot be used to discover which listings staff have
/// not published yet.
/// </para>
/// </summary>
public sealed class OwnershipService(
    AppDbContext dbContext,
    IAuditLog auditLog,
    UserManager<ApplicationUser> userManager,
    INepalDateProvider dates)
{
    // ---------------------------------------------------------------- member side

    internal async Task<ClaimSubmitOutcome> ClaimAsync(
        Guid claimantUserId,
        string slug,
        string? evidence,
        CancellationToken cancellationToken)
    {
        var business = await PublicContentScope.Businesses(dbContext.Businesses)
            .Where(item => item.Slug == slug)
            .FirstOrDefaultAsync(cancellationToken);

        if (business is null)
        {
            return new ClaimSubmitOutcome(ClaimSubmitStatus.NotFound);
        }

        if (business.OwnerUserId == claimantUserId)
        {
            return new ClaimSubmitOutcome(ClaimSubmitStatus.AlreadyOwner);
        }

        if (business.OwnerUserId is not null)
        {
            return new ClaimSubmitOutcome(ClaimSubmitStatus.AlreadyOwned);
        }

        // The one rule the database cannot hold for us: MySQL has no partial unique index, so
        // "one pending claim per person per listing" is checked here. A rejected claim does not
        // block a corrected retry, which is exactly why it cannot be a plain unique index either.
        var hasPendingClaim = await dbContext.BusinessClaims.AnyAsync(
            claim => claim.BusinessId == business.Id
                && claim.ClaimantUserId == claimantUserId
                && claim.Status == BusinessClaimStatus.Pending,
            cancellationToken);

        if (hasPendingClaim)
        {
            return new ClaimSubmitOutcome(ClaimSubmitStatus.AlreadyClaimed);
        }

        var claim = new BusinessClaim
        {
            Id = Guid.NewGuid(),
            BusinessId = business.Id,
            ClaimantUserId = claimantUserId,
            Evidence = Blank(evidence),
            Status = BusinessClaimStatus.Pending,
            CreatedAt = dates.UtcNow,
        };

        dbContext.BusinessClaims.Add(claim);
        await dbContext.SaveChangesAsync(cancellationToken);

        return new ClaimSubmitOutcome(
            ClaimSubmitStatus.Created,
            new ClaimResponse(
                claim.Id,
                business.Id,
                business.Slug,
                business.Name,
                claim.Status,
                claim.Evidence,
                claim.CreatedAt,
                claim.ReviewedAt,
                claim.ReviewNote));
    }

    /// <summary>
    /// Everything the account page needs in one read: the listings this person manages, in any
    /// publish state, and every claim they have filed, newest first. Drafts appear here because
    /// ownership does not care about publication — an owner whose listing staff retracted still
    /// manages it, and losing sight of it would be exactly the wrong outcome.
    /// </summary>
    internal async Task<MyBusinessesResponse> ListMineAsync(Guid userId, CancellationToken cancellationToken)
    {
        var owned = await dbContext.Businesses
            .Where(business => business.OwnerUserId == userId)
            .Include(business => business.District)
            .OrderBy(business => business.Name)
            .ThenBy(business => business.Id)
            .Select(business => new MyBusinessSummary(
                business.Id,
                business.Slug,
                business.Name,
                business.Category,
                business.District == null ? string.Empty : business.District.Name,
                business.IsPublished,
                business.IsVerified))
            .ToListAsync(cancellationToken);

        var claims = await dbContext.BusinessClaims
            .Where(claim => claim.ClaimantUserId == userId)
            .Include(claim => claim.Business)
            .OrderByDescending(claim => claim.CreatedAt)
            .ThenBy(claim => claim.Id)
            .Select(claim => new ClaimResponse(
                claim.Id,
                claim.BusinessId,
                claim.Business == null ? string.Empty : claim.Business.Slug,
                claim.Business == null ? string.Empty : claim.Business.Name,
                claim.Status,
                claim.Evidence,
                claim.CreatedAt,
                claim.ReviewedAt,
                claim.ReviewNote))
            .ToListAsync(cancellationToken);

        var verifications = await dbContext.BusinessVerificationRequests
            .Where(request => request.SubmittedByUserId == userId)
            .Include(request => request.Business)
            .OrderByDescending(request => request.CreatedAt)
            .ThenBy(request => request.Id)
            .Select(request => new VerificationResponse(
                request.Id,
                request.BusinessId,
                request.Business == null ? string.Empty : request.Business.Slug,
                request.Business == null ? string.Empty : request.Business.Name,
                request.Status,
                request.Note,
                request.CreatedAt,
                request.ReviewedAt,
                request.ReviewNote))
            .ToListAsync(cancellationToken);

        return new MyBusinessesResponse(owned, claims, verifications);
    }

    // ----------------------------------------------------------------- staff side

    /// <summary>
    /// The queue. Pending work sorts first — the enum is stored as its name, so the column
    /// itself would order "Approved" ahead of "Pending" alphabetically — and newest first
    /// within each band, which is the order the report queue already teaches.
    /// </summary>
    internal async Task<PagedResult<AdminClaimSummaryResponse>> ListForAdminAsync(
        PageRequest paging,
        BusinessClaimStatus? status,
        CancellationToken cancellationToken)
    {
        var matching =
            from claim in dbContext.BusinessClaims
            join claimant in dbContext.Users on claim.ClaimantUserId equals claimant.Id
            join business in dbContext.Businesses on claim.BusinessId equals business.Id
            where status == null || claim.Status == status
            orderby (claim.Status == BusinessClaimStatus.Pending ? 0 : 1) ascending,
                    claim.CreatedAt descending
            select new
            {
                claim.Id,
                claim.BusinessId,
                Slug = business.Slug,
                BusinessName = business.Name,
                ClaimantName = claimant.FullName,
                ClaimantEmail = claimant.Email,
                claim.Status,
                claim.Evidence,
                claim.CreatedAt,
                claim.ReviewedAt,
                claim.ReviewNote,
            };

        var total = await matching.CountAsync(cancellationToken);
        var clamped = paging.ClampTo(total);

        var rows = await matching
            .Skip(clamped.Skip)
            .Take(clamped.PageSize)
            .ToListAsync(cancellationToken);

        var items = rows
            .Select(row => new AdminClaimSummaryResponse(
                row.Id,
                row.BusinessId,
                row.Slug,
                row.BusinessName,
                row.ClaimantName,
                row.ClaimantEmail,
                row.Status,
                row.Evidence,
                row.CreatedAt,
                row.ReviewedAt,
                row.ReviewNote))
            .ToList();

        return new PagedResult<AdminClaimSummaryResponse>(items, clamped.Page, clamped.PageSize, total);
    }

    /// <summary>
    /// Approves a claim: the listing gains an owner, every other pending claim on it is
    /// settled in the same pass, the claimant gains the business role, and the decision is
    /// written to the audit log — all by one SaveChanges. Approving a claim for a listing that
    /// already has a different owner is refused, because ownership moved while the queue sat
    /// open and the newer fact wins.
    /// </summary>
    internal async Task<ClaimReviewOutcome> ApproveAsync(
        Guid claimId,
        string? reviewNote,
        Guid adminUserId,
        string? ip,
        CancellationToken cancellationToken)
    {
        var claim = await LoadForReviewAsync(claimId, cancellationToken);

        if (claim is null)
        {
            return new ClaimReviewOutcome(ClaimReviewStatus.NotFound);
        }

        if (claim.Status != BusinessClaimStatus.Pending)
        {
            return new ClaimReviewOutcome(ClaimReviewStatus.AlreadyReviewed, View(claim));
        }

        var business = claim.Business!;
        var alreadyOwnedByOther = business.OwnerUserId is { } ownerId && ownerId != claim.ClaimantUserId;

        if (alreadyOwnedByOther)
        {
            return new ClaimReviewOutcome(ClaimReviewStatus.BusinessAlreadyOwned, View(claim));
        }

        // Rivals are settled here rather than left in the queue: once one claim wins, the
        // others are asking for something that can no longer be granted, and an open queue
        // row for a decided question is a decision nobody will ever make.
        var rivals = await dbContext.BusinessClaims
            .Where(other => other.BusinessId == claim.BusinessId
                && other.Id != claim.Id
                && other.Status == BusinessClaimStatus.Pending)
            .ToListAsync(cancellationToken);

        var reviewedAt = dates.UtcNow;

        foreach (var rival in rivals)
        {
            rival.Status = BusinessClaimStatus.Rejected;
            rival.ReviewedByUserId = adminUserId;
            rival.ReviewedAt = reviewedAt;
            rival.ReviewNote = "Another claim on this listing was approved.";
        }

        claim.Status = BusinessClaimStatus.Approved;
        claim.ReviewedByUserId = adminUserId;
        claim.ReviewedAt = reviewedAt;
        claim.ReviewNote = Blank(reviewNote);

        business.OwnerUserId = claim.ClaimantUserId;

        // The audit entry is written before the role is granted, not after, so the identity
        // store's save — which shares this request's DbContext — flushes the claim, the
        // rival settlements, the ownership change and the log line in a single transaction.
        // A failure in the grant then rolls all of it back rather than leaving an owner with
        // no role and no record of how it happened.
        var heldBefore = (await userManager.GetRolesAsync(claim.Claimant!))
            .Contains(Roles.Business, StringComparer.Ordinal);

        auditLog.Record(
            adminUserId,
            AuditActions.BusinessClaimApproved,
            "business_claim",
            claim.Id.ToString(),
            before: new { status = nameof(BusinessClaimStatus.Pending), ownerUserId = (Guid?)null },
            after: new
            {
                status = nameof(BusinessClaimStatus.Approved),
                ownerUserId = claim.ClaimantUserId,
                roleGranted = !heldBefore,
                rivalClaimsRejected = rivals.Count,
            },
            ip);

        if (!heldBefore && await GrantBusinessRoleAsync(claim.Claimant!) is null)
        {
            return new ClaimReviewOutcome(ClaimReviewStatus.Failed);
        }

        await dbContext.SaveChangesAsync(cancellationToken);

        return new ClaimReviewOutcome(ClaimReviewStatus.Approved, View(claim));
    }

    /// <summary>Turns one pending claim down, leaving the listing unowned.</summary>
    internal async Task<ClaimReviewOutcome> RejectAsync(
        Guid claimId,
        string? reviewNote,
        Guid adminUserId,
        string? ip,
        CancellationToken cancellationToken)
    {
        var claim = await LoadForReviewAsync(claimId, cancellationToken);

        if (claim is null)
        {
            return new ClaimReviewOutcome(ClaimReviewStatus.NotFound);
        }

        if (claim.Status != BusinessClaimStatus.Pending)
        {
            return new ClaimReviewOutcome(ClaimReviewStatus.AlreadyReviewed, View(claim));
        }

        claim.Status = BusinessClaimStatus.Rejected;
        claim.ReviewedByUserId = adminUserId;
        claim.ReviewedAt = dates.UtcNow;
        claim.ReviewNote = Blank(reviewNote);

        auditLog.Record(
            adminUserId,
            AuditActions.BusinessClaimRejected,
            "business_claim",
            claim.Id.ToString(),
            before: new { status = nameof(BusinessClaimStatus.Pending) },
            after: new { status = nameof(BusinessClaimStatus.Rejected), note = claim.ReviewNote },
            ip);

        await dbContext.SaveChangesAsync(cancellationToken);

        return new ClaimReviewOutcome(ClaimReviewStatus.Rejected, View(claim));
    }

    // ------------------------------------------------------------------ internals

    private async Task<BusinessClaim?> LoadForReviewAsync(Guid claimId, CancellationToken cancellationToken) =>
        await dbContext.BusinessClaims
            .Include(claim => claim.Business)
            .Include(claim => claim.Claimant)
            .FirstOrDefaultAsync(claim => claim.Id == claimId, cancellationToken);

    /// <summary>
    /// Adds the business role when the account does not hold it yet. Null means the identity
    /// store refused, which is the one outcome that must stop the approval: a listing with an
    /// owner who cannot reach the owner screen is worse than a claim left in the queue. The
    /// role already being present is success, not failure — it just means this approval is not
    /// what put it there.
    /// </summary>
    private async Task<bool?> GrantBusinessRoleAsync(ApplicationUser claimant)
    {
        var currentRoles = await userManager.GetRolesAsync(claimant);

        if (currentRoles.Contains(Roles.Business, StringComparer.Ordinal))
        {
            return true;
        }

        var result = await userManager.AddToRoleAsync(claimant, Roles.Business);

        return result.Succeeded ? true : null;
    }

    private static AdminClaimSummaryResponse View(BusinessClaim claim) => new(
        claim.Id,
        claim.BusinessId,
        claim.Business?.Slug ?? string.Empty,
        claim.Business?.Name ?? string.Empty,
        claim.Claimant?.FullName ?? string.Empty,
        claim.Claimant?.Email,
        claim.Status,
        claim.Evidence,
        claim.CreatedAt,
        claim.ReviewedAt,
        claim.ReviewNote);

    private static string? Blank(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
