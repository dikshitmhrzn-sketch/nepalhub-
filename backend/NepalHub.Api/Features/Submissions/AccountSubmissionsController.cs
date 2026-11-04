using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using NepalHub.Api.Common;
using NepalHub.Api.Data.Entities;

namespace NepalHub.Api.Features.Submissions;

/// <summary>
/// The listings one person has sent in: the drafts they write, the queue status of each,
/// and the two ways a draft of their own moves — correcting it, and sending a turned-down
/// one back for another look.
///
/// Signed in only — no role — because this is a person's own screen: the fallback policy in
/// <c>Program</c> is the entire test, and every read and write here is scoped to the caller's
/// own submissions rather than to any permission that could widen it. Somebody else's
/// submission answers the same 404 as one that never existed, so this controller cannot be
/// used to discover what other people have sent in.
/// </summary>
[ApiController]
[Route("api/account/submissions")]
[Produces("application/json")]
public sealed class AccountSubmissionsController(
    ContentSubmissionService submissionService,
    UserManager<ApplicationUser> userManager) : ControllerBase
{
    /// <summary>
    /// Everything this person has sent in, newest open work first, with the draft's slug,
    /// title and whether it has already gone live so the list reads as their work rather
    /// than as bare ids.
    /// </summary>
    [HttpGet]
    [ProducesResponseType<ApiResponse<IReadOnlyList<SubmissionResponse>>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ApiResponse<object>>(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<ApiResponse<IReadOnlyList<SubmissionResponse>>>> List(
        CancellationToken cancellationToken)
    {
        var user = await userManager.GetUserAsync(User);

        if (user is null)
        {
            return SessionInvalid();
        }

        var result = await submissionService.ListMineAsync(user.Id, cancellationToken);

        return Ok(ApiResponse<IReadOnlyList<SubmissionResponse>>.Ok(result));
    }

    /// <summary>
    /// One of this person's submissions with the draft body behind it, for the editor that
    /// corrects it. The form opens filled with what was actually sent rather than with what
    /// the author remembers sending, and somebody else's submission answers the same 404
    /// as one that never existed.
    /// </summary>
    [HttpGet("{id:guid}")]
    [ProducesResponseType<ApiResponse<SubmissionDetailResponse>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ApiResponse<object>>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ApiResponse<object>>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ApiResponse<SubmissionDetailResponse>>> Detail(
        Guid id,
        CancellationToken cancellationToken)
    {
        var user = await userManager.GetUserAsync(User);

        if (user is null)
        {
            return SessionInvalid();
        }

        var detail = await submissionService.GetDetailAsync(user.Id, id, cancellationToken);

        return detail is null
            ? NotFound(ApiResponse<object>.Fail(
                new ApiError("submission_not_found", "That submission is not available.")))
            : Ok(ApiResponse<SubmissionDetailResponse>.Ok(detail));
    }

    /// <summary>
    /// Writes a job draft and opens the request against it in one step. The row is born
    /// unpublished — the only thing this grants is the authorship of a draft, and going
    /// live is what the queue exists to decide. Bounded per IP like filing a claim,
    /// because it opens a row in that same staff queue.
    /// </summary>
    [HttpPost("jobs")]
    [EnableRateLimiting(RateLimitPolicies.Submission)]
    [ProducesResponseType<ApiResponse<SubmissionResponse>>(StatusCodes.Status201Created)]
    [ProducesResponseType<ApiResponse<object>>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ApiResponse<object>>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ApiResponse<object>>(StatusCodes.Status409Conflict)]
    [ProducesResponseType<ApiResponse<object>>(StatusCodes.Status429TooManyRequests)]
    public async Task<ActionResult<ApiResponse<SubmissionResponse>>> SubmitJob(
        SubmitJobRequest request,
        CancellationToken cancellationToken) =>
        await SubmitAsync(
            (authorId, content, note, ct) => submissionService.SubmitJobAsync(authorId, content, note, ct),
            request.Content,
            request.Note,
            cancellationToken);

    /// <summary>Writes an event draft and opens the request against it, as for a job.</summary>
    [HttpPost("events")]
    [EnableRateLimiting(RateLimitPolicies.Submission)]
    [ProducesResponseType<ApiResponse<SubmissionResponse>>(StatusCodes.Status201Created)]
    [ProducesResponseType<ApiResponse<object>>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ApiResponse<object>>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ApiResponse<object>>(StatusCodes.Status409Conflict)]
    [ProducesResponseType<ApiResponse<object>>(StatusCodes.Status429TooManyRequests)]
    public async Task<ActionResult<ApiResponse<SubmissionResponse>>> SubmitEvent(
        SubmitEventRequest request,
        CancellationToken cancellationToken) =>
        await SubmitAsync(
            (authorId, content, note, ct) => submissionService.SubmitEventAsync(authorId, content, note, ct),
            request.Content,
            request.Note,
            cancellationToken);

    /// <summary>Writes an offer draft and opens the request against it, as for a job.</summary>
    [HttpPost("deals")]
    [EnableRateLimiting(RateLimitPolicies.Submission)]
    [ProducesResponseType<ApiResponse<SubmissionResponse>>(StatusCodes.Status201Created)]
    [ProducesResponseType<ApiResponse<object>>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ApiResponse<object>>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ApiResponse<object>>(StatusCodes.Status409Conflict)]
    [ProducesResponseType<ApiResponse<object>>(StatusCodes.Status429TooManyRequests)]
    public async Task<ActionResult<ApiResponse<SubmissionResponse>>> SubmitDeal(
        SubmitDealRequest request,
        CancellationToken cancellationToken) =>
        await SubmitAsync(
            (authorId, content, note, ct) => submissionService.SubmitDealAsync(authorId, content, note, ct),
            request.Content,
            request.Note,
            cancellationToken);

    /// <summary>
    /// The author's correction to a draft that has not gone live: same body the staff
    /// editor would send, same validation, and still no way to express a publish — the
    /// flag is forced off inside the service no matter what the body claims.
    /// </summary>
    [HttpPut("{id:guid}")]
    [ProducesResponseType<ApiResponse<SubmissionResponse>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ApiResponse<object>>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ApiResponse<object>>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ApiResponse<object>>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ApiResponse<object>>(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<ApiResponse<SubmissionResponse>>> Update(
        Guid id,
        UpdateSubmissionRequest request,
        CancellationToken cancellationToken)
    {
        var user = await userManager.GetUserAsync(User);

        if (user is null)
        {
            return SessionInvalid();
        }

        var outcome = await submissionService.UpdateDraftAsync(user.Id, id, request, cancellationToken);

        return outcome.Status switch
        {
            SubmissionEditStatus.Updated => Ok(ApiResponse<SubmissionResponse>.Ok(outcome.Submission!)),

            SubmissionEditStatus.NotFound => NotFound(ApiResponse<object>.Fail(
                new ApiError("submission_not_found", "That submission is not available."))),

            SubmissionEditStatus.KindMismatch => BadRequest(ApiResponse<object>.Fail(new ApiError(
                "validation_failed", "The submission could not be updated.", outcome.FieldErrors))),

            SubmissionEditStatus.Invalid => BadRequest(ApiResponse<object>.Fail(new ApiError(
                "validation_failed", "The draft could not be saved.", outcome.FieldErrors))),

            SubmissionEditStatus.SlugTaken => Conflict(ApiResponse<object>.Fail(new ApiError(
                "slug_taken", outcome.Message ?? "That slug is already used by another listing."))),

            _ => Conflict(ApiResponse<object>.Fail(new ApiError(
                "already_published",
                outcome.Message
                    ?? "This submission has been approved and its listing is live, so it can no longer be edited here."))),
        };
    }

    /// <summary>
    /// Sends a turned-down submission back into the queue. Only a rejection can be
    /// resubmitted: a pending one is already waiting, and an approved one is already
    /// live, so both answer 409 rather than quietly re-opening a settled question.
    /// </summary>
    [HttpPost("{id:guid}/resubmit")]
    [EnableRateLimiting(RateLimitPolicies.Submission)]
    [ProducesResponseType<ApiResponse<SubmissionResponse>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ApiResponse<object>>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ApiResponse<object>>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ApiResponse<object>>(StatusCodes.Status409Conflict)]
    [ProducesResponseType<ApiResponse<object>>(StatusCodes.Status429TooManyRequests)]
    public async Task<ActionResult<ApiResponse<SubmissionResponse>>> Resubmit(
        Guid id,
        CancellationToken cancellationToken)
    {
        var user = await userManager.GetUserAsync(User);

        if (user is null)
        {
            return SessionInvalid();
        }

        var outcome = await submissionService.ResubmitAsync(user.Id, id, cancellationToken);

        return outcome.Status switch
        {
            SubmissionResubmitStatus.Resubmitted => Ok(ApiResponse<SubmissionResponse>.Ok(outcome.Submission!)),

            SubmissionResubmitStatus.NotFound => NotFound(ApiResponse<object>.Fail(
                new ApiError("submission_not_found", "That submission is not available."))),

            _ => Conflict(ApiResponse<object>.Fail(new ApiError(
                "submission_not_rejected",
                "Only a turned-down submission can be sent again. This one is still waiting for staff, or has already been approved."))),
        };
    }

    private async Task<ActionResult<ApiResponse<SubmissionResponse>>> SubmitAsync<TContent>(
        Func<Guid, TContent, string?, CancellationToken, Task<SubmissionSubmitOutcome>> submit,
        TContent content,
        string? note,
        CancellationToken cancellationToken)
    {
        var user = await userManager.GetUserAsync(User);

        if (user is null)
        {
            return SessionInvalid();
        }

        var outcome = await submit(user.Id, content, note, cancellationToken);

        return outcome.Status switch
        {
            SubmissionSubmitStatus.Created => Created(
                "/api/account/submissions",
                ApiResponse<SubmissionResponse>.Ok(outcome.Submission!)),

            SubmissionSubmitStatus.SlugTaken => Conflict(ApiResponse<object>.Fail(new ApiError(
                "slug_taken", outcome.Message ?? "That slug is already used by another listing."))),

            _ => BadRequest(ApiResponse<object>.Fail(new ApiError(
                "validation_failed", "The submission could not be sent.", outcome.FieldErrors))),
        };
    }

    private UnauthorizedObjectResult SessionInvalid() =>
        Unauthorized(ApiResponse<object>.Fail(
            new ApiError("session_invalid", "The session is no longer valid.")));
}
