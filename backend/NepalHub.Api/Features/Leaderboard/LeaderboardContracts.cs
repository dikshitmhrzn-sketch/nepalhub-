namespace NepalHub.Api.Features.Leaderboard;

/// <summary>
/// The window a leaderboard ranks over. Absent or <c>all</c> means the whole account
/// history; the other two are cut by the Nepal day and the Nepal week, because a visitor
/// in Kathmandu asking for "today" means the Kathmandu day and not the UTC one.
/// </summary>
public enum LeaderboardPeriod
{
    All,
    Today,
    Week,
}

/// <summary>
/// One row of the board. <c>Xp</c> is the figure the rank was decided by: lifetime XP on
/// <see cref="LeaderboardPeriod.All"/>, the XP earned inside the window on the other two,
/// so the number beside a rank always adds up to the order it sits in.
/// </summary>
public sealed record LeaderboardEntryResponse(int Rank, string DisplayName, int Level, int Xp);
