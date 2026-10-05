import { apiClient } from '@/lib/apiClient';

import type { PagedResult, PlatformRole, UserAdminSummary, UserStatusFilter } from './types';

/** One page of accounts, newest first, narrowed by whatever the screen asked for. */
export function listUsers(
  query: {
    page: number;
    pageSize: number;
    search?: string;
    status: UserStatusFilter;
    role?: PlatformRole;
  },
  signal?: AbortSignal,
): Promise<PagedResult<UserAdminSummary>> {
  return apiClient.get<PagedResult<UserAdminSummary>>('/admin/users', {
    query: {
      page: query.page,
      pageSize: query.pageSize,
      search: query.search || undefined,
      status: query.status === 'all' ? undefined : query.status,
      role: query.role,
    },
    signal,
  });
}

/** Switches one account back on. Answered with the updated account. */
export function activateUser(id: string): Promise<UserAdminSummary> {
  return apiClient.post<UserAdminSummary>(`/admin/users/${encodeURIComponent(id)}/activate`);
}

/** Switches one account off: it can no longer sign in. */
export function deactivateUser(id: string): Promise<UserAdminSummary> {
  return apiClient.post<UserAdminSummary>(`/admin/users/${encodeURIComponent(id)}/deactivate`);
}

/**
 * Hands one role to an account. The account signs in again before it means anything,
 * because the roles travel inside the cookie — the screen says so after a grant.
 */
export function grantUserRole(id: string, role: PlatformRole): Promise<UserAdminSummary> {
  return apiClient.post<UserAdminSummary>(`/admin/users/${encodeURIComponent(id)}/roles/grant`, {
    role,
  });
}

/** Takes one role back off an account. */
export function revokeUserRole(id: string, role: PlatformRole): Promise<UserAdminSummary> {
  return apiClient.post<UserAdminSummary>(`/admin/users/${encodeURIComponent(id)}/roles/revoke`, {
    role,
  });
}
