import { useCallback, useEffect, useRef, useState } from 'react';

import { ApiError } from '@/lib/apiClient';

import { search } from './api';
import { minQueryLength, type SearchQuery, type SearchResponse } from './types';

export type SearchState =
  | { status: 'loading'; data: null; error: null }
  | { status: 'success'; data: SearchResponse; error: null }
  | { status: 'error'; data: null; error: ApiError };

const initialState: SearchState = { status: 'loading', data: null, error: null };

function toApiError(error: unknown): ApiError {
  if (error instanceof ApiError) {
    return error;
  }

  return new ApiError(0, 'unknown_error', 'Something went wrong while contacting the server.');
}

/**
 * Loads one page of results. The query is passed in rather than owned here, so the page component
 * keeps the URL in charge of the term, the module and the page number.
 *
 * A query with no term in it is never sent. The API refuses a term under two characters, so
 * searching an empty box would turn an ordinary pause into an error, and the reader would see a
 * red message for having not yet typed anything.
 */
export function useSearchResults(query: SearchQuery | null) {
  const [state, setState] = useState<SearchState>(initialState);
  const activeRequest = useRef<AbortController | null>(null);

  const term = query?.q.trim() ?? '';
  const page = query?.page ?? 1;
  const pageSize = query?.pageSize;
  const module = query?.module ?? 'All';

  // Only the four scalars above are in the dependency list. The caller builds a fresh object on
  // every render, so depending on `query` itself would make this effect re-run forever.
  const enabled = term.length >= minQueryLength;

  const load = useCallback(() => {
    activeRequest.current?.abort();

    if (!enabled) {
      setState(initialState);

      return;
    }

    const controller = new AbortController();
    activeRequest.current = controller;
    setState(initialState);

    void (async () => {
      try {
        const data = await search({ q: term, page, pageSize, module }, controller.signal);

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
  }, [enabled, module, page, pageSize, term]);

  useEffect(() => {
    load();

    return () => {
      activeRequest.current?.abort();
    };
  }, [load]);

  return { ...state, reload: load };
}