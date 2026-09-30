import { env } from '@/config/env';

const DEFAULT_TIMEOUT_MS = 15_000;

export interface ApiErrorPayload {
  code: string;
  message: string;
  details?: unknown;
}

/** Mirrors `ApiResponse<T>` returned by the ASP.NET Core API. */
interface ApiEnvelope<T> {
  success: boolean;
  data: T | null;
  error: ApiErrorPayload | null;
}

export class ApiError extends Error {
  readonly status: number;
  readonly code: string;
  readonly details: unknown;

  constructor(status: number, code: string, message: string, details?: unknown) {
    super(message);
    this.name = 'ApiError';
    this.status = status;
    this.code = code;
    this.details = details;
  }

  get isTimeout(): boolean {
    return this.code === 'request_timeout';
  }

  get isCancelled(): boolean {
    return this.code === 'request_cancelled';
  }

  get isNetworkError(): boolean {
    return this.code === 'network_error';
  }
}

export type QueryParams = Record<string, string | number | boolean | null | undefined>;

export interface RequestOptions {
  method?: 'GET' | 'POST' | 'PUT' | 'PATCH' | 'DELETE';
  body?: unknown;
  query?: QueryParams;
  signal?: AbortSignal;
  timeoutMs?: number;
}

function buildUrl(path: string, query?: QueryParams): string {
  const normalisedPath = path.startsWith('/') ? path : `/${path}`;
  const search = new URLSearchParams();

  for (const [key, value] of Object.entries(query ?? {})) {
    if (value !== undefined && value !== null) {
      search.append(key, String(value));
    }
  }

  const queryString = search.toString();
  return `${env.apiBaseUrl}${normalisedPath}${queryString ? `?${queryString}` : ''}`;
}

const SAFE_METHODS = new Set(['GET', 'HEAD', 'OPTIONS']);

const CSRF_PATH = '/auth/csrf';
const CSRF_PATH_FALLBACK = 'X-CSRF-TOKEN';

interface CsrfToken {
  headerName: string;
  token: string;
}

let cachedCsrfToken: CsrfToken | null = null;
let pendingCsrfToken: Promise<CsrfToken> | null = null;

/**
 * The API protects every unsafe request with antiforgery validation, and the
 * token is bound to the current identity. It is therefore fetched on demand and
 * thrown away whenever the session changes.
 */
export function clearCsrfToken(): void {
  cachedCsrfToken = null;
  pendingCsrfToken = null;
}

async function fetchCsrfToken(): Promise<CsrfToken> {
  const response = await fetch(buildUrl(CSRF_PATH), {
    method: 'GET',
    headers: { Accept: 'application/json' },
    credentials: 'include',
  });

  if (!response.ok) {
    throw new ApiError(response.status, 'csrf_unavailable', 'Could not start a secure session.');
  }

  const payload = (await parseBody(response)) as
    | { success?: boolean; data?: CsrfToken | null }
    | null;
  const token = payload?.data;

  if (!payload?.success || !token?.token) {
    throw new ApiError(response.status, 'csrf_unavailable', 'Could not start a secure session.');
  }

  return { headerName: token.headerName || CSRF_PATH_FALLBACK, token: token.token };
}

async function ensureCsrfToken(): Promise<CsrfToken> {
  if (cachedCsrfToken) {
    return cachedCsrfToken;
  }

  pendingCsrfToken ??= fetchCsrfToken().finally(() => {
    pendingCsrfToken = null;
  });
  cachedCsrfToken = await pendingCsrfToken;

  return cachedCsrfToken;
}

function linkAbortSignals(timeoutController: AbortController, signal?: AbortSignal): void {
  if (!signal) {
    return;
  }

  if (signal.aborted) {
    timeoutController.abort();
    return;
  }

  signal.addEventListener('abort', () => timeoutController.abort(), { once: true });
}

async function parseBody(response: Response): Promise<unknown> {
  const contentType = response.headers.get('content-type') ?? '';
  if (!contentType.includes('application/json')) {
    return null;
  }

  try {
    return await response.json();
  } catch {
    return null;
  }
}

function toApiError(status: number, payload: unknown, fallbackMessage: string): ApiError {
  if (payload && typeof payload === 'object') {
    const problem = payload as ApiErrorPayload & { title?: string; error?: ApiErrorPayload };

    if (problem.error && typeof problem.error === 'object') {
      return new ApiError(status, problem.error.code, problem.error.message, problem.error.details);
    }

    if (typeof problem.code === 'string' && typeof problem.message === 'string') {
      return new ApiError(status, problem.code, problem.message, problem.details);
    }

    if (typeof problem.title === 'string') {
      return new ApiError(status, 'server_error', problem.title);
    }
  }

  return new ApiError(status, 'unexpected_error', fallbackMessage);
}

async function send<T>(path: string, options: RequestOptions = {}): Promise<T> {
  const { method = 'GET', body, query, signal, timeoutMs = DEFAULT_TIMEOUT_MS } = options;

  const timeoutController = new AbortController();
  const timer = setTimeout(() => timeoutController.abort(), timeoutMs);
  linkAbortSignals(timeoutController, signal);

  const headers: Record<string, string> = { Accept: 'application/json' };
  if (body !== undefined) {
    headers['Content-Type'] = 'application/json';
  }

  if (!SAFE_METHODS.has(method)) {
    const csrf = await ensureCsrfToken();
    headers[csrf.headerName] = csrf.token;
  }

  let response: Response;
  try {
    response = await fetch(buildUrl(path, query), {
      method,
      headers,
      body: body === undefined ? undefined : JSON.stringify(body),
      signal: timeoutController.signal,
      credentials: 'include',
    });
  } catch (error) {
    if (timeoutController.signal.aborted) {
      const wasCancelled = signal?.aborted === true;
      throw new ApiError(
        0,
        wasCancelled ? 'request_cancelled' : 'request_timeout',
        wasCancelled ? 'The request was cancelled.' : 'The request timed out. Please try again.',
      );
    }

    throw new ApiError(0, 'network_error', 'Could not reach the server. Check your connection.', error);
  } finally {
    clearTimeout(timer);
  }

  const payload = await parseBody(response);

  if (!response.ok) {
    throw toApiError(response.status, payload, `Request failed with status ${response.status}.`);
  }

  if (payload && typeof payload === 'object' && 'success' in payload) {
    const envelope = payload as ApiEnvelope<T>;

    if (!envelope.success || envelope.data === null) {
      throw toApiError(response.status, envelope.error, 'The server returned an unsuccessful response.');
    }

    return envelope.data;
  }

  return payload as T;
}

async function request<T>(path: string, options: RequestOptions = {}): Promise<T> {
  try {
    return await send<T>(path, options);
  } catch (error) {
    // The token is tied to the current identity, so one that predates a sign-in
    // or sign-out is stale. Drop it and try the request once more.
    const method = options.method ?? 'GET';
    if (error instanceof ApiError && error.code === 'csrf_failed' && !SAFE_METHODS.has(method)) {
      clearCsrfToken();
      return send<T>(path, options);
    }

    throw error;
  }
}

export const apiClient = {
  get: <T>(path: string, options?: Omit<RequestOptions, 'method' | 'body'>) =>
    request<T>(path, { ...options, method: 'GET' }),
  post: <T>(path: string, body?: unknown, options?: Omit<RequestOptions, 'method' | 'body'>) =>
    request<T>(path, { ...options, method: 'POST', body }),
  put: <T>(path: string, body?: unknown, options?: Omit<RequestOptions, 'method' | 'body'>) =>
    request<T>(path, { ...options, method: 'PUT', body }),
  patch: <T>(path: string, body?: unknown, options?: Omit<RequestOptions, 'method' | 'body'>) =>
    request<T>(path, { ...options, method: 'PATCH', body }),
  delete: <T>(path: string, options?: Omit<RequestOptions, 'method' | 'body'>) =>
    request<T>(path, { ...options, method: 'DELETE' }),
};
