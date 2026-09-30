import { Link } from 'react-router';

import { Container } from '@/components/ui/Container';

import styles from './NotFoundPage.module.css';

export function NotFoundPage() {
  return (
    <Container as="section" className={styles.section}>
      <p>404</p>
      <h1 className={styles.title}>This page does not exist yet.</h1>
      <p className={styles.text}>
        The link may be wrong, or the part of Nepal Hub it points to has not been built.
      </p>
      <p className={styles.action}>
        <Link to="/">Go back to home</Link>
      </p>
    </Container>
  );
}
