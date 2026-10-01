import { Link } from 'react-router';

import {
  categoryLabel,
  formatLocation,
  formatPhone,
  openNowLabel,
  openNowTone,
} from '@/features/businesses/format';
import type { BusinessSummary } from '@/features/businesses/types';

import styles from './BusinessCard.module.css';

interface BusinessCardProps {
  business: BusinessSummary;
}

/**
 * One row in the public directory. The name is the link.
 *
 * The card carries no description on purpose: the list endpoint projects a summary without it,
 * and a page of cards is scannable precisely because each one is short. The open/closed badge is
 * tri-state, because a listing with no hours published is neither open nor closed and saying
 * otherwise would be a guess.
 */
export function BusinessCard({ business }: BusinessCardProps) {
  const tone = openNowTone(business.isOpenNow);

  return (
    <article className={styles.card}>
      <div className={styles.badges}>
        <p className={styles.category}>{categoryLabel(business.category)}</p>
        {business.isVerified ? <span className={styles.verified}>Verified</span> : null}
        <span className={`${styles.openBadge} ${styles[tone]}`}>
          {openNowLabel(business.isOpenNow)}
        </span>
      </div>

      <h3 className={styles.name}>
        <Link to={`/businesses/${business.slug}`} className={styles.link}>
          {business.name}
        </Link>
      </h3>

      <div className={styles.footer}>
        <span className={styles.meta}>
          {formatLocation(business.provinceName, business.districtName)}
        </span>

        {business.address ? <span className={styles.meta}>{business.address}</span> : null}

        {business.phone ? (
          <a className={styles.meta} href={`tel:${business.phone}`}>
            {formatPhone(business.phone)}
          </a>
        ) : null}
      </div>
    </article>
  );
}