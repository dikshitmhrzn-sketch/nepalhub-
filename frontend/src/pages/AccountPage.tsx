import { useState } from 'react';

import { BadgeCard } from '@/components/badges/BadgeCard';
import { Button } from '@/components/ui/Button';
import { Container } from '@/components/ui/Container';
import { StateMessage } from '@/components/ui/StateMessage';
import { useAuth } from '@/features/auth/AuthContext';
import { useBadges } from '@/features/badges/useBadges';
import { getErrorMessage } from '@/lib/apiErrors';

import styles from './AccountPage.module.css';

export function AccountPage() {
  const { user, isAdmin, signOut } = useAuth();
  const badges = useBadges();

  const [isSigningOut, setIsSigningOut] = useState(false);
  const [error, setError] = useState<string | null>(null);

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
          Accounts start confirmed because email sending is not connected yet. Saved tools and the
          rest of your profile arrive with the next phases.
          {isAdmin ? ' You have administrator access.' : ''}
        </p>
      </div>

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
