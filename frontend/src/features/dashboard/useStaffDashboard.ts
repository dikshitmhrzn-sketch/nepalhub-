import { useCallback, useEffect, useRef, useState } from 'react';

import { ApiError } from '@/lib/apiClient';

import { getStaffDashboard } from './api';
import type { DashboardSummary } from './types';

export type StaffDashboardState =
  | { status: 'loading'; data: null; error: null }
  | { status: 'success'; data: DashboardSummary; error: null }
  | { status: 'error'; data: null; error: ApiError };

const initialState: StaffDashboardState = { status: 'loading', data: null, error: null };

export function toApiError(error: unknown): ApiError {
  if (error instanceof ApiError) {
    return error;
  }

  return new ApiError(0, 'unknown_error', 'Something went wrong while contacting the server.');
}

/**
 * The dashboard's counts, loaded once per visit with a `reload` for the error state.
 *
 * One request rather than one per module: the API reads every count in a single response, so
 * eight hooks would be eight round trips to build one screen. There is no refresh timer —
 * these rows are written by staff, and a number that changes while somebody is reading it is
 * worse than one that is a few seconds old.
 */
export function useStaffDashboard() {
  const [state, setState] = useState<StaffDashboardState>(initialState);
  const activeRequest = useRef<AbortController | null>(null);

  const load = useCallback(() => {
    activeRequest.current?.abort();

    const controller = new AbortController();
    activeRequest.current = controller;
    setState(initialState);

    void (async () => {
      try {
        const data = await getStaffDashboard(controller.signal);

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
