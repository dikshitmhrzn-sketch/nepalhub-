import { useCallback, useEffect, useRef, useState } from 'react';

import { ApiError } from '@/lib/apiClient';

import { getStaffJob, listStaffJobs } from './api';
import type { JobAdmin, JobAdminListQuery, JobAdminSummary, PagedResult } from './types';
import { toApiError } from './useJobs';

export type StaffJobListState =
  | { status: 'loading'; data: null; error: null }
  | { status: 'success'; data: PagedResult<JobAdminSummary>; error: null }
  | { status: 'error'; data: null; error: ApiError };

const initialState: StaffJobListState = { status: 'loading', data: null, error: null };

/**
 * The staff board, which shows drafts and closed postings as well as live ones. Those rows are
 * the point of the screen: deciding what to retire is staff work, and a public-only list would
 * hide exactly the rows that need attention.
 */
export function useStaffJobList(query: JobAdminListQuery) {
  const [state, setState] = useState<StaffJobListState>(initialState);
  const activeRequest = useRef<AbortController | null>(null);
  const { page, pageSize, search, province, district, category, employmentType, workMode, status } =
    query;

  const load = useCallback(() => {
    activeRequest.current?.abort();

    const controller = new AbortController();
    activeRequest.current = controller;
    setState(initialState);

    void (async () => {
      try {
        const data = await listStaffJobs(
          {
            page,
            pageSize,
            search,
            province,
            district,
            category,
            employmentType,
            workMode,
            status,
          },
          controller.signal,
        );

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
  }, [category, district, employmentType, page, pageSize, province, search, status, workMode]);

  useEffect(() => {
    load();

    return () => {
      activeRequest.current?.abort();
    };
  }, [load]);

  return { ...state, reload: load };
}

export type StaffJobDetailState =
  | { status: 'loading'; data: null; error: null }
  | { status: 'success'; data: JobAdmin; error: null }
  | { status: 'error'; data: null; error: ApiError };

const detailInitialState: StaffJobDetailState = { status: 'loading', data: null, error: null };

/**
 * Loads the full row for the editor. A null id means "creating", which is a valid state here
 * rather than a loading one.
 */
export function useStaffJob(id: string | null) {
  const [state, setState] = useState<StaffJobDetailState>(detailInitialState);
  const activeRequest = useRef<AbortController | null>(null);

  const load = useCallback(() => {
    activeRequest.current?.abort();

    if (!id) {
      setState(detailInitialState);

      return;
    }

    const controller = new AbortController();
    activeRequest.current = controller;
    setState(detailInitialState);

    void (async () => {
      try {
        const data = await getStaffJob(id, controller.signal);

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