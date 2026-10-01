import { apiClient } from '@/lib/apiClient';

export interface Province {
  id: string;
  code: string;
  name: string;
}

export interface District {
  id: string;
  code: string;
  name: string;
}

export function listProvinces(signal?: AbortSignal): Promise<Province[]> {
  return apiClient.get<Province[]>('/reference/provinces', { signal });
}

export function listDistricts(provinceCode: string, signal?: AbortSignal): Promise<District[]> {
  return apiClient.get<District[]>(`/reference/provinces/${encodeURIComponent(provinceCode)}/districts`, {
    signal,
  });
}