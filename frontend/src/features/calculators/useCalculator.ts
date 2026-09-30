import { useCallback, useEffect, useRef, useState } from 'react';

import { ApiError } from '@/lib/apiClient';

import { getCalculator } from './api';
import type { CalculatorDetail } from './types';

export type CalculatorDetailState =
  | { status: 'loading'; data: null; error: null }
  | { status: 'success'; data: CalculatorDetail; error: null }
  | { status: 'error'; data: null; error: ApiError };

const initialState: CalculatorDetailState = { status: 'loading', data: null, error: null };

function toApiError(error: unknown): ApiError {
  if (error instanceof ApiError) {
    return error;
  }

  return new ApiError(0, 'unknown_error', 'Something went wrong while contacting the server.');
}

/** Loads one calculator by slug. The slug is a parameter, so navigating works. */
export function useCalculator(slug: string) {
  const [state, setState] = useState<CalculatorDetailState>(initialState);
  const activeRequest = useRef<AbortController | null>(null);

  const load = useCallback(() => {
    activeRequest.current?.abort();

    const controller = new AbortController();
    activeRequest.current = controller;
    setState(initialState);

    void (async () => {
      try {
        const data = await getCalculator(slug, controller.signal);

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
