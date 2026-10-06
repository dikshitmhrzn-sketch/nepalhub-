import { afterEach, describe, expect, it, vi } from 'vitest';

import { ApiError } from '@/lib/apiClient';

import { checkoutErrorMessage, openProviderCheckout } from './checkout';

describe('openProviderCheckout', () => {
  afterEach(() => {
    vi.restoreAllMocks();
    document.body.replaceChildren();
  });

  it('posts the signed fields to the provider and leaves nothing behind', () => {
    const captured: { form: HTMLFormElement | null } = { form: null };
    const submit = vi
      .spyOn(HTMLFormElement.prototype, 'submit')
      .mockImplementation(function (this: HTMLFormElement) {
        // Read the form while it is still in the document: it is removed the moment the
        // navigation it asks for has been started.
        captured.form = this;
      });

    openProviderCheckout({
      reference: 'PAY-1',
      checkoutUrl: 'https://rc-epay.esewa.com.np/api/epay/main/v2/payment',
      fields: {
        total_amount: '3000',
        transaction_uuid: 'PAY-1',
        product_code: 'EPAY_TEST',
        signature: 'abc',
      },
      status: 'Pending',
    });

    expect(submit).toHaveBeenCalledTimes(1);
    expect(captured.form).not.toBeNull();

    const form = captured.form as HTMLFormElement;
    expect(form.method).toBe('post');
    expect(form.action).toBe('https://rc-epay.esewa.com.np/api/epay/main/v2/payment');

    const field = (name: string) =>
      form.querySelector<HTMLInputElement>(`input[name="${name}"]`)?.value;

    expect(field('total_amount')).toBe('3000');
    expect(field('transaction_uuid')).toBe('PAY-1');
    expect(field('product_code')).toBe('EPAY_TEST');
    expect(field('signature')).toBe('abc');

    expect(document.querySelector('form')).toBeNull();
  });

  it('tells the caller when there is no form to post, rather than sending them nowhere', () => {
    vi.spyOn(HTMLFormElement.prototype, 'submit').mockImplementation(() => {});

    expect(() =>
      openProviderCheckout({
        reference: 'PAY-1',
        checkoutUrl: null,
        fields: null,
        status: 'Succeeded',
      }),
    ).toThrowError(ApiError);

    expect(document.querySelector('form')).toBeNull();
  });
});

describe('checkoutErrorMessage', () => {
  it('writes out each refusal a checkout can answer with', () => {
    const message = (code: string, status = 409) =>
      checkoutErrorMessage(new ApiError(status, code, 'raw'), 'Fallback.');

    expect(message('payments_not_configured', 503)).toMatch(/not switched on/u);
    expect(message('payment_not_required')).toMatch(/nothing to pay for/u);
    expect(message('already_booked')).toMatch(/already hold a place/u);
    expect(message('event_full')).toMatch(/fully booked/u);
    expect(message('event_started')).toMatch(/already started/u);
    expect(message('plan_unavailable')).toMatch(/cannot be bought/u);
    expect(message('business_not_found', 404)).toMatch(/not yours/u);
    expect(message('session_invalid', 401)).toMatch(/sign in again/u);
  });

  it('falls back to the server\u2019s own sentence for anything it does not know', () => {
    expect(
      checkoutErrorMessage(new ApiError(418, 'teapot', 'I am a teapot.'), 'Fallback.'),
    ).toBe('I am a teapot.');

    expect(checkoutErrorMessage(new Error('offline'), 'Fallback.')).toBe('Fallback.');
  });
});
