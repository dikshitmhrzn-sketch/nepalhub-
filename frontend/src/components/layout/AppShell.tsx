import { NavLink, Outlet } from 'react-router';

import { Container } from '@/components/ui/Container';

import styles from './AppShell.module.css';

const navigation = [{ label: 'Home', to: '/' }];

const plannedModules = [
  'Tools',
  'Quiz',
  'Student Hub',
  'Jobs',
  'Businesses',
  'Deals',
  'Events',
  'Search',
];

export function AppShell() {
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
              {navigation.map((item) => (
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
        </Container>
      </header>

      <main id="main-content" className={styles.main}>
        <Outlet />
      </main>

      <footer className={styles.footer}>
        <Container className={styles.footerInner}>
          <div>
            <p>Nepal Hub — one useful website for everyday Nepal.</p>
            <p>Phase 1 scaffolding. No modules are live yet.</p>
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
