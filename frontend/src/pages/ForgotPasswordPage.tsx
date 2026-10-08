import { useState, type FormEvent } from 'react';
import { Link, useNavigate } from 'react-router';

import { Button } from '@/components/ui/Button';
import { Container } from '@/components/ui/Container';
import { forgotPassword } from '@/features/auth/api';
import { getFieldErrors, getErrorMessage } from '@/lib/apiErrors';

import styles from './AuthPage.module.css';

/**
 * Asks for a reset link without ever saying whether the address holds an account: the
 * server answers the same either way, and this page carries that answer forward by
 * showing the same "check your email" screen for every address that was submitted.
 */
export function ForgotPasswordPage() {
  const navigate = useNavigate();

  const [email, setEmail] = useState('');
  const [fieldErrors, setFieldErrors] = useState<Record<string, string>>({});
  const [formError, setFormError] = useState<string | null>(null);
  const [isSubmitting, setIsSubmitting] = useState(false);

  async function handleSubmit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    setIsSubmitting(true);
    setFieldErrors({});
    setFormError(null);

    const submitted = email.trim();

    try {
      await forgotPassword({ email: submitted });
      navigate('/check-email', {
        replace: true,
        state: { email: submitted, purpose: 'reset' },
      });
    } catch (error) {
      setFieldErrors(getFieldErrors(error));
      setFormError(getErrorMessage(error, 'Could not send the link. Please try again.'));
      setIsSubmitting(false);
    }
  }

  return (
    <Container as="section" className={styles.page}>
      <div className={styles.panel}>
        <header className={styles.header}>
          <h1 className={styles.title}>Forgot your password?</h1>
          <p className={styles.subtitle}>
            Enter the address on the account and a one-time link to set a new password will
            follow. The link works once and expires in an hour.
          </p>
        </header>

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

          <Button type="submit" fullWidth disabled={isSubmitting}>
            {isSubmitting ? 'Sending…' : 'Send a reset link'}
          </Button>
        </form>

        <p className={styles.footer}>
          Remembered it? <Link to="/login">Sign in</Link>
        </p>
      </div>
    </Container>
  );
}
