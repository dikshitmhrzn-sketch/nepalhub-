import { apiClient } from '@/lib/apiClient';

export interface ApiHealth {
  status: string;
  database: string;
  timestamp: string;
}

export function getApiHealth(signal?: AbortSignal): Promise<ApiHealth> {
  return apiClient.get<ApiHealth>('/health', { signal, timeoutMs: 8000 });
}
