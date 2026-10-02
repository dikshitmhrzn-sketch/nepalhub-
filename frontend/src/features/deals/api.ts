import { apiClient } from '@/lib/apiClient';

import type {
  DealAdmin,
  DealAdminListQuery,
  DealAdminSummary,
  DealBusinessOption,
  DealDetail,
  DealListQuery,
  DealSummary,
  PagedResult,
  SaveDealRequest,
} from './types';

export function listDeals(query: DealListQuery, signal?: AbortSignal): Promise<PagedResult<DealSummary>> {
  return apiClient.get<PagedResult<DealSummary>>('/deals', {
    query: {
      page: query.page,
      pageSize: query.pageSize,
      search: query.search,
      province: query.province,
      district: query.district,
      sort: query.sort,
    },
    signal,
  });
}

export function getDeal(slug: string, signal?: AbortSignal): Promise<DealDetail> {
  return apiClient.get<DealDetail>(`/deals/${encodeURIComponent(slug)}`, { signal });
}

/**
 * The published businesses an offer can be attached to, for the editor's picker. Separate from the
 * offers list because it is a different question with a different answer shape.
 */
export function listDealBusinessOptions(
  search: string | undefined,
  signal?: AbortSignal,
): Promise<DealBusinessOption[]> {
  return apiClient.get<DealBusinessOption[]>('/admin/deals/businesses', {
    query: { search },
    signal,
  });
}

export function listStaffDeals(
  query: DealAdminListQuery,
  signal?: AbortSignal,
): Promise<PagedResult<DealAdminSummary>> {
  return apiClient.get<PagedResult<DealAdminSummary>>('/admin/deals', {
    query: {
      page: query.page,
      pageSize: query.pageSize,
      search: query.search,
      province: query.province,
      district: query.district,
      status: query.status,
    },
    signal,
  });
}

export function getStaffDeal(id: string, signal?: AbortSignal): Promise<DealAdmin> {
  return apiClient.get<DealAdmin>(`/admin/deals/${encodeURIComponent(id)}`, { signal });
}

export function createDeal(body: SaveDealRequest): Promise<DealAdmin> {
  return apiClient.post<DealAdmin>('/admin/deals', body);
}

export function updateDeal(id: string, body: SaveDealRequest): Promise<DealAdmin> {
  return apiClient.put<DealAdmin>(`/admin/deals/${encodeURIComponent(id)}`, body);
}

/** The API answers 204 with an empty body, so there is nothing to return. */
export function deleteDeal(id: string): Promise<void> {
  return apiClient.delete<void>(`/admin/deals/${encodeURIComponent(id)}`);
}