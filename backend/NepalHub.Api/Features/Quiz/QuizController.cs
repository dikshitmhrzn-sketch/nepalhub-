using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using NepalHub.Api.Common;
using NepalHub.Api.Data.Entities;
using NepalHub.Api.Features.Auth;

namespace NepalHub.Api.Features.Quiz;

/// <summary>
/// The daily quiz. Reading it and submitting it are both anonymous, because a visitor
/// should be able to try a quiz before deciding to register. What an account buys is
/// persistence: XP, a streak and a stored result.
///
/// Note what is deliberately absent: a class-level <c>[AllowAnonymous]</c>. That attribute
/// is collected across all endpoint metadata, so putting it on the class would override the
/// <c>[Authorize]</c> on the result and stats actions below and hand every user's stored
/// progression to anonymous callers. The fallback policy already denies by default, so the
/// two public actions opt out individually and everything else stays closed.
/// </summary>
[ApiController]
[Route("api/quiz")]
[Produces("application/json")]
public sealed class QuizController(
    QuizService quizService,
    UserManager<ApplicationUser> userManager) : ControllerBase
{
    /// <summary>
    /// Today's questions. The payload deliberately carries no correct answer and no
    /// explanation, so nothing in the response can be used to skip the quiz.
    /// </summary>
    [HttpGet("daily")]
    [AllowAnonymous]
    [ProducesResponseType<ApiResponse<DailyQuizResponse>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ApiResponse<object>>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ApiResponse<DailyQuizResponse>>> GetDaily(CancellationToken cancellationToken)
    {
        var author = await userManager.GetUserAsync(User);
        var outcome = await quizService.GetDailyQuizAsync(author?.Id, cancellationToken);

        if (outcome.Status == DailyQuizStatus.NotAvailable)
        {
            return NotFound(ApiResponse<object>.Fail(new ApiError(
                "quiz_not_available",
                outcome.Message ?? "There is no quiz available today.")));
        }

        return Ok(ApiResponse<DailyQuizResponse>.Ok(outcome.Quiz!));
    }

    /// <summary>Grades a whole quiz in one request and, for a signed-in caller, records it.</summary>
    [HttpPost("daily/submit")]
    [AllowAnonymous]
    [EnableRateLimiting(RateLimitPolicies.Quiz)]
    [ProducesResponseType<ApiResponse<QuizResultResponse>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ApiResponse<object>>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ApiResponse<object>>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ApiResponse<object>>(StatusCodes.Status409Conflict)]
    [ProducesResponseType<ApiResponse<object>>(StatusCodes.Status429TooManyRequests)]
    public async Task<ActionResult<ApiResponse<QuizResultResponse>>> Submit(
        SubmitQuizRequest request,
        CancellationToken cancellationToken)
    {
        var author = await userManager.GetUserAsync(User);
        var outcome = await quizService.SubmitAsync(request, author?.Id, cancellationToken);

        return outcome.Status switch
        {
            SubmitStatus.Success => Ok(ApiResponse<QuizResultResponse>.Ok(outcome.Result!)),

            SubmitStatus.NotAvailable => NotFound(ApiResponse<object>.Fail(new ApiError(
                "quiz_not_available",
                "There is no quiz available today."))),

            SubmitStatus.Invalid => BadRequest(ApiResponse<object>.Fail(new ApiError(
                "validation_failed", "The quiz could not be submitted.", outcome.FieldErrors))),

            // A second attempt in the same day is a conflict, not a validation failure,
            // because the request is well formed and the answer is simply "not any more".
            _ => Conflict(ApiResponse<object>.Fail(new ApiError(
                "quiz_already_completed",
                outcome.Message ?? "You have already played today's quiz."))),
        };
    }

    /// <summary>The signed-in player's stored result for today, for revisiting the review.</summary>
    [HttpGet("daily/result")]
    [Authorize]
    [ProducesResponseType<ApiResponse<QuizResultResponse>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ApiResponse<object>>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ApiResponse<object>>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ApiResponse<QuizResultResponse>>> GetResult(CancellationToken cancellationToken)
    {
        var user = await userManager.GetUserAsync(User);
        if (user is null)
        {
            return Unauthorized();
        }

        var result = await quizService.GetResultAsync(user.Id, cancellationToken);
        if (result is null)
        {
            return NotFound(ApiResponse<object>.Fail(new ApiError(
                "quiz_not_completed",
                "You have not played today's quiz yet.")));
        }

        return Ok(ApiResponse<QuizResultResponse>.Ok(result));
    }

    /// <summary>Total XP, level and streak, so the quiz page can greet a returning player.</summary>
    [HttpGet("stats")]
    [Authorize]
    [ProducesResponseType<ApiResponse<QuizStatsResponse>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ApiResponse<object>>(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<ApiResponse<QuizStatsResponse>>> GetStats(CancellationToken cancellationToken)
    {
        var user = await userManager.GetUserAsync(User);
        if (user is null)
        {
            return Unauthorized();
        }

        return Ok(ApiResponse<QuizStatsResponse>.Ok(await quizService.GetStatsAsync(user.Id, cancellationToken)));
    }
}
