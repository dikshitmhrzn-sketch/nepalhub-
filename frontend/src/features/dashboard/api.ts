import { apiClient } from '@/lib/apiClient';

import type { DashboardSummary } from './types';

/**
 * The staff dashboard's one request. It is staff-only on the server, so an anonymous reader
 * never sends it: the route is behind `RequireStaff`, which is the same rule the API's
 * `staff-only` policy enforces.
 */
export function getStaffDashboard(signal?: AbortSignal): Promise<DashboardSummary> {
  return apiClient.get<DashboardSummary>('/admin/dashboard', { signal });
}
