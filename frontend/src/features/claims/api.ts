import { apiClient } from '@/lib/apiClient';

import type { ContentClaimKind, ContentClaimsOverview, MyContentClaim, PostedContentClaim } from './types';

/** Files an instant claim on one published post and hides it from the public. */
export function fileContentClaim(
  kind: ContentClaimKind,
  slug: string,
  note: string | null,
): Promise<MyContentClaim> {
  const base = kind === 'Job' ? 'jobs' : kind === 'Event' ? 'events' : 'deals';

  return apiClient.post<MyContentClaim>(`/${base}/${encodeURIComponent(slug)}/claim`, { note });
}

/** Both account boxes: claims filed, and claims on posts created. */
export function listContentClaims(signal?: AbortSignal): Promise<ContentClaimsOverview> {
  return apiClient.get<ContentClaimsOverview>('/account/content-claims', { signal });
}

/** The claimant's own claims only, newest first. */
export function listMyContentClaims(signal?: AbortSignal): Promise<MyContentClaim[]> {
  return apiClient.get<MyContentClaim[]>('/account/my-claims', { signal });
}

/** Claims on posts this person created; pending first, then newest. */
export function listPostedContentClaims(signal?: AbortSignal): Promise<PostedContentClaim[]> {
  return apiClient.get<PostedContentClaim[]>('/account/posted-claims', { signal });
}

/** The poster accepts the claimant; the post stays hidden. */
export function acceptContentClaim(id: string, reviewNote: string | null): Promise<PostedContentClaim> {
  return apiClient.post<PostedContentClaim>(`/account/posted-claims/${id}/accept`, { reviewNote });
}

/** The poster releases the claim; the post is public again. */
export function releaseContentClaim(id: string, reviewNote: string | null): Promise<PostedContentClaim> {
  return apiClient.post<PostedContentClaim>(`/account/posted-claims/${id}/release`, { reviewNote });
}
