import { useCallback, useEffect, useRef, useState } from 'react';

import { ApiError } from '@/lib/apiClient';

import { getStaffBusiness, listStaffBusinesses } from './api';
import type {
  BusinessAdmin,
  BusinessAdminListQuery,
  BusinessAdminSummary,
  PagedResult,
} from './types';
import { toApiError } from './useBusinesses';

export type StaffBusinessListState =
  | { status: 'loading'; data: null; error: null }
  | { status: 'success'; data: PagedResult<BusinessAdminSummary>; error: null }
  | { status: 'error'; data: null; error: ApiError };

const initialState: StaffBusinessListState = { status: 'loading', data: null, error: null };

/**
 * The staff directory, which shows drafts as well as live listings. Those rows are the point of
 * the screen: deciding what to publish is staff work, and a public-only list would hide exactly
 * the rows that still need attention.
 */
export function useStaffBusinessList(query: BusinessAdminListQuery) {
  const [state, setState] = useState<StaffBusinessListState>(initialState);
  const activeRequest = useRef<AbortController | null>(null);
  const { page, pageSize, search, province, district, category, verified, status } = query;

  const load = useCallback(() => {
    activeRequest.current?.abort();

    const controller = new AbortController();
    activeRequest.current = controller;
    setState(initialState);

    void (async () => {
      try {
        const data = await listStaffBusinesses(
          { page, pageSize, search, province, district, category, verified, status },
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
  }, [category, district, page, pageSize, province, search, status, verified]);

  useEffect(() => {
    load();

    return () => {
      activeRequest.current?.abort();
    };
  }, [load]);

  return { ...state, reload: load };
}

export type StaffBusinessDetailState =
  | { status: 'loading'; data: null; error: null }
  | { status: 'success'; data: BusinessAdmin; error: null }
  | { status: 'error'; data: null; error: ApiError };

const detailInitialState: StaffBusinessDetailState = { status: 'loading', data: null, error: null };

/**
 * Loads the full row for the editor. A null id means "creating", which is a valid state here
 * rather than a loading one.
 */
export function useStaffBusiness(id: string | null) {
  const [state, setState] = useState<StaffBusinessDetailState>(detailInitialState);
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
        const data = await getStaffBusiness(id, controller.signal);

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