import { useCallback, useEffect, useRef, useState } from 'react';

import { ApiError } from '@/lib/apiClient';

import { listContentClaims } from './api';
import type { ContentClaimsOverview } from './types';

function toApiError(error: unknown): ApiError {
  if (error instanceof ApiError) {
    return error;
  }

  return new ApiError(0, 'unknown_error', 'Something went wrong while contacting the server.');
}

export type ContentClaimsState =
  | { status: 'loading'; data: null; error: null }
  | { status: 'success'; data: ContentClaimsOverview; error: null }
  | { status: 'error'; data: null; error: ApiError };

const initialState: ContentClaimsState = { status: 'loading', data: null, error: null };

/** Loads both account claim boxes: claims filed, and claims on posts created. */
export function useContentClaims() {
  const [state, setState] = useState<ContentClaimsState>(initialState);
  const activeRequest = useRef<AbortController | null>(null);

  const load = useCallback(() => {
    activeRequest.current?.abort();

    const controller = new AbortController();
    activeRequest.current = controller;
    setState(initialState);

    void (async () => {
      try {
        const data = await listContentClaims(controller.signal);

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
