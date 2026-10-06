import { useState } from 'react';
import { NavLink, Outlet, useLocation, useNavigate } from 'react-router';

import { HeaderSearch } from '@/components/search/HeaderSearch';
import { Button } from '@/components/ui/Button';
import { Container } from '@/components/ui/Container';
import { useAuth } from '@/features/auth/AuthContext';

import styles from './AppShell.module.css';

interface NavigationItem {
  label: string;
  to: string;
  requiresAuth?: boolean;
  requiresStaff?: boolean;
  requiresAdmin?: boolean;
}

const navigation: NavigationItem[] = [
  { label: 'Home', to: '/' },
  { label: 'Tools', to: '/tools' },
  { label: 'Daily quiz', to: '/quiz' },
  { label: 'Student Hub', to: '/students' },
  { label: 'Jobs', to: '/jobs' },
  { label: 'Businesses', to: '/businesses' },
  { label: 'Offers', to: '/deals' },
  { label: 'Events', to: '/events' },
  { label: 'Leaderboard', to: '/leaderboard' },
  { label: 'Account', to: '/account', requiresAuth: true },
  { label: 'Payments', to: '/account/payments', requiresAuth: true },
  { label: 'My businesses', to: '/my-business', requiresAuth: true },
  { label: 'Dashboard', to: '/admin', requiresStaff: true },
  { label: 'Report queue', to: '/admin/reports', requiresStaff: true },
  { label: 'Ownership claims', to: '/admin/claims', requiresStaff: true },
  { label: 'Verification queue', to: '/admin/verifications', requiresStaff: true },
  { label: 'Manage calculators', to: '/admin/calculators', requiresStaff: true },
  { label: 'Manage quiz', to: '/admin/quiz', requiresStaff: true },
  { label: 'Manage resources', to: '/admin/students', requiresStaff: true },
  { label: 'Manage jobs', to: '/admin/jobs', requiresStaff: true },
  { label: 'Manage businesses', to: '/admin/businesses', requiresStaff: true },
  { label: 'Manage offers', to: '/admin/deals', requiresStaff: true },
  { label: 'Manage events', to: '/admin/events', requiresStaff: true },
  { label: 'Accounts', to: '/admin/users', requiresAdmin: true },
  { label: 'Audit log', to: '/admin/audit-log', requiresAdmin: true },
  { label: 'Plan requests', to: '/admin/subscription-requests', requiresAdmin: true },
  { label: 'Payments', to: '/admin/payments', requiresAdmin: true },
];

// Search is reached from the header box on every page rather than from a link here, which would be a
// tenth item in a row that already wraps on a phone.

const footerLinks: NavigationItem[] = [
  { label: 'Search', to: '/search' },
  { label: 'Jobs', to: '/jobs' },
  { label: 'Offers', to: '/deals' },
  { label: 'Events', to: '/events' },
];

function AccountActions() {
  const { status, user, signOut } = useAuth();
  const navigate = useNavigate();
  const [isSigningOut, setIsSigningOut] = useState(false);

  if (status === 'loading') {
    return null;
  }

  if (status === 'anonymous') {
    return (
      <div className={styles.authArea}>
        <NavLink
          to="/login"
          className={({ isActive }) =>
            isActive ? `${styles.navLink} ${styles.navLinkActive}` : styles.navLink
          }
        >
          Sign in
        </NavLink>
        <NavLink to="/register" className={styles.signUpLink}>
          Create account
        </NavLink>
      </div>
    );
  }

  async function handleSignOut() {
    setIsSigningOut(true);
    try {
      await signOut();
      navigate('/', { replace: true });
    } finally {
      setIsSigningOut(false);
    }
  }

  return (
    <div className={styles.authArea}>
      <span className={styles.greeting}>{user?.fullName}</span>
      <Button size="small" variant="ghost" onClick={() => void handleSignOut()} disabled={isSigningOut}>
        {isSigningOut ? 'Signing out…' : 'Sign out'}
      </Button>
    </div>
  );
}

export function AppShell() {
  const { status, isAdmin, isStaff } = useAuth();
  const location = useLocation();

  // The search page has its own larger box, so the header's is left out there rather than putting
  // two identical search boxes on one screen.
  const showHeaderSearch = location.pathname !== '/search';

  return (
    <div className={styles.shell}>
      <a className={styles.skipLink} href="#main-content">
        Skip to content
      </a>

      <header className={styles.header}>
        <Container className={styles.headerInner}>
          <NavLink to="/" className={styles.brand}>
            <span className={styles.brandMark} aria-hidden="true">
              NH
            </span>
            <span className={styles.brandText}>Nepal Hub</span>
          </NavLink>

          <nav aria-label="Primary">
            <ul className={styles.navList}>
              {navigation
                .filter((item) => {
                  if (item.requiresAdmin) {
                    return isAdmin;
                  }

                  if (item.requiresStaff) {
                    return isStaff;
                  }

                  return !item.requiresAuth || status === 'authenticated';
                })
                .map((item) => (
                  <li key={item.to}>
                    <NavLink
                      to={item.to}
                      end
                      className={({ isActive }) =>
                        isActive ? `${styles.navLink} ${styles.navLinkActive}` : styles.navLink
                      }
                    >
                      {item.label}
                    </NavLink>
                  </li>
                ))}
            </ul>
          </nav>

          <AccountActions />
        </Container>

        {showHeaderSearch ? (
          <Container className={styles.searchRow}>
            <HeaderSearch />
          </Container>
        ) : null}
      </header>

      <main id="main-content" className={styles.main}>
        <Outlet />
      </main>

      <footer className={styles.footer}>
        <Container className={styles.footerInner}>
          <div>
            <p>Nepal Hub — one useful website for everyday Nepal.</p>
            <p>
              Accounts, calculators, the daily quiz, the Student Hub, the job board, the business
              directory, offers, events and search are live.
            </p>
          </div>

          <div>
            <p className={styles.footerLabel}>Looking for something?</p>
            <ul className={styles.footerLinks}>
              {footerLinks.map((link) => (
                <li key={link.to}>
                  <NavLink to={link.to} className={styles.footerLink}>
                    {link.label}
                  </NavLink>
                </li>
              ))}
            </ul>
          </div>
        </Container>
      </footer>
    </div>
  );
}
