import { apiClient } from '@/lib/apiClient';

import type { PagedResult } from '@/features/businesses/types';

import {
  submissionKindQuery,
  submissionStatusQuery,
  type AdminSubmissionRow,
  type SubmissionDetail,
  type SubmissionKindFilter,
  type SubmissionStatusFilter,
  type SubmissionView,
  type SubmitDealRequest,
  type SubmitEventRequest,
  type SubmitJobRequest,
  type UpdateSubmissionRequest,
} from './types';

/** Everything this person has sent in, open work first. Their own rows and nobody else's. */
export function listMySubmissions(signal?: AbortSignal): Promise<SubmissionView[]> {
  return apiClient.get<SubmissionView[]>('/account/submissions', { signal });
}

/** One of their submissions with the draft body behind it, for the editor that corrects it. */
export function getSubmission(id: string, signal?: AbortSignal): Promise<SubmissionDetail> {
  return apiClient.get<SubmissionDetail>(`/account/submissions/${encodeURIComponent(id)}`, {
    signal,
  });
}

/**
 * Writes a job draft and opens the request against it in one step. The row this creates
 * is born unpublished — going live is the one thing the queue exists to decide.
 */
export function submitJobSubmission(body: SubmitJobRequest): Promise<SubmissionView> {
  return apiClient.post<SubmissionView>('/account/submissions/jobs', body);
}

export function submitEventSubmission(body: SubmitEventRequest): Promise<SubmissionView> {
  return apiClient.post<SubmissionView>('/account/submissions/events', body);
}

export function submitDealSubmission(body: SubmitDealRequest): Promise<SubmissionView> {
  return apiClient.post<SubmissionView>('/account/submissions/deals', body);
}

/**
 * The author's correction to one draft, body and note replaced together. A submission
 * another moderator approved in the meantime answers 409 for the page to explain.
 */
export function updateSubmission(
  id: string,
  body: UpdateSubmissionRequest,
): Promise<SubmissionView> {
  return apiClient.put<SubmissionView>(`/account/submissions/${encodeURIComponent(id)}`, body);
}

/** Sends a turned-down submission back into the queue. Only a rejection can be resent. */
export function resubmitSubmission(id: string): Promise<SubmissionView> {
  return apiClient.post<SubmissionView>(
    `/account/submissions/${encodeURIComponent(id)}/resubmit`,
  );
}

/** One page of the staff queue, open work first when no window is selected. */
export function listSubmissions(
  query: {
    page: number;
    pageSize: number;
    status?: SubmissionStatusFilter;
    kind?: SubmissionKindFilter;
  },
  signal?: AbortSignal,
): Promise<PagedResult<AdminSubmissionRow>> {
  return apiClient.get<PagedResult<AdminSubmissionRow>>('/admin/content-submissions', {
    query: {
      page: query.page,
      pageSize: query.pageSize,
      status: submissionStatusQuery(query.status ?? 'all'),
      kind: submissionKindQuery(query.kind ?? 'all'),
    },
    signal,
  });
}

/**
 * Approves or rejects one submission. Approval publishes the draft when it satisfies the
 * publish rules; a draft that has gained a blocker answers 409 with those blockers, and a
 * row another moderator decided answers 409 too — both land on the page's own row.
 */
export function reviewSubmission(
  id: string,
  decision: 'approve' | 'reject',
  reviewNote: string | null,
): Promise<AdminSubmissionRow> {
  return apiClient.post<AdminSubmissionRow>(
    `/admin/content-submissions/${encodeURIComponent(id)}/${decision}`,
    { reviewNote },
  );
}
