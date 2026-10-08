import { useState, type FormEvent } from 'react';
import { Link, useSearchParams } from 'react-router';

import { Button } from '@/components/ui/Button';
import { Container } from '@/components/ui/Container';
import { StateMessage } from '@/components/ui/StateMessage';
import { resetPassword } from '@/features/auth/api';
import { ApiError } from '@/lib/apiClient';
import { getFieldErrors, getErrorMessage } from '@/lib/apiErrors';

import styles from './AuthPage.module.css';

/**
 * Where the reset link lands: a one-time address plus token in the url and a fresh
 * password in the form. A refused link — expired, spent, minted for another purpose —
 * is shown as exactly that with the way to ask for a new one, because the link is the
 * only credential this page has and it has already failed. A password the policy
 * refuses leaves the link intact, so the form is simply shown the reason to fix.
 */
export function ResetPasswordPage() {
  const [searchParams] = useSearchParams();
  const email = searchParams.get('email') ?? '';
  const token = searchParams.get('token') ?? '';

  const [password, setPassword] = useState('');
  const [confirmation, setConfirmation] = useState('');
  const [fieldErrors, setFieldErrors] = useState<Record<string, string>>({});
  const [formError, setFormError] = useState<string | null>(null);
  const [isSubmitting, setIsSubmitting] = useState(false);
  const [isDone, setIsDone] = useState(false);
  const [linkRefused, setLinkRefused] = useState(false);

  async function handleSubmit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    setFieldErrors({});
    setFormError(null);

    if (password !== confirmation) {
      setFieldErrors({ confirmation: 'The two passwords do not match.' });
      return;
    }

    setIsSubmitting(true);

    try {
      await resetPassword({ email, token, password });
      setIsDone(true);
    } catch (error) {
      const message = getErrorMessage(error, 'Could not set the new password. Please try again.');
      setFormError(message);

      if (error instanceof ApiError && error.code === 'token_invalid') {
        setLinkRefused(true);
      } else {
        setFieldErrors(getFieldErrors(error));
      }
    } finally {
      setIsSubmitting(false);
    }
  }

  if (!email || !token) {
    return (
      <Container as="section" className={styles.page}>
        <div className={styles.panel}>
          <StateMessage
            tone="error"
            title="This link is incomplete"
            description="The address and the token travel together in the link. Ask for a new one to get both."
            action={
              <Link to="/forgot-password" className={styles.subtitle}>
                Ask for a new link
              </Link>
            }
          />
        </div>
      </Container>
    );
  }

  if (linkRefused) {
    return (
      <Container as="section" className={styles.page}>
        <div className={styles.panel}>
          <StateMessage
            tone="error"
            title="That link no longer works"
            description="Reset links expire in an hour and can only be spent once. Ask for a new one and it will arrive in a moment."
            action={
              <Link to="/forgot-password" className={styles.subtitle}>
                Ask for a new link
              </Link>
            }
          />
        </div>
      </Container>
    );
  }

  if (isDone) {
    return (
      <Container as="section" className={styles.page}>
        <div className={styles.panel}>
          <header className={styles.header}>
            <h1 className={styles.title}>Password changed</h1>
            <p className={styles.subtitle}>
              The new password is in effect. Anywhere it was still signed in has been signed
              out, so use this one from now on.
            </p>
          </header>

          <p className={styles.footer}>
            <Link to="/login">Sign in with your new password</Link>
          </p>
        </div>
      </Container>
    );
  }

  return (
    <Container as="section" className={styles.page}>
      <div className={styles.panel}>
        <header className={styles.header}>
          <h1 className={styles.title}>Set a new password</h1>
          <p className={styles.subtitle}>
            The link is good for one change. Pick something this account has not used before.
          </p>
        </header>

        <form className={styles.form} onSubmit={handleSubmit} noValidate>
          {formError ? (
            <p className={styles.alert} role="alert">
              {formError}
            </p>
          ) : null}

          <div className={styles.field}>
            <label className={styles.label} htmlFor="password">
              New password
            </label>
            <input
              className={styles.input}
              id="password"
              name="password"
              type="password"
              autoComplete="new-password"
              required
              value={password}
              aria-invalid={fieldErrors.password ? true : undefined}
              aria-describedby={fieldErrors.password ? 'password-error' : undefined}
              onChange={(event) => setPassword(event.target.value)}
            />
            {fieldErrors.password ? (
              <span className={styles.fieldError} id="password-error">
                {fieldErrors.password}
              </span>
            ) : null}
          </div>

          <div className={styles.field}>
            <label className={styles.label} htmlFor="confirmation">
              Confirm new password
            </label>
            <input
              className={styles.input}
              id="confirmation"
              name="confirmation"
              type="password"
              autoComplete="new-password"
              required
              value={confirmation}
              aria-invalid={fieldErrors.confirmation ? true : undefined}
              aria-describedby={fieldErrors.confirmation ? 'confirmation-error' : undefined}
              onChange={(event) => setConfirmation(event.target.value)}
            />
            {fieldErrors.confirmation ? (
              <span className={styles.fieldError} id="confirmation-error">
                {fieldErrors.confirmation}
              </span>
            ) : null}
          </div>

          <Button type="submit" fullWidth disabled={isSubmitting}>
            {isSubmitting ? 'Setting…' : 'Set the new password'}
          </Button>
        </form>

        <p className={styles.footer}>
          <Link to="/login">Back to sign in</Link>
        </p>
      </div>
    </Container>
  );
}
