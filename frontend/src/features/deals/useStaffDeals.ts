import { useCallback, useEffect, useRef, useState } from 'react';

import { ApiError } from '@/lib/apiClient';

import { getStaffDeal, listDealBusinessOptions, listStaffDeals } from './api';
import type {
  DealAdmin,
  DealAdminListQuery,
  DealAdminSummary,
  DealBusinessOption,
  PagedResult,
} from './types';
import { toApiError } from './useDeals';

export type StaffDealListState =
  | { status: 'loading'; data: null; error: null }
  | { status: 'success'; data: PagedResult<DealAdminSummary>; error: null }
  | { status: 'error'; data: null; error: ApiError };

const initialState: StaffDealListState = { status: 'loading', data: null, error: null };

/**
 * The staff offers board, which shows drafts and expired offers as well as live ones. Those rows
 * are the point of the screen: deciding what to publish and what to retire is staff work, and a
 * live-only list would hide exactly the rows that still need attention.
 */
export function useStaffDealList(query: DealAdminListQuery) {
  const [state, setState] = useState<StaffDealListState>(initialState);
  const activeRequest = useRef<AbortController | null>(null);
  const { page, pageSize, search, province, district, status } = query;

  const load = useCallback(() => {
    activeRequest.current?.abort();

    const controller = new AbortController();
    activeRequest.current = controller;
    setState(initialState);

    void (async () => {
      try {
        const data = await listStaffDeals(
          { page, pageSize, search, province, district, status },
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
  }, [district, page, pageSize, province, search, status]);

  useEffect(() => {
    load();

    return () => {
      activeRequest.current?.abort();
    };
  }, [load]);

  return { ...state, reload: load };
}

export type StaffDealDetailState =
  | { status: 'loading'; data: null; error: null }
  | { status: 'success'; data: DealAdmin; error: null }
  | { status: 'error'; data: null; error: ApiError };

const detailInitialState: StaffDealDetailState = { status: 'loading', data: null, error: null };

/**
 * Loads the full row for the editor. A null id means "creating", which is a valid state here rather
 * than a loading one.
 */
export function useStaffDeal(id: string | null) {
  const [state, setState] = useState<StaffDealDetailState>(detailInitialState);
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
        const data = await getStaffDeal(id, controller.signal);

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

export type BusinessOptionsState =
  | { status: 'loading'; data: DealBusinessOption[]; error: null }
  | { status: 'success'; data: DealBusinessOption[]; error: null }
  | { status: 'error'; data: DealBusinessOption[]; error: ApiError };

/**
 * The businesses an offer can be attached to.
 *
 * The search term is part of the hook rather than something the editor filters afterwards, because
 * the API returns at most 100 published businesses. Filtering a capped list in the browser would
 * silently hide a business that is past the cap, so the narrowing has to happen on the server where
 * the cap is applied.
 */
export function useDealBusinessOptions(search: string): BusinessOptionsState {
  const [state, setState] = useState<BusinessOptionsState>({
    status: 'loading',
    data: [],
    error: null,
  });
  const activeRequest = useRef<AbortController | null>(null);

  const load = useCallback(() => {
    activeRequest.current?.abort();

    const controller = new AbortController();
    activeRequest.current = controller;
    setState({ status: 'loading', data: [], error: null });

    void (async () => {
      try {
        const trimmed = search.trim();
        const data = await listDealBusinessOptions(
          trimmed === '' ? undefined : trimmed,
          controller.signal,
        );

        if (!controller.signal.aborted) {
          setState({ status: 'success', data, error: null });
        }
      } catch (error) {
        if (controller.signal.aborted) {
          return;
        }

        setState({ status: 'error', data: [], error: toApiError(error) });
      }
    })();
  }, [search]);

  useEffect(() => {
    load();

    return () => {
      activeRequest.current?.abort();
    };
  }, [load]);

  return state;
}