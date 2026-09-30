using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using NepalHub.Api.Common;
using NepalHub.Api.Data.Entities;

namespace NepalHub.Api.Features.Calculators;

/// <summary>
/// The public tools hub. Reading the catalogue and running a calculation need no
/// account, which is why the class opts out of the fallback authorisation policy.
/// </summary>
[ApiController]
[Route("api/calculators")]
[Produces("application/json")]
[AllowAnonymous]
public sealed class CalculatorsController(CalculatorService calculatorService) : ControllerBase
{
    [HttpGet]
    [ProducesResponseType<ApiResponse<PagedResult<CalculatorSummaryResponse>>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ApiResponse<object>>(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<ApiResponse<PagedResult<CalculatorSummaryResponse>>>> List(
        int? page,
        int? pageSize,
        string? search,
        string? category,
        CancellationToken cancellationToken)
    {
        // The category is taken as a string and parsed here rather than bound as an
        // enum, so an unknown value produces a specific error code instead of relying
        // on model binding to guess a source and emit a generic message.
        if (!TryParseCategory(category, out var selected))
        {
            return BadRequest(ApiResponse<object>.Fail(new ApiError(
                "category_invalid",
                $"'{category}' is not a known category. Valid values are: {string.Join(", ", Enum.GetNames<CalculatorCategory>())}.")));
        }

        var paging = PageRequest.Normalise(page, pageSize);
        var result = await calculatorService.ListAsync(paging, search, selected, includeUnpublished: false, cancellationToken);

        return Ok(ApiResponse<PagedResult<CalculatorSummaryResponse>>.Ok(result));
    }

    /// <summary>Accepts a category name, case-insensitively, but not an arbitrary number.</summary>
    private static bool TryParseCategory(string? value, out CalculatorCategory? category)
    {
        category = null;

        if (string.IsNullOrWhiteSpace(value))
        {
            return true;
        }

        if (Enum.TryParse<CalculatorCategory>(value.Trim(), ignoreCase: true, out var parsed)
            && Enum.IsDefined(parsed))
        {
            category = parsed;
            return true;
        }

        return false;
    }

    [HttpGet("{slug}")]
    [ProducesResponseType<ApiResponse<CalculatorDetailResponse>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ApiResponse<object>>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ApiResponse<CalculatorDetailResponse>>> GetBySlug(
        string slug,
        CancellationToken cancellationToken)
    {
        var calculator = await calculatorService.FindBySlugAsync(slug, includeUnpublished: false, cancellationToken);
        if (calculator is null)
        {
            return NotFound(ApiResponse<object>.Fail(
                new ApiError("calculator_not_found", $"No calculator with slug '{slug}'.")));
        }

        return Ok(ApiResponse<CalculatorDetailResponse>.Ok(calculator));
    }

    [HttpPost("{slug}/calculate")]
    [EnableRateLimiting(RateLimitPolicies.Calculate)]
    [ProducesResponseType<ApiResponse<CalculationResponse>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ApiResponse<object>>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ApiResponse<object>>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ApiResponse<object>>(StatusCodes.Status429TooManyRequests)]
    [ProducesResponseType<ApiResponse<object>>(StatusCodes.Status501NotImplemented)]
    public async Task<ActionResult<ApiResponse<CalculationResponse>>> Calculate(
        string slug,
        CalculateRequest? request,
        CancellationToken cancellationToken)
    {
        var outcome = await calculatorService.CalculateAsync(slug, request?.Inputs, cancellationToken);

        return outcome.Status switch
        {
            CalculationStatus.Success => Ok(ApiResponse<CalculationResponse>.Ok(outcome.Response!)),

            CalculationStatus.NotFound => NotFound(ApiResponse<object>.Fail(
                new ApiError("calculator_not_found", $"No calculator with slug '{slug}'."))),

            CalculationStatus.Unavailable => StatusCode(
                StatusCodes.Status501NotImplemented,
                ApiResponse<object>.Fail(new ApiError("calculator_unavailable", outcome.Message!))),

            CalculationStatus.Invalid => BadRequest(ApiResponse<object>.Fail(
                new ApiError("validation_failed", "Some inputs are not valid.", outcome.FieldErrors))),

            _ => BadRequest(ApiResponse<object>.Fail(
                new ApiError("calculation_failed", outcome.Message!))),
        };
    }
}
