import { useCallback, useEffect, useRef, useState } from 'react';

import { ApiError } from '@/lib/apiClient';

import { listMySubmissions } from './api';
import type { SubmissionView } from './types';

function toApiError(error: unknown): ApiError {
  if (error instanceof ApiError) {
    return error;
  }

  return new ApiError(0, 'unknown_error', 'Something went wrong while contacting the server.');
}

export type MySubmissionsState =
  | { status: 'loading'; data: null; error: null }
  | { status: 'success'; data: SubmissionView[]; error: null }
  | { status: 'error'; data: null; error: ApiError };

const initialState: MySubmissionsState = { status: 'loading', data: null, error: null };

/**
 * Everything this person has sent in, open work first so the row they are waiting on is
 * the one they see. Reloaded after every write, because the list — not the response of
 * whichever call happened to finish last — is what the screen reads.
 */
export function useMySubmissions() {
  const [state, setState] = useState<MySubmissionsState>(initialState);
  const activeRequest = useRef<AbortController | null>(null);

  const load = useCallback(() => {
    activeRequest.current?.abort();

    const controller = new AbortController();
    activeRequest.current = controller;
    setState(initialState);

    void (async () => {
      try {
        const data = await listMySubmissions(controller.signal);

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
