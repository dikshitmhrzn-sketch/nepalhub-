import { useCallback, useEffect, useRef, useState } from 'react';

import { ApiError } from '@/lib/apiClient';

import type { PagedResult } from '@/features/businesses/types';

import { getPaymentConfig, listAdminPayments, listMyPayments } from './api';
import type { AdminPayment, PaymentConfig, PaymentFilter, PaymentSummary } from './types';

function toApiError(error: unknown): ApiError {
  if (error instanceof ApiError) {
    return error;
  }

  return new ApiError(0, 'unknown_error', 'Something went wrong while contacting the server.');
}

export type PaymentConfigState =
  | { status: 'loading'; data: null; error: null }
  | { status: 'success'; data: PaymentConfig; error: null }
  | { status: 'error'; data: null; error: ApiError };

const initialConfigState: PaymentConfigState = { status: 'loading', data: null, error: null };

/**
 * Whether a provider is wired up at all. Loaded once per screen that offers a checkout
 * button; a screen that never asks whether payment exists never makes this call.
 */
export function usePaymentConfig() {
  const [state, setState] = useState<PaymentConfigState>(initialConfigState);
  const activeRequest = useRef<AbortController | null>(null);

  const load = useCallback(() => {
    activeRequest.current?.abort();

    const controller = new AbortController();
    activeRequest.current = controller;
    setState(initialConfigState);

    void (async () => {
      try {
        const data = await getPaymentConfig(controller.signal);

        if (!controller.signal.aborted) {
          setState({ status: 'success', data, error: null });
        }
      } catch {
        if (controller.signal.aborted) {
          return;
        }

        // No provider to offer is a fact the screen can live with; the buttons simply
        // keep to the request path, and the server has the last word on every checkout.
        setState({ status: 'success', data: { available: false, provider: 'none' }, error: null });
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

export type PaymentsState =
  | { status: 'loading'; data: null; error: null }
  | { status: 'success'; data: PagedResult<PaymentSummary>; error: null }
  | { status: 'error'; data: null; error: ApiError };

const initialPaymentsState: PaymentsState = { status: 'loading', data: null, error: null };

/** Loads one page of the buyer's own payment history. */
export function useMyPayments(query: { page: number; pageSize: number }) {
  const [state, setState] = useState<PaymentsState>(initialPaymentsState);
  const activeRequest = useRef<AbortController | null>(null);
  const { page, pageSize } = query;

  const load = useCallback(() => {
    activeRequest.current?.abort();

    const controller = new AbortController();
    activeRequest.current = controller;
    setState(initialPaymentsState);

    void (async () => {
      try {
        const data = await listMyPayments({ page, pageSize }, controller.signal);

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
  }, [page, pageSize]);

  useEffect(() => {
    load();

    return () => {
      activeRequest.current?.abort();
    };
  }, [load]);

  return { ...state, reload: load };
}

export type AdminPaymentsState =
  | { status: 'loading'; data: null; error: null }
  | { status: 'success'; data: PagedResult<AdminPayment>; error: null }
  | { status: 'error'; data: null; error: ApiError };

const initialAdminState: AdminPaymentsState = { status: 'loading', data: null, error: null };

/** Loads one page of the admin list. The window and page come in so the URL owns both. */
export function useAdminPayments(query: {
  page: number;
  pageSize: number;
  filter?: PaymentFilter;
}) {
  const [state, setState] = useState<AdminPaymentsState>(initialAdminState);
  const activeRequest = useRef<AbortController | null>(null);
  const { page, pageSize, filter } = query;

  const load = useCallback(() => {
    activeRequest.current?.abort();

    const controller = new AbortController();
    activeRequest.current = controller;
    setState(initialAdminState);

    void (async () => {
      try {
        const data = await listAdminPayments({ page, pageSize, filter }, controller.signal);

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
