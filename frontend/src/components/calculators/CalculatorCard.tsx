import { Link } from 'react-router';

import type { CalculatorSummary } from '@/features/calculators/types';

import styles from './CalculatorCard.module.css';

interface CalculatorCardProps {
  calculator: CalculatorSummary;
}

/** One row in the public catalogue. The whole card is the link. */
export function CalculatorCard({ calculator }: CalculatorCardProps) {
  return (
    <article className={styles.card}>
      <p className={styles.category}>{calculator.category}</p>
      <h3 className={styles.name}>
        <Link to={`/calculators/${calculator.slug}`} className={styles.link}>
          {calculator.name}
        </Link>
      </h3>
      <p className={styles.summary}>{calculator.summary}</p>
      <span className={styles.cta} aria-hidden="true">
        Open calculator →
      </span>
    </article>
  );
}
