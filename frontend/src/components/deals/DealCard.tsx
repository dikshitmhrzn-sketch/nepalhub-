import { Link } from 'react-router';

import { formatLocation, remainingLabel } from '@/features/deals/format';
import type { DealSummary } from '@/features/deals/types';

import styles from './DealCard.module.css';

export interface DealCardProps {
  deal: DealSummary;
}

/**
 * One offer as a row on the offers page.
 *
 * The title is the only link. A card has exactly one destination, and the discount text, the
 * business name and the location are all facts about that destination rather than places of their
 * own — the business name in particular is printed rather than linked, because a link inside the
 * card's own link is not a thing HTML can express.
 */
export function DealCard({ deal }: DealCardProps) {
  const location = formatLocation(deal.districtName, deal.provinceName);

  return (
    <article className={styles.card}>
      <div className={styles.header}>
        {deal.discountText ? <span className={styles.discount}>{deal.discountText}</span> : null}
        <span className={styles.remaining} data-urgent={deal.daysRemaining <= 3}>
          {remainingLabel(deal.daysRemaining)}
        </span>
      </div>

      <h2 className={styles.title}>
        <Link className={styles.link} to={`/deals/${encodeURIComponent(deal.slug)}`}>
          {deal.title}
        </Link>
      </h2>

      {deal.summary ? <p className={styles.summary}>{deal.summary}</p> : null}

      <footer className={styles.footer}>
        {deal.businessName ? <span className={styles.meta}>{deal.businessName}</span> : null}
        {location ? <span className={styles.meta}>{location}</span> : null}
      </footer>
    </article>
  );
}