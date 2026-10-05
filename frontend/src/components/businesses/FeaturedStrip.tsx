import { Link } from 'react-router';

import { categoryLabel } from '@/features/businesses/format';
import { useFeaturedBusinesses } from '@/features/businesses/useBusinesses';

import styles from './FeaturedStrip.module.css';

/**
 * The labelled strip above the directory: up to six listings whose featured window covers
 * today, alphabetical so nobody bought a ranking. The label is part of the design — a
 * reader should see that these are paid placements rather than staff picks.
 *
 * An empty strip, or one that could not be fetched, renders nothing at all: the directory
 * below must not go down, or even gain an empty shelf, because the advert did.
 */
export function FeaturedStrip() {
  const featured = useFeaturedBusinesses();

  if (featured.status !== 'success' || featured.data.length === 0) {
    return null;
  }

  return (
    <section className={styles.strip} aria-label="Featured listings">
      <div className={styles.head}>
        <h2 className={styles.title}>Featured listings</h2>
        <p className={styles.note}>Paid placements, shown alphabetically.</p>
      </div>

      <ul className={styles.list}>
        {featured.data.map((business) => (
          <li key={business.id}>
            <Link
              to={`/businesses/${encodeURIComponent(business.slug)}`}
              className={styles.card}
            >
              <span className={styles.badge}>Featured</span>
              <span className={styles.name}>{business.name}</span>
              <span className={styles.meta}>
                {categoryLabel(business.category)} · {business.districtName}
              </span>
            </Link>
          </li>
        ))}
      </ul>
    </section>
  );
}
