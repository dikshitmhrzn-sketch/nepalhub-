import { apiClient } from '@/lib/apiClient';

import type {
  JobAdmin,
  JobAdminListQuery,
  JobAdminSummary,
  JobDetail,
  JobListQuery,
  JobSummary,
  PagedResult,
  SaveJobRequest,
} from './types';

export function listJobs(query: JobListQuery, signal?: AbortSignal): Promise<PagedResult<JobSummary>> {
  return apiClient.get<PagedResult<JobSummary>>('/jobs', {
    query: {
      page: query.page,
      pageSize: query.pageSize,
      search: query.search,
      province: query.province,
      district: query.district,
      category: query.category,
      employmentType: query.employmentType,
      workMode: query.workMode,
      sort: query.sort,
    },
    signal,
  });
}

export function getJob(slug: string, signal?: AbortSignal): Promise<JobDetail> {
  return apiClient.get<JobDetail>(`/jobs/${encodeURIComponent(slug)}`, { signal });
}

export function listStaffJobs(
  query: JobAdminListQuery,
  signal?: AbortSignal,
): Promise<PagedResult<JobAdminSummary>> {
  return apiClient.get<PagedResult<JobAdminSummary>>('/admin/jobs', {
    query: {
      page: query.page,
      pageSize: query.pageSize,
      search: query.search,
      province: query.province,
      district: query.district,
      category: query.category,
      employmentType: query.employmentType,
      workMode: query.workMode,
      status: query.status,
    },
    signal,
  });
}

export function getStaffJob(id: string, signal?: AbortSignal): Promise<JobAdmin> {
  return apiClient.get<JobAdmin>(`/admin/jobs/${encodeURIComponent(id)}`, { signal });
}

export function createJob(body: SaveJobRequest): Promise<JobAdmin> {
  return apiClient.post<JobAdmin>('/admin/jobs', body);
}

export function updateJob(id: string, body: SaveJobRequest): Promise<JobAdmin> {
  return apiClient.put<JobAdmin>(`/admin/jobs/${encodeURIComponent(id)}`, body);
}

/** The API answers 204 with an empty body, so there is nothing to return. */
export function deleteJob(id: string): Promise<void> {
  return apiClient.delete<void>(`/admin/jobs/${encodeURIComponent(id)}`);
}