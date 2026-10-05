import { apiClient } from '@/lib/apiClient';

import type { PagedResult } from '@/features/businesses/types';

import {
  claimStatusQuery,
  type AdminClaimRow,
  type ClaimFilter,
  type ClaimView,
  type MyBusinessesResponse,
  type OwnerBusiness,
  type SaveOwnerBusinessRequest,
} from './types';

/** Every listing this person manages and every claim they have filed. */
export function listMyBusinesses(signal?: AbortSignal): Promise<MyBusinessesResponse> {
  return apiClient.get<MyBusinessesResponse>('/account/businesses', { signal });
}

/** One owned listing in full, for the editor. Somebody else's answers the same 404 as none. */
export function getMyBusiness(slug: string, signal?: AbortSignal): Promise<OwnerBusiness> {
  return apiClient.get<OwnerBusiness>(`/account/businesses/${encodeURIComponent(slug)}`, { signal });
}

/**
 * Saves the owner's fields. The whole week is sent every time — the API replaces the set
 * rather than diffing it, so a partial week would silently erase the missing days.
 */
export function saveMyBusiness(slug: string, body: SaveOwnerBusinessRequest): Promise<OwnerBusiness> {
  return apiClient.put<OwnerBusiness>(`/account/businesses/${encodeURIComponent(slug)}`, body);
}

/** Files a claim against one published listing. A blank evidence note is legitimate. */
export function fileClaim(slug: string, evidence: string | null): Promise<ClaimView> {
  return apiClient.post<ClaimView>(`/businesses/${encodeURIComponent(slug)}/claim`, { evidence });
}

/** One page of the staff queue, open work first when no window is selected. */
export function listClaims(
  query: { page: number; pageSize: number; filter?: ClaimFilter },
  signal?: AbortSignal,
): Promise<PagedResult<AdminClaimRow>> {
  return apiClient.get<PagedResult<AdminClaimRow>>('/admin/business-claims', {
    query: {
      page: query.page,
      pageSize: query.pageSize,
      status: claimStatusQuery(query.filter ?? 'all'),
    },
    signal,
  });
}

/**
 * Approves or rejects one claim. The note is the staff member's own words on the record,
 * and a claim another moderator decided in the meantime answers 409 for the page to explain.
 */
export function reviewClaim(
  id: string,
  decision: 'approve' | 'reject',
  reviewNote: string | null,
): Promise<AdminClaimRow> {
  return apiClient.post<AdminClaimRow>(`/admin/business-claims/${id}/${decision}`, { reviewNote });
}
