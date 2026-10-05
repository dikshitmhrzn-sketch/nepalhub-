import { useCallback, useEffect, useRef, useState } from 'react';

import { ApiError } from '@/lib/apiClient';

import { listReports } from './api';
import type { PagedResult, ReportQueueFilter, ReportQueueRow } from './types';

export type ReportQueueState =
  | { status: 'loading'; data: null; error: null }
  | { status: 'success'; data: PagedResult<ReportQueueRow>; error: null }
  | { status: 'error'; data: null; error: ApiError };

const initialState: ReportQueueState = { status: 'loading', data: null, error: null };

function toApiError(error: unknown): ApiError {
  if (error instanceof ApiError) {
    return error;
  }

  return new ApiError(0, 'unknown_error', 'Something went wrong while contacting the server.');
}

/**
 * Loads one page of the staff queue. The window and page are passed in, so the page keeps
 * the URL in charge of both, and closing a report reloads the same query rather than
 * patching the row in place — the queue's order depends on status, and a row that just
 * changed status no longer knows where it belongs.
 */
export function useReportQueue(query: {
  page: number;
  pageSize: number;
  status: ReportQueueFilter;
}) {
  const [state, setState] = useState<ReportQueueState>(initialState);
  const activeRequest = useRef<AbortController | null>(null);
  const { page, pageSize, status } = query;

  const load = useCallback(() => {
    activeRequest.current?.abort();

    const controller = new AbortController();
    activeRequest.current = controller;
    setState(initialState);

    void (async () => {
      try {
        const data = await listReports({ page, pageSize, status }, controller.signal);

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
  }, [page, pageSize, status]);

  useEffect(() => {
    load();

    return () => {
      activeRequest.current?.abort();
    };
  }, [load]);

  return { ...state, reload: load };
}
