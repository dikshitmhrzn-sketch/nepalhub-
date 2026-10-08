import { apiClient, clearCsrfToken } from '@/lib/apiClient';

import type {
  CsrfToken,
  ForgotPasswordRequest,
  LoginRequest,
  RegisterRequest,
  ResendVerificationRequest,
  ResetPasswordRequest,
  Session,
  SessionUser,
  VerifyEmailRequest,
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

/*
 * Recovery and confirmation never sign anyone in and never mint a session, so none of
 * them touch the antiforgery token: the answers are the same whether or not the address
 * belongs to an account, and the link itself — not a cookie — is what the next step runs on.
 */

export function verifyEmail(request: VerifyEmailRequest, signal?: AbortSignal): Promise<{ verified: boolean }> {
  return apiClient.post<{ verified: boolean }>('/auth/verify-email', request, { signal });
}

export function resendVerification(
  request: ResendVerificationRequest,
  signal?: AbortSignal,
): Promise<{ sent: boolean }> {
  return apiClient.post<{ sent: boolean }>('/auth/resend-verification', request, { signal });
}

export function forgotPassword(
  request: ForgotPasswordRequest,
  signal?: AbortSignal,
): Promise<{ sent: boolean }> {
  return apiClient.post<{ sent: boolean }>('/auth/forgot-password', request, { signal });
}

export function resetPassword(
  request: ResetPasswordRequest,
  signal?: AbortSignal,
): Promise<{ reset: boolean }> {
  return apiClient.post<{ reset: boolean }>('/auth/reset-password', request, { signal });
}
