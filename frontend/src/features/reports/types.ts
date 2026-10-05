/**
 * Mirrors the moderation contracts exposed by the ASP.NET Core API: what a member files
 * against a listing, and what the staff queue reads back.
 */

/** The kinds of listing a member can complain about. The API serialises these by name. */
export type ReportTargetType = 'Job' | 'Business' | 'Deal' | 'Event' | 'StudentResource';

export type ReportReason = 'Spam' | 'Misleading' | 'Inappropriate' | 'Broken' | 'Other';

export type ReportStatus = 'Open' | 'Resolved' | 'Dismissed';

export const reportReasons: readonly ReportReason[] = [
  'Spam',
  'Misleading',
  'Inappropriate',
  'Broken',
  'Other',
];

/** The reason as a reader would say it, keeping the wire value out of the form. */
export const reportReasonLabels: Record<ReportReason, string> = {
  Spam: 'Spam or advertising',
  Misleading: 'Misleading or a scam',
  Inappropriate: 'Inappropriate content',
  Broken: 'Broken or out of date',
  Other: 'Something else',
};

export const reportTargetLabels: Record<ReportTargetType, string> = {
  Job: 'Job',
  Business: 'Business',
  Deal: 'Offer',
  Event: 'Event',
  StudentResource: 'Resource',
};

export interface CreateReportRequest {
  targetType: ReportTargetType;
  targetId: string;
  reason: ReportReason;
  note?: string | null;
}

/** What filing answers with — enough for the confirmation, never the whole record. */
export interface ReportView {
  id: string;
  targetType: ReportTargetType;
  targetId: string;
  reason: ReportReason;
  status: ReportStatus;
  createdAt: string;
}

/** One row of the staff queue, with the target's title and slug joined on for display. */
export interface ReportQueueRow {
  id: string;
  targetType: ReportTargetType;
  targetId: string;
  targetTitle: string | null;
  targetSlug: string | null;
  reporterName: string;
  reason: ReportReason;
  note: string | null;
  status: ReportStatus;
  createdAt: string;
  resolvedByName: string | null;
  resolvedAt: string | null;
  resolutionNote: string | null;
}

/** The queue's status window. `all` is the API's own default: open work first, newest first. */
export type ReportQueueFilter = 'all' | 'open' | 'resolved' | 'dismissed';

export const reportQueueFilters: readonly ReportQueueFilter[] = [
  'all',
  'open',
  'resolved',
  'dismissed',
];

export const reportQueueFilterLabels: Record<ReportQueueFilter, string> = {
  all: 'All',
  open: 'Open',
  resolved: 'Resolved',
  dismissed: 'Dismissed',
};

/** Where a queue row's target lives on the public site, by module type. */
export function targetHref(row: Pick<ReportQueueRow, 'targetType' | 'targetSlug'>): string | null {
  if (!row.targetSlug) {
    return null;
  }

  const slug = encodeURIComponent(row.targetSlug);

  switch (row.targetType) {
    case 'Job':
      return `/jobs/${slug}`;
    case 'Business':
      return `/businesses/${slug}`;
    case 'Deal':
      return `/deals/${slug}`;
    case 'Event':
      return `/events/${slug}`;
    default:
      return `/students/resources/${slug}`;
  }
}

/** The one shape every paged endpoint in the API returns. */
export interface PagedResult<T> {
  items: T[];
  page: number;
  pageSize: number;
  totalCount: number;
  totalPages: number;
  hasPrevious: boolean;
  hasNext: boolean;
}
