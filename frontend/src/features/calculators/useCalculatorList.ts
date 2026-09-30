import { useCallback, useEffect, useRef, useState } from 'react';

import { ApiError } from '@/lib/apiClient';

import { listCalculators } from './api';
import type { CalculatorListQuery, PagedResult, CalculatorSummary } from './types';

export type CalculatorListState =
  | { status: 'loading'; data: null; error: null }
  | { status: 'success'; data: PagedResult<CalculatorSummary>; error: null }
  | { status: 'error'; data: null; error: ApiError };

const initialState: CalculatorListState = { status: 'loading', data: null, error: null };

export function toApiError(error: unknown): ApiError {
  if (error instanceof ApiError) {
    return error;
  }

  return new ApiError(0, 'unknown_error', 'Something went wrong while contacting the server.');
}

/**
 * Loads one page of the public catalogue. The query is passed in rather than owned
 * here, so the page component keeps the search box and the URL in charge of paging.
 */
export function useCalculatorList(query: CalculatorListQuery) {
  const [state, setState] = useState<CalculatorListState>(initialState);
  const activeRequest = useRef<AbortController | null>(null);
  const { page, pageSize, search, category } = query;

  const load = useCallback(() => {
    activeRequest.current?.abort();

    const controller = new AbortController();
    activeRequest.current = controller;
    setState(initialState);

    void (async () => {
      try {
        const data = await listCalculators(
          { page, pageSize, search, category },
          controller.signal,
        );

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
  }, [category, page, pageSize, search]);

  useEffect(() => {
    load();

    return () => {
      activeRequest.current?.abort();
    };
  }, [load]);

  return { ...state, reload: load };
}
