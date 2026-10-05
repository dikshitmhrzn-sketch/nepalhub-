import { useCallback, useEffect, useRef, useState } from 'react';

import { ApiError } from '@/lib/apiClient';

import { getAccountBadges } from './api';
import type { AccountBadges } from './types';

export type BadgesState =
  | { status: 'loading'; data: null; error: null }
  | { status: 'success'; data: AccountBadges; error: null }
  | { status: 'error'; data: null; error: ApiError };

const initialState: BadgesState = { status: 'loading', data: null, error: null };

export function toApiError(error: unknown): ApiError {
  if (error instanceof ApiError) {
    return error;
  }

  return new ApiError(0, 'unknown_error', 'Something went wrong while contacting the server.');
}

/**
 * This person's badges, in the order the API returns them.
 *
 * A plain loader rather than something that re-fetches on a timer: the rows are only written by
 * finishing a quiz, so there is nothing arriving while the page is open that a reader is waiting
 * to see. `reload` is exposed for a button on the error state, which is the one case where asking
 * again is what someone actually wants.
 *
 * The list is not filtered to earned badges. Every badge is returned, locked ones included, so
 * that a person with nothing yet is shown what there is to aim at rather than an empty panel.
 */
export function useBadges() {
  const [state, setState] = useState<BadgesState>(initialState);
  const activeRequest = useRef<AbortController | null>(null);

  const load = useCallback(() => {
    activeRequest.current?.abort();

    const controller = new AbortController();
    activeRequest.current = controller;
    setState(initialState);

    void (async () => {
      try {
        const data = await getAccountBadges(controller.signal);

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
  }, []);

  useEffect(() => {
    load();

    return () => {
      activeRequest.current?.abort();
    };
  }, [load]);

  return { ...state, reload: load };
}
