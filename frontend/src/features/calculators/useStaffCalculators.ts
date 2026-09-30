import { useCallback, useEffect, useRef, useState } from 'react';

import { ApiError } from '@/lib/apiClient';

import { getStaffCalculator, listStaffCalculators } from './api';
import type { CalculatorAdmin, CalculatorAdminSummary, PagedResult } from './types';

export type StaffCalculatorListState =
  | { status: 'loading'; data: null; error: null }
  | { status: 'success'; data: PagedResult<CalculatorAdminSummary>; error: null }
  | { status: 'error'; data: null; error: ApiError };

const initialState: StaffCalculatorListState = { status: 'loading', data: null, error: null };

function toApiError(error: unknown): ApiError {
  if (error instanceof ApiError) {
    return error;
  }

  return new ApiError(0, 'unknown_error', 'Something went wrong while contacting the server.');
}

/** The staff catalogue, which shows drafts as well as published rows. */
export function useStaffCalculatorList(query: { page?: number; pageSize?: number; search?: string }) {
  const [state, setState] = useState<StaffCalculatorListState>(initialState);
  const activeRequest = useRef<AbortController | null>(null);
  const { page, pageSize, search } = query;

  const load = useCallback(() => {
    activeRequest.current?.abort();

    const controller = new AbortController();
    activeRequest.current = controller;
    setState(initialState);

    void (async () => {
      try {
        const data = await listStaffCalculators(
          { page, pageSize, search },
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
  }, [page, pageSize, search]);

  useEffect(() => {
    load();

    return () => {
      activeRequest.current?.abort();
    };
  }, [load]);

  return { ...state, reload: load };
}

export type StaffCalculatorDetailState =
  | { status: 'loading'; data: null; error: null }
  | { status: 'success'; data: CalculatorAdmin; error: null }
  | { status: 'error'; data: null; error: ApiError };

const detailInitialState: StaffCalculatorDetailState = { status: 'loading', data: null, error: null };

/**
 * Loads the full row for the edit form. A null id means "creating", which is a
 * valid state here rather than a loading one.
 */
export function useStaffCalculator(id: string | null) {
  const [state, setState] = useState<StaffCalculatorDetailState>(detailInitialState);
  const activeRequest = useRef<AbortController | null>(null);

  const load = useCallback(() => {
    activeRequest.current?.abort();

    if (!id) {
      setState(detailInitialState);

      return;
    }

    const controller = new AbortController();
    activeRequest.current = controller;
    setState(detailInitialState);

    void (async () => {
      try {
        const data = await getStaffCalculator(id, controller.signal);

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
  }, [id]);

  useEffect(() => {
    load();

    return () => {
      activeRequest.current?.abort();
    };
  }, [load]);

  return { ...state, reload: load };
}
