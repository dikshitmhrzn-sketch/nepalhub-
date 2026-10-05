import { apiClient } from '@/lib/apiClient';

import type { PagedResult } from '@/features/businesses/types';

import {
  subscriptionStatusQuery,
  type AdminSubscriptionRequestRow,
  type Plan,
  type PlanTier,
  type SubscriptionRequestFilter,
  type SubscriptionState,
} from './types';

/** The public plan catalogue, cheapest first. Anonymous, so a reader can see the prices. */
export function listPlans(signal?: AbortSignal): Promise<Plan[]> {
  return apiClient.get<Plan[]>('/plans', { signal });
}

/** One listing's standing: which plan, is the window live, is a request waiting. */
export function getSubscriptionState(slug: string, signal?: AbortSignal): Promise<SubscriptionState> {
  return apiClient.get<SubscriptionState>(
    `/account/businesses/${encodeURIComponent(slug)}/subscription`,
    { signal },
  );
}

/**
 * Asks for a plan. The free plan is granted on the spot; featured lands in the admin queue.
 * Either way the response is the listing's standing, so the screen re-renders from one shape.
 */
export function requestPlan(slug: string, tier: PlanTier, note: string | null): Promise<SubscriptionState> {
  return apiClient.post<SubscriptionState>(
    `/account/businesses/${encodeURIComponent(slug)}/subscription-requests`,
    { tier, note },
  );
}

/** One page of the admin queue, waiting requests first when no window is selected. */
export function listSubscriptionRequests(
  query: { page: number; pageSize: number; filter?: SubscriptionRequestFilter },
  signal?: AbortSignal,
): Promise<PagedResult<AdminSubscriptionRequestRow>> {
  return apiClient.get<PagedResult<AdminSubscriptionRequestRow>>('/admin/subscription-requests', {
    query: {
      page: query.page,
      pageSize: query.pageSize,
      status: subscriptionStatusQuery(query.filter ?? 'all'),
    },
    signal,
  });
}

/** Approves or rejects one request. Approval opens or rolls the window in the same save. */
export function reviewSubscriptionRequest(
  id: string,
  decision: 'approve' | 'reject',
  reviewNote: string | null,
): Promise<AdminSubscriptionRequestRow> {
  return apiClient.post<AdminSubscriptionRequestRow>(
    `/admin/subscription-requests/${id}/${decision}`,
    { reviewNote },
  );
}

/** A direct grant or extension from the admin side, by the same extension rule. */
export function grantPlan(businessId: string, tier: PlanTier): Promise<SubscriptionState> {
  return apiClient.post<SubscriptionState>('/admin/subscriptions/grant', { businessId, tier });
}

/** Stops a listing's window now; the subscription row is kept as the record. */
export function cancelPlan(businessId: string): Promise<SubscriptionState> {
  return apiClient.post<SubscriptionState>(`/admin/subscriptions/${businessId}/cancel`);
}
