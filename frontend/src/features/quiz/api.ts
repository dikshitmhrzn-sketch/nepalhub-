import { apiClient } from '@/lib/apiClient';

import type {
  DailyQuizResponse,
  PagedResult,
  QuizQuestionAdmin,
  QuizQuestionAdminSummary,
  QuizResultResponse,
  QuizStatsResponse,
  SaveQuizQuestionRequest,
  SubmitQuizRequest,
} from './types';

/** Anonymous callers are allowed to play, so this deliberately sends no credentials logic of its own. */
export function getDailyQuiz(signal?: AbortSignal): Promise<DailyQuizResponse> {
  return apiClient.get<DailyQuizResponse>('/quiz/daily', { signal });
}

export function submitQuiz(body: SubmitQuizRequest): Promise<QuizResultResponse> {
  return apiClient.post<QuizResultResponse>('/quiz/daily/submit', body);
}

/** Signed-in only. Returns 404 when the player has not played today. */
export function getDailyQuizResult(signal?: AbortSignal): Promise<QuizResultResponse> {
  return apiClient.get<QuizResultResponse>('/quiz/daily/result', { signal });
}

export function getQuizStats(signal?: AbortSignal): Promise<QuizStatsResponse> {
  return apiClient.get<QuizStatsResponse>('/quiz/stats', { signal });
}

export function listStaffQuizQuestions(
  query: { page?: number; pageSize?: number; search?: string; category?: string },
  signal?: AbortSignal,
): Promise<PagedResult<QuizQuestionAdminSummary>> {
  return apiClient.get<PagedResult<QuizQuestionAdminSummary>>('/admin/quiz/questions', {
    query: { page: query.page, pageSize: query.pageSize, search: query.search, category: query.category },
    signal,
  });
}

export function getStaffQuizQuestion(id: string, signal?: AbortSignal): Promise<QuizQuestionAdmin> {
  return apiClient.get<QuizQuestionAdmin>(`/admin/quiz/questions/${encodeURIComponent(id)}`, { signal });
}

export function createStaffQuizQuestion(body: SaveQuizQuestionRequest): Promise<QuizQuestionAdmin> {
  return apiClient.post<QuizQuestionAdmin>('/admin/quiz/questions', body);
}

export function updateStaffQuizQuestion(
  id: string,
  body: SaveQuizQuestionRequest,
): Promise<QuizQuestionAdmin> {
  return apiClient.put<QuizQuestionAdmin>(`/admin/quiz/questions/${encodeURIComponent(id)}`, body);
}

/** The API answers 204 with an empty body, so there is nothing to return. */
export function deleteStaffQuizQuestion(id: string): Promise<void> {
  return apiClient.delete<void>(`/admin/quiz/questions/${encodeURIComponent(id)}`);
}
