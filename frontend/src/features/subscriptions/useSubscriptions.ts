import { useCallback, useEffect, useRef, useState } from 'react';

import { ApiError } from '@/lib/apiClient';

import type { PagedResult } from '@/features/businesses/types';

import { getSubscriptionState, listPlans, listSubscriptionRequests } from './api';
import type {
  AdminSubscriptionRequestRow,
  Plan,
  SubscriptionRequestFilter,
  SubscriptionState,
} from './types';

function toApiError(error: unknown): ApiError {
  if (error instanceof ApiError) {
    return error;
  }

  return new ApiError(0, 'unknown_error', 'Something went wrong while contacting the server.');
}

export type PlansState =
  | { status: 'loading'; data: null; error: null }
  | { status: 'success'; data: Plan[]; error: null }
  | { status: 'error'; data: null; error: ApiError };

const initialPlansState: PlansState = { status: 'loading', data: null, error: null };

/** Loads the plan catalogue once for the screen that offers it. */
export function usePlans() {
  const [state, setState] = useState<PlansState>(initialPlansState);
  const activeRequest = useRef<AbortController | null>(null);

  const load = useCallback(() => {
    activeRequest.current?.abort();

    const controller = new AbortController();
    activeRequest.current = controller;
    setState(initialPlansState);

    void (async () => {
      try {
        const data = await listPlans(controller.signal);

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

export type SubscriptionStateView =
  | { status: 'loading'; data: null; error: null }
  | { status: 'success'; data: SubscriptionState; error: null }
  | { status: 'error'; data: null; error: ApiError };

const initialSubscriptionState: SubscriptionStateView = {
  status: 'loading',
  data: null,
  error: null,
};

/** Loads one owned listing's standing with the plans. A null slug means nobody is selected. */
export function useSubscriptionState(slug: string | null) {
  const [state, setState] = useState<SubscriptionStateView>(initialSubscriptionState);
  const activeRequest = useRef<AbortController | null>(null);

  const load = useCallback(() => {
    activeRequest.current?.abort();

    if (!slug) {
      setState(initialSubscriptionState);
      return;
    }

    const controller = new AbortController();
    activeRequest.current = controller;
    setState(initialSubscriptionState);

    void (async () => {
      try {
        const data = await getSubscriptionState(slug, controller.signal);

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

export type SubscriptionRequestsState =
  | { status: 'loading'; data: null; error: null }
  | { status: 'success'; data: PagedResult<AdminSubscriptionRequestRow>; error: null }
  | { status: 'error'; data: null; error: ApiError };

const initialRequestsState: SubscriptionRequestsState = {
  status: 'loading',
  data: null,
  error: null,
};

/** Loads one page of the admin queue. The window and page come in so the URL owns both. */
export function useSubscriptionRequests(query: {
  page: number;
  pageSize: number;
  filter?: SubscriptionRequestFilter;
}) {
  const [state, setState] = useState<SubscriptionRequestsState>(initialRequestsState);
  const activeRequest = useRef<AbortController | null>(null);
  const { page, pageSize, filter } = query;

  const load = useCallback(() => {
    activeRequest.current?.abort();

    const controller = new AbortController();
    activeRequest.current = controller;
    setState(initialRequestsState);

    void (async () => {
      try {
        const data = await listSubscriptionRequests({ page, pageSize, filter }, controller.signal);

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
  }, [filter, page, pageSize]);

  useEffect(() => {
    load();

    return () => {
      activeRequest.current?.abort();
    };
  }, [load]);

  return { ...state, reload: load };
}
