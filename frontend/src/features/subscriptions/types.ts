/**
 * Mirrors the subscription contracts exposed by the ASP.NET Core API: the plan catalogue,
 * one listing's standing, and the admin queue that decides requests.
 */

/** What a listing can be on. The API serialises these by name. */
export type PlanTier = 'Free' | 'Featured';

/** Where the request sits in the admin queue. */
export type SubscriptionRequestStatus = 'Pending' | 'Approved' | 'Rejected';

/** One plan as the public catalogue shows it. The price is a catalogue figure only. */
export interface Plan {
  id: string;
  tier: PlanTier;
  name: string;
  description: string | null;
  periodDays: number;
  priceNpr: number;
}

/**
 * One listing's standing with the plans, derived at read time against today's Nepal date —
 * so an expired window reports itself without any job having run.
 */
export interface SubscriptionState {
  businessId: string;
  slug: string;
  tier: PlanTier | null;
  planName: string | null;
  startsOn: string | null;
  endsOn: string | null;
  isActive: boolean;
  pendingRequestId: string | null;
}

/** One row of the admin queue: which listing, who asked, and where it stands. */
export interface AdminSubscriptionRequestRow {
  id: string;
  businessId: string;
  slug: string;
  businessName: string;
  tier: PlanTier;
  requesterName: string;
  requesterEmail: string | null;
  note: string | null;
  status: SubscriptionRequestStatus;
  createdAt: string;
  reviewedAt: string | null;
  reviewNote: string | null;
}

/** The queue's status window, as the API's own parser names them. */
export type SubscriptionRequestFilter = 'all' | 'Pending' | 'Approved' | 'Rejected';

export const subscriptionRequestFilters: readonly SubscriptionRequestFilter[] = [
  'all',
  'Pending',
  'Approved',
  'Rejected',
];

export const subscriptionRequestFilterLabels: Record<SubscriptionRequestFilter, string> = {
  all: 'All',
  Pending: 'Waiting',
  Approved: 'Approved',
  Rejected: 'Rejected',
};

export function subscriptionStatusQuery(filter: SubscriptionRequestFilter): string | undefined {
  return filter === 'all' ? undefined : filter;
}

export const planTierLabels: Record<PlanTier, string> = {
  Free: 'Free',
  Featured: 'Featured',
};

/** How a window reads on screen: live, waiting, or over. */
export function describeSubscription(state: SubscriptionState): string {
  if (state.pendingRequestId) {
    return 'A request is waiting for a decision.';
  }

  if (!state.tier) {
    return 'No plan yet.';
  }

  if (state.isActive) {
    return `${state.planName ?? state.tier} is live until ${state.endsOn}.`;
  }

  return `${state.planName ?? state.tier} expired on ${state.endsOn}.`;
}
