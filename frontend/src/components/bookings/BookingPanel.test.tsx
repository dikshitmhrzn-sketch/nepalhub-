import { fireEvent, render, screen, waitFor } from '@testing-library/react';
import { RouterProvider, createMemoryRouter } from 'react-router';
import { beforeEach, describe, expect, it, vi, type MockInstance } from 'vitest';

import { useAuth } from '@/features/auth/AuthContext';
import { bookEvent, cancelBooking } from '@/features/bookings/api';
import { startCheckout } from '@/features/payments/api';
import { ApiError } from '@/lib/apiClient';

import { BookingPanel } from './BookingPanel';

vi.mock('@/features/auth/AuthContext', () => ({ useAuth: vi.fn() }));
vi.mock('@/features/bookings/api', () => ({ bookEvent: vi.fn(), cancelBooking: vi.fn() }));
vi.mock('@/features/payments/api', () => ({ startCheckout: vi.fn() }));

function authAs(status: 'authenticated' | 'anonymous' | 'loading') {
  vi.mocked(useAuth).mockReturnValue({
    status,
    user:
      status === 'authenticated'
        ? {
            id: 'user-1',
            email: 'reader@example.com',
            fullName: 'Reader One',
            avatarUrl: null,
            isEmailConfirmed: true,
            level: 1,
            xp: 0,
            roles: ['user'],
          }
        : null,
    isAdmin: false,
    isStaff: false,
    signIn: vi.fn(),
    signUp: vi.fn(),
    signOut: vi.fn(),
    reload: vi.fn(),
  });
}

const booking = {
  id: 'booking-1',
  eventId: 'event-1',
  eventSlug: 'kirtan-night',
  title: 'Kirtan Night',
  startsAt: '2026-10-20T18:00:00',
  quantity: 2,
  status: 'Confirmed' as const,
  createdAt: '2026-10-05T12:00:00Z',
  cancelledAt: null,
};

function renderPanel(overrides: Partial<Parameters<typeof BookingPanel>[0]> = {}) {
  const onChanged = vi.fn();

  // The panel links to the sign-in page, so it always renders inside a router.
  const router = createMemoryRouter(
    [
      {
        path: '/events/:slug',
        element: (
          <BookingPanel
            slug="kirtan-night"
            capacity={10}
            remainingSpots={4}
            started={false}
            onChanged={onChanged}
            {...overrides}
          />
        ),
      },
    ],
    { initialEntries: ['/events/kirtan-night'] },
  );

  render(<RouterProvider router={router} />);

  return { onChanged };
}

describe('BookingPanel', () => {
  let submit: MockInstance;

  beforeEach(() => {
    authAs('authenticated');
    vi.mocked(bookEvent).mockReset();
    vi.mocked(cancelBooking).mockReset();
    vi.mocked(startCheckout).mockReset();

    // The checkout posts a form to the provider. jsdom has no navigation to offer, so the
    // submit itself is what the test watches.
    submit = vi.spyOn(HTMLFormElement.prototype, 'submit').mockImplementation(() => {});
  });

  it('is not on the page at all for an event with no cap', () => {
    renderPanel({ capacity: null, remainingSpots: null });

    expect(screen.queryByRole('button')).toBeNull();
  });

  it('says the bookings are closed once the event has begun', () => {
    renderPanel({ started: true });

    expect(screen.getByText('This event has started, so bookings are closed.')).toBeTruthy();
    expect(screen.queryByRole('button')).toBeNull();
  });

  it('says it is fully booked rather than offering a button that would fail', () => {
    renderPanel({ remainingSpots: 0 });

    expect(screen.getByText('This event is fully booked.')).toBeTruthy();
    expect(screen.queryByRole('button')).toBeNull();
  });

  it('tells a signed-out reader where to sign in', () => {
    authAs('anonymous');
    renderPanel();

    expect(screen.getByRole('link', { name: 'Sign in' })).toBeTruthy();
    expect(screen.queryByRole('button', { name: /Book/u })).toBeNull();
  });

  it('counts what is left and books the chosen number of places', async () => {
    vi.mocked(bookEvent).mockResolvedValue(booking);
    const { onChanged } = renderPanel();

    expect(screen.getByText('4 places left')).toBeTruthy();

    fireEvent.click(screen.getByRole('button', { name: 'One more place' }));
    fireEvent.click(screen.getByRole('button', { name: 'Book 2 places' }));

    await waitFor(() =>
      expect(screen.getByText('You hold 2 places at this event.')).toBeTruthy(),
    );
    expect(bookEvent).toHaveBeenCalledWith('kirtan-night', 2);
    expect(onChanged).toHaveBeenCalledTimes(1);
  });

  it('never offers more places than the event has left', () => {
    renderPanel({ remainingSpots: 1 });

    const fewer = screen.getByRole('button', { name: 'One fewer place' }) as HTMLButtonElement;
    const more = screen.getByRole('button', { name: 'One more place' }) as HTMLButtonElement;

    expect(fewer.disabled).toBe(true);
    expect(more.disabled).toBe(true);
  });

  it('explains a full event instead of failing silently', async () => {
    vi.mocked(bookEvent).mockRejectedValue(new ApiError(409, 'event_full', 'Fully booked.'));
    renderPanel();

    fireEvent.click(screen.getByRole('button', { name: 'Book my place' }));

    expect(await screen.findByText('This event is fully booked.')).toBeTruthy();
    expect(screen.getByRole('button', { name: 'Book my place' })).toBeTruthy();
  });

  it('releases the place and offers the way back in', async () => {
    vi.mocked(bookEvent).mockResolvedValue(booking);
    vi.mocked(cancelBooking).mockResolvedValue({
      ...booking,
      status: 'Cancelled',
      cancelledAt: '2026-10-06T09:00:00Z',
    });

    const { onChanged } = renderPanel();
    fireEvent.click(screen.getByRole('button', { name: 'Book my place' }));
    await waitFor(() => expect(screen.getByText(/^You hold/u)).toBeTruthy());

    fireEvent.click(screen.getByRole('button', { name: 'Release my place' }));

    await waitFor(() =>
      expect(screen.getByText(/Your place has been released/u)).toBeTruthy(),
    );
    expect(cancelBooking).toHaveBeenCalledWith('kirtan-night');
    expect(onChanged).toHaveBeenCalledTimes(2);
  });

  it('sells a priced place rather than booking it', async () => {
    vi.mocked(startCheckout).mockResolvedValue({
      reference: 'PAY-1',
      checkoutUrl: 'https://rc-epay.esewa.com.np/api/epay/main/v2/payment',
      fields: { total_amount: '1000' },
      status: 'Pending',
    });

    renderPanel({ ticketPrice: 500 });

    // The price is on the panel before anything is clicked, so the total is never a
    // surprise at the provider's door.
    expect(screen.getByText(/NPR 500 per place/u)).toBeTruthy();

    fireEvent.click(screen.getByRole('button', { name: 'One more place' }));
    fireEvent.click(screen.getByRole('button', { name: 'Pay NPR 1,000' }));

    await waitFor(() => expect(startCheckout).toHaveBeenCalledTimes(1));
    expect(vi.mocked(startCheckout).mock.calls[0][0]).toEqual({
      kind: 'EventTicket',
      slug: 'kirtan-night',
      quantity: 2,
    });

    // Nothing was booked: the place is held by the payment, not by a booking.
    expect(bookEvent).not.toHaveBeenCalled();
    await waitFor(() => expect(submit).toHaveBeenCalledTimes(1));
  });

  it('says why a priced checkout could not start', async () => {
    vi.mocked(startCheckout).mockRejectedValue(
      new ApiError(503, 'payments_not_configured', 'No provider.'),
    );

    renderPanel({ ticketPrice: 500 });

    fireEvent.click(screen.getByRole('button', { name: 'Pay NPR 500' }));

    expect(
      await screen.findByText(
        'Online payment is not switched on on this site yet, so nothing can be bought here.',
      ),
    ).toBeTruthy();
    expect(submit).not.toHaveBeenCalled();
    expect(screen.getByRole('button', { name: 'Pay NPR 500' })).toBeTruthy();
  });
});
