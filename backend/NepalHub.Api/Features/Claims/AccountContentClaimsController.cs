using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using NepalHub.Api.Common;
using NepalHub.Api.Data.Entities;

namespace NepalHub.Api.Features.Claims;

/// <summary>
/// The account page's two claim boxes: every claim this person filed, and every claim on a
/// post they created — plus accept and release for the poster's side.
///
/// <para>
/// Signed in only, no role. Every read is scoped to the caller (claimant or poster) and
/// somebody else's claim answers the same 404 as one that never existed, so this controller
/// cannot be used to discover other people's posts.
/// </para>
/// </summary>
[ApiController]
[Produces("application/json")]
public sealed class AccountContentClaimsController(
    ContentClaimService claimService,
    UserManager<ApplicationUser> userManager) : ControllerBase
{
    /// <summary>Both account boxes in one read: claims filed, and claims on posts created.</summary>
    [HttpGet("api/account/content-claims")]
    [ProducesResponseType<ApiResponse<ContentClaimsOverview>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ApiResponse<object>>(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<ApiResponse<ContentClaimsOverview>>> Overview(
        CancellationToken cancellationToken)
    {
        var user = await userManager.GetUserAsync(User);

        if (user is null)
        {
            return SessionInvalid();
        }

        var mine = await claimService.ListMineAsync(user.Id, cancellationToken);
        var onMyPosts = await claimService.ListOnMyPostsAsync(user.Id, cancellationToken);

        return Ok(ApiResponse<ContentClaimsOverview>.Ok(
            new ContentClaimsOverview(mine, onMyPosts)));
    }

    /// <summary>The claimant's own claims only, newest first.</summary>
    [HttpGet("api/account/my-claims")]
    [ProducesResponseType<ApiResponse<IReadOnlyList<MyContentClaimResponse>>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ApiResponse<object>>(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<ApiResponse<IReadOnlyList<MyContentClaimResponse>>>> ListMine(
        CancellationToken cancellationToken)
    {
        var user = await userManager.GetUserAsync(User);

        if (user is null)
        {
            return SessionInvalid();
        }

        var claims = await claimService.ListMineAsync(user.Id, cancellationToken);

        return Ok(ApiResponse<IReadOnlyList<MyContentClaimResponse>>.Ok(claims));
    }

    /// <summary>Claims on posts this person created; pending first, then newest.</summary>
    [HttpGet("api/account/posted-claims")]
    [ProducesResponseType<ApiResponse<IReadOnlyList<PostedContentClaimResponse>>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ApiResponse<object>>(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<ApiResponse<IReadOnlyList<PostedContentClaimResponse>>>> ListPosted(
        CancellationToken cancellationToken)
    {
        var user = await userManager.GetUserAsync(User);

        if (user is null)
        {
            return SessionInvalid();
        }

        var claims = await claimService.ListOnMyPostsAsync(user.Id, cancellationToken);

        return Ok(ApiResponse<IReadOnlyList<PostedContentClaimResponse>>.Ok(claims));
    }

    /// <summary>The poster accepts the claimant; the post stays hidden.</summary>
    [HttpPost("api/account/posted-claims/{id:guid}/accept")]
    [ProducesResponseType<ApiResponse<PostedContentClaimResponse>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ApiResponse<object>>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ApiResponse<object>>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ApiResponse<object>>(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<ApiResponse<PostedContentClaimResponse>>> Accept(
        Guid id,
        ReviewContentClaimRequest request,
        CancellationToken cancellationToken) =>
        await DecideAsync(id, accept: true, request, cancellationToken);

    /// <summary>The poster releases the claim; the post is public again.</summary>
    [HttpPost("api/account/posted-claims/{id:guid}/release")]
    [ProducesResponseType<ApiResponse<PostedContentClaimResponse>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ApiResponse<object>>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ApiResponse<object>>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ApiResponse<object>>(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<ApiResponse<PostedContentClaimResponse>>> Release(
        Guid id,
        ReviewContentClaimRequest request,
        CancellationToken cancellationToken) =>
        await DecideAsync(id, accept: false, request, cancellationToken);

    private async Task<ActionResult<ApiResponse<PostedContentClaimResponse>>> DecideAsync(
        Guid id,
        bool accept,
        ReviewContentClaimRequest request,
        CancellationToken cancellationToken)
    {
        var user = await userManager.GetUserAsync(User);

        if (user is null)
        {
            return SessionInvalid();
        }

        var outcome = accept
            ? await claimService.AcceptAsync(id, user.Id, request.ReviewNote, cancellationToken)
            : await claimService.ReleaseAsync(id, user.Id, request.ReviewNote, cancellationToken);

        return outcome.Status switch
        {
            ContentClaimDecisionStatus.Accepted => Ok(
                ApiResponse<PostedContentClaimResponse>.Ok(outcome.Claim!)),

            ContentClaimDecisionStatus.NotFound => NotFound(ApiResponse<object>.Fail(
                new ApiError(
                    "claim_not_found",
                    "That claim is not on one of your posts."))),

            _ => Conflict(ApiResponse<object>.Fail(
                new ApiError(
                    "already_reviewed",
                    "That claim has already been decided."))),
        };
    }

    private ActionResult SessionInvalid() => Unauthorized(ApiResponse<object>.Fail(
        new ApiError("session_invalid", "The session is no longer valid.")));
}
