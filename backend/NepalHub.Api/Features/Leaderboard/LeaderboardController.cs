using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NepalHub.Api.Common;

namespace NepalHub.Api.Features.Leaderboard;

/// <summary>
/// The public leaderboard.
///
/// A class-level <c>[AllowAnonymous]</c> is safe here for the reason it is safe on the job
/// board: every action this controller has or is likely to have is a public read of a
/// published ranking. The attribute is collected across all endpoint metadata, so a later
/// signed-in action must not be added to this controller; it would be opened up silently.
///
/// The period argument is read as a string and parsed here rather than bound as an enum,
/// so an unrecognised value produces a <c>period_invalid</c> the UI can explain instead of
/// a framework binding message about a parameter.
/// </summary>
[ApiController]
[Route("api/leaderboard")]
[Produces("application/json")]
[AllowAnonymous]
public sealed class LeaderboardController(LeaderboardService leaderboardService) : ControllerBase
{
    /// <summary>
    /// One page of a ranking. The default window is all time, page 1, and the response is
    /// the same paged shape every other list on the site returns, so a reader who has
    /// paged a job board knows how to page this too.
    /// </summary>
    [HttpGet]
    [ProducesResponseType<ApiResponse<PagedResult<LeaderboardEntryResponse>>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ApiResponse<object>>(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<ApiResponse<PagedResult<LeaderboardEntryResponse>>>> List(
        int? page,
        int? pageSize,
        string? period,
        CancellationToken cancellationToken)
    {
        if (!TryParsePeriod(period, out var selectedPeriod))
        {
            return BadFilter(nameof(period));
        }

        var result = await leaderboardService.ListAsync(
            selectedPeriod,
            PageRequest.Normalise(page, pageSize),
            cancellationToken);

        return Ok(ApiResponse<PagedResult<LeaderboardEntryResponse>>.Ok(result));
    }

    /// <summary>
    /// An absent or blank period is the whole board, and an unrecognised one is refused
    /// rather than defaulted to all time: a typo that quietly widens a result set is a
    /// wrong answer the caller cannot see.
    /// </summary>
    private static bool TryParsePeriod(string? raw, out LeaderboardPeriod period)
    {
        period = LeaderboardPeriod.All;

        if (string.IsNullOrWhiteSpace(raw))
        {
            return true;
        }

        return Enum.TryParse<LeaderboardPeriod>(raw.Trim(), ignoreCase: true, out period)
            && Enum.IsDefined(period);
    }

    private BadRequestObjectResult BadFilter(string field)
    {
        IReadOnlyList<FieldError> details =
        [
            new FieldError(field, "invalid", "Choose one of the offered values."),
        ];

        return BadRequest(ApiResponse<object>.Fail(new ApiError(
            $"{field}_invalid",
            $"'{field}' is not a time window this leaderboard understands.",
            details)));
    }
}
