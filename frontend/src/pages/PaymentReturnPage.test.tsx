import { fireEvent, render, screen, waitFor } from '@testing-library/react';
import { RouterProvider, createMemoryRouter } from 'react-router';
import { beforeEach, describe, expect, it, vi } from 'vitest';

import { verifyPayment } from '@/features/payments/api';
import { ApiError } from '@/lib/apiClient';

import { PaymentReturnPage } from './PaymentReturnPage';

vi.mock('@/features/payments/api', () => ({ verifyPayment: vi.fn() }));

const payment = {
  reference: 'PAY-1',
  kind: 'EventTicket' as const,
  status: 'Succeeded' as const,
  amountNpr: 1000,
  description: '2 places at Kirtan Night',
  failureCode: null,
  providerReference: '100001',
  createdAt: '2026-10-06T09:00:00Z',
  settledAt: '2026-10-06T09:01:00Z',
  refundedAt: null,
};

function renderReturn(path = '/payments/return/PAY-1?data=abc&signature=xyz') {
  const router = createMemoryRouter(
    [
      { path: '/payments/return/:reference', element: <PaymentReturnPage /> },
      { path: '/payments/failed/:reference', element: <PaymentReturnPage declaredFailure /> },
    ],
    { initialEntries: [path] },
  );

  render(<RouterProvider router={router} />);
}

describe('PaymentReturnPage', () => {
  beforeEach(() => {
    vi.mocked(verifyPayment).mockReset();
  });

  it('settles from the provider\u2019s answer and shows what was paid for', async () => {
    vi.mocked(verifyPayment).mockResolvedValue(payment);

    renderReturn();

    expect(await screen.findByText('Payment confirmed')).toBeTruthy();
    expect(screen.getByText(/2 places at Kirtan Night/u)).toBeTruthy();
    expect(screen.getByText('Reference PAY-1')).toBeTruthy();

    // The signed payload it arrived with is offered to the first check.
    expect(vi.mocked(verifyPayment).mock.calls[0][0]).toEqual({
      reference: 'PAY-1',
      data: 'abc',
      signature: 'xyz',
    });
  });

  it('says a payment has not settled yet and can be asked about again', async () => {
    vi.mocked(verifyPayment).mockResolvedValue({
      ...payment,
      status: 'Pending',
      settledAt: null,
      providerReference: null,
    });

    renderReturn();

    expect(await screen.findByText('Still waiting for the provider')).toBeTruthy();

    fireEvent.click(screen.getByRole('button', { name: 'Check again' }));

    await waitFor(() => expect(vi.mocked(verifyPayment).mock.calls.length).toBe(2));
  });

  it('refuses a reply whose signature did not check out, then asks without it', async () => {
    vi.mocked(verifyPayment)
      .mockRejectedValueOnce(new ApiError(400, 'payment_signature_invalid', 'Not signed.'))
      .mockResolvedValue(payment);

    renderReturn();

    expect(await screen.findByText('That reply could not be confirmed')).toBeTruthy();
    expect(screen.getByRole('button', { name: 'Ask the provider again' })).toBeTruthy();

    fireEvent.click(screen.getByRole('button', { name: 'Ask the provider again' }));

    expect(await screen.findByText('Payment confirmed')).toBeTruthy();

    // The payload that was refused is not offered a second time: the check by reference
    // is the question the settlement answers anyway.
    expect(vi.mocked(verifyPayment).mock.calls[1][0]).toEqual({
      reference: 'PAY-1',
      data: null,
      signature: null,
    });
  });

  it('explains a reference that does not belong to this account', async () => {
    vi.mocked(verifyPayment).mockRejectedValue(
      new ApiError(404, 'payment_not_found', 'No payment with that reference belongs to you.'),
    );

    renderReturn();

    expect(await screen.findByText('No payment with that reference')).toBeTruthy();
    expect(screen.getByRole('link', { name: 'See your payments' })).toBeTruthy();
  });

  it('repeats nothing about a failure the url claims, and asks the provider either way', async () => {
    vi.mocked(verifyPayment).mockResolvedValue(payment);

    renderReturn('/payments/failed/PAY-1');

    // A "failed" return is a claim by the address bar; the provider's own answer wins.
    expect(await screen.findByText('The provider says this payment went through')).toBeTruthy();
  });
});
