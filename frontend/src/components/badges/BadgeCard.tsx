import {
  badgeIcon,
  formatAwardedAt,
  progressLabel,
  progressPercent,
} from '@/features/badges/format';
import type { AccountBadge } from '@/features/badges/types';

import styles from './BadgeCard.module.css';

export interface BadgeCardProps {
  badge: AccountBadge;
}

/**
 * One badge on the account page.
 *
 * A locked badge is shown as plainly as an earned one rather than greyed out or hidden. Someone
 * who has finished one quiz should be able to see that there is a seventh-day streak to reach,
 * because a badge the account page does not mention is a badge this site does not have.
 *
 * The progress meter is decorative: the same number is printed beside it as text, and the count
 * it is showing is already stated in the description above, so the bar is a second reading of a
 * fact rather than the only one.
 */
export function BadgeCard({ badge }: BadgeCardProps) {
  const awardedOn = formatAwardedAt(badge.awardedAt);

  return (
    <article className={styles.card} data-awarded={badge.awarded}>
      <div className={styles.header}>
        <span className={styles.icon} aria-hidden="true">
          {badgeIcon(badge.icon)}
        </span>

        <h3 className={styles.name}>{badge.name}</h3>

        {badge.awarded ? <span className={styles.earnedTag}>Earned</span> : null}
      </div>

      <p className={styles.description}>{badge.description}</p>

      <footer className={styles.footer}>
        <div
          className={styles.meter}
          role="img"
          aria-label={`${badge.name}: ${progressLabel(badge)}`}
        >
          <span className={styles.fill} style={{ width: `${progressPercent(badge)}%` }} />
        </div>

        <p className={styles.progress}>
          {progressLabel(badge)}
          {awardedOn ? ` · ${awardedOn}` : ''}
        </p>
      </footer>
    </article>
  );
}
