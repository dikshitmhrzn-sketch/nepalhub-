using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using NepalHub.Api.Common;
using NepalHub.Api.Data;
using NepalHub.Api.Data.Entities;
using NepalHub.Api.Features.Admin;
using NepalHub.Api.Features.Auth;
using NepalHub.Api.Features.Ownership;
using NepalHub.Api.Features.Quiz;

namespace NepalHub.Api.Features.Claims;

/// <summary>
/// Instant claims on published businesses — the job-claim twin for the directory.
/// Filing leaves the listing public (unlike jobs): only the poster's accept sets
/// <see cref="Business.IsClaimed"/> and writes <see cref="Business.OwnerUserId"/>.
/// One active claim per listing is checked here, not by a unique index: MySQL has no
/// partial unique index, and a released claimant must be able to try again.
/// </summary>
public sealed class BusinessClaimService(
    AppDbContext dbContext,
    IAuditLog auditLog,
    UserManager<ApplicationUser> userManager,
    INepalDateProvider dates)
{
    /// <summary>Files a claim on one published, unowned listing. The listing stays public.</summary>
    internal async Task<ClaimSubmitOutcome> FileClaimAsync(
        Guid claimantUserId,
        string slug,
        string? evidence,
        CancellationToken cancellationToken)
    {
        // Deliberately not PublicContentScope: a claim on an accepted (hidden) listing must
        // answer already_owned rather than a 404, and a pending claim leaves the row public.
        var business = await dbContext.Businesses.AsNoTracking()
            .Where(item => item.Slug == slug && item.IsPublished)
            .FirstOrDefaultAsync(cancellationToken);

        if (business is null)
        {
            return new ClaimSubmitOutcome(ClaimSubmitStatus.NotFound);
        }

        if (business.OwnerUserId == claimantUserId)
        {
            return new ClaimSubmitOutcome(ClaimSubmitStatus.AlreadyOwner);
        }

        if (business.OwnerUserId is not null || business.IsClaimed)
        {
            return new ClaimSubmitOutcome(ClaimSubmitStatus.AlreadyOwned);
        }

        if (business.CreatedByUserId is null)
        {
            return new ClaimSubmitOutcome(ClaimSubmitStatus.NoPoster);
        }

        var activeClaims = await ActiveClaimsOnAsync(business.Id, cancellationToken);

        if (activeClaims.Count > 0)
        {
            return new ClaimSubmitOutcome(
                activeClaims.Any(claim => claim.ClaimantUserId == claimantUserId)
                    ? ClaimSubmitStatus.AlreadyClaimed
                    : ClaimSubmitStatus.AlreadyTaken);
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

        auditLog.Record(
            claimantUserId,
            AuditActions.BusinessClaimed,
            "business_claim",
            claim.Id.ToString(),
            after: new { slug, title = business.Name, status = nameof(BusinessClaimStatus.Pending) });

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

    /// <summary>Every business claim this person filed, newest first.</summary>
    internal async Task<IReadOnlyList<MyContentClaimResponse>> ListMineAsync(
        Guid claimantUserId,
        CancellationToken cancellationToken)
    {
        var claims = await dbContext.BusinessClaims.AsNoTracking()
            .Include(claim => claim.Business)
            .Where(claim => claim.ClaimantUserId == claimantUserId)
            .OrderByDescending(claim => claim.CreatedAt)
            .ToListAsync(cancellationToken);

        return claims.Select(ToMine).ToList();
    }

    /// <summary>Every claim on a listing this person created, pending first, then newest.</summary>
    internal async Task<IReadOnlyList<PostedContentClaimResponse>> ListOnMyPostsAsync(
        Guid posterUserId,
        CancellationToken cancellationToken)
    {
        var claims = await dbContext.BusinessClaims.AsNoTracking()
            .Include(claim => claim.Claimant)
            .Include(claim => claim.Business)
            .Where(claim => claim.Business != null && claim.Business.CreatedByUserId == posterUserId)
            .OrderByDescending(claim => claim.Status == BusinessClaimStatus.Pending)
            .ThenByDescending(claim => claim.CreatedAt)
            .ToListAsync(cancellationToken);

        return claims.Select(ToPosted).ToList();
    }

    /// <summary>The poster accepts: owner + role + IsClaimed, listing hides. Terminal for this claim.</summary>
    internal async Task<ContentClaimDecisionOutcome> AcceptAsync(
        Guid claimId,
        Guid posterUserId,
        string? reviewNote,
        CancellationToken cancellationToken)
    {
        var claim = await dbContext.BusinessClaims
            .Include(item => item.Business)
            .Include(item => item.Claimant)
            .FirstOrDefaultAsync(item => item.Id == claimId, cancellationToken);

        if (claim is null || claim.Business?.CreatedByUserId != posterUserId)
        {
            return new ContentClaimDecisionOutcome(ContentClaimDecisionStatus.NotFound);
        }

        if (claim.Status != BusinessClaimStatus.Pending)
        {
            return new ContentClaimDecisionOutcome(ContentClaimDecisionStatus.AlreadyReviewed);
        }

        var business = claim.Business!;
        var alreadyOwnedByOther = business.OwnerUserId is { } ownerId && ownerId != claim.ClaimantUserId;

        if (alreadyOwnedByOther || business.IsClaimed)
        {
            return new ContentClaimDecisionOutcome(ContentClaimDecisionStatus.AlreadyReviewed);
        }

        var rivals = await dbContext.BusinessClaims
            .Where(other => other.BusinessId == claim.BusinessId
                && other.Id != claim.Id
                && other.Status == BusinessClaimStatus.Pending)
            .ToListAsync(cancellationToken);

        var reviewedAt = dates.UtcNow;

        foreach (var rival in rivals)
        {
            rival.Status = BusinessClaimStatus.Released;
            rival.ReviewedAt = reviewedAt;
            rival.ReviewNote = "Another claim on this listing was accepted.";
        }

        claim.Status = BusinessClaimStatus.Accepted;
        claim.ReviewedAt = reviewedAt;
        claim.ReviewNote = Blank(reviewNote);

        business.OwnerUserId = claim.ClaimantUserId;
        business.IsClaimed = true;

        var heldBefore = (await userManager.GetRolesAsync(claim.Claimant!))
            .Contains(Roles.Business, StringComparer.Ordinal);

        auditLog.Record(
            posterUserId,
            AuditActions.BusinessClaimAccepted,
            "business_claim",
            claim.Id.ToString(),
            before: new { status = nameof(BusinessClaimStatus.Pending), ownerUserId = (Guid?)null, isClaimed = false },
            after: new
            {
                status = nameof(BusinessClaimStatus.Accepted),
                ownerUserId = claim.ClaimantUserId,
                isClaimed = true,
                roleGranted = !heldBefore,
                rivalClaimsReleased = rivals.Count,
            });

        if (!heldBefore && await GrantBusinessRoleAsync(claim.Claimant!) is null)
        {
            return new ContentClaimDecisionOutcome(ContentClaimDecisionStatus.NotFound);
        }

        await dbContext.SaveChangesAsync(cancellationToken);

        return new ContentClaimDecisionOutcome(
            ContentClaimDecisionStatus.Accepted,
            ToPosted(claim));
    }

    /// <summary>The poster releases a pending claim; the listing stays public. Not allowed after Accept.</summary>
    internal async Task<ContentClaimDecisionOutcome> ReleaseAsync(
        Guid claimId,
        Guid posterUserId,
        string? reviewNote,
        CancellationToken cancellationToken)
    {
        var claim = await dbContext.BusinessClaims
            .Include(item => item.Business)
            .Include(item => item.Claimant)
            .FirstOrDefaultAsync(item => item.Id == claimId, cancellationToken);

        if (claim is null || claim.Business?.CreatedByUserId != posterUserId)
        {
            return new ContentClaimDecisionOutcome(ContentClaimDecisionStatus.NotFound);
        }

        if (claim.Status != BusinessClaimStatus.Pending)
        {
            return new ContentClaimDecisionOutcome(ContentClaimDecisionStatus.AlreadyReviewed);
        }

        claim.Status = BusinessClaimStatus.Released;
        claim.ReviewedAt = dates.UtcNow;
        claim.ReviewNote = Blank(reviewNote);

        // Pending never set IsClaimed; clear anyway so a stray flag cannot leave the listing hidden.
        if (claim.Business is { } business && business.IsClaimed && business.OwnerUserId is null)
        {
            business.IsClaimed = false;
        }

        auditLog.Record(
            posterUserId,
            AuditActions.BusinessClaimReleased,
            "business_claim",
            claim.Id.ToString(),
            before: new { status = nameof(BusinessClaimStatus.Pending) },
            after: new { status = nameof(BusinessClaimStatus.Released), note = claim.ReviewNote });

        await dbContext.SaveChangesAsync(cancellationToken);

        return new ContentClaimDecisionOutcome(
            ContentClaimDecisionStatus.Released,
            ToPosted(claim));
    }

    private async Task<List<BusinessClaim>> ActiveClaimsOnAsync(Guid businessId, CancellationToken cancellationToken) =>
        await dbContext.BusinessClaims.AsNoTracking()
            .Where(claim => claim.BusinessId == businessId
                && (claim.Status == BusinessClaimStatus.Pending
                    || claim.Status == BusinessClaimStatus.Accepted))
            .ToListAsync(cancellationToken);

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

    private static MyContentClaimResponse ToMine(BusinessClaim claim) =>
        new(
            claim.Id,
            ClaimKind.Business,
            claim.Business?.Slug ?? string.Empty,
            claim.Business?.Name ?? string.Empty,
            MapStatus(claim.Status),
            claim.Evidence,
            claim.CreatedAt,
            claim.ReviewedAt,
            claim.ReviewNote);

    private static PostedContentClaimResponse ToPosted(BusinessClaim claim) =>
        new(
            claim.Id,
            ClaimKind.Business,
            claim.Business?.Slug ?? string.Empty,
            claim.Business?.Name ?? string.Empty,
            claim.Claimant?.FullName
                ?? claim.Claimant?.UserName
                ?? claim.Claimant?.Email
                ?? "A member",
            claim.Claimant?.Email,
            MapStatus(claim.Status),
            claim.Evidence,
            claim.CreatedAt,
            claim.ReviewedAt,
            claim.ReviewNote);

    /// <summary>Legacy staff statuses and the instant-claim statuses share one wire enum.</summary>
    private static ContentClaimStatus MapStatus(BusinessClaimStatus status) => status switch
    {
        BusinessClaimStatus.Accepted => ContentClaimStatus.Accepted,
        BusinessClaimStatus.Released => ContentClaimStatus.Released,
        _ => ContentClaimStatus.Pending,
    };

    private static string? Blank(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
