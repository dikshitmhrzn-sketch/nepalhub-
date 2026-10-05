import { apiClient } from '@/lib/apiClient';

import type { LeaderboardEntry, LeaderboardPeriod, PagedResult } from './types';

/**
 * The board for one period, as one page of at most a hundred rows. The API ranks and
 * caps the entries server-side, so this asks for the whole board rather than paging it:
 * a leaderboard that stops at twenty would leave the reader wondering what came next.
 */
export function listLeaderboard(
  period: LeaderboardPeriod,
  signal?: AbortSignal,
): Promise<PagedResult<LeaderboardEntry>> {
  return apiClient.get<PagedResult<LeaderboardEntry>>('/leaderboard', {
    query: { period, pageSize: 100 },
    signal,
  });
}
