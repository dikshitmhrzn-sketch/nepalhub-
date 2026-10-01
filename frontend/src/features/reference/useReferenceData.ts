import { useCallback, useEffect, useRef, useState } from 'react';

import { ApiError } from '@/lib/apiClient';

import { listDistricts, listProvinces } from './api';
import type { District, Province } from './api';

export interface ReferenceData {
  provinces: Province[];
  districtsByProvince: Record<string, District[]>;
}

export type ReferenceState =
  | { status: 'loading'; data: ReferenceData | null; error: null }
  | { status: 'success'; data: ReferenceData; error: null }
  | { status: 'error'; data: null; error: ApiError };

const initialState: ReferenceState = { status: 'loading', data: null, error: null };

function toApiError(error: unknown): ApiError {
  if (error instanceof ApiError) {
    return error;
  }

  return new ApiError(0, 'unknown_error', 'Something went wrong while contacting the server.');
}

/**
 * Loads the provinces, and then the districts of each one.
 *
 * The districts are fetched for every province up front rather than on demand because a filter
 * panel offers both selects at once, and a province that is chosen after the page has loaded has
 * to have a district list waiting. There are only seven provinces and a few dozen districts, so
 * this is a few small requests rather than anything worth paginating.
 *
 * One failure fails the whole load: a filter panel showing a province with no districts under it
 * would silently drop postings, which is worse than an error the reader can act on.
 */
export function useReferenceData() {
  const [state, setState] = useState<ReferenceState>(initialState);
  const activeRequest = useRef<AbortController | null>(null);

  const load = useCallback(() => {
    activeRequest.current?.abort();

    const controller = new AbortController();
    activeRequest.current = controller;
    setState(initialState);

    void (async () => {
      try {
        const provinces = await listProvinces(controller.signal);
        const entries = await Promise.all(
          provinces.map(async (province) => {
            const districts = await listDistricts(province.code, controller.signal);

            return [province.code, districts] as const;
          }),
        );

        if (!controller.signal.aborted) {
          setState({
            status: 'success',
            data: { provinces, districtsByProvince: Object.fromEntries(entries) },
            error: null,
          });
        }
      } catch (error) {
        if (controller.signal.aborted) {
          return;
        }

        setState({ status: 'error', data: null, error: toApiError(error) });
      }
    })();
  }, []);

  useEffect(() => {
    load();

    return () => {
      activeRequest.current?.abort();
    };
  }, [load]);

  return { ...state, reload: load };
}