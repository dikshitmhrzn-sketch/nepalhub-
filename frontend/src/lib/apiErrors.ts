import { ApiError } from '@/lib/apiClient';

export interface FieldError {
  field: string;
  code: string;
  message: string;
}

/**
 * Turns the API's `error.details` array into a field -> message map so a form can
 * show the message the server produced next to the right input.
 */
export function getFieldErrors(error: unknown): Record<string, string> {
  if (!(error instanceof ApiError) || !Array.isArray(error.details)) {
    return {};
  }

  const messages: Record<string, string> = {};

  for (const detail of error.details as FieldError[]) {
    if (detail?.field && detail.message && !messages[detail.field]) {
      messages[detail.field] = detail.message;
    }
  }

  return messages;
}

/** A short message suitable for showing above a form. */
export function getErrorMessage(error: unknown, fallback: string): string {
  if (error instanceof ApiError) {
    if (error.isNetworkError || error.isTimeout) {
      return error.message;
    }

    if (error.code === 'rate_limited') {
      return 'Too many attempts. Please wait a minute and try again.';
    }

    return error.message || fallback;
  }

  return fallback;
}
