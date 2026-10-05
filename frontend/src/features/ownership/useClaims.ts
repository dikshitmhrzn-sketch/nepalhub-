import { useCallback, useEffect, useRef, useState } from 'react';

import { ApiError } from '@/lib/apiClient';

import type { PagedResult } from '@/features/businesses/types';

import { listClaims } from './api';
import type { AdminClaimRow, ClaimFilter } from './types';

function toApiError(error: unknown): ApiError {
  if (error instanceof ApiError) {
    return error;
  }

  return new ApiError(0, 'unknown_error', 'Something went wrong while contacting the server.');
}

export type ClaimsState =
  | { status: 'loading'; data: null; error: null }
  | { status: 'success'; data: PagedResult<AdminClaimRow>; error: null }
  | { status: 'error'; data: null; error: ApiError };

const initialState: ClaimsState = { status: 'loading', data: null, error: null };

/** Loads one page of the claim queue. The window and page come in so the URL owns both. */
export function useClaims(query: { page: number; pageSize: number; filter?: ClaimFilter }) {
  const [state, setState] = useState<ClaimsState>(initialState);
  const activeRequest = useRef<AbortController | null>(null);
  const { page, pageSize, filter } = query;

  const load = useCallback(() => {
    activeRequest.current?.abort();

    const controller = new AbortController();
    activeRequest.current = controller;
    setState(initialState);

    void (async () => {
      try {
        const data = await listClaims({ page, pageSize, filter }, controller.signal);

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
