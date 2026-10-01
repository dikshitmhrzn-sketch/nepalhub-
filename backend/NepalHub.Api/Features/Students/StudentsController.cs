using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NepalHub.Api.Common;
using NepalHub.Api.Data.Entities;

namespace NepalHub.Api.Features.Students;

/// <summary>
/// The public student resource catalogue.
///
/// A class-level <c>[AllowAnonymous]</c> is safe here, and this is the reason it differs from
/// the quiz controller, which cannot have one: every action on this controller is a public read.
/// There is no action that depends on who is asking, because nothing in this phase is
/// per-visitor, and saved items are deferred. The attribute is collected across all endpoint
/// metadata, so putting it on a controller that later grows a signed-in action would quietly
/// open that action up. If a personal action is ever added here it must be a separate controller
/// with the attribute removed.
/// </summary>
[ApiController]
[Route("api/students/resources")]
[Produces("application/json")]
[AllowAnonymous]
public sealed class StudentsController(StudentResourceService resourceService) : ControllerBase
{
    /// <summary>
    /// The catalogue. Drafts and expired rows are excluded by the service, not by anything the
    /// caller can influence.
    ///
    /// The filter and sort arguments are read as strings and parsed here rather than bound as
    /// enums, so an unrecognised value produces a specific <c>type_invalid</c> or
    /// <c>sort_invalid</c> the UI can explain, instead of a framework binding message about a
    /// parameter. <paramref name="name"/> is passed in because the enum's own name is not the
    /// argument name: the query string says <c>type</c>, not <c>resourceType</c>.
    /// </summary>
    [HttpGet]
    [ProducesResponseType<ApiResponse<PagedResult<ResourceSummaryResponse>>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ApiResponse<object>>(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<ApiResponse<PagedResult<ResourceSummaryResponse>>>> List(
        int? page,
        int? pageSize,
        string? search,
        string? type,
        string? category,
        string? level,
        string? sort,
        CancellationToken cancellationToken)
    {
        if (!TryParseFilter<ResourceType>(type, nameof(type), out var selectedType, out var typeError))
        {
            return BadFilter(typeError);
        }

        if (!TryParseFilter<ResourceCategory>(category, nameof(category), out var selectedCategory, out var categoryError))
        {
            return BadFilter(categoryError);
        }

        if (!TryParseFilter<EducationLevel>(level, nameof(level), out var selectedLevel, out var levelError))
        {
            return BadFilter(levelError);
        }

        if (!TryParseFilter<ResourceSort>(sort, nameof(sort), out var parsedSort, out var sortError))
        {
            return BadFilter(sortError);
        }

        var query = new ResourceListQuery(
            PageRequest.Normalise(page, pageSize),
            search,
            selectedType,
            selectedCategory,
            selectedLevel,
            parsedSort ?? ResourceSort.Newest);

        var result = await resourceService.ListAsync(query, cancellationToken);

        return Ok(ApiResponse<PagedResult<ResourceSummaryResponse>>.Ok(result));
    }

    /// <summary>
    /// One resource. A draft, an expired row and a slug that never existed all return the same
    /// 404, so this endpoint cannot be used to discover which slugs are real but not yet public.
    /// </summary>
    [HttpGet("{slug}")]
    [ProducesResponseType<ApiResponse<ResourceDetailResponse>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ApiResponse<object>>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ApiResponse<ResourceDetailResponse>>> GetBySlug(
        string slug,
        CancellationToken cancellationToken)
    {
        var resource = await resourceService.FindBySlugAsync(slug, cancellationToken);

        if (resource is null)
        {
            return NotFound(ApiResponse<object>.Fail(
                new ApiError("resource_not_found", "That resource is not available.")));
        }

        return Ok(ApiResponse<ResourceDetailResponse>.Ok(resource));
    }

    /// <summary>
    /// Parses an optional enum argument. A blank value means "no filter" or "the default", which
    /// is what both an absent and an empty query-string argument mean to a reader.
    /// </summary>
    private static bool TryParseFilter<T>(string? raw, string name, out T? value, out string field)
        where T : struct, Enum
    {
        if (string.IsNullOrWhiteSpace(raw))
        {
            value = null;
            field = name;

            return true;
        }

        if (Enum.TryParse<T>(raw.Trim(), ignoreCase: true, out var parsed) && Enum.IsDefined(parsed))
        {
            value = parsed;
            field = name;

            return true;
        }

        value = null;
        field = name;

        return false;
    }

    private BadRequestObjectResult BadFilter(string field)
    {
        IReadOnlyList<FieldError> details =
        [
            new FieldError(field, "invalid", "Choose one of the offered values."),
        ];

        return BadRequest(ApiResponse<object>.Fail(new ApiError(
            $"{field}_invalid",
            $"'{field}' is not a value this catalogue understands.",
            details)));
    }
}
