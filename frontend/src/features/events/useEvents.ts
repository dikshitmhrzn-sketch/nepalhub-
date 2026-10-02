import { useCallback, useEffect, useRef, useState } from 'react';

import { ApiError } from '@/lib/apiClient';

import { getEvent, listEvents } from './api';
import type { EventDetail, EventListQuery, EventSummary, PagedResult } from './types';

export type EventListState =
  | { status: 'loading'; data: null; error: null }
  | { status: 'success'; data: PagedResult<EventSummary>; error: null }
  | { status: 'error'; data: null; error: ApiError };

const initialState: EventListState = { status: 'loading', data: null, error: null };

export function toApiError(error: unknown): ApiError {
  if (error instanceof ApiError) {
    return error;
  }

  return new ApiError(0, 'unknown_error', 'Something went wrong while contacting the server.');
}

/**
 * Loads one page of upcoming events. The query is passed in rather than owned here, so the page
 * component keeps filters and the URL in charge of paging.
 *
 * Finished events are already gone by the time this runs: the API drops them, so the page never has
 * to decide whether to show one.
 */
export function useEventList(query: EventListQuery) {
  const [state, setState] = useState<EventListState>(initialState);
  const activeRequest = useRef<AbortController | null>(null);
  const { page, pageSize, search, province, district, category, sort } = query;

  const load = useCallback(() => {
    activeRequest.current?.abort();

    const controller = new AbortController();
    activeRequest.current = controller;
    setState(initialState);

    void (async () => {
      try {
        const data = await listEvents(
          { page, pageSize, search, province, district, category, sort },
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
  }, [category, district, page, pageSize, province, search, sort]);

  useEffect(() => {
    load();

    return () => {
      activeRequest.current?.abort();
    };
  }, [load]);

  return { ...state, reload: load };
}

export type EventDetailState =
  | { status: 'loading'; data: null; error: null }
  | { status: 'success'; data: EventDetail; error: null }
  | { status: 'error'; data: null; error: ApiError };

const detailInitialState: EventDetailState = { status: 'loading', data: null, error: null };

/**
 * Loads one event by its slug. The API answers 404 for a draft, for an event that has finished and
 * for a slug that never existed, so a reader cannot tell those cases apart. That is deliberate: a
 * finished event must not confirm it was ever public.
 */
export function useEvent(slug: string) {
  const [state, setState] = useState<EventDetailState>(detailInitialState);
  const activeRequest = useRef<AbortController | null>(null);

  const load = useCallback(() => {
    activeRequest.current?.abort();

    if (!slug) {
      setState(detailInitialState);

      return;
    }

    const controller = new AbortController();
    activeRequest.current = controller;
    setState(detailInitialState);

    void (async () => {
      try {
        const data = await getEvent(slug, controller.signal);

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
  }, [slug]);

  useEffect(() => {
    load();

    return () => {
      activeRequest.current?.abort();
    };
  }, [load]);

  return { ...state, reload: load };
}