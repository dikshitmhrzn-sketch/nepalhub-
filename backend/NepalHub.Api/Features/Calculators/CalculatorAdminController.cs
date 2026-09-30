using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using NepalHub.Api.Common;
using NepalHub.Api.Data.Entities;
using NepalHub.Api.Features.Auth;

namespace NepalHub.Api.Features.Calculators;

/// <summary>
/// Staff management of the calculator catalogue. It sits in this module's folder
/// rather than its own so the whole slice stays in one place.
/// </summary>
[ApiController]
[Route("api/admin/calculators")]
[Produces("application/json")]
[Authorize(Policy = AuthPolicies.StaffOnly)]
public sealed class CalculatorAdminController(
    CalculatorService calculatorService,
    UserManager<ApplicationUser> userManager) : ControllerBase
{
    [HttpGet]
    [ProducesResponseType<ApiResponse<PagedResult<CalculatorAdminSummaryResponse>>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<PagedResult<CalculatorAdminSummaryResponse>>>> List(
        int? page,
        int? pageSize,
        string? search,
        CancellationToken cancellationToken)
    {
        var paging = PageRequest.Normalise(page, pageSize);
        var result = await calculatorService.ListForAdminAsync(paging, search, cancellationToken);

        return Ok(ApiResponse<PagedResult<CalculatorAdminSummaryResponse>>.Ok(result));
    }

    [HttpGet("{id:guid}")]
    [ProducesResponseType<ApiResponse<CalculatorAdminResponse>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ApiResponse<object>>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ApiResponse<CalculatorAdminResponse>>> GetById(
        Guid id,
        CancellationToken cancellationToken)
    {
        var calculator = await calculatorService.FindForAdminAsync(id, cancellationToken);
        if (calculator is null)
        {
            return NotFound(ApiResponse<object>.Fail(
                new ApiError("calculator_not_found", $"No calculator with id '{id}'.")));
        }

        return Ok(ApiResponse<CalculatorAdminResponse>.Ok(calculator));
    }

    [HttpPost]
    [ProducesResponseType<ApiResponse<CalculatorDetailResponse>>(StatusCodes.Status201Created)]
    [ProducesResponseType<ApiResponse<object>>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ApiResponse<object>>(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<ApiResponse<CalculatorDetailResponse>>> Create(
        SaveCalculatorRequest request,
        CancellationToken cancellationToken)
    {
        var author = await userManager.GetUserAsync(User);
        var outcome = await calculatorService.CreateAsync(request, author?.Id, cancellationToken);

        return outcome.Status switch
        {
            SaveStatus.Created => Created(
                $"/api/calculators/{outcome.Calculator!.Slug}",
                ApiResponse<CalculatorDetailResponse>.Ok(outcome.Calculator)),

            SaveStatus.SlugTaken => Conflict(ApiResponse<object>.Fail(new ApiError(
                "slug_taken", $"A calculator with slug '{request.Slug.Trim()}' already exists."))),

            SaveStatus.Invalid => BadRequest(ApiResponse<object>.Fail(new ApiError(
                "validation_failed", "The calculator could not be saved.", outcome.FieldErrors))),

            _ => BadRequest(ApiResponse<object>.Fail(
                new ApiError("save_failed", outcome.Message ?? "The calculator could not be saved."))),
        };
    }

    [HttpPut("{id:guid}")]
    [ProducesResponseType<ApiResponse<CalculatorDetailResponse>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ApiResponse<object>>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ApiResponse<object>>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ApiResponse<object>>(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<ApiResponse<CalculatorDetailResponse>>> Update(
        Guid id,
        SaveCalculatorRequest request,
        CancellationToken cancellationToken)
    {
        var outcome = await calculatorService.UpdateAsync(id, request, cancellationToken);

        return outcome.Status switch
        {
            SaveStatus.Updated => Ok(ApiResponse<CalculatorDetailResponse>.Ok(outcome.Calculator!)),

            SaveStatus.NotFound => NotFound(ApiResponse<object>.Fail(
                new ApiError("calculator_not_found", $"No calculator with id '{id}'."))),

            // A different slug is a different row, so this is a conflict rather than
            // a validation failure.
            SaveStatus.SlugImmutable => Conflict(ApiResponse<object>.Fail(
                new ApiError("slug_immutable", outcome.Message!))),

            SaveStatus.Invalid => BadRequest(ApiResponse<object>.Fail(new ApiError(
                "validation_failed", "The calculator could not be saved.", outcome.FieldErrors))),

            _ => BadRequest(ApiResponse<object>.Fail(
                new ApiError("save_failed", outcome.Message ?? "The calculator could not be saved."))),
        };
    }

    /// <summary>Removes a draft. Anything that has been published is retracted instead.</summary>
    [HttpDelete("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ApiResponse<object>>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ApiResponse<object>>(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken)
    {
        var status = await calculatorService.DeleteAsync(id, cancellationToken);

        return status switch
        {
            DeleteStatus.Deleted => NoContent(),
            DeleteStatus.NotFound => NotFound(ApiResponse<object>.Fail(
                new ApiError("calculator_not_found", $"No calculator with id '{id}'."))),
            _ => Conflict(ApiResponse<object>.Fail(new ApiError(
                "calculator_published",
                "This calculator has been published, so it cannot be deleted. Unpublish it instead."))),
        };
    }
}
