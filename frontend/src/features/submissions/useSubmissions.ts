import { useCallback, useEffect, useRef, useState } from 'react';

import { ApiError } from '@/lib/apiClient';

import type { PagedResult } from '@/features/businesses/types';

import { listSubmissions } from './api';
import type {
  AdminSubmissionRow,
  SubmissionKindFilter,
  SubmissionStatusFilter,
} from './types';

function toApiError(error: unknown): ApiError {
  if (error instanceof ApiError) {
    return error;
  }

  return new ApiError(0, 'unknown_error', 'Something went wrong while contacting the server.');
}

export type SubmissionsState =
  | { status: 'loading'; data: null; error: null }
  | { status: 'success'; data: PagedResult<AdminSubmissionRow>; error: null }
  | { status: 'error'; data: null; error: ApiError };

const initialState: SubmissionsState = { status: 'loading', data: null, error: null };

/**
 * Loads one page of the submission queue. The status window, the kind window and the page
 * all come in so the URL owns all three — the same terms the claim and verification
 * queues keep, so a moderator who has learned one has learned this one.
 */
export function useSubmissions(query: {
  page: number;
  pageSize: number;
  status?: SubmissionStatusFilter;
  kind?: SubmissionKindFilter;
}) {
  const [state, setState] = useState<SubmissionsState>(initialState);
  const activeRequest = useRef<AbortController | null>(null);
  const { page, pageSize, status, kind } = query;

  const load = useCallback(() => {
    activeRequest.current?.abort();

    const controller = new AbortController();
    activeRequest.current = controller;
    setState(initialState);

    void (async () => {
      try {
        const data = await listSubmissions({ page, pageSize, status, kind }, controller.signal);

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
  }, [kind, page, pageSize, status]);

  useEffect(() => {
    load();

    return () => {
      activeRequest.current?.abort();
    };
  }, [load]);

  return { ...state, reload: load };
}
