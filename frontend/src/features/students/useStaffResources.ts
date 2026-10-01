import { useCallback, useEffect, useRef, useState } from 'react';

import { ApiError } from '@/lib/apiClient';

import { getStaffResource, listStaffResources } from './api';
import type { PagedResult, ResourceAdmin, ResourceAdminListQuery, ResourceAdminSummary } from './types';
import { toApiError } from './useResources';

export type StaffResourceListState =
  | { status: 'loading'; data: null; error: null }
  | { status: 'success'; data: PagedResult<ResourceAdminSummary>; error: null }
  | { status: 'error'; data: null; error: ApiError };

const initialState: StaffResourceListState = { status: 'loading', data: null, error: null };

/**
 * The staff catalogue, which shows drafts and rows past their deadline as well as live
 * ones. Those rows are the whole point of the screen: deciding what to retire is staff
 * work, and a public-only list would hide the very rows that need attention.
 */
export function useStaffResourceList(query: ResourceAdminListQuery) {
  const [state, setState] = useState<StaffResourceListState>(initialState);
  const activeRequest = useRef<AbortController | null>(null);
  const { page, pageSize, search, type, category, level, status } = query;

  const load = useCallback(() => {
    activeRequest.current?.abort();

    const controller = new AbortController();
    activeRequest.current = controller;
    setState(initialState);

    void (async () => {
      try {
        const data = await listStaffResources(
          { page, pageSize, search, type, category, level, status },
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
  }, [category, level, page, pageSize, search, status, type]);

  useEffect(() => {
    load();

    return () => {
      activeRequest.current?.abort();
    };
  }, [load]);

  return { ...state, reload: load };
}

export type StaffResourceDetailState =
  | { status: 'loading'; data: null; error: null }
  | { status: 'success'; data: ResourceAdmin; error: null }
  | { status: 'error'; data: null; error: ApiError };

const detailInitialState: StaffResourceDetailState = { status: 'loading', data: null, error: null };

/**
 * Loads the full row for the edit form. A null id means "creating", which is a valid
 * state here rather than a loading one.
 */
export function useStaffResource(id: string | null) {
  const [state, setState] = useState<StaffResourceDetailState>(detailInitialState);
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
        const data = await getStaffResource(id, controller.signal);

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
