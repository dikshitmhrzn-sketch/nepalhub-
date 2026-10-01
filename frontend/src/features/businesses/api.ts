import { apiClient } from '@/lib/apiClient';

import type {
  BusinessAdmin,
  BusinessAdminListQuery,
  BusinessAdminSummary,
  BusinessDetail,
  BusinessListQuery,
  BusinessSummary,
  PagedResult,
  SaveBusinessRequest,
} from './types';

export function listBusinesses(
  query: BusinessListQuery,
  signal?: AbortSignal,
): Promise<PagedResult<BusinessSummary>> {
  return apiClient.get<PagedResult<BusinessSummary>>('/businesses', {
    query: {
      page: query.page,
      pageSize: query.pageSize,
      search: query.search,
      province: query.province,
      district: query.district,
      category: query.category,
      verified: query.verified,
      sort: query.sort,
    },
    signal,
  });
}

export function getBusiness(slug: string, signal?: AbortSignal): Promise<BusinessDetail> {
  return apiClient.get<BusinessDetail>(`/businesses/${encodeURIComponent(slug)}`, { signal });
}

export function listStaffBusinesses(
  query: BusinessAdminListQuery,
  signal?: AbortSignal,
): Promise<PagedResult<BusinessAdminSummary>> {
  return apiClient.get<PagedResult<BusinessAdminSummary>>('/admin/businesses', {
    query: {
      page: query.page,
      pageSize: query.pageSize,
      search: query.search,
      province: query.province,
      district: query.district,
      category: query.category,
      verified: query.verified,
      status: query.status,
    },
    signal,
  });
}

export function getStaffBusiness(id: string, signal?: AbortSignal): Promise<BusinessAdmin> {
  return apiClient.get<BusinessAdmin>(`/admin/businesses/${encodeURIComponent(id)}`, { signal });
}

export function createBusiness(body: SaveBusinessRequest): Promise<BusinessAdmin> {
  return apiClient.post<BusinessAdmin>('/admin/businesses', body);
}

export function updateBusiness(id: string, body: SaveBusinessRequest): Promise<BusinessAdmin> {
  return apiClient.put<BusinessAdmin>(`/admin/businesses/${encodeURIComponent(id)}`, body);
}

/** The API answers 204 with an empty body, so there is nothing to return. */
export function deleteBusiness(id: string): Promise<void> {
  return apiClient.delete<void>(`/admin/businesses/${encodeURIComponent(id)}`);
}