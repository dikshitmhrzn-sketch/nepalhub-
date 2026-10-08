import { useEffect, useState, type FormEvent } from 'react';
import { Link, useSearchParams } from 'react-router';

import { Button } from '@/components/ui/Button';
import { Container } from '@/components/ui/Container';
import { StateMessage } from '@/components/ui/StateMessage';
import { resendVerification, verifyEmail } from '@/features/auth/api';
import { ApiError } from '@/lib/apiClient';
import { getErrorMessage } from '@/lib/apiErrors';

import styles from './AuthPage.module.css';

type Phase =
  | { status: 'checking' }
  | { status: 'verified' }
  | { status: 'error'; error: ApiError };

/**
 * Where the confirmation link in the mail lands. The address and the token travel in the
 * url, and the POST fires as soon as the page opens — a confirmation is something a
 * person does by clicking, not by finding a button. An answer of "verified" is
 * idempotent on the server, so a refresh or a duplicated link lands on the same screen.
 */
export function VerifyEmailPage() {
  const [searchParams] = useSearchParams();
  const email = searchParams.get('email') ?? '';
  const token = searchParams.get('token') ?? '';

  const [phase, setPhase] = useState<Phase>({ status: 'checking' });
  const [sentAgain, setSentAgain] = useState(false);
  const [formError, setFormError] = useState<string | null>(null);
  const [isSubmitting, setIsSubmitting] = useState(false);

  useEffect(() => {
    if (!email || !token) {
      setPhase({
        status: 'error',
        error: new ApiError(400, 'token_invalid', 'This link is incomplete. Request a new one.'),
      });
      return;
    }

    let cancelled = false;
    setPhase({ status: 'checking' });

    void (async () => {
      try {
        await verifyEmail({ email, token });
        if (!cancelled) {
          setPhase({ status: 'verified' });
        }
      } catch (error) {
        if (!cancelled) {
          setPhase({
            status: 'error',
            error:
              error instanceof ApiError
                ? error
                : new ApiError(0, 'unknown_error', 'Something went wrong while confirming.'),
          });
        }
      }
    })();

    return () => {
      cancelled = true;
    };
  }, [email, token]);

  async function handleSendAgain(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    setIsSubmitting(true);
    setFormError(null);

    try {
      await resendVerification({ email });
      setSentAgain(true);
    } catch (error) {
      setFormError(getErrorMessage(error, 'Could not send a new link. Please try again.'));
    } finally {
      setIsSubmitting(false);
    }
  }

  if (phase.status === 'checking') {
    return (
      <Container as="section" className={styles.page}>
        <div className={styles.panel}>
          <StateMessage tone="loading" title="Confirming your address…" />
        </div>
      </Container>
    );
  }

  if (phase.status === 'verified') {
    return (
      <Container as="section" className={styles.page}>
        <div className={styles.panel}>
          <header className={styles.header}>
            <h1 className={styles.title}>Email confirmed</h1>
            <p className={styles.subtitle}>
              The address on your account has been checked. You can sign in now — everything
              you saved will be where you left it.
            </p>
          </header>

          <p className={styles.footer}>
            <Link to="/login">Sign in to your account</Link>
          </p>
        </div>
      </Container>
    );
  }

  /*
   * A refused link is a link that ran out — expired, spent, or mistyped — and the server
   * refuses it identically for every reason, so the only useful reply here is a fresh one.
   * Without an address to send to (a hand-trimmed url), the door back is registration.
   */
  return (
    <Container as="section" className={styles.page}>
      <div className={styles.panel}>
        <header className={styles.header}>
          <h1 className={styles.title}>That link no longer works</h1>
          <p className={styles.subtitle}>
            Confirmation links expire and can only be spent once. Ask for a new one and it
            will arrive in a moment.
          </p>
        </header>

        <StateMessage
          tone="error"
          title="The link was refused"
          description={getErrorMessage(phase.error, 'This link is invalid or has expired.')}
        />

        {formError ? (
          <p className={styles.alert} role="alert">
            {formError}
          </p>
        ) : null}

        {email ? (
          sentAgain ? (
            <p className={styles.subtitle} role="status">
              A new link is on its way to <strong>{email}</strong>. Open it to confirm your
              address.
            </p>
          ) : (
            <form className={styles.form} onSubmit={handleSendAgain}>
              <Button type="submit" variant="secondary" fullWidth disabled={isSubmitting}>
                {isSubmitting ? 'Sending…' : 'Send a new link'}
              </Button>
            </form>
          )
        ) : null}

        <p className={styles.footer}>
          {email ? (
            <>
              Back to <Link to="/login">sign in</Link> or <Link to="/register">create an
              account</Link>
            </>
          ) : (
            <>
              <Link to="/register">Create an account</Link> or <Link to="/login">sign in</Link>
            </>
          )}
        </p>
      </div>
    </Container>
  );
}
