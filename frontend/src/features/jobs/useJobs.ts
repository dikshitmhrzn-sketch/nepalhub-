import { useCallback, useEffect, useRef, useState } from 'react';

import { ApiError } from '@/lib/apiClient';

import { getJob, listJobs } from './api';
import type { JobDetail, JobListQuery, JobSummary, PagedResult } from './types';

export type JobListState =
  | { status: 'loading'; data: null; error: null }
  | { status: 'success'; data: PagedResult<JobSummary>; error: null }
  | { status: 'error'; data: null; error: ApiError };

const initialState: JobListState = { status: 'loading', data: null, error: null };

export function toApiError(error: unknown): ApiError {
  if (error instanceof ApiError) {
    return error;
  }

  return new ApiError(0, 'unknown_error', 'Something went wrong while contacting the server.');
}

/**
 * Loads one page of the public job board. The query is passed in rather than owned here, so the
 * page component keeps filters and the URL in charge of paging.
 */
export function useJobList(query: JobListQuery) {
  const [state, setState] = useState<JobListState>(initialState);
  const activeRequest = useRef<AbortController | null>(null);
  const { page, pageSize, search, province, district, category, employmentType, workMode, sort } =
    query;

  const load = useCallback(() => {
    activeRequest.current?.abort();

    const controller = new AbortController();
    activeRequest.current = controller;
    setState(initialState);

    void (async () => {
      try {
        const data = await listJobs(
          {
            page,
            pageSize,
            search,
            province,
            district,
            category,
            employmentType,
            workMode,
            sort,
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
  }, [category, district, employmentType, page, pageSize, province, search, sort, workMode]);

  useEffect(() => {
    load();

    return () => {
      activeRequest.current?.abort();
    };
  }, [load]);

  return { ...state, reload: load };
}

export type JobDetailState =
  | { status: 'loading'; data: null; error: null }
  | { status: 'success'; data: JobDetail; error: null }
  | { status: 'error'; data: null; error: ApiError };

const detailInitialState: JobDetailState = { status: 'loading', data: null, error: null };

/**
 * Loads one posting by its slug. The API answers 404 for a draft, for a closed posting and for a
 * slug that never existed, so a reader cannot tell those cases apart. That is deliberate: a closed
 * posting must not confirm it was ever public.
 */
export function useJob(slug: string) {
  const [state, setState] = useState<JobDetailState>(detailInitialState);
  const activeRequest = useRef<AbortController | null>(null);

  const load = useCallback(() => {
    activeRequest.current?.abort();

    if (!slug) {
      setState(detailInitialState);

      return;
    }

    const controller = new AbortController();
    activeRequest.current = controller;
    setState(detailInitialState);

    void (async () => {
      try {
        const data = await getJob(slug, controller.signal);

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