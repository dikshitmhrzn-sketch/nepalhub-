import { useState } from 'react';

import { Button } from '@/components/ui/Button';
import { StateMessage } from '@/components/ui/StateMessage';
import {
  acceptContentClaim,
  releaseContentClaim,
} from '@/features/claims/api';
import {
  contentClaimKindLabels,
  contentClaimStatusLabels,
  type ContentClaimsOverview,
  type MyContentClaim,
  type PostedContentClaim,
} from '@/features/claims/types';
import { getErrorMessage } from '@/lib/apiErrors';

import styles from './AccountClaims.module.css';

interface AccountClaimsProps {
  overview: ContentClaimsOverview;
  onChanged: () => void;
}

/**
 * The account page's two claim boxes: what this person filed, and what others have filed
 * on posts they created. The poster's side is where accept and release live — the only
 * controls that decide whether a claimed post ever comes back.
 */
export function AccountClaims({ overview, onChanged }: AccountClaimsProps) {
  const [decisionError, setDecisionError] = useState<string | null>(null);
  const [pendingId, setPendingId] = useState<string | null>(null);

  async function decide(claim: PostedContentClaim, decision: 'accept' | 'release') {
    setPendingId(claim.id);
    setDecisionError(null);

    try {
      if (decision === 'accept') {
        await acceptContentClaim(claim.id, null);
      } else {
        await releaseContentClaim(claim.id, null);
      }

      onChanged();
    } catch (error) {
      setDecisionError(getErrorMessage(error, 'Could not save that decision. Please try again.'));
    } finally {
      setPendingId(null);
    }
  }

  return (
    <>
      <section className={styles.panel} aria-labelledby="my-claims-heading">
        <header className={styles.header}>
          <h2 className={styles.title} id="my-claims-heading">
            My claims
          </h2>
          <p className={styles.subtitle}>
            Posts you have claimed. They leave the public pages until the person who posted
            them decides.
          </p>
        </header>

        {overview.mine.length === 0 ? (
          <StateMessage
            tone="info"
            title="No claims yet"
            description="When you claim a job, offer or event, it appears here."
          />
        ) : (
          <ul className={styles.list}>
            {overview.mine.map((claim) => (
              <li key={claim.id} className={styles.row}>
                <div className={styles.rowMain}>
                  <p className={styles.rowTitle}>
                    <span className={styles.kind}>{contentClaimKindLabels[claim.kind]}</span>{' '}
                    {claim.title}
                  </p>
                  <p className={styles.rowMeta}>
                    <span className={statusClass(claim.status)}>
                      {contentClaimStatusLabels[claim.status]}
                    </span>
                    {' · '}
                    {formatDate(claim.createdAt)}
                  </p>
                  {claim.note ? <p className={styles.note}>{claim.note}</p> : null}
                  {claim.reviewNote ? (
                    <p className={styles.note}>Poster: {claim.reviewNote}</p>
                  ) : null}
                </div>
              </li>
            ))}
          </ul>
        )}
      </section>

      <section className={styles.panel} aria-labelledby="posted-claims-heading">
        <header className={styles.header}>
          <h2 className={styles.title} id="posted-claims-heading">
            Claims on my posts
          </h2>
          <p className={styles.subtitle}>
            Accept keeps the post hidden and acknowledges the claimant. Release puts it back
            on the public pages.
          </p>
        </header>

        {decisionError ? (
          <p className={styles.alert} role="alert">
            {decisionError}
          </p>
        ) : null}

        {overview.onMyPosts.length === 0 ? (
          <StateMessage
            tone="info"
            title="No claims on your posts"
            description="When someone claims a post you created, it appears here with their name."
          />
        ) : (
          <ul className={styles.list}>
            {overview.onMyPosts.map((claim) => (
              <li key={claim.id} className={styles.row}>
                <div className={styles.rowMain}>
                  <p className={styles.rowTitle}>
                    <span className={styles.kind}>{contentClaimKindLabels[claim.kind]}</span>{' '}
                    {claim.title}
                  </p>
                  <p className={styles.rowMeta}>
                    <span className={statusClass(claim.status)}>
                      {contentClaimStatusLabels[claim.status]}
                    </span>
                    {' · '}
                    {claim.claimantName}
                    {claim.claimantEmail ? ` · ${claim.claimantEmail}` : ''} ·{' '}
                    {formatDate(claim.createdAt)}
                  </p>
                  {claim.note ? <p className={styles.note}>{claim.note}</p> : null}
                </div>

                {claim.status === 'Pending' ? (
                  <div className={styles.actions}>
                    <Button
                      size="small"
                      disabled={pendingId === claim.id}
                      onClick={() => void decide(claim, 'accept')}
                    >
                      Accept
                    </Button>
                    <Button
                      variant="secondary"
                      size="small"
                      disabled={pendingId === claim.id}
                      onClick={() => void decide(claim, 'release')}
                    >
                      Release
                    </Button>
                  </div>
                ) : null}
              </li>
            ))}
          </ul>
        )}
      </section>
    </>
  );
}

function statusClass(status: MyContentClaim['status']): string {
  switch (status) {
    case 'Pending':
      return `${styles.status} ${styles.statusPending}`;
    case 'Accepted':
      return `${styles.status} ${styles.statusAccepted}`;
    default:
      return `${styles.status} ${styles.statusReleased}`;
  }
}

function formatDate(value: string): string {
  const date = new Date(value);

  return Number.isNaN(date.getTime())
    ? value
    : date.toLocaleDateString(undefined, { year: 'numeric', month: 'short', day: 'numeric' });
}
