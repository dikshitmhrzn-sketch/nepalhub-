import { apiClient } from '@/lib/apiClient';

import type {
  Calculation,
  CalculatorAdmin,
  CalculatorAdminSummary,
  CalculatorDetail,
  CalculatorListQuery,
  CalculatorSummary,
  PagedResult,
  SaveCalculatorRequest,
} from './types';

export function listCalculators(
  query: CalculatorListQuery,
  signal?: AbortSignal,
): Promise<PagedResult<CalculatorSummary>> {
  return apiClient.get<PagedResult<CalculatorSummary>>('/calculators', {
    query: { page: query.page, pageSize: query.pageSize, search: query.search, category: query.category },
    signal,
  });
}

export function getCalculator(slug: string, signal?: AbortSignal): Promise<CalculatorDetail> {
  return apiClient.get<CalculatorDetail>(`/calculators/${encodeURIComponent(slug)}`, { signal });
}

export function calculate(
  slug: string,
  inputs: Record<string, string | number>,
  signal?: AbortSignal,
): Promise<Calculation> {
  return apiClient.post<Calculation>(
    `/calculators/${encodeURIComponent(slug)}/calculate`,
    { inputs },
    { signal },
  );
}

export function listStaffCalculators(
  query: { page?: number; pageSize?: number; search?: string },
  signal?: AbortSignal,
): Promise<PagedResult<CalculatorAdminSummary>> {
  return apiClient.get<PagedResult<CalculatorAdminSummary>>('/admin/calculators', {
    query: { page: query.page, pageSize: query.pageSize, search: query.search },
    signal,
  });
}

export function getStaffCalculator(id: string, signal?: AbortSignal): Promise<CalculatorAdmin> {
  return apiClient.get<CalculatorAdmin>(`/admin/calculators/${encodeURIComponent(id)}`, { signal });
}

export function createCalculator(body: SaveCalculatorRequest): Promise<CalculatorDetail> {
  return apiClient.post<CalculatorDetail>('/admin/calculators', body);
}

export function updateCalculator(id: string, body: SaveCalculatorRequest): Promise<CalculatorDetail> {
  return apiClient.put<CalculatorDetail>(`/admin/calculators/${encodeURIComponent(id)}`, body);
}

/** The API answers 204 with an empty body, so there is nothing to return. */
export function deleteCalculator(id: string): Promise<void> {
  return apiClient.delete<void>(`/admin/calculators/${encodeURIComponent(id)}`);
}
