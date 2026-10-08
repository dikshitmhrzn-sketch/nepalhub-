import { useState, type FormEvent } from 'react';
import { Link, Navigate, useLocation, useNavigate } from 'react-router';

import { Button } from '@/components/ui/Button';
import { Container } from '@/components/ui/Container';
import { StateMessage } from '@/components/ui/StateMessage';
import { useAuth } from '@/features/auth/AuthContext';
import { resendVerification } from '@/features/auth/api';
import { ApiError } from '@/lib/apiClient';
import { getErrorMessage, getFieldErrors } from '@/lib/apiErrors';

import styles from './AuthPage.module.css';

interface RedirectState {
  from?: string;
}

export function LoginPage() {
  const { status, signIn } = useAuth();
  const navigate = useNavigate();
  const location = useLocation();

  const [email, setEmail] = useState('');
  const [password, setPassword] = useState('');
  const [rememberMe, setRememberMe] = useState(true);
  const [fieldErrors, setFieldErrors] = useState<Record<string, string>>({});
  const [formError, setFormError] = useState<string | null>(null);
  const [isSubmitting, setIsSubmitting] = useState(false);
  const [needsConfirmation, setNeedsConfirmation] = useState(false);
  const [isResending, setIsResending] = useState(false);

  if (status === 'authenticated') {
    return <Navigate to="/account" replace />;
  }

  async function handleSubmit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    setIsSubmitting(true);
    setFieldErrors({});
    setFormError(null);

    try {
      await signIn({ email: email.trim(), password, rememberMe });
      const destination = (location.state as RedirectState | null)?.from ?? '/account';
      navigate(destination, { replace: true });
    } catch (error) {
      setFieldErrors(getFieldErrors(error));

      /*
       * The password was right — the server checked it before saying this — so the only
       * useful reply is the link the person is missing, not a retype of the password
       * they just got through.
       */
      if (error instanceof ApiError && error.code === 'email_not_confirmed') {
        setNeedsConfirmation(true);
        setFormError('Confirm your email address before signing in. We can send the link again.');
      } else {
        setNeedsConfirmation(false);
        setFormError(getErrorMessage(error, 'Could not sign you in. Please try again.'));
      }

      setIsSubmitting(false);
    }
  }

  async function handleResendConfirmation() {
    setIsResending(true);
    setFormError(null);

    try {
      const submitted = email.trim();
      await resendVerification({ email: submitted });
      navigate('/check-email', {
        replace: true,
        state: { email: submitted, purpose: 'verify' },
      });
    } catch (error) {
      setFormError(getErrorMessage(error, 'Could not send the link. Please try again.'));
      setIsResending(false);
    }
  }

  return (
    <Container as="section" className={styles.page}>
      <div className={styles.panel}>
        <header className={styles.header}>
          <h1 className={styles.title}>Sign in</h1>
          <p className={styles.subtitle}>
            Your account keeps your saved tools, quiz streak and applications together.
          </p>
        </header>

        {status === 'loading' ? (
          <StateMessage tone="loading" title="Checking your session" />
        ) : (
          <form className={styles.form} onSubmit={handleSubmit} noValidate>
            {formError ? (
              <p className={styles.alert} role="alert">
                {formError}
              </p>
            ) : null}

            <div className={styles.field}>
              <label className={styles.label} htmlFor="email">
                Email
              </label>
              <input
                className={styles.input}
                id="email"
                name="email"
                type="email"
                autoComplete="email"
                required
                value={email}
                aria-invalid={fieldErrors.email ? true : undefined}
                aria-describedby={fieldErrors.email ? 'email-error' : undefined}
                onChange={(event) => setEmail(event.target.value)}
              />
              {fieldErrors.email ? (
                <span className={styles.fieldError} id="email-error">
                  {fieldErrors.email}
                </span>
              ) : null}
            </div>

            <div className={styles.field}>
              <label className={styles.label} htmlFor="password">
                Password
              </label>
              <input
                className={styles.input}
                id="password"
                name="password"
                type="password"
                autoComplete="current-password"
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

            <p className={styles.hint}>
              <Link to="/forgot-password">Forgot your password?</Link>
            </p>

            <label className={styles.checkbox}>
              <input
                type="checkbox"
                name="rememberMe"
                checked={rememberMe}
                onChange={(event) => setRememberMe(event.target.checked)}
              />
              Keep me signed in on this device
            </label>

            <Button type="submit" fullWidth disabled={isSubmitting}>
              {isSubmitting ? 'Signing in…' : 'Sign in'}
            </Button>

            {needsConfirmation ? (
              <Button
                type="button"
                variant="secondary"
                fullWidth
                disabled={isResending}
                onClick={() => void handleResendConfirmation()}
              >
                {isResending ? 'Sending the link…' : 'Email me the confirmation link again'}
              </Button>
            ) : null}
          </form>
        )}

        <p className={styles.footer}>
          New to Nepal Hub? <Link to="/register">Create an account</Link>
        </p>
      </div>
    </Container>
  );
}
