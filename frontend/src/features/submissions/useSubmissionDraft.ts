import { useCallback, useEffect, useRef, useState } from 'react';

import { ApiError } from '@/lib/apiClient';

import { getSubmission } from './api';
import type { SubmissionDetail } from './types';

function toApiError(error: unknown): ApiError {
  if (error instanceof ApiError) {
    return error;
  }

  return new ApiError(0, 'unknown_error', 'Something went wrong while contacting the server.');
}

export type SubmissionDraftState =
  | { status: 'loading'; data: null; error: null }
  | { status: 'success'; data: SubmissionDetail; error: null }
  | { status: 'error'; data: null; error: ApiError };

const initialState: SubmissionDraftState = { status: 'loading', data: null, error: null };

/**
 * One submission with its draft body, for the editor that corrects it. A null id means
 * "composing", which is a valid state here rather than a loading one — the same contract
 * `useStaffJob` keeps, so an editor written against one reads against the other.
 */
export function useSubmissionDraft(id: string | null) {
  const [state, setState] = useState<SubmissionDraftState>(initialState);
  const activeRequest = useRef<AbortController | null>(null);

  const load = useCallback(() => {
    activeRequest.current?.abort();

    if (!id) {
      setState(initialState);

      return;
    }

    const controller = new AbortController();
    activeRequest.current = controller;
    setState(initialState);

    void (async () => {
      try {
        const data = await getSubmission(id, controller.signal);

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
  }, [id]);

  useEffect(() => {
    load();

    return () => {
      activeRequest.current?.abort();
    };
  }, [load]);

  return { ...state, reload: load };
}
