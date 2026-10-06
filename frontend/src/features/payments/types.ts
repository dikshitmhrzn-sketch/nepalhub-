/**
 * Mirrors the payment contracts exposed by the ASP.NET Core API: whether checkout exists at
 * all, the handshake that sends a buyer to the provider, and the payment rows as a buyer or
 * an administrator reads them.
 *
 * The API serialises enums by name, so these are the literal wire values.
 */

/** What is being bought. One checkout buys one kind of thing, never both. */
export type PaymentKind = 'Subscription' | 'EventTicket';

/**
 * Where a payment stands.
 *
 * `RefundDue` and `Refunded` are deliberately separate: money that is owed back and money
 * that has gone back are different statements, and only the provider's own status check is
 * allowed to move between them.
 */
export type PaymentStatus = 'Pending' | 'Succeeded' | 'Failed' | 'RefundDue' | 'Refunded';

/** Whether a provider is wired up. Carries no merchant code, url or key — never those. */
export interface PaymentConfig {
  available: boolean;
  provider: string;
}

/**
 * The form the browser posts to leave this site. The url and the fields come together or
 * not at all: a reference with no form is a payment that has already finished.
 */
export interface CheckoutHandshake {
  reference: string;
  checkoutUrl: string | null;
  fields: Record<string, string> | null;
  status: PaymentStatus;
}

/** One payment as the buyer reads it. */
export interface Payment {
  reference: string;
  kind: PaymentKind;
  status: PaymentStatus;
  amountNpr: number;
  description: string;
  failureCode: string | null;
  providerReference: string | null;
  createdAt: string;
  settledAt: string | null;
  refundedAt: string | null;
}

/** One row of the buyer's own history. */
export interface PaymentSummary {
  reference: string;
  kind: PaymentKind;
  status: PaymentStatus;
  amountNpr: number;
  description: string;
  failureCode: string | null;
  createdAt: string;
  settledAt: string | null;
  refundedAt: string | null;
}

/** The admin list carries the buyer too: a refund question is always about a person. */
export interface AdminPayment extends PaymentSummary {
  buyerName: string;
  buyerEmail: string | null;
}

/** The queue's status window, as the API's own parser names them. */
export type PaymentFilter = 'all' | PaymentStatus;

export const paymentFilters: readonly PaymentFilter[] = [
  'all',
  'Pending',
  'Succeeded',
  'Failed',
  'RefundDue',
  'Refunded',
];

export const paymentFilterLabels: Record<PaymentFilter, string> = {
  all: 'All',
  Pending: 'Waiting',
  Succeeded: 'Paid',
  Failed: 'Failed',
  RefundDue: 'Refund due',
  Refunded: 'Refunded',
};

export function paymentStatusQuery(filter: PaymentFilter): string | undefined {
  return filter === 'all' ? undefined : filter;
}

/** How a status reads on a row, where the filter's shorter labels are too terse. */
export const paymentStatusLabels: Record<PaymentStatus, string> = {
  Pending: 'Waiting for payment',
  Succeeded: 'Paid',
  Failed: 'Did not go through',
  RefundDue: 'Owed back',
  Refunded: 'Refunded',
};

/** The kind as a sentence fragment, since the row says what the money was for. */
export const paymentKindLabels: Record<PaymentKind, string> = {
  Subscription: 'Plan',
  EventTicket: 'Event ticket',
};

/** What a buyer is told about a payment that has not finished. */
export function describePayment(payment: PaymentSummary): string {
  switch (payment.status) {
    case 'Succeeded':
      return `Paid${payment.settledAt ? ` on ${payment.settledAt.slice(0, 10)}` : ''}.`;
    case 'Failed':
      return 'The provider did not complete this payment, so nothing was taken.';
    case 'RefundDue':
      return 'This money is owed back and has not been returned yet.';
    case 'Refunded':
      return `Refunded${payment.refundedAt ? ` on ${payment.refundedAt.slice(0, 10)}` : ''}.`;
    default:
      return 'Waiting for the provider to confirm.';
  }
}
