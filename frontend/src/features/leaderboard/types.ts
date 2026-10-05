/** Mirrors the leaderboard contracts exposed by the ASP.NET Core API. */

/**
 * The windows the board ranks over. The API parses these case-insensitively, and a value
 * outside this list is refused there rather than quietly widened to the whole board.
 * Weeks run Saturday to Friday, Nepal time.
 */
export type LeaderboardPeriod = 'all' | 'today' | 'week';

/** Every period the board offers, in the order the tabs show them. */
export const leaderboardPeriods: readonly LeaderboardPeriod[] = ['all', 'today', 'week'];

/**
 * One row of the board. `xp` is the figure the rank was decided by: lifetime XP on the
 * all-time board, the XP earned inside the window on the other two, so the number beside
 * a rank always adds up to the order it sits in.
 */
export interface LeaderboardEntry {
  rank: number;
  displayName: string;
  level: number;
  xp: number;
}

/** The one shape every paged endpoint in the API returns. */
export interface PagedResult<T> {
  items: T[];
  page: number;
  pageSize: number;
  totalCount: number;
  totalPages: number;
  hasPrevious: boolean;
  hasNext: boolean;
}
