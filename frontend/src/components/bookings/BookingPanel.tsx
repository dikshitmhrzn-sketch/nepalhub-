import { useState } from 'react';
import { Link } from 'react-router';

import { Button } from '@/components/ui/Button';
import { useAuth } from '@/features/auth/AuthContext';
import { bookEvent, cancelBooking } from '@/features/bookings/api';
import type { Booking } from '@/features/bookings/types';
import { startCheckout } from '@/features/payments/api';
import { checkoutErrorMessage, openProviderCheckout } from '@/features/payments/checkout';
import { ApiError } from '@/lib/apiClient';
import { getErrorMessage } from '@/lib/apiErrors';

import styles from './BookingPanel.module.css';

interface BookingPanelProps {
  slug: string;
  /** Null means the event has no cap, in which case this panel has nothing to count. */
  capacity: number | null;
  remainingSpots: number | null;
  /** Whether the event has already started. Booking latches only before the start. */
  started: boolean;
  /**
   * What one place costs, in whole rupees. Zero is the free event this panel has always
   * taken a place at; anything above it sells the place instead of booking it.
   */
  ticketPrice?: number;
  /** Called after a booking or cancellation lands, so the page can reload its counts. */
  onChanged: () => void;
}

type Phase = 'idle' | 'sending';

/**
 * Taking a place at an event: how many are left, a stepper within the one-to-four bound the
 * API enforces, and the way back out again.
 *
 * The reader's own booking is held in this component rather than read back from the server,
 * because the API deliberately exposes no "do I hold a place" read — booking twice answers
 * 409 with its own sentence, which is what the page says instead. A signed-out visitor gets
 * the sign-in link rather than a form that would fail at the door.
 */
export function BookingPanel({
  slug,
  capacity,
  remainingSpots,
  started,
  ticketPrice = 0,
  onChanged,
}: BookingPanelProps) {
  const { status } = useAuth();
  const [phase, setPhase] = useState<Phase>('idle');
  const [quantity, setQuantity] = useState(1);
  const [booking, setBooking] = useState<Booking | null>(null);
  const [released, setReleased] = useState(false);
  const [error, setError] = useState<string | null>(null);

  // An uncapped event has nothing to sell from this panel: there is no place count to show
  // and the organiser's own registration link already covers how to attend.
  if (capacity === null) {
    return null;
  }

  if (started) {
    return (
      <p className={styles.note}>This event has started, so bookings are closed.</p>
    );
  }

  if (booking) {
    return (
      <div className={styles.panel}>
        <p className={styles.heading} role="status">
          You hold {booking.quantity === 1 ? 'a place' : `${booking.quantity} places`} at this event.
        </p>
        <p className={styles.meta}>
          Booked {new Date(booking.createdAt).toLocaleDateString()} · Cancel before the start to
          release your place.
        </p>
        <Button
          type="button"
          variant="secondary"
          size="small"
          disabled={phase === 'sending'}
          onClick={() => void handleCancel()}
        >
          {phase === 'sending' ? 'Releasing…' : 'Release my place'}
        </Button>
        {error ? (
          <p className={styles.error} role="alert">
            {error}
          </p>
        ) : null}
      </div>
    );
  }

  if (status === 'anonymous') {
    return (
      <p className={styles.note}>
        <Link to="/login" className={styles.link}>
          Sign in
        </Link>{' '}
        to book a place at this event.
      </p>
    );
  }

  if (status === 'loading') {
    return null;
  }

  if (released) {
    return (
      <p className={styles.note} role="status">
        Your place has been released. You can book again if you change your mind.
      </p>
    );
  }

  const soldOut = remainingSpots !== null && remainingSpots <= 0;

  if (soldOut) {
    return <p className={styles.note}>This event is fully booked.</p>;
  }

  const paid = ticketPrice > 0;
  const total = ticketPrice * quantity;

  /**
   * Priced events do not book, they sell: the place is held by the checkout, the buyer
   * leaves for the provider's own pages, and the seat survives only as long as the money
   * takes to arrive. The form that carries them is built and posted here rather than
   * navigated to, because that is the shape the provider signed.
   */
  async function handlePay() {
    setPhase('sending');
    setError(null);

    try {
      const handshake = await startCheckout({
        kind: 'EventTicket',
        slug,
        quantity,
      });

      openProviderCheckout(handshake);
      return;
    } catch (payError) {
      setError(
        checkoutErrorMessage(payError, 'Could not start the payment. Please try again.'),
      );
      setPhase('idle');
    }
  }

  async function handleBook() {
    setPhase('sending');
    setError(null);

    try {
      const confirmed = await bookEvent(slug, quantity);
      setBooking(confirmed);
      onChanged();
    } catch (bookError) {
      if (bookError instanceof ApiError) {
        switch (bookError.code) {
          case 'event_full':
            setError('This event is fully booked.');
            break;
          case 'already_booked':
            setError('You already hold a place at this event.');
            break;
          case 'event_started':
            setError('This event has already started, so it is closed for bookings.');
            break;
          default:
            setError(getErrorMessage(bookError, 'Could not book a place. Please try again.'));
        }
      } else {
        setError(getErrorMessage(bookError, 'Could not book a place. Please try again.'));
      }
    } finally {
      setPhase('idle');
    }
  }

  async function handleCancel() {
    setPhase('sending');
    setError(null);

    try {
      await cancelBooking(slug);
      setBooking(null);
      setReleased(true);
      onChanged();
    } catch (cancelError) {
      if (cancelError instanceof ApiError && cancelError.code === 'event_started') {
        setError('This event has already started, so a place can no longer be released.');
      } else {
        setError(getErrorMessage(cancelError, 'Could not release the place. Please try again.'));
      }
    } finally {
      setPhase('idle');
    }
  }

  const isSending = phase === 'sending';
  const maxQuantity = remainingSpots === null ? 4 : Math.min(4, remainingSpots);

  return (
    <div className={styles.panel}>
      <p className={styles.heading}>
        {remainingSpots === null
          ? 'Places available'
          : `${remainingSpots} ${remainingSpots === 1 ? 'place' : 'places'} left`}
      </p>

      {paid ? (
        <p className={styles.meta}>
          NPR {ticketPrice.toLocaleString()} per place
          {quantity > 1 ? ` · NPR ${total.toLocaleString()} for ${quantity}` : ''} · paid
          through eSewa
        </p>
      ) : null}

      <div className={styles.stepper}>
        <label className={styles.stepperLabel} htmlFor="booking-quantity">
          Places
        </label>
        <button
          type="button"
          className={styles.step}
          aria-label="One fewer place"
          disabled={isSending || quantity <= 1}
          onClick={() => setQuantity((value) => Math.max(1, value - 1))}
        >
          −
        </button>
        <output id="booking-quantity" className={styles.quantity}>
          {quantity}
        </output>
        <button
          type="button"
          className={styles.step}
          aria-label="One more place"
          disabled={isSending || quantity >= maxQuantity}
          onClick={() => setQuantity((value) => Math.min(maxQuantity, value + 1))}
        >
          +
        </button>
        <span className={styles.stepperNote}>one to four at a time</span>
      </div>

      {error ? (
        <p className={styles.error} role="alert">
          {error}
        </p>
      ) : null}

      <Button
        type="button"
        size="small"
        disabled={isSending}
        onClick={() => {
          if (paid) {
            void handlePay();
          } else {
            void handleBook();
          }
        }}
      >
        {isSending
          ? paid
            ? 'Taking you to eSewa…'
            : 'Booking…'
          : paid
            ? `Pay NPR ${total.toLocaleString()}`
            : quantity === 1
              ? 'Book my place'
              : `Book ${quantity} places`}
      </Button>
    </div>
  );
}
