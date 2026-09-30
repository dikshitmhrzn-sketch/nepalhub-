/**
 * Mirrors `QuizContracts.cs`. The API serialises enums as strings, so `category`
 * arrives as one of the names below rather than a number.
 */
export const quizCategories = [
  'Nepal',
  'GeneralKnowledge',
  'Science',
  'Technology',
  'History',
  'Geography',
] as const;

export type QuizCategory = (typeof quizCategories)[number];

export const quizCategoryLabels: Record<QuizCategory, string> = {
  Nepal: 'Nepal',
  GeneralKnowledge: 'General knowledge',
  Science: 'Science',
  Technology: 'Technology',
  History: 'History',
  Geography: 'Geography',
};

export interface PagedResult<T> {
  items: T[];
  page: number;
  pageSize: number;
  totalCount: number;
  totalPages: number;
  hasPrevious: boolean;
  hasNext: boolean;
}

/**
 * What the public is allowed to see before submitting. Note the absence of `isCorrect`
 * and `explanation`: if either ever appeared here, the quiz would be playable by
 * reading the network tab.
 */
export interface QuizOptionResponse {
  id: string;
  position: number;
  text: string;
}

export interface QuizQuestionResponse {
  id: string;
  position: number;
  text: string;
  category: QuizCategory;
  options: QuizOptionResponse[];
}

export interface DailyQuizResponse {
  id: string;
  quizDate: string;
  questionCount: number;
  maxXpAvailable: number;
  alreadyCompleted: boolean;
  completedAt: string | null;
  questions: QuizQuestionResponse[];
}

export interface SubmittedAnswer {
  questionId: string;
  optionId: string;
}

export interface SubmitQuizRequest {
  answers: SubmittedAnswer[];
}

export interface QuizAnswerReview {
  questionId: string;
  text: string;
  category: QuizCategory;
  selectedOptionId: string;
  selectedOptionText: string;
  correctOptionId: string;
  correctOptionText: string;
  isCorrect: boolean;
  explanation: string;
}

/**
 * `recorded` is false for an anonymous visitor, and the counters beside it are then
 * null rather than a zero that would read as "you have no XP" instead of "nothing is
 * being tracked for you".
 */
export interface QuizProgressionResponse {
  recorded: boolean;
  xpAwarded: number;
  totalXp: number | null;
  level: number | null;
  streakCount: number | null;
  lastActiveOn: string | null;
}

export interface QuizResultResponse {
  attemptId: string;
  quizDate: string;
  score: number;
  maxScore: number;
  correctCount: number;
  completedAt: string;
  progression: QuizProgressionResponse;
  reviews: QuizAnswerReview[];
}

export interface QuizStatsResponse {
  totalXp: number;
  level: number;
  streakCount: number;
  xpToNextLevel: number;
  quizzesCompleted: number;
  lastActiveOn: string | null;
}

export interface SaveQuizOption {
  text: string;
  isCorrect: boolean;
}

export interface QuizQuestionAdminSummary {
  id: string;
  text: string;
  category: QuizCategory;
  isPublished: boolean;
  optionCount: number;
  correctOptionCount: number;
  hasBeenPlayed: boolean;
  updatedAt: string;
  createdByEmail: string | null;
}

export interface QuizQuestionAdmin {
  id: string;
  text: string;
  explanation: string;
  category: QuizCategory;
  isPublished: boolean;
  createdAt: string;
  updatedAt: string;
  createdByEmail: string | null;
  options: SaveQuizOption[];
}

export interface SaveQuizQuestionRequest {
  text: string;
  explanation: string;
  category: QuizCategory;
  isPublished: boolean;
  options: SaveQuizOption[];
}
