import { useCallback, useRef, useState } from 'react';

import { ApiError } from '@/lib/apiClient';

import { calculate } from './api';
import type { Calculation } from './types';

export type CalculationState =
  | { status: 'idle'; data: null; error: null }
  | { status: 'loading'; data: null; error: null }
  | { status: 'success'; data: Calculation; error: null }
  | { status: 'error'; data: null; error: ApiError };

const initialState: CalculationState = { status: 'idle', data: null, error: null };

function toApiError(error: unknown): ApiError {
  if (error instanceof ApiError) {
    return error;
  }

  return new ApiError(0, 'unknown_error', 'Something went wrong while contacting the server.');
}

/**
 * Runs a calculation on demand. Unlike the read hooks this one stays idle until the
 * visitor submits, because a calculation is an action rather than a page load.
 */
export function useCalculation(slug: string) {
  const [state, setState] = useState<CalculationState>(initialState);
  const activeRequest = useRef<AbortController | null>(null);

  const run = useCallback(
    async (inputs: Record<string, string | number>) => {
      activeRequest.current?.abort();

      const controller = new AbortController();
      activeRequest.current = controller;
      setState({ status: 'loading', data: null, error: null });

      try {
        const data = await calculate(slug, inputs, controller.signal);

        if (!controller.signal.aborted) {
          setState({ status: 'success', data, error: null });
        }
      } catch (error) {
        if (controller.signal.aborted) {
          return;
        }

        setState({ status: 'error', data: null, error: toApiError(error) });
      }
    },
    [slug],
  );

  const reset = useCallback(() => {
    activeRequest.current?.abort();
    setState(initialState);
  }, []);

  return { ...state, run, reset };
}
