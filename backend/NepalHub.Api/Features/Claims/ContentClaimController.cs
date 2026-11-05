using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using NepalHub.Api.Common;
using NepalHub.Api.Data.Entities;

namespace NepalHub.Api.Features.Claims;

/// <summary>
/// Filing an instant claim on a published job, offer or event.
///
/// <para>
/// Its own controller rather than actions on the three public controllers because each of
/// those is class-level <c>[AllowAnonymous]</c>, and a personal action there would run
/// without a session by attribute. The claim needs a signed-in member, which the fallback
/// policy supplies without naming a role.
/// </para>
///
/// <para>
/// Rate-limited per IP on the same reasoning as filing a business claim or a report: the
/// write hides a public row, and a loop of them is exactly the flood that must not land.
/// </para>
/// </summary>
[ApiController]
[Produces("application/json")]
public sealed class ContentClaimController(
    ContentClaimService claimService,
    UserManager<ApplicationUser> userManager) : ControllerBase
{
    /// <summary>
    /// Claims one published job and hides it from the public immediately. A draft, a closed
    /// posting and an unknown address all answer 404; an already-claimed posting answers 409
    /// with a code the screen can explain.
    /// </summary>
    [HttpPost("api/jobs/{slug}/claim")]
    [EnableRateLimiting(RateLimitPolicies.Claim)]
    [ProducesResponseType<ApiResponse<MyContentClaimResponse>>(StatusCodes.Status201Created)]
    [ProducesResponseType<ApiResponse<object>>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ApiResponse<object>>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ApiResponse<object>>(StatusCodes.Status409Conflict)]
    [ProducesResponseType<ApiResponse<object>>(StatusCodes.Status429TooManyRequests)]
    public Task<ActionResult<ApiResponse<MyContentClaimResponse>>> ClaimJob(
        string slug,
        FileContentClaimRequest request,
        CancellationToken cancellationToken) =>
        ClaimAsync(SubmissionKind.Job, slug, request, cancellationToken);

    /// <summary>Claims one published offer and hides it from the public immediately.</summary>
    [HttpPost("api/deals/{slug}/claim")]
    [EnableRateLimiting(RateLimitPolicies.Claim)]
    [ProducesResponseType<ApiResponse<MyContentClaimResponse>>(StatusCodes.Status201Created)]
    [ProducesResponseType<ApiResponse<object>>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ApiResponse<object>>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ApiResponse<object>>(StatusCodes.Status409Conflict)]
    [ProducesResponseType<ApiResponse<object>>(StatusCodes.Status429TooManyRequests)]
    public Task<ActionResult<ApiResponse<MyContentClaimResponse>>> ClaimDeal(
        string slug,
        FileContentClaimRequest request,
        CancellationToken cancellationToken) =>
        ClaimAsync(SubmissionKind.Deal, slug, request, cancellationToken);

    /// <summary>Claims one published event and hides it from the public immediately.</summary>
    [HttpPost("api/events/{slug}/claim")]
    [EnableRateLimiting(RateLimitPolicies.Claim)]
    [ProducesResponseType<ApiResponse<MyContentClaimResponse>>(StatusCodes.Status201Created)]
    [ProducesResponseType<ApiResponse<object>>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ApiResponse<object>>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ApiResponse<object>>(StatusCodes.Status409Conflict)]
    [ProducesResponseType<ApiResponse<object>>(StatusCodes.Status429TooManyRequests)]
    public Task<ActionResult<ApiResponse<MyContentClaimResponse>>> ClaimEvent(
        string slug,
        FileContentClaimRequest request,
        CancellationToken cancellationToken) =>
        ClaimAsync(SubmissionKind.Event, slug, request, cancellationToken);

    private async Task<ActionResult<ApiResponse<MyContentClaimResponse>>> ClaimAsync(
        SubmissionKind kind,
        string slug,
        FileContentClaimRequest request,
        CancellationToken cancellationToken)
    {
        var user = await userManager.GetUserAsync(User);

        if (user is null)
        {
            return Unauthorized(ApiResponse<object>.Fail(
                new ApiError("session_invalid", "The session is no longer valid.")));
        }

        var outcome = await claimService.FileClaimAsync(
            kind,
            slug,
            user.Id,
            request.Note,
            cancellationToken);

        return outcome.Status switch
        {
            FileContentClaimStatus.Created => Created(
                "/api/account/content-claims",
                ApiResponse<MyContentClaimResponse>.Ok(outcome.Claim!)),

            FileContentClaimStatus.NotFound => NotFound(ApiResponse<object>.Fail(
                new ApiError(
                    "content_not_found",
                    "That post is not available. It may have closed, been retracted, or the link may be out of date."))),

            FileContentClaimStatus.NoPoster => Conflict(ApiResponse<object>.Fail(
                new ApiError(
                    "no_poster",
                    "This post has no one to hand the claim to, so it cannot be claimed."))),

            FileContentClaimStatus.AlreadyClaimed => Conflict(ApiResponse<object>.Fail(
                new ApiError(
                    "already_claimed",
                    "You already have an active claim on this post."))),

            _ => Conflict(ApiResponse<object>.Fail(
                new ApiError(
                    "already_taken",
                    "Someone already claimed this post."))),
        };
    }
}
