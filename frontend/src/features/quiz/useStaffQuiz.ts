import { useCallback, useEffect, useRef, useState } from 'react';

import {
  getStaffQuizQuestion,
  listStaffQuizQuestions,
} from './api';
import type { PagedResult, QuizQuestionAdmin, QuizQuestionAdminSummary } from './types';
import { toApiError } from './useDailyQuiz';

export type StaffQuizQuestionListState =
  | { status: 'loading'; data: null; error: null }
  | { status: 'success'; data: PagedResult<QuizQuestionAdminSummary>; error: null }
  | { status: 'error'; data: null; error: ReturnType<typeof toApiError> };

const initialState: StaffQuizQuestionListState = { status: 'loading', data: null, error: null };

/** The staff catalogue, which shows drafts as well as published rows. */
export function useStaffQuizQuestionList(query: {
  page?: number;
  pageSize?: number;
  search?: string;
  category?: string;
}) {
  const [state, setState] = useState<StaffQuizQuestionListState>(initialState);
  const activeRequest = useRef<AbortController | null>(null);
  const { page, pageSize, search, category } = query;

  const load = useCallback(() => {
    activeRequest.current?.abort();

    const controller = new AbortController();
    activeRequest.current = controller;
    setState(initialState);

    void (async () => {
      try {
        const data = await listStaffQuizQuestions({ page, pageSize, search, category }, controller.signal);

        if (!controller.signal.aborted) {
          setState({ status: 'success', data, error: null });
        }
      } catch (error) {
        if (controller.signal.aborted) {
          return;
        }

        setState({ status: 'error', data: null, error: toApiError(error) });
      }
    })();
  }, [category, page, pageSize, search]);

  useEffect(() => {
    load();

    return () => {
      activeRequest.current?.abort();
    };
  }, [load]);

  return { ...state, reload: load };
}

export type StaffQuizQuestionDetailState =
  | { status: 'loading'; data: null; error: null }
  | { status: 'success'; data: QuizQuestionAdmin; error: null }
  | { status: 'error'; data: null; error: ReturnType<typeof toApiError> };

const detailInitialState: StaffQuizQuestionDetailState = { status: 'loading', data: null, error: null };

/**
 * Loads the full row for the edit form. A null id means "creating", which is a valid
 * state here rather than a loading one.
 */
export function useStaffQuizQuestion(id: string | null) {
  const [state, setState] = useState<StaffQuizQuestionDetailState>(detailInitialState);
  const activeRequest = useRef<AbortController | null>(null);

  const load = useCallback(() => {
    activeRequest.current?.abort();

    if (!id) {
      setState(detailInitialState);

      return;
    }

    const controller = new AbortController();
    activeRequest.current = controller;
    setState(detailInitialState);

    void (async () => {
      try {
        const data = await getStaffQuizQuestion(id, controller.signal);

        if (!controller.signal.aborted) {
          setState({ status: 'success', data, error: null });
        }
      } catch (error) {
        if (controller.signal.aborted) {
          return;
        }

        setState({ status: 'error', data: null, error: toApiError(error) });
      }
    })();
  }, [id]);

  useEffect(() => {
    load();

    return () => {
      activeRequest.current?.abort();
    };
  }, [load]);

  return { ...state, reload: load };
}
