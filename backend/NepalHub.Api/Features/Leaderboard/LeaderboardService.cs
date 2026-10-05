using Microsoft.EntityFrameworkCore;
using NepalHub.Api.Common;
using NepalHub.Api.Data;
using NepalHub.Api.Features.Quiz;

namespace NepalHub.Api.Features.Leaderboard;

/// <summary>
/// Ranks accounts at the moment they are asked for, from indexes the rest of the site
/// already maintains.
///
/// There is no leaderboard table and no background service keeping one sorted: the
/// all-time order is a seek over <c>ix_users_level_xp</c> and the period boards are a
/// group over <c>ix_quiz_attempts_completed_at</c>, so a page of the board cannot
/// disagree with the XP behind it. That reverses the earlier plan for a stored
/// leaderboard_entries table, and the reversal is recorded in the roadmap rather than
/// quietly dropped: every read here is bounded by the page size, the count is one
/// indexed COUNT against a capped window, and a refresh job would only buy staleness
/// for a figure that is already correct on demand.
/// </summary>
public sealed class LeaderboardService(AppDbContext dbContext, INepalDateProvider dates)
{
    /// <summary>
    /// The board publishes the top of each window and nothing deeper. Ranking is for
    /// recognition rather than analytics, and the cap keeps both the count and the
    /// deepest Skip bounded whatever the account count grows to.
    /// </summary>
    internal const int MaxEntries = 100;

    /// <summary>
    /// Shown where an account carries no display name, which registration's own
    /// validation should prevent but a row migrated or edited by hand may still hold.
    /// The email is never used as a stand-in: the board is public and an address is
    /// not something to publish on it.
    /// </summary>
    private const string FallbackDisplayName = "Member";

    /// <summary>
    /// One page of the requested window. Deactivated accounts are filtered here rather
    /// than by a caller-supplied flag, so leaving the board is a matter of the staff
    /// switching an account off and not of every reader remembering to exclude it.
    /// </summary>
    public async Task<PagedResult<LeaderboardEntryResponse>> ListAsync(
        LeaderboardPeriod period,
        PageRequest paging,
        CancellationToken cancellationToken)
    {
        return period == LeaderboardPeriod.All
            ? await ListAllTimeAsync(paging, cancellationToken)
            : await ListWindowAsync(period, paging, cancellationToken);
    }

    private async Task<PagedResult<LeaderboardEntryResponse>> ListAllTimeAsync(
        PageRequest paging,
        CancellationToken cancellationToken)
    {
        var active = dbContext.Users.Where(user => user.IsActive);

        var total = Math.Min(await active.CountAsync(cancellationToken), MaxEntries);
        var clamped = paging.ClampTo(total);

        var rows = await active
            .OrderByDescending(user => user.Xp)
            .ThenBy(user => user.CreatedAt)
            .ThenBy(user => user.Id)
            .Skip(clamped.Skip)
            .Take(clamped.PageSize)
            .Select(user => new { user.FullName, user.Level, LifetimeXp = user.Xp })
            .ToListAsync(cancellationToken);

        return Build(clamped, total, rows, (row, rank) =>
            new LeaderboardEntryResponse(rank, DisplayName(row.FullName), row.Level, row.LifetimeXp));
    }

    private async Task<PagedResult<LeaderboardEntryResponse>> ListWindowAsync(
        LeaderboardPeriod period,
        PageRequest paging,
        CancellationToken cancellationToken)
    {
        var start = WindowStartUtc(period);

        var window =
            from attempt in dbContext.QuizAttempts
            join user in dbContext.Users on attempt.UserId equals user.Id
            where user.IsActive && attempt.CompletedAt >= start
            group attempt.XpAwarded by new { user.Id, user.FullName, user.Level, user.CreatedAt }
                into earned
            orderby earned.Sum() descending,
                    earned.Count() descending,
                    earned.Key.CreatedAt ascending,
                    earned.Key.Id ascending
            select new { earned.Key.FullName, earned.Key.Level, WindowXp = earned.Sum() };

        var total = Math.Min(await window.CountAsync(cancellationToken), MaxEntries);
        var clamped = paging.ClampTo(total);

        var rows = await window
            .Skip(clamped.Skip)
            .Take(clamped.PageSize)
            .ToListAsync(cancellationToken);

        return Build(clamped, total, rows, (row, rank) =>
            new LeaderboardEntryResponse(rank, DisplayName(row.FullName), row.Level, row.WindowXp));
    }

    /// <summary>
    /// The UTC instant the window opened. Attempt timestamps are stored in UTC, so the
    /// Nepal day and week boundaries are converted exactly once, here, instead of each
    /// query comparing against a local clock it cannot have.
    /// </summary>
    private DateTime WindowStartUtc(LeaderboardPeriod period) =>
        period == LeaderboardPeriod.Today
            ? dates.UtcStartOfDay(dates.Today)
            : dates.UtcStartOfDay(StartOfWeek(dates.Today));

    /// <summary>
    /// The Saturday the current Nepal week began on. The board's week runs Saturday to
    /// Friday, the weekend-first week a Kathmandu reader expects, and the offset
    /// reaches back to the most recent Saturday including today itself: Sunday is one
    /// day past it, Saturday is none.
    /// </summary>
    private static DateOnly StartOfWeek(DateOnly today) =>
        today.AddDays(-(((int)today.DayOfWeek + 1) % 7));

    private static string DisplayName(string fullName) =>
        string.IsNullOrWhiteSpace(fullName) ? FallbackDisplayName : fullName.Trim();

    /// <summary>
    /// Turns the rows a page returned into ranked entries. Ranks are absolute positions
    /// within the whole board rather than positions within the page, so page two starts
    /// at the number it actually occupies and two readers comparing rows see the same
    /// rank for the same account.
    /// </summary>
    private static PagedResult<LeaderboardEntryResponse> Build<TRow>(
        PageRequest paging,
        int total,
        IReadOnlyList<TRow> rows,
        Func<TRow, int, LeaderboardEntryResponse> rank)
    {
        var items = rows
            .Select((row, index) => rank(row, paging.Skip + index + 1))
            .ToList();

        return new PagedResult<LeaderboardEntryResponse>(items, paging.Page, paging.PageSize, total);
    }
}
