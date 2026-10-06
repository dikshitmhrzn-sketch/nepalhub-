import { apiClient } from '@/lib/apiClient';

import type { PagedResult } from '@/features/businesses/types';
import type { PlanTier } from '@/features/subscriptions/types';

import { paymentStatusQuery, type PaymentFilter } from './types';
import type {
  AdminPayment,
  CheckoutHandshake,
  Payment,
  PaymentConfig,
  PaymentKind,
  PaymentSummary,
} from './types';

/**
 * Whether checkout exists at all. Anonymous and cacheable by the page that offers a button,
 * because the listing and event pages ask before anybody has signed in.
 */
export function getPaymentConfig(signal?: AbortSignal): Promise<PaymentConfig> {
  return apiClient.get<PaymentConfig>('/payments/config', { signal });
}

export interface CheckoutRequest {
  kind: PaymentKind;
  /** The listing or the event being bought for, by the address the site uses. */
  slug: string;
  /** Which plan a subscription checkout is for. Free is never a checkout. */
  tier?: PlanTier;
  /** Places held by one ticket checkout, one to four. */
  quantity?: number;
}

/**
 * Starts one checkout: takes the seat or reuses the question already being asked, writes the
 * payment row, and answers with the signed form to post to the provider. Nothing is charged
 * here — the charge happens on the provider's own pages.
 */
export function startCheckout(body: CheckoutRequest): Promise<CheckoutHandshake> {
  return apiClient.post<CheckoutHandshake>('/payments/checkout', body);
}

/**
 * Asks what became of one reference, carrying the signed payload when the provider sent one
 * back with the buyer. The signature is checked and still not believed: settlement asks the
 * provider directly, so an edited return URL settles nothing.
 */
export function verifyPayment(body: {
  reference: string;
  data?: string | null;
  signature?: string | null;
}): Promise<Payment> {
  return apiClient.post<Payment>('/payments/verify', body);
}

/** One payment of the buyer's own, read back after a refresh or a closed tab. */
export function getPayment(reference: string, signal?: AbortSignal): Promise<Payment> {
  return apiClient.get<Payment>(`/payments/${encodeURIComponent(reference)}`, { signal });
}

/** One page of the buyer's own history. */
export function listMyPayments(
  query: { page: number; pageSize: number },
  signal?: AbortSignal,
): Promise<PagedResult<PaymentSummary>> {
  return apiClient.get<PagedResult<PaymentSummary>>('/account/payments', {
    query: { page: query.page, pageSize: query.pageSize },
    signal,
  });
}

/** One page of the admin list, waiting payments first when no window is selected. */
export function listAdminPayments(
  query: { page: number; pageSize: number; filter?: PaymentFilter },
  signal?: AbortSignal,
): Promise<PagedResult<AdminPayment>> {
  return apiClient.get<PagedResult<AdminPayment>>('/admin/payments', {
    query: {
      page: query.page,
      pageSize: query.pageSize,
      status: paymentStatusQuery(query.filter ?? 'all'),
    },
    signal,
  });
}

/**
 * Asks the provider by hand what became of one reference. This is how a settlement nobody
 * came back for is found, and how a refund taken in the provider's own portal is noticed.
 */
export function checkPaymentStatus(reference: string): Promise<Payment> {
  return apiClient.post<Payment>(
    `/admin/payments/${encodeURIComponent(reference)}/check-status`,
  );
}
