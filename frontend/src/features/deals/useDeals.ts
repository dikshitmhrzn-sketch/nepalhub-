import { useCallback, useEffect, useRef, useState } from 'react';

import { ApiError } from '@/lib/apiClient';

import { getDeal, listDeals } from './api';
import type { DealDetail, DealListQuery, DealSummary, PagedResult } from './types';

export type DealListState =
  | { status: 'loading'; data: null; error: null }
  | { status: 'success'; data: PagedResult<DealSummary>; error: null }
  | { status: 'error'; data: null; error: ApiError };

const initialState: DealListState = { status: 'loading', data: null, error: null };

export function toApiError(error: unknown): ApiError {
  if (error instanceof ApiError) {
    return error;
  }

  return new ApiError(0, 'unknown_error', 'Something went wrong while contacting the server.');
}

/**
 * Loads one page of live offers. The query is passed in rather than owned here, so the page
 * component keeps filters and the URL in charge of paging.
 */
export function useDealList(query: DealListQuery) {
  const [state, setState] = useState<DealListState>(initialState);
  const activeRequest = useRef<AbortController | null>(null);
  const { page, pageSize, search, province, district, sort } = query;

  const load = useCallback(() => {
    activeRequest.current?.abort();

    const controller = new AbortController();
    activeRequest.current = controller;
    setState(initialState);

    void (async () => {
      try {
        const data = await listDeals(
          { page, pageSize, search, province, district, sort },
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
  }, [district, page, pageSize, province, search, sort]);

  useEffect(() => {
    load();

    return () => {
      activeRequest.current?.abort();
    };
  }, [load]);

  return { ...state, reload: load };
}

export type DealDetailState =
  | { status: 'loading'; data: null; error: null }
  | { status: 'success'; data: DealDetail; error: null }
  | { status: 'error'; data: null; error: ApiError };

const detailInitialState: DealDetailState = { status: 'loading', data: null, error: null };

/**
 * Loads one offer by its slug. The API answers 404 for a draft, for an offer that has ended and for
 * a slug that never existed, so a reader cannot tell those cases apart. That is deliberate: an
 * expired offer must not confirm it was ever public.
 */
export function useDeal(slug: string) {
  const [state, setState] = useState<DealDetailState>(detailInitialState);
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
        const data = await getDeal(slug, controller.signal);

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