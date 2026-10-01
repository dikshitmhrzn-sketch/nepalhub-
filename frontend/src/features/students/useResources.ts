import { useCallback, useEffect, useRef, useState } from 'react';

import { ApiError } from '@/lib/apiClient';

import { getResource, listResources } from './api';
import type { PagedResult, ResourceDetail, ResourceListQuery, ResourceSummary } from './types';

export type ResourceListState =
  | { status: 'loading'; data: null; error: null }
  | { status: 'success'; data: PagedResult<ResourceSummary>; error: null }
  | { status: 'error'; data: null; error: ApiError };

const initialState: ResourceListState = { status: 'loading', data: null, error: null };

export function toApiError(error: unknown): ApiError {
  if (error instanceof ApiError) {
    return error;
  }

  return new ApiError(0, 'unknown_error', 'Something went wrong while contacting the server.');
}

/**
 * Loads one page of the public catalogue. The query is passed in rather than owned
 * here, so the page component keeps the filters and the URL in charge of paging.
 */
export function useResourceList(query: ResourceListQuery) {
  const [state, setState] = useState<ResourceListState>(initialState);
  const activeRequest = useRef<AbortController | null>(null);
  const { page, pageSize, search, type, category, level, sort } = query;

  const load = useCallback(() => {
    activeRequest.current?.abort();

    const controller = new AbortController();
    activeRequest.current = controller;
    setState(initialState);

    void (async () => {
      try {
        const data = await listResources(
          { page, pageSize, search, type, category, level, sort },
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
  }, [category, level, page, pageSize, search, sort, type]);

  useEffect(() => {
    load();

    return () => {
      activeRequest.current?.abort();
    };
  }, [load]);

  return { ...state, reload: load };
}

export type ResourceDetailState =
  | { status: 'loading'; data: null; error: null }
  | { status: 'success'; data: ResourceDetail; error: null }
  | { status: 'error'; data: null; error: ApiError };

const detailInitialState: ResourceDetailState = { status: 'loading', data: null, error: null };

/**
 * Loads one resource by its slug. The API answers 404 for a draft and for a row past
 * its deadline, so a reader cannot tell the two apart, which is deliberate: a closed
 * scholarship should not confirm it once existed.
 */
export function useResource(slug: string) {
  const [state, setState] = useState<ResourceDetailState>(detailInitialState);
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
        const data = await getResource(slug, controller.signal);

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
