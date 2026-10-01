import { useCallback, useEffect, useRef, useState } from 'react';

import { ApiError } from '@/lib/apiClient';

import { getBusiness, listBusinesses } from './api';
import type {
  BusinessDetail,
  BusinessListQuery,
  BusinessSummary,
  PagedResult,
} from './types';

export type BusinessListState =
  | { status: 'loading'; data: null; error: null }
  | { status: 'success'; data: PagedResult<BusinessSummary>; error: null }
  | { status: 'error'; data: null; error: ApiError };

const initialState: BusinessListState = { status: 'loading', data: null, error: null };

export function toApiError(error: unknown): ApiError {
  if (error instanceof ApiError) {
    return error;
  }

  return new ApiError(0, 'unknown_error', 'Something went wrong while contacting the server.');
}

/**
 * Loads one page of the public directory. The query is passed in rather than owned here, so the
 * page component keeps filters and the URL in charge of paging.
 */
export function useBusinessList(query: BusinessListQuery) {
  const [state, setState] = useState<BusinessListState>(initialState);
  const activeRequest = useRef<AbortController | null>(null);
  const { page, pageSize, search, province, district, category, verified, sort } = query;

  const load = useCallback(() => {
    activeRequest.current?.abort();

    const controller = new AbortController();
    activeRequest.current = controller;
    setState(initialState);

    void (async () => {
      try {
        const data = await listBusinesses(
          { page, pageSize, search, province, district, category, verified, sort },
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
  }, [category, district, page, pageSize, province, search, sort, verified]);

  useEffect(() => {
    load();

    return () => {
      activeRequest.current?.abort();
    };
  }, [load]);

  return { ...state, reload: load };
}

export type BusinessDetailState =
  | { status: 'loading'; data: null; error: null }
  | { status: 'success'; data: BusinessDetail; error: null }
  | { status: 'error'; data: null; error: ApiError };

const detailInitialState: BusinessDetailState = { status: 'loading', data: null, error: null };

/**
 * Loads one listing by its slug. The API answers 404 for a draft and for a slug that never
 * existed, so a reader cannot tell those cases apart. That is deliberate: a retracted listing
 * must not confirm it was ever public.
 */
export function useBusiness(slug: string) {
  const [state, setState] = useState<BusinessDetailState>(detailInitialState);
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
        const data = await getBusiness(slug, controller.signal);

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