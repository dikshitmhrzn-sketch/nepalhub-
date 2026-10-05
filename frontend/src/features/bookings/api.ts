import { apiClient } from '@/lib/apiClient';

import type { Booking } from './types';

/**
 * Books places at one event. One to four at a time. Full, already-booked and
 * already-started all answer 409 with a code the page turns into a sentence.
 */
export function bookEvent(slug: string, quantity: number): Promise<Booking> {
  return apiClient.post<Booking>(`/events/${encodeURIComponent(slug)}/booking`, { quantity });
}

/** Gives the place back. The row keeps its cancellation stamp rather than being deleted. */
export function cancelBooking(slug: string): Promise<Booking> {
  return apiClient.delete<Booking>(`/events/${encodeURIComponent(slug)}/booking`);
}
