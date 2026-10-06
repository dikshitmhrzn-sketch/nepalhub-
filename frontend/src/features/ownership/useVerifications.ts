import { useCallback, useEffect, useRef, useState } from 'react';

import { ApiError } from '@/lib/apiClient';

import type { PagedResult } from '@/features/businesses/types';

import { listVerifications } from './api';
import type { AdminVerificationRow, VerificationFilter } from './types';

function toApiError(error: unknown): ApiError {
  if (error instanceof ApiError) {
    return error;
  }

  return new ApiError(0, 'unknown_error', 'Something went wrong while contacting the server.');
}

export type VerificationsState =
  | { status: 'loading'; data: null; error: null }
  | { status: 'success'; data: PagedResult<AdminVerificationRow>; error: null }
  | { status: 'error'; data: null; error: ApiError };

const initialState: VerificationsState = { status: 'loading', data: null, error: null };

/**
 * Loads one page of the verification queue. The window and page come in so the URL owns
 * both, exactly as the claim queue's hook does — the two queues sit on the same screen's
 * terms so a moderator who has learned one has learned the other.
 */
export function useVerifications(query: {
  page: number;
  pageSize: number;
  filter?: VerificationFilter;
}) {
  const [state, setState] = useState<VerificationsState>(initialState);
  const activeRequest = useRef<AbortController | null>(null);
  const { page, pageSize, filter } = query;

  const load = useCallback(() => {
    activeRequest.current?.abort();

    const controller = new AbortController();
    activeRequest.current = controller;
    setState(initialState);

    void (async () => {
      try {
        const data = await listVerifications({ page, pageSize, filter }, controller.signal);

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
  }, [filter, page, pageSize]);

  useEffect(() => {
    load();

    return () => {
      activeRequest.current?.abort();
    };
  }, [load]);

  return { ...state, reload: load };
}
