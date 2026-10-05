import { apiClient } from '@/lib/apiClient';

import type { AuditLogEntry, PagedResult } from './types';

/**
 * One page of the trail, newest first. Both filters are exact matches rather than
 * searches: this screen answers "what happened to this subject", not "find me something
 * like this", so a blank means no filter and anything else is matched whole.
 */
export function listAuditLog(
  query: { page: number; pageSize: number; action?: string; targetType?: string },
  signal?: AbortSignal,
): Promise<PagedResult<AuditLogEntry>> {
  return apiClient.get<PagedResult<AuditLogEntry>>('/admin/audit-log', {
    query: {
      page: query.page,
      pageSize: query.pageSize,
      action: query.action || undefined,
      targetType: query.targetType || undefined,
    },
    signal,
  });
}
