import { apiClient } from '@/lib/apiClient';

import type { SearchQuery, SearchResponse } from './types';

/**
 * One keyword search across every public module.
 *
 * `signal` is threaded through rather than left to the default timeout, because the header form
 * replaces a request on every keystroke and an abandoned one has to be cancelled rather than left
 * to arrive after the reader has moved on.
 */
export function search(query: SearchQuery, signal?: AbortSignal): Promise<SearchResponse> {
  return apiClient.get<SearchResponse>('/search', {
    query: {
      q: query.q,
      page: query.page,
      pageSize: query.pageSize,
      // Omitted rather than sent as `All`: the API treats a missing module and an explicit `All`
      // the same way, and leaving it out keeps the shared URL the one a reader would type.
      module: query.module === 'All' ? undefined : query.module,
    },
    signal,
  });
}