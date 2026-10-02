import { useCallback, useEffect, useRef, useState } from 'react';

import { ApiError } from '@/lib/apiClient';

import { getStaffEvent, listEventBusinessOptions, listStaffEvents } from './api';
import type {
  EventAdmin,
  EventAdminListQuery,
  EventAdminSummary,
  EventBusinessOption,
  PagedResult,
} from './types';
import { toApiError } from './useEvents';

export type StaffEventListState =
  | { status: 'loading'; data: null; error: null }
  | { status: 'success'; data: PagedResult<EventAdminSummary>; error: null }
  | { status: 'error'; data: null; error: ApiError };

const initialState: StaffEventListState = { status: 'loading', data: null, error: null };

/**
 * The staff events board, which shows drafts and finished events as well as upcoming ones. Those
 * rows are the point of the screen: deciding what to publish and what has passed is staff work, and
 * a public-only list would hide exactly the rows that still need attention.
 */
export function useStaffEventList(query: EventAdminListQuery) {
  const [state, setState] = useState<StaffEventListState>(initialState);
  const activeRequest = useRef<AbortController | null>(null);
  const { page, pageSize, search, province, district, category, status } = query;

  const load = useCallback(() => {
    activeRequest.current?.abort();

    const controller = new AbortController();
    activeRequest.current = controller;
    setState(initialState);

    void (async () => {
      try {
        const data = await listStaffEvents(
          { page, pageSize, search, province, district, category, status },
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
  }, [category, district, page, pageSize, province, search, status]);

  useEffect(() => {
    load();

    return () => {
      activeRequest.current?.abort();
    };
  }, [load]);

  return { ...state, reload: load };
}

export type StaffEventDetailState =
  | { status: 'loading'; data: null; error: null }
  | { status: 'success'; data: EventAdmin; error: null }
  | { status: 'error'; data: null; error: ApiError };

const detailInitialState: StaffEventDetailState = { status: 'loading', data: null, error: null };

/**
 * Loads the full row for the editor. A null id means "creating", which is a valid state here rather
 * than a loading one.
 */
export function useStaffEvent(id: string | null) {
  const [state, setState] = useState<StaffEventDetailState>(detailInitialState);
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
        const data = await getStaffEvent(id, controller.signal);

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
  | { status: 'loading'; data: EventBusinessOption[]; error: null }
  | { status: 'success'; data: EventBusinessOption[]; error: null }
  | { status: 'error'; data: EventBusinessOption[]; error: ApiError };

/**
 * The businesses an event can be attached to.
 *
 * The search term is part of the hook rather than something the editor filters afterwards, because
 * the API returns at most 100 published businesses. Filtering a capped list in the browser would
 * silently hide a business that is past the cap, so the narrowing has to happen on the server where
 * the cap is applied.
 */
export function useEventBusinessOptions(search: string): BusinessOptionsState {
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
        const data = await listEventBusinessOptions(
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