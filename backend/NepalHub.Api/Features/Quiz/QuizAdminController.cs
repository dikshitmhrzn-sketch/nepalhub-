using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using NepalHub.Api.Common;
using NepalHub.Api.Data.Entities;
using NepalHub.Api.Features.Auth;

namespace NepalHub.Api.Features.Quiz;

/// <summary>
/// Staff management of the question pool. It sits in this module's folder alongside the
/// public controller so the whole slice stays in one place, exactly as the calculator
/// admin does. This is the only shape in the codebase that ever carries a correct answer,
/// which is why it is behind the staff policy.
/// </summary>
[ApiController]
[Route("api/admin/quiz/questions")]
[Produces("application/json")]
[Authorize(Policy = AuthPolicies.StaffOnly)]
public sealed class QuizAdminController(
    QuizService quizService,
    UserManager<ApplicationUser> userManager) : ControllerBase
{
    [HttpGet]
    [ProducesResponseType<ApiResponse<PagedResult<QuizQuestionAdminSummaryResponse>>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<PagedResult<QuizQuestionAdminSummaryResponse>>>> List(
        int? page,
        int? pageSize,
        string? search,
        QuizCategory? category,
        CancellationToken cancellationToken)
    {
        var paging = PageRequest.Normalise(page, pageSize);
        var result = await quizService.ListForAdminAsync(paging, search, category, cancellationToken);

        return Ok(ApiResponse<PagedResult<QuizQuestionAdminSummaryResponse>>.Ok(result));
    }

    [HttpGet("{id:guid}")]
    [ProducesResponseType<ApiResponse<QuizQuestionAdminResponse>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ApiResponse<object>>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ApiResponse<QuizQuestionAdminResponse>>> GetById(
        Guid id,
        CancellationToken cancellationToken)
    {
        var question = await quizService.FindForAdminAsync(id, cancellationToken);
        if (question is null)
        {
            return NotFound(ApiResponse<object>.Fail(
                new ApiError("question_not_found", $"No question with id '{id}'.")));
        }

        return Ok(ApiResponse<QuizQuestionAdminResponse>.Ok(question));
    }

    [HttpPost]
    [ProducesResponseType<ApiResponse<QuizQuestionAdminResponse>>(StatusCodes.Status201Created)]
    [ProducesResponseType<ApiResponse<object>>(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<ApiResponse<QuizQuestionAdminResponse>>> Create(
        SaveQuizQuestionRequest request,
        CancellationToken cancellationToken)
    {
        var author = await userManager.GetUserAsync(User);
        var outcome = await quizService.CreateAsync(request, author?.Id, cancellationToken);

        return outcome.Status == QuizSaveStatus.Created
            ? Created($"/api/admin/quiz/questions/{outcome.Question!.Id}", ApiResponse<QuizQuestionAdminResponse>.Ok(outcome.Question))
            : BadRequest(ApiResponse<object>.Fail(new ApiError(
                "validation_failed", "The question could not be saved.", outcome.FieldErrors)));
    }

    [HttpPut("{id:guid}")]
    [ProducesResponseType<ApiResponse<QuizQuestionAdminResponse>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ApiResponse<object>>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ApiResponse<object>>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ApiResponse<object>>(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<ApiResponse<QuizQuestionAdminResponse>>> Update(
        Guid id,
        SaveQuizQuestionRequest request,
        CancellationToken cancellationToken)
    {
        var outcome = await quizService.UpdateAsync(id, request, cancellationToken);

        return outcome.Status switch
        {
            QuizSaveStatus.Updated => Ok(ApiResponse<QuizQuestionAdminResponse>.Ok(outcome.Question!)),

            QuizSaveStatus.NotFound => NotFound(ApiResponse<object>.Fail(
                new ApiError("question_not_found", $"No question with id '{id}'."))),

            // A conflict, not a validation failure: the request was well formed, the state
            // of the question simply no longer allows it.
            QuizSaveStatus.InUse => Conflict(ApiResponse<object>.Fail(
                new ApiError("question_in_use", outcome.Message ?? "This question has already been played."))),

            _ => BadRequest(ApiResponse<object>.Fail(new ApiError(
                "validation_failed", "The question could not be saved.", outcome.FieldErrors))),
        };
    }

    /// <summary>
    /// Removes a draft. A published question is retracted instead, and a question that has
    /// already been played is refused even as a draft, because it is part of somebody's
    /// stored result.
    /// </summary>
    [HttpDelete("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ApiResponse<object>>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ApiResponse<object>>(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken)
    {
        var status = await quizService.DeleteAsync(id, cancellationToken);

        return status switch
        {
            QuizDeleteStatus.Deleted => NoContent(),

            QuizDeleteStatus.NotFound => NotFound(ApiResponse<object>.Fail(
                new ApiError("question_not_found", $"No question with id '{id}'."))),

            QuizDeleteStatus.Published => Conflict(ApiResponse<object>.Fail(new ApiError(
                "question_published",
                "This question has been published, so it cannot be deleted. Unpublish it instead."))),

            _ => Conflict(ApiResponse<object>.Fail(new ApiError(
                "question_in_use",
                "This question has already been part of a quiz, so it cannot be deleted. Unpublish it instead."))),
        };
    }
}
