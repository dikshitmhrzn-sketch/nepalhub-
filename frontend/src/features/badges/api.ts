import { apiClient } from '@/lib/apiClient';

import type { AccountBadges } from './types';

/**
 * This person's own badges. The only verb on this resource is a GET: a badge is granted by
 * finishing a quiz, never by a request that asks for one, so there is nothing to submit and the
 * response needs no antiforgery token.
 */
export function getAccountBadges(signal?: AbortSignal): Promise<AccountBadges> {
  return apiClient.get<AccountBadges>('/account/badges', { signal });
}
