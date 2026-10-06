import { apiClient } from '@/lib/apiClient';

import type { PagedResult } from '@/features/businesses/types';

import {
  claimStatusQuery,
  verificationStatusQuery,
  type AdminClaimRow,
  type AdminVerificationRow,
  type ClaimFilter,
  type ClaimView,
  type MyBusinessesResponse,
  type OwnerBusiness,
  type RegisterBusinessRequest,
  type SaveOwnerBusinessRequest,
  type VerificationFilter,
  type VerificationView,
} from './types';

/** Every listing this person manages, every claim they have filed, every verification they asked for. */
export function listMyBusinesses(signal?: AbortSignal): Promise<MyBusinessesResponse> {
  return apiClient.get<MyBusinessesResponse>('/account/businesses', { signal });
}

/**
 * Registers a listing of the caller's own. The row comes back owned, unpublished and
 * unverified, and it appears on the ownership screen and nowhere public.
 */
export function registerBusiness(body: RegisterBusinessRequest): Promise<OwnerBusiness> {
  return apiClient.post<OwnerBusiness>('/account/businesses', body);
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

/** Asks staff to check one of this person's drafts. A blank note is legitimate. */
export function submitVerification(slug: string, note: string | null): Promise<VerificationView> {
  return apiClient.post<VerificationView>(
    `/account/businesses/${encodeURIComponent(slug)}/verification`,
    { note },
  );
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

/** One page of the verification queue, open work first when no window is selected. */
export function listVerifications(
  query: { page: number; pageSize: number; filter?: VerificationFilter },
  signal?: AbortSignal,
): Promise<PagedResult<AdminVerificationRow>> {
  return apiClient.get<PagedResult<AdminVerificationRow>>('/admin/business-verifications', {
    query: {
      page: query.page,
      pageSize: query.pageSize,
      status: verificationStatusQuery(query.filter ?? 'all'),
    },
    signal,
  });
}

/**
 * Approves or rejects one verification request. Approval publishes the listing and badges
 * it when it satisfies the publish rules; a listing that has gained a blocker since it was
 * submitted answers 409 for the page to explain rather than going out half-checked.
 */
export function reviewVerification(
  id: string,
  decision: 'approve' | 'reject',
  reviewNote: string | null,
): Promise<AdminVerificationRow> {
  return apiClient.post<AdminVerificationRow>(
    `/admin/business-verifications/${id}/${decision}`,
    { reviewNote },
  );
}
