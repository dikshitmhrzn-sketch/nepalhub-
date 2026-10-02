import { apiClient } from '@/lib/apiClient';

import type {
  EventAdmin,
  EventAdminListQuery,
  EventAdminSummary,
  EventBusinessOption,
  EventDetail,
  EventListQuery,
  EventSummary,
  PagedResult,
  SaveEventRequest,
} from './types';

export function listEvents(
  query: EventListQuery,
  signal?: AbortSignal,
): Promise<PagedResult<EventSummary>> {
  return apiClient.get<PagedResult<EventSummary>>('/events', {
    query: {
      page: query.page,
      pageSize: query.pageSize,
      search: query.search,
      province: query.province,
      district: query.district,
      category: query.category,
      sort: query.sort,
    },
    signal,
  });
}

export function getEvent(slug: string, signal?: AbortSignal): Promise<EventDetail> {
  return apiClient.get<EventDetail>(`/events/${encodeURIComponent(slug)}`, { signal });
}

/**
 * The published businesses an event can be attached to, for the editor's picker. Separate from the
 * events list because it is a different question with a different answer shape.
 */
export function listEventBusinessOptions(
  search: string | undefined,
  signal?: AbortSignal,
): Promise<EventBusinessOption[]> {
  return apiClient.get<EventBusinessOption[]>('/admin/events/businesses', {
    query: { search },
    signal,
  });
}

export function listStaffEvents(
  query: EventAdminListQuery,
  signal?: AbortSignal,
): Promise<PagedResult<EventAdminSummary>> {
  return apiClient.get<PagedResult<EventAdminSummary>>('/admin/events', {
    query: {
      page: query.page,
      pageSize: query.pageSize,
      search: query.search,
      province: query.province,
      district: query.district,
      category: query.category,
      status: query.status,
    },
    signal,
  });
}

export function getStaffEvent(id: string, signal?: AbortSignal): Promise<EventAdmin> {
  return apiClient.get<EventAdmin>(`/admin/events/${encodeURIComponent(id)}`, { signal });
}

export function createEvent(body: SaveEventRequest): Promise<EventAdmin> {
  return apiClient.post<EventAdmin>('/admin/events', body);
}

export function updateEvent(id: string, body: SaveEventRequest): Promise<EventAdmin> {
  return apiClient.put<EventAdmin>(`/admin/events/${encodeURIComponent(id)}`, body);
}

/** The API answers 204 with an empty body, so there is nothing to return. */
export function deleteEvent(id: string): Promise<void> {
  return apiClient.delete<void>(`/admin/events/${encodeURIComponent(id)}`);
}