import { useCallback, useEffect, useRef, useState } from 'react';

import { ApiError } from '@/lib/apiClient';

import { listAuditLog } from './api';
import type { AuditLogEntry, PagedResult } from './types';

export type AuditLogState =
  | { status: 'loading'; data: null; error: null }
  | { status: 'success'; data: PagedResult<AuditLogEntry>; error: null }
  | { status: 'error'; data: null; error: ApiError };

const initialState: AuditLogState = { status: 'loading', data: null, error: null };

function toApiError(error: unknown): ApiError {
  if (error instanceof ApiError) {
    return error;
  }

  return new ApiError(0, 'unknown_error', 'Something went wrong while contacting the server.');
}

/** Loads one page of the trail. The filters and page are passed in so the URL owns both. */
export function useAuditLog(query: {
  page: number;
  pageSize: number;
  action?: string;
  targetType?: string;
}) {
  const [state, setState] = useState<AuditLogState>(initialState);
  const activeRequest = useRef<AbortController | null>(null);
  const { page, pageSize, action, targetType } = query;

  const load = useCallback(() => {
    activeRequest.current?.abort();

    const controller = new AbortController();
    activeRequest.current = controller;
    setState(initialState);

    void (async () => {
      try {
        const data = await listAuditLog({ page, pageSize, action, targetType }, controller.signal);

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
  }, [action, page, pageSize, targetType]);

  useEffect(() => {
    load();

    return () => {
      activeRequest.current?.abort();
    };
  }, [load]);

  return { ...state, reload: load };
}
