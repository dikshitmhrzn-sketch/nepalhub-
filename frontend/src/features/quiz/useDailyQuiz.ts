import { useCallback, useEffect, useRef, useState } from 'react';

import { ApiError } from '@/lib/apiClient';

import { getDailyQuiz, getDailyQuizResult, getQuizStats, submitQuiz } from './api';
import type {
  DailyQuizResponse,
  QuizResultResponse,
  QuizStatsResponse,
  SubmitQuizRequest,
} from './types';

export function toApiError(error: unknown): ApiError {
  if (error instanceof ApiError) {
    return error;
  }

  return new ApiError(0, 'unknown_error', 'Something went wrong while contacting the server.');
}

export type DailyQuizState =
  | { status: 'loading'; data: null; error: null }
  | { status: 'success'; data: DailyQuizResponse; error: null }
  | { status: 'error'; data: null; error: ApiError };

const initialState: DailyQuizState = { status: 'loading', data: null, error: null };

export function useDailyQuiz() {
  const [state, setState] = useState<DailyQuizState>(initialState);
  const activeRequest = useRef<AbortController | null>(null);

  const load = useCallback(() => {
    activeRequest.current?.abort();

    const controller = new AbortController();
    activeRequest.current = controller;
    setState(initialState);

    void (async () => {
      try {
        const data = await getDailyQuiz(controller.signal);

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
  }, []);

  useEffect(() => {
    load();

    return () => {
      activeRequest.current?.abort();
    };
  }, [load]);

  return { ...state, reload: load };
}

export type QuizResultState =
  | { status: 'idle'; data: null; error: null }
  | { status: 'loading'; data: null; error: null }
  | { status: 'success'; data: QuizResultResponse; error: null }
  | { status: 'error'; data: null; error: ApiError };

const resultInitialState: QuizResultState = { status: 'idle', data: null, error: null };

/**
 * The stored result for today. `enabled` is false while an anonymous visitor is still
 * answering, because this endpoint requires a session and would only produce a 401.
 */
export function useDailyQuizResult(enabled: boolean) {
  const [state, setState] = useState<QuizResultState>(resultInitialState);
  const activeRequest = useRef<AbortController | null>(null);

  const load = useCallback(() => {
    activeRequest.current?.abort();

    if (!enabled) {
      setState(resultInitialState);

      return;
    }

    const controller = new AbortController();
    activeRequest.current = controller;
    setState({ status: 'loading', data: null, error: null });

    void (async () => {
      try {
        const data = await getDailyQuizResult(controller.signal);

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
  }, [enabled]);

  useEffect(() => {
    load();

    return () => {
      activeRequest.current?.abort();
    };
  }, [load]);

  return { ...state, reload: load };
}

export type QuizStatsState =
  | { status: 'loading'; data: null; error: null }
  | { status: 'success'; data: QuizStatsResponse; error: null }
  | { status: 'error'; data: null; error: ApiError };

const statsInitialState: QuizStatsState = { status: 'loading', data: null, error: null };

export function useQuizStats(enabled: boolean) {
  const [state, setState] = useState<QuizStatsState>(statsInitialState);
  const activeRequest = useRef<AbortController | null>(null);

  const load = useCallback(() => {
    activeRequest.current?.abort();

    if (!enabled) {
      setState(statsInitialState);

      return;
    }

    const controller = new AbortController();
    activeRequest.current = controller;
    setState(statsInitialState);

    void (async () => {
      try {
        const data = await getQuizStats(controller.signal);

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
  }, [enabled]);

  useEffect(() => {
    load();

    return () => {
      activeRequest.current?.abort();
    };
  }, [load]);

  return { ...state, reload: load };
}

export type QuizSubmitState =
  | { status: 'idle'; data: null; error: null }
  | { status: 'submitting'; data: null; error: null }
  | { status: 'success'; data: QuizResultResponse; error: null }
  | { status: 'error'; data: null; error: ApiError };

const submitInitialState: QuizSubmitState = { status: 'idle', data: null, error: null };

/**
 * An action rather than a loader, so it starts idle and is never cancelled by a route
 * change mid-flight: a submit that has already reached the server has to be allowed to
 * finish so the result can be shown.
 */
export function useQuizSubmit() {
  const [state, setState] = useState<QuizSubmitState>(submitInitialState);

  const run = useCallback(async (body: SubmitQuizRequest) => {
    setState({ status: 'submitting', data: null, error: null });

    try {
      const data = await submitQuiz(body);
      setState({ status: 'success', data, error: null });

      return data;
    } catch (error) {
      const apiError = toApiError(error);
      setState({ status: 'error', data: null, error: apiError });

      return null;
    }
  }, []);

  const reset = useCallback(() => setState(submitInitialState), []);

  return { ...state, run, reset };
}
