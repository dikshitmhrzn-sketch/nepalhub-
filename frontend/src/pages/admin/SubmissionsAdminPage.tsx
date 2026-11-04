import { useState } from 'react';
import { useSearchParams } from 'react-router';

import { DealEditor } from '@/components/deals/DealEditor';
import { EventEditor } from '@/components/events/EventEditor';
import { JobEditor } from '@/components/jobs/JobEditor';
import { SubmissionQueueCard } from '@/components/submissions/SubmissionQueueCard';
import { Button } from '@/components/ui/Button';
import { Container } from '@/components/ui/Container';
import { Pagination } from '@/components/ui/Pagination';
import { StateMessage } from '@/components/ui/StateMessage';
import { useReferenceData } from '@/features/reference/useReferenceData';
import {
  submissionKindFilters,
  submissionKindFilterLabels,
  submissionStatusFilters,
  submissionStatusFilterLabels,
  type SubmissionKindFilter,
  type SubmissionKind,
  type SubmissionStatusFilter,
} from '@/features/submissions/types';
import { useSubmissions } from '@/features/submissions/useSubmissions';
import { getErrorMessage } from '@/lib/apiErrors';

import styles from './SubmissionsAdminPage.module.css';

const PAGE_SIZE = 20;

/**
 * The queue for drafts members sent in: two windows over the same rows — status and kind —
 * because three sorts of content share one table and a moderator usually arrives looking
 * for one of them.
 *
 * Edit opens the ordinary staff editor for the draft behind the row, not a queue-shaped
 * twin of it: a queued submission is an unpublished row, so the same form that corrects a
 * staff draft corrects this one, and what comes back is the row the queue reads. Approving
 * is the moment the draft goes public, so a draft failing the publish rules answers on its
 * own card with the list of what is missing.
 */
export function SubmissionsAdminPage() {
  const [searchParams, setSearchParams] = useSearchParams();
  const [editing, setEditing] = useState<{ id: string; kind: SubmissionKind } | null>(null);
  const [notice, setNotice] = useState<string | null>(null);

  const reference = useReferenceData();

  const page = Math.max(Number(searchParams.get('page') ?? '1') || 1, 1);
  const requestedStatus = searchParams.get('status');
  const status = submissionStatusFilters.find((value) => value === requestedStatus) ?? 'all';
  const requestedKind = searchParams.get('kind');
  const kind = submissionKindFilters.find((value) => value === requestedKind) ?? 'all';

  const queue = useSubmissions({ page, pageSize: PAGE_SIZE, status, kind });

  function selectStatus(next: SubmissionStatusFilter) {
    const params = new URLSearchParams(searchParams);

    if (next === 'all') {
      params.delete('status');
    } else {
      params.set('status', next);
    }

    params.delete('page');
    setSearchParams(params, { replace: true });
  }

  function selectKind(next: SubmissionKindFilter) {
    const params = new URLSearchParams(searchParams);

    if (next === 'all') {
      params.delete('kind');
    } else {
      params.set('kind', next);
    }

    params.delete('page');
    setSearchParams(params, { replace: true });
  }

  function goToPage(next: number) {
    const params = new URLSearchParams(searchParams);
    params.set('page', String(next));
    setSearchParams(params, { replace: true });
  }

  function closeEditor() {
    setEditing(null);
  }

  function handleSaved(saved: { id: string; title: string; isPublished: boolean }) {
    closeEditor();
    setNotice(
      saved.isPublished
        ? `Published “${saved.title}”.`
        : `Saved “${saved.title}”. It is still unpublished, so it reaches readers when the queue approves it.`,
    );
    queue.reload();
  }

  return (
    <Container as="section" className={styles.page}>
      <header className={styles.header}>
        <h1 className={styles.title}>Submission queue</h1>
        <p className={styles.subtitle}>
          Jobs, events and offers members sent in for review, open work at the top. Approving
          publishes the draft when it satisfies the publish rules, turning it down leaves it with
          the author so they can correct it and send it again, and Edit opens the same editor
          staff use for a row of their own.
        </p>
      </header>

      {notice ? (
        <p className={styles.notice} role="status">
          {notice}
        </p>
      ) : null}

      {editing ? (
        reference.status === 'success' ? (
          editing.kind === 'Event' ? (
            <EventEditor
              key={editing.id}
              id={editing.id}
              reference={reference.data}
              onCancel={closeEditor}
              onSaved={handleSaved}
            />
          ) : editing.kind === 'Deal' ? (
            <DealEditor
              key={editing.id}
              id={editing.id}
              reference={reference.data}
              onCancel={closeEditor}
              onSaved={handleSaved}
            />
          ) : (
            <JobEditor
              key={editing.id}
              id={editing.id}
              reference={reference.data}
              onCancel={closeEditor}
              onSaved={handleSaved}
            />
          )
        ) : reference.status === 'error' ? (
          <StateMessage
            tone="error"
            title="Could not load the location lists"
            description={getErrorMessage(reference.error, 'The editor needs them to set a district.')}
            action={
              <Button variant="secondary" size="small" onClick={reference.reload}>
                Try again
              </Button>
            }
          />
        ) : (
          <StateMessage tone="loading" title="Loading the editor…" />
        )
      ) : null}

      <div className={styles.tabGroups}>
        <div className={styles.tabs} role="group" aria-label="Submission status">
          {submissionStatusFilters.map((value) => (
            <button
              key={value}
              type="button"
              className={value === status ? `${styles.tab} ${styles.tabActive}` : styles.tab}
              aria-pressed={value === status}
              onClick={() => selectStatus(value)}
            >
              {submissionStatusFilterLabels[value]}
            </button>
          ))}
        </div>

        <div className={styles.tabs} role="group" aria-label="Submission kind">
          {submissionKindFilters.map((value) => (
            <button
              key={value}
              type="button"
              className={value === kind ? `${styles.tab} ${styles.tabActive}` : styles.tab}
              aria-pressed={value === kind}
              onClick={() => selectKind(value)}
            >
              {submissionKindFilterLabels[value]}
            </button>
          ))}
        </div>
      </div>

      {queue.status === 'loading' ? (
        <StateMessage tone="loading" title="Loading the queue" />
      ) : null}

      {queue.status === 'error' ? (
        <StateMessage
          tone="error"
          title="Could not load the queue"
          description={getErrorMessage(queue.error, 'Please try again in a moment.')}
          action={
            <Button variant="secondary" size="small" onClick={queue.reload}>
              Try again
            </Button>
          }
        />
      ) : null}

      {queue.status === 'success' && queue.data.items.length === 0 ? (
        <StateMessage
          tone="empty"
          title="No submissions here"
          description="Nothing matches these windows. Try another one, or check back when a member sends something in."
        />
      ) : null}

      {queue.status === 'success' && queue.data.items.length > 0 ? (
        <>
          <ul className={styles.list}>
            {queue.data.items.map((row) => (
              <SubmissionQueueCard
                key={row.id}
                row={row}
                onChanged={queue.reload}
                onEdit={(target) => {
                  setNotice(null);
                  setEditing({ id: target.itemId, kind: target.kind });
                }}
              />
            ))}
          </ul>

          <Pagination
            page={queue.data.page}
            totalPages={queue.data.totalPages}
            totalCount={queue.data.totalCount}
            label="submissions"
            onPageChange={goToPage}
          />
        </>
      ) : null}
    </Container>
  );
}
