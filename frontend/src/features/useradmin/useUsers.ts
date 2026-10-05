import { useCallback, useEffect, useRef, useState } from 'react';

import { ApiError } from '@/lib/apiClient';

import { listUsers } from './api';
import type { PagedResult, PlatformRole, UserAdminSummary, UserStatusFilter } from './types';

export type UserListState =
  | { status: 'loading'; data: null; error: null }
  | { status: 'success'; data: PagedResult<UserAdminSummary>; error: null }
  | { status: 'error'; data: null; error: ApiError };

const initialState: UserListState = { status: 'loading', data: null, error: null };

function toApiError(error: unknown): ApiError {
  if (error instanceof ApiError) {
    return error;
  }

  return new ApiError(0, 'unknown_error', 'Something went wrong while contacting the server.');
}

/**
 * Loads one page of the account list. The filters and page are passed in, so the URL
 * stays in charge of what the screen is showing, and the page reloads the same query
 * after an action rather than patching the row — the account may have left the window
 * the filter is on.
 */
export function useUsers(query: {
  page: number;
  pageSize: number;
  search?: string;
  status: UserStatusFilter;
  role?: PlatformRole;
}) {
  const [state, setState] = useState<UserListState>(initialState);
  const activeRequest = useRef<AbortController | null>(null);
  const { page, pageSize, search, status, role } = query;

  const load = useCallback(() => {
    activeRequest.current?.abort();

    const controller = new AbortController();
    activeRequest.current = controller;
    setState(initialState);

    void (async () => {
      try {
        const data = await listUsers({ page, pageSize, search, status, role }, controller.signal);

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
  }, [page, pageSize, role, search, status]);

  useEffect(() => {
    load();

    return () => {
      activeRequest.current?.abort();
    };
  }, [load]);

  return { ...state, reload: load };
}
