import { apiClient } from '@/lib/apiClient';

import type {
  ResourceAdmin,
  ResourceAdminListQuery,
  ResourceAdminSummary,
  ResourceDetail,
  ResourceListQuery,
  ResourceSummary,
  PagedResult,
  SaveResourceRequest,
} from './types';

export function listResources(
  query: ResourceListQuery,
  signal?: AbortSignal,
): Promise<PagedResult<ResourceSummary>> {
  return apiClient.get<PagedResult<ResourceSummary>>('/students/resources', {
    query: {
      page: query.page,
      pageSize: query.pageSize,
      search: query.search,
      type: query.type,
      category: query.category,
      level: query.level,
      sort: query.sort,
    },
    signal,
  });
}

export function getResource(slug: string, signal?: AbortSignal): Promise<ResourceDetail> {
  return apiClient.get<ResourceDetail>(`/students/resources/${encodeURIComponent(slug)}`, { signal });
}

export function listStaffResources(
  query: ResourceAdminListQuery,
  signal?: AbortSignal,
): Promise<PagedResult<ResourceAdminSummary>> {
  return apiClient.get<PagedResult<ResourceAdminSummary>>('/admin/students/resources', {
    query: {
      page: query.page,
      pageSize: query.pageSize,
      search: query.search,
      type: query.type,
      category: query.category,
      level: query.level,
      status: query.status,
    },
    signal,
  });
}

export function getStaffResource(id: string, signal?: AbortSignal): Promise<ResourceAdmin> {
  return apiClient.get<ResourceAdmin>(`/admin/students/resources/${encodeURIComponent(id)}`, { signal });
}

export function createResource(body: SaveResourceRequest): Promise<ResourceAdmin> {
  return apiClient.post<ResourceAdmin>('/admin/students/resources', body);
}

export function updateResource(id: string, body: SaveResourceRequest): Promise<ResourceAdmin> {
  return apiClient.put<ResourceAdmin>(`/admin/students/resources/${encodeURIComponent(id)}`, body);
}

/** The API answers 204 with an empty body, so there is nothing to return. */
export function deleteResource(id: string): Promise<void> {
  return apiClient.delete<void>(`/admin/students/resources/${encodeURIComponent(id)}`);
}
