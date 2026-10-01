import { useState } from 'react';
import { NavLink, Outlet, useNavigate } from 'react-router';

import { Button } from '@/components/ui/Button';
import { Container } from '@/components/ui/Container';
import { useAuth } from '@/features/auth/AuthContext';

import styles from './AppShell.module.css';

interface NavigationItem {
  label: string;
  to: string;
  requiresAuth?: boolean;
  requiresStaff?: boolean;
}

const navigation: NavigationItem[] = [
  { label: 'Home', to: '/' },
  { label: 'Tools', to: '/tools' },
  { label: 'Daily quiz', to: '/quiz' },
  { label: 'Student Hub', to: '/students' },
  { label: 'Jobs', to: '/jobs' },
  { label: 'Businesses', to: '/businesses' },
  { label: 'Account', to: '/account', requiresAuth: true },
  { label: 'Manage calculators', to: '/admin/calculators', requiresStaff: true },
  { label: 'Manage quiz', to: '/admin/quiz', requiresStaff: true },
  { label: 'Manage resources', to: '/admin/students', requiresStaff: true },
  { label: 'Manage jobs', to: '/admin/jobs', requiresStaff: true },
  { label: 'Manage businesses', to: '/admin/businesses', requiresStaff: true },
];

const plannedModules = ['Deals', 'Events', 'Search'];

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
  const { status, isStaff } = useAuth();

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
      </header>

      <main id="main-content" className={styles.main}>
        <Outlet />
      </main>

      <footer className={styles.footer}>
        <Container className={styles.footerInner}>
          <div>
            <p>Nepal Hub — one useful website for everyday Nepal.</p>
            <p>
              Accounts, calculators, the daily quiz, the Student Hub, the job board and the
              business directory are live.
            </p>
          </div>

          <div>
            <p className={styles.plannedLabel}>Planned modules</p>
            <ul className={styles.planned}>
              {plannedModules.map((module) => (
                <li key={module} className={styles.plannedItem} aria-disabled="true">
                  {module}
                </li>
              ))}
            </ul>
          </div>
        </Container>
      </footer>
    </div>
  );
}
