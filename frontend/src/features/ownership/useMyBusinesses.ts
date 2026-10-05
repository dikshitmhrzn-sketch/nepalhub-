import { useCallback, useEffect, useRef, useState } from 'react';

import { ApiError } from '@/lib/apiClient';

import { getMyBusiness, listMyBusinesses } from './api';
import type { MyBusinessesResponse, OwnerBusiness } from './types';

function toApiError(error: unknown): ApiError {
  if (error instanceof ApiError) {
    return error;
  }

  return new ApiError(0, 'unknown_error', 'Something went wrong while contacting the server.');
}

export type MyBusinessesState =
  | { status: 'loading'; data: null; error: null }
  | { status: 'success'; data: MyBusinessesResponse; error: null }
  | { status: 'error'; data: null; error: ApiError };

const initialListState: MyBusinessesState = { status: 'loading', data: null, error: null };

/** Loads the ownership screen: what this person manages, and where each claim stands. */
export function useMyBusinesses() {
  const [state, setState] = useState<MyBusinessesState>(initialListState);
  const activeRequest = useRef<AbortController | null>(null);

  const load = useCallback(() => {
    activeRequest.current?.abort();

    const controller = new AbortController();
    activeRequest.current = controller;
    setState(initialListState);

    void (async () => {
      try {
        const data = await listMyBusinesses(controller.signal);

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
  }, []);

  useEffect(() => {
    load();

    return () => {
      activeRequest.current?.abort();
    };
  }, [load]);

  return { ...state, reload: load };
}

export type MyBusinessState =
  | { status: 'loading'; data: null; error: null }
  | { status: 'success'; data: OwnerBusiness; error: null }
  | { status: 'error'; data: null; error: ApiError };

const initialDetailState: MyBusinessState = { status: 'loading', data: null, error: null };

/** Loads one owned listing for the editor. A null slug means nobody is selected yet. */
export function useMyBusiness(slug: string | null) {
  const [state, setState] = useState<MyBusinessState>(initialDetailState);
  const activeRequest = useRef<AbortController | null>(null);

  const load = useCallback(() => {
    activeRequest.current?.abort();

    if (!slug) {
      setState(initialDetailState);
      return;
    }

    const controller = new AbortController();
    activeRequest.current = controller;
    setState(initialDetailState);

    void (async () => {
      try {
        const data = await getMyBusiness(slug, controller.signal);

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
