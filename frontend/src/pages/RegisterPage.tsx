import { useState, type FormEvent } from 'react';
import { Link, Navigate, useNavigate } from 'react-router';

import { Button } from '@/components/ui/Button';
import { Container } from '@/components/ui/Container';
import { StateMessage } from '@/components/ui/StateMessage';
import { useAuth } from '@/features/auth/AuthContext';
import { getErrorMessage, getFieldErrors } from '@/lib/apiErrors';

import styles from './AuthPage.module.css';

export function RegisterPage() {
  const { status, signUp } = useAuth();
  const navigate = useNavigate();

  const [fullName, setFullName] = useState('');
  const [email, setEmail] = useState('');
  const [phoneNumber, setPhoneNumber] = useState('');
  const [password, setPassword] = useState('');
  const [fieldErrors, setFieldErrors] = useState<Record<string, string>>({});
  const [formError, setFormError] = useState<string | null>(null);
  const [isSubmitting, setIsSubmitting] = useState(false);

  if (status === 'authenticated') {
    return <Navigate to="/account" replace />;
  }

  async function handleSubmit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    setIsSubmitting(true);
    setFieldErrors({});
    setFormError(null);

    try {
      await signUp({
        fullName: fullName.trim(),
        email: email.trim(),
        password,
        phoneNumber: phoneNumber.trim() || null,
      });
      navigate('/account', { replace: true });
    } catch (error) {
      setFieldErrors(getFieldErrors(error));
      setFormError(getErrorMessage(error, 'Could not create your account. Please try again.'));
      setIsSubmitting(false);
    }
  }

  return (
    <Container as="section" className={styles.page}>
      <div className={styles.panel}>
        <header className={styles.header}>
          <h1 className={styles.title}>Create your account</h1>
          <p className={styles.subtitle}>
            One account for the tools, the daily quiz and everything Nepal Hub adds next.
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
              <label className={styles.label} htmlFor="fullName">
                Full name
              </label>
              <input
                className={styles.input}
                id="fullName"
                name="fullName"
                type="text"
                autoComplete="name"
                required
                value={fullName}
                aria-invalid={fieldErrors.fullName ? true : undefined}
                aria-describedby={fieldErrors.fullName ? 'fullName-error' : undefined}
                onChange={(event) => setFullName(event.target.value)}
              />
              {fieldErrors.fullName ? (
                <span className={styles.fieldError} id="fullName-error">
                  {fieldErrors.fullName}
                </span>
              ) : null}
            </div>

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
              <label className={styles.label} htmlFor="phoneNumber">
                Phone number <span className={styles.hint}>(optional)</span>
              </label>
              <input
                className={styles.input}
                id="phoneNumber"
                name="phoneNumber"
                type="tel"
                autoComplete="tel"
                value={phoneNumber}
                aria-invalid={fieldErrors.phoneNumber ? true : undefined}
                aria-describedby={fieldErrors.phoneNumber ? 'phoneNumber-error' : undefined}
                onChange={(event) => setPhoneNumber(event.target.value)}
              />
              {fieldErrors.phoneNumber ? (
                <span className={styles.fieldError} id="phoneNumber-error">
                  {fieldErrors.phoneNumber}
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
                autoComplete="new-password"
                required
                value={password}
                aria-invalid={fieldErrors.password ? true : undefined}
                aria-describedby={fieldErrors.password ? 'password-error' : 'password-hint'}
                onChange={(event) => setPassword(event.target.value)}
              />
              {fieldErrors.password ? (
                <span className={styles.fieldError} id="password-error">
                  {fieldErrors.password}
                </span>
              ) : (
                <span className={styles.hint} id="password-hint">
                  At least 8 characters, with an upper case letter, a lower case letter, a digit and a
                  symbol.
                </span>
              )}
            </div>

            <Button type="submit" fullWidth disabled={isSubmitting}>
              {isSubmitting ? 'Creating your account…' : 'Create account'}
            </Button>
          </form>
        )}

        <p className={styles.footer}>
          Already have an account? <Link to="/login">Sign in</Link>
        </p>
      </div>
    </Container>
  );
}
