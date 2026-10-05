import { useCallback, useEffect, useRef, useState } from 'react';

import { ApiError } from '@/lib/apiClient';

import { listLeaderboard } from './api';
import type { LeaderboardEntry, LeaderboardPeriod, PagedResult } from './types';

export type LeaderboardState =
  | { status: 'loading'; data: null; error: null }
  | { status: 'success'; data: PagedResult<LeaderboardEntry>; error: null }
  | { status: 'error'; data: null; error: ApiError };

const initialState: LeaderboardState = { status: 'loading', data: null, error: null };

function toApiError(error: unknown): ApiError {
  if (error instanceof ApiError) {
    return error;
  }

  return new ApiError(0, 'unknown_error', 'Something went wrong while contacting the server.');
}

/**
 * Loads the whole board for one period. The period is passed in rather than owned here,
 * so the page keeps the URL in charge of which window is on screen, and switching tabs
 * aborts the request that is no longer wanted rather than letting two answers race.
 */
export function useLeaderboard(period: LeaderboardPeriod) {
  const [state, setState] = useState<LeaderboardState>(initialState);
  const activeRequest = useRef<AbortController | null>(null);

  const load = useCallback(() => {
    activeRequest.current?.abort();

    const controller = new AbortController();
    activeRequest.current = controller;
    setState(initialState);

    void (async () => {
      try {
        const data = await listLeaderboard(period, controller.signal);

        if (!controller.signal.aborted) {
          setState({ status: 'success', data, error: null });
        }
      } catch (error) {
        if (controller.signal.aborted) {
          return;
        }

        setState({ status: 'error', data: null, error: toApiError(error) });
      }
    })();
  }, [period]);

  useEffect(() => {
    load();

    return () => {
      activeRequest.current?.abort();
    };
  }, [load]);

  return { ...state, reload: load };
}
