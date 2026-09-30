import { apiClient, clearCsrfToken } from '@/lib/apiClient';

import type {
  CsrfToken,
  LoginRequest,
  RegisterRequest,
  Session,
  SessionUser,
} from './types';

/**
 * The antiforgery token is bound to the signed-in identity, so it has to be
 * dropped whenever the session changes.
 */
function sessionChanged(): void {
  clearCsrfToken();
}

export function getSession(signal?: AbortSignal): Promise<Session> {
  return apiClient.get<Session>('/auth/session', { signal, timeoutMs: 10_000 });
}

export function getCurrentUser(signal?: AbortSignal): Promise<SessionUser> {
  return apiClient.get<SessionUser>('/account/me', { signal, timeoutMs: 10_000 });
}

export function getCsrfToken(signal?: AbortSignal): Promise<CsrfToken> {
  return apiClient.get<CsrfToken>('/auth/csrf', { signal, timeoutMs: 10_000 });
}

export async function login(request: LoginRequest, signal?: AbortSignal): Promise<SessionUser> {
  const user = await apiClient.post<SessionUser>('/auth/login', request, { signal });
  sessionChanged();

  return user;
}

export async function register(request: RegisterRequest, signal?: AbortSignal): Promise<SessionUser> {
  const user = await apiClient.post<SessionUser>('/auth/register', request, { signal });
  sessionChanged();

  return user;
}

export async function logout(signal?: AbortSignal): Promise<void> {
  await apiClient.post<{ signedOut: boolean }>('/auth/logout', undefined, { signal });
  sessionChanged();
}
