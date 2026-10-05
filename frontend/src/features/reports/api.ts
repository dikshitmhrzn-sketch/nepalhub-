import { apiClient } from '@/lib/apiClient';

import type {
  CreateReportRequest,
  PagedResult,
  ReportQueueRow,
  ReportQueueFilter,
  ReportView,
} from './types';

/**
 * Files one report. A duplicate answers 409 with `already_reported`, which the button
 * shows as a sentence rather than as a failure: the complaint is already in the queue,
 * and the member's part is done.
 */
export function fileReport(body: CreateReportRequest): Promise<ReportView> {
  return apiClient.post<ReportView>('/reports', body);
}

/** One page of the staff queue, open work first when no window is chosen. */
export function listReports(
  query: { page: number; pageSize: number; status: ReportQueueFilter },
  signal?: AbortSignal,
): Promise<PagedResult<ReportQueueRow>> {
  return apiClient.get<PagedResult<ReportQueueRow>>('/admin/reports', {
    query: {
      page: query.page,
      pageSize: query.pageSize,
      status: query.status === 'all' ? undefined : query.status,
    },
    signal,
  });
}

/** Closes an open report either way; the endpoint picks resolve or dismiss from this. */
export function closeReport(
  id: string,
  body: { status: 'Resolved' | 'Dismissed'; resolutionNote?: string | null },
): Promise<ReportView> {
  const action = body.status === 'Resolved' ? 'resolve' : 'dismiss';

  return apiClient.post<ReportView>(`/admin/reports/${encodeURIComponent(id)}/${action}`, {
    resolutionNote: body.resolutionNote ?? null,
  });
}
