/**
 * Mirrors the content-submission contracts exposed by the ASP.NET Core API: the drafts a
 * member has sent in and where each one stands, and the staff queue that decides.
 *
 * The queue reads like the claim and verification queues do — a status window over work
 * that is open first — with a kind window beside it, because three sorts of content share
 * one table and a moderator usually arrives looking for one of them.
 */

import type { SaveDealRequest } from '@/features/deals/types';
import type { SaveEventRequest } from '@/features/events/types';
import type { SaveJobRequest } from '@/features/jobs/types';

/** Which content block a submission points at. The API serialises these by name. */
export type SubmissionKind = 'Job' | 'Event' | 'Deal';

/** Where the submission sits in the queue staff work through. */
export type SubmissionStatus = 'Pending' | 'Approved' | 'Rejected';

/** One submission as the author sees it, from the submit screen's history list. */
export interface SubmissionView {
  id: string;
  kind: SubmissionKind;
  itemId: string;
  slug: string;
  title: string;
  isPublished: boolean;
  status: SubmissionStatus;
  note: string | null;
  createdAt: string;
  reviewedAt: string | null;
  reviewNote: string | null;
}

/** One row of the staff queue: the draft, who wrote it, and where it stands. */
export interface AdminSubmissionRow extends SubmissionView {
  submitterName: string;
  submitterEmail: string | null;
}

/**
 * One submission with the draft body beside it, for the editor that corrects it. Exactly
 * one of the three payloads is set, matching `submission.kind` — the others are null so a
 * wrong-kind form can never be built from this response by accident.
 */
export interface SubmissionDetail {
  submission: SubmissionView;
  job: SaveJobRequest | null;
  event: SaveEventRequest | null;
  deal: SaveDealRequest | null;
}

/** Opening a job submission: the draft body and the author's own words, or a blank note. */
export interface SubmitJobRequest {
  content: SaveJobRequest;
  note: string | null;
}

export interface SubmitEventRequest {
  content: SaveEventRequest;
  note: string | null;
}

export interface SubmitDealRequest {
  content: SaveDealRequest;
  note: string | null;
}

/**
 * The author's edit to one draft. A discriminated union rather than three optional
 * bodies: the kind names which block carries the content, exactly as the API reads it,
 * so a mismatched pair cannot be assembled here.
 */
export type UpdateSubmissionRequest =
  | { kind: 'Job'; job: SaveJobRequest; note: string | null }
  | { kind: 'Event'; event: SaveEventRequest; note: string | null }
  | { kind: 'Deal'; deal: SaveDealRequest; note: string | null };

/** The queue's status window, as the API's own parser names them. */
export type SubmissionStatusFilter = 'all' | 'Pending' | 'Approved' | 'Rejected';

export const submissionStatusFilters: readonly SubmissionStatusFilter[] = [
  'all',
  'Pending',
  'Approved',
  'Rejected',
];

export const submissionStatusFilterLabels: Record<SubmissionStatusFilter, string> = {
  all: 'All',
  Pending: 'Open',
  Approved: 'Approved',
  Rejected: 'Rejected',
};

export const submissionStatusLabels: Record<SubmissionStatus, string> = {
  Pending: 'Waiting for staff',
  Approved: 'Approved',
  Rejected: 'Turned down',
};

/** The kind window beside the status one, blank meaning every kind. */
export type SubmissionKindFilter = 'all' | SubmissionKind;

export const submissionKindFilters: readonly SubmissionKindFilter[] = [
  'all',
  'Job',
  'Event',
  'Deal',
];

export const submissionKindFilterLabels: Record<SubmissionKindFilter, string> = {
  all: 'Every kind',
  Job: 'Jobs',
  Event: 'Events',
  Deal: 'Offers',
};

/** What a row of one kind is called on screen — "offer" rather than the table's "deal". */
export const submissionKindLabels: Record<SubmissionKind, string> = {
  Job: 'Job',
  Event: 'Event',
  Deal: 'Offer',
};

/** A blank status means no filter; anything else is an exact match, as the API reads it. */
export function submissionStatusQuery(
  filter: SubmissionStatusFilter,
): string | undefined {
  return filter === 'all' ? undefined : filter;
}

/** A blank kind means no filter; anything else is an exact match, as the API reads it. */
export function submissionKindQuery(filter: SubmissionKindFilter): string | undefined {
  return filter === 'all' ? undefined : filter;
}
