import { useState, type FormEvent } from 'react';
import { Link, Navigate, useLocation } from 'react-router';

import { Button } from '@/components/ui/Button';
import { Container } from '@/components/ui/Container';
import { forgotPassword, resendVerification } from '@/features/auth/api';
import { getErrorMessage } from '@/lib/apiErrors';

import styles from './AuthPage.module.css';

interface CheckEmailState {
  email: string;
  purpose: 'verify' | 'reset';
}

/**
 * The screen both roads lead to: after an account is created it waits for the
 * confirmation link, after a recovery ask it waits for the reset link. It never says
 * whether the address belongs to anyone — the server's answer is carried here unchanged —
 * and "send it again" spends the same always-answers endpoint, so a stranger clicking it
 * learns nothing a stranger did not already know.
 */
export function CheckEmailPage() {
  const location = useLocation();
  const state = location.state as CheckEmailState | null;

  const [formError, setFormError] = useState<string | null>(null);
  const [sentAgain, setSentAgain] = useState(false);
  const [isSubmitting, setIsSubmitting] = useState(false);

  // A visitor who lands here directly has nothing to check for; the sign-in page is the
  // nearest useful place to put them.
  if (!state?.email) {
    return <Navigate to="/login" replace />;
  }

  const { email, purpose } = state;
  const isVerification = purpose === 'verify';

  async function handleSendAgain(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    setIsSubmitting(true);
    setFormError(null);

    try {
      if (isVerification) {
        await resendVerification({ email });
      } else {
        await forgotPassword({ email });
      }

      setSentAgain(true);
    } catch (error) {
      setFormError(getErrorMessage(error, 'Could not send the link. Please try again.'));
    } finally {
      setIsSubmitting(false);
    }
  }

  return (
    <Container as="section" className={styles.page}>
      <div className={styles.panel}>
        <header className={styles.header}>
          <h1 className={styles.title}>{isVerification ? 'Confirm your email' : 'Check your email'}</h1>
          <p className={styles.subtitle}>
            {isVerification ? (
              <>
                A confirmation link is on its way to <strong>{email}</strong>. Open it to
                finish creating your account, then sign in as usual.
              </>
            ) : (
              <>
                If an account belongs to <strong>{email}</strong>, a password reset link is
                on its way. It works once and expires in an hour — check the spam folder if
                it does not arrive.
              </>
            )}
          </p>
        </header>

        {formError ? (
          <p className={styles.alert} role="alert">
            {formError}
          </p>
        ) : null}

        {sentAgain ? (
          <p className={styles.subtitle} role="status">
            Another link is on its way. Give it a minute — and if it still has not arrived,
            the address may not have an account on this site.
          </p>
        ) : (
          <form className={styles.form} onSubmit={handleSendAgain}>
            <Button type="submit" variant="secondary" fullWidth disabled={isSubmitting}>
              {isSubmitting ? 'Sending…' : 'Send it again'}
            </Button>
          </form>
        )}

        <p className={styles.footer}>
          {isVerification ? (
            <>
              Already confirmed? <Link to="/login">Sign in</Link>
            </>
          ) : (
            <>
              Wrong address? <Link to="/forgot-password">Try another</Link> or{' '}
              <Link to="/login">back to sign in</Link>
            </>
          )}
        </p>
      </div>
    </Container>
  );
}
