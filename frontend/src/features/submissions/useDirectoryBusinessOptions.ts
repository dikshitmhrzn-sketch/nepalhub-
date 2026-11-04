import { useCallback, useEffect, useRef, useState } from 'react';

import { ApiError } from '@/lib/apiClient';

import { listBusinesses } from '@/features/businesses/api';

import type { BusinessOptionsState } from '@/features/deals/useStaffDeals';

/**
 * The businesses a submission's draft can be attached to, searched through the public
 * directory rather than the staff picker — a member has no staff endpoints, and the
 * directory already lists exactly what the staff picker offers: published listings,
 * searched on the server so a capped list cannot quietly hide a match.
 *
 * `enabled` is false while the editor is in staff mode, where the staff picker's own
 * endpoint is the right one: a disabled hook issues no request at all rather than one
 * that would answer 403.
 */
export function useDirectoryBusinessOptions(
  search: string,
  enabled: boolean,
): BusinessOptionsState {
  const [state, setState] = useState<BusinessOptionsState>({
    status: 'loading',
    data: [],
    error: null,
  });
  const activeRequest = useRef<AbortController | null>(null);

  const load = useCallback(() => {
    activeRequest.current?.abort();

    if (!enabled) {
      setState({ status: 'success', data: [], error: null });

      return;
    }

    const controller = new AbortController();
    activeRequest.current = controller;
    setState({ status: 'loading', data: [], error: null });

    void (async () => {
      try {
        const trimmed = search.trim();
        const page = await listBusinesses(
          { search: trimmed === '' ? undefined : trimmed, page: 1, pageSize: 100 },
          controller.signal,
        );

        if (!controller.signal.aborted) {
          const data = page.items.map((item) => ({ slug: item.slug, name: item.name }));
          setState({ status: 'success', data, error: null });
        }
      } catch (error) {
        if (controller.signal.aborted) {
          return;
        }

        setState({
          status: 'error',
          data: [],
          error:
            error instanceof ApiError
              ? error
              : new ApiError(0, 'unknown_error', 'Something went wrong while contacting the server.'),
        });
      }
    })();
  }, [enabled, search]);

  useEffect(() => {
    load();

    return () => {
      activeRequest.current?.abort();
    };
  }, [load]);

  return state;
}
