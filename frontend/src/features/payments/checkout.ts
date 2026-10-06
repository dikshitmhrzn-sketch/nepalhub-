import { ApiError } from '@/lib/apiClient';
import { getErrorMessage } from '@/lib/apiErrors';

import type { CheckoutHandshake } from './types';

/**
 * Posts the signed form to the provider in the buyer's own browser.
 *
 * A form rather than a navigation to a url with a query string, because that is what the
 * protocol expects and what keeps the fields the provider signed in one place: they travel
 * as POSTed form values, which is where they were signed from. The form is built, submitted
 * and removed — nothing about the provider's url is kept in this app's state, and the
 * secret that signed the fields never left the server that did it.
 */
export function openProviderCheckout(handshake: CheckoutHandshake): void {
  const { checkoutUrl, fields } = handshake;

  // A reference with no form is a payment that has already finished one way or another.
  // There is nowhere to send the buyer, so the caller is told rather than shown a broken
  // redirect.
  if (!checkoutUrl || !fields) {
    throw new ApiError(0, 'checkout_finished', 'That payment has already finished.');
  }

  const form = document.createElement('form');
  form.method = 'POST';
  form.action = checkoutUrl;
  form.setAttribute('aria-hidden', 'true');

  for (const [name, value] of Object.entries(fields)) {
    const input = document.createElement('input');
    input.type = 'hidden';
    input.name = name;
    input.value = value;
    form.appendChild(input);
  }

  document.body.appendChild(form);
  form.submit();
  form.remove();
}

/**
 * What a checkout refusal says on screen. Each code the API can answer with is written out
 * here rather than passed through, because "409" tells a buyer holding a card nothing.
 */
export function checkoutErrorMessage(error: unknown, fallback: string): string {
  if (!(error instanceof ApiError)) {
    return getErrorMessage(error, fallback);
  }

  switch (error.code) {
    case 'payments_not_configured':
      return 'Online payment is not switched on on this site yet, so nothing can be bought here.';
    case 'payment_not_required':
      return 'There is no price on that, so there is nothing to pay for.';
    case 'business_not_found':
      return 'That listing is not yours to buy a plan for.';
    case 'event_not_found':
      return 'That event is not available.';
    case 'plan_unavailable':
      return 'That plan cannot be bought right now.';
    case 'event_started':
      return 'This event has already started, so tickets are closed.';
    case 'already_booked':
      return 'You already hold a place at this event.';
    case 'event_full':
      return 'This event is fully booked.';
    case 'session_invalid':
      return 'Your session is no longer valid. Please sign in again.';
    default:
      return getErrorMessage(error, fallback);
  }
}
