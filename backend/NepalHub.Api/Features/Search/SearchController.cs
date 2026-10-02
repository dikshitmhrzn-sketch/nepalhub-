using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using NepalHub.Api.Common;

namespace NepalHub.Api.Features.Search;

/// <summary>
/// Keyword search across every public module.
///
/// <para>
/// Anonymous by design. The whole point of a site-wide search box in the header is that it works
/// before someone has an account, and every module it reads is already public: this endpoint cannot
/// reach a draft, an expired listing, an unpublished offer, or a past event. That is what
/// <c>PublicContentScope</c> is for, and search has no other way to read a row.
/// </para>
/// </summary>
[ApiController]
[Route("api/search")]
[Produces("application/json")]
[AllowAnonymous]
public sealed class SearchController(SearchService searchService) : ControllerBase
{
    [HttpGet]
    [EnableRateLimiting(RateLimitPolicies.Search)]
    [ProducesResponseType<ApiResponse<SearchResponse>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ApiResponse<object>>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ApiResponse<object>>(StatusCodes.Status429TooManyRequests)]
    public async Task<ActionResult<ApiResponse<SearchResponse>>> Search(
        string? q,
        int? page,
        int? pageSize,
        string? module,
        CancellationToken cancellationToken)
    {
        var term = SearchText.Prepare(q);

        if (term.Length < SearchLimits.MinQueryLength)
        {
            return BadRequest(ApiResponse<object>.Fail(new ApiError(
                "query_too_short",
                $"Enter at least {SearchLimits.MinQueryLength} characters to search.")));
        }

        if (term.Length > SearchLimits.MaxQueryLength)
        {
            return BadRequest(ApiResponse<object>.Fail(new ApiError(
                "query_too_long",
                $"Search terms are limited to {SearchLimits.MaxQueryLength} characters.")));
        }

        // The module is taken as a string and parsed here rather than bound as an enum, so an
        // unknown value produces a specific error code naming the valid ones instead of a generic
        // model-binding message.
        if (!TryParseModule(module, out var parsedModule))
        {
            return BadRequest(ApiResponse<object>.Fail(new ApiError(
                "module_invalid",
                $"'{module}' is not a known module. Valid values are: {string.Join(", ", Enum.GetNames<SearchModule>())}.")));
        }

        // Search caps page size lower than the shared list convention, so the cap is applied here
        // rather than through PageRequest.Normalise, which would silently hand back 24-row pages.
        var normalisedPage = page is null || page < 1 ? 1 : page.Value;
        var requestedSize = pageSize is null || pageSize < 1 ? SearchLimits.DefaultPageSize : pageSize.Value;
        var paging = new PageRequest(normalisedPage, Math.Min(requestedSize, SearchLimits.MaxPageSize));

        // An omitted module means "everything", which is the default state of the tab strip rather
        // than a special case the client has to send.
        var selected = parsedModule ?? SearchModule.All;
        var result = await searchService.SearchAsync(term, paging, selected, cancellationToken);

        return Ok(ApiResponse<SearchResponse>.Ok(result));
    }

    /// <summary>Accepts a module name, case-insensitively, but not an arbitrary number.</summary>
    private static bool TryParseModule(string? value, out SearchModule? module)
    {
        module = null;

        if (string.IsNullOrWhiteSpace(value))
        {
            return true;
        }

        if (Enum.TryParse<SearchModule>(value.Trim(), ignoreCase: true, out var parsed)
            && Enum.IsDefined(parsed))
        {
            module = parsed;
            return true;
        }

        return false;
    }
}