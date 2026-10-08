import { useState, type FormEvent } from 'react';

import { BadgeCard } from '@/components/badges/BadgeCard';
import { Button } from '@/components/ui/Button';
import { Container } from '@/components/ui/Container';
import { StateMessage } from '@/components/ui/StateMessage';
import { useAuth } from '@/features/auth/AuthContext';
import { changePassword } from '@/features/auth/api';
import { useBadges } from '@/features/badges/useBadges';
import { getErrorMessage, getFieldErrors } from '@/lib/apiErrors';

import styles from './AccountPage.module.css';

export function AccountPage() {
  const { user, isAdmin, signOut } = useAuth();
  const badges = useBadges();

  const [isSigningOut, setIsSigningOut] = useState(false);
  const [error, setError] = useState<string | null>(null);

  const [currentPassword, setCurrentPassword] = useState('');
  const [newPassword, setNewPassword] = useState('');
  const [passwordFieldErrors, setPasswordFieldErrors] = useState<Record<string, string>>({});
  const [passwordError, setPasswordError] = useState<string | null>(null);
  const [passwordChanged, setPasswordChanged] = useState(false);
  const [isChangingPassword, setIsChangingPassword] = useState(false);

  if (!user) {
    return <StateMessage tone="loading" title="Loading your account" />;
  }

  async function handleSignOut() {
    setIsSigningOut(true);
    setError(null);

    try {
      await signOut();
    } catch {
      setError(getErrorMessage(null, 'Could not sign you out. Please try again.'));
      setIsSigningOut(false);
    }
  }

  async function handleChangePassword(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    setPasswordFieldErrors({});
    setPasswordError(null);
    setPasswordChanged(false);
    setIsChangingPassword(true);

    try {
      await changePassword({ currentPassword, newPassword });
      setPasswordChanged(true);
      setCurrentPassword('');
      setNewPassword('');
    } catch (changeError) {
      setPasswordFieldErrors(getFieldErrors(changeError));
      setPasswordError(
        getErrorMessage(changeError, 'Could not change your password. Please try again.'),
      );
    } finally {
      setIsChangingPassword(false);
    }
  }

  const badgeList = badges.status === 'success' ? badges.data.badges : [];

  return (
    <Container as="section" className={styles.page}>
      <div className={styles.panel}>
        <header className={styles.header}>
          <div>
            <h1 className={styles.title}>{user.fullName}</h1>
            <p className={styles.subtitle}>Your Nepal Hub account</p>
          </div>

          <Button
            variant="secondary"
            onClick={() => void handleSignOut()}
            disabled={isSigningOut}
          >
            {isSigningOut ? 'Signing out…' : 'Sign out'}
          </Button>
        </header>

        {error ? (
          <p className={styles.notice} role="alert">
            {error}
          </p>
        ) : null}

        <dl className={styles.details}>
          <div className={styles.row}>
            <dt className={styles.key}>Email</dt>
            <dd className={styles.value}>{user.email}</dd>
          </div>
          <div className={styles.row}>
            <dt className={styles.key}>Email confirmed</dt>
            <dd className={styles.value}>{user.isEmailConfirmed ? 'Yes' : 'Not yet'}</dd>
          </div>
          <div className={styles.row}>
            <dt className={styles.key}>Level</dt>
            <dd className={styles.value}>{user.level}</dd>
          </div>
          <div className={styles.row}>
            <dt className={styles.key}>XP</dt>
            <dd className={styles.value}>{user.xp}</dd>
          </div>
          <div className={styles.row}>
            <dt className={styles.key}>Roles</dt>
            <dd className={styles.value}>
              <ul className={styles.roles}>
                {user.roles.map((role) => (
                  <li className={styles.role} key={role}>
                    {role}
                  </li>
                ))}
              </ul>
            </dd>
          </div>
        </dl>

        <p className={styles.notice}>
          {user.isEmailConfirmed ? null : (
            <>
              This address has not been confirmed yet. Sign out and ask for a new link from
              the sign-in page to fix that.{' '}
            </>
          )}
          Saved tools and the rest of your profile arrive with the next phases.
          {isAdmin ? ' You have administrator access.' : ''}
        </p>
      </div>

      <section className={styles.passwordPanel} aria-labelledby="password-heading">
        <header>
          <h2 className={styles.passwordTitle} id="password-heading">
            Change password
          </h2>
          <p className={styles.passwordSubtitle}>
            The current password is required. Everywhere the old one was still signed in is
            signed out when the new one takes.
          </p>
        </header>

        {passwordError ? (
          <p className={styles.alert} role="alert">
            {passwordError}
          </p>
        ) : null}

        {passwordChanged ? (
          <p className={styles.notice} role="status">
            Password changed. Use the new one the next time you sign in.
          </p>
        ) : null}

        <form className={styles.passwordForm} onSubmit={handleChangePassword} noValidate>
          <div className={styles.field}>
            <label className={styles.label} htmlFor="currentPassword">
              Current password
            </label>
            <input
              className={styles.input}
              id="currentPassword"
              name="currentPassword"
              type="password"
              autoComplete="current-password"
              required
              value={currentPassword}
              aria-invalid={passwordFieldErrors.currentPassword ? true : undefined}
              aria-describedby={
                passwordFieldErrors.currentPassword ? 'currentPassword-error' : undefined
              }
              onChange={(event) => setCurrentPassword(event.target.value)}
            />
            {passwordFieldErrors.currentPassword ? (
              <span className={styles.fieldError} id="currentPassword-error">
                {passwordFieldErrors.currentPassword}
              </span>
            ) : null}
          </div>

          <div className={styles.field}>
            <label className={styles.label} htmlFor="newPassword">
              New password
            </label>
            <input
              className={styles.input}
              id="newPassword"
              name="newPassword"
              type="password"
              autoComplete="new-password"
              required
              value={newPassword}
              aria-invalid={passwordFieldErrors.password ? true : undefined}
              aria-describedby={passwordFieldErrors.password ? 'newPassword-error' : 'newPassword-hint'}
              onChange={(event) => setNewPassword(event.target.value)}
            />
            {passwordFieldErrors.password ? (
              <span className={styles.fieldError} id="newPassword-error">
                {passwordFieldErrors.password}
              </span>
            ) : (
              <span className={styles.hint} id="newPassword-hint">
                At least 8 characters, with an upper case letter, a lower case letter, a digit and
                a symbol.
              </span>
            )}
          </div>

          <div className={styles.actions}>
            <Button type="submit" disabled={isChangingPassword}>
              {isChangingPassword ? 'Changing…' : 'Change password'}
            </Button>
          </div>
        </form>
      </section>

      <section className={styles.badgesPanel} aria-labelledby="badges-heading">
        <header className={styles.badgesHeader}>
          <div>
            <h2 className={styles.badgesTitle} id="badges-heading">
              Badges
            </h2>
            <p className={styles.badgesSubtitle}>
              Earned by finishing the daily quiz. Nothing here is bought with XP.
            </p>
          </div>

          {badges.status === 'success' && badges.data.badges.length > 0 ? (
            <p className={styles.badgesCount}>
              {badges.data.awardedCount} of {badges.data.badges.length} earned
            </p>
          ) : null}
        </header>

        {badges.status === 'loading' ? (
          <StateMessage tone="loading" title="Loading your badges…" />
        ) : null}

        {badges.status === 'error' ? (
          <StateMessage
            tone="error"
            title="Could not load your badges"
            description={getErrorMessage(badges.error, 'Please try again in a moment.')}
            action={
              <Button variant="secondary" size="small" onClick={badges.reload}>
                Try again
              </Button>
            }
          />
        ) : null}

        {badges.status === 'success' && badgeList.length === 0 ? (
          <StateMessage
            tone="info"
            title="No badges yet"
            description="The first one arrives as soon as you finish a daily quiz."
          />
        ) : null}

        {badgeList.length > 0 ? (
          <ul className={styles.badgeGrid}>
            {badgeList.map((badge) => (
              <li key={badge.code}>
                <BadgeCard badge={badge} />
              </li>
            ))}
          </ul>
        ) : null}
      </section>
    </Container>
  );
}
