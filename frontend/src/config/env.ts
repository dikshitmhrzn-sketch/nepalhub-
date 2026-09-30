/*
 * Frontend configuration.
 *
 * Only variables prefixed with VITE_ are available here, and every one of them
 * is bundled into public JavaScript. Secrets must never be read from this file.
 */

const defaultApiBaseUrl = '/api';

const apiBaseUrl = import.meta.env.VITE_API_BASE_URL || defaultApiBaseUrl;

export const env = {
  /** Same-origin by default so the Vite dev proxy can forward API calls. */
  apiBaseUrl: apiBaseUrl.replace(/\/+$/, ''),
  /** Absolute API URL in production, proxied relative path in development. */
  usesProxy: apiBaseUrl.startsWith('/'),
  isDevelopment: import.meta.env.DEV,
  isProduction: import.meta.env.PROD,
} as const;
