/** Mirrors the booking contracts exposed by the ASP.NET Core API. */

/** What a booking says on the attendee's screen. Cancelling keeps the row with a stamp. */
export type BookingStatus = 'Confirmed' | 'Cancelled';

/** One booking: one person holding places at one event. */
export interface Booking {
  id: string;
  eventId: string;
  eventSlug: string;
  title: string;
  startsAt: string;
  quantity: number;
  status: BookingStatus;
  createdAt: string;
  cancelledAt: string | null;
}

export const bookingStatusLabels: Record<BookingStatus, string> = {
  Confirmed: 'Place confirmed',
  Cancelled: 'Cancelled',
};
