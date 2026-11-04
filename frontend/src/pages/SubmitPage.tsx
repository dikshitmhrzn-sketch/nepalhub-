import { useState, type ReactNode } from 'react';
import { Link, useSearchParams } from 'react-router';

import { DealEditor } from '@/components/deals/DealEditor';
import { EventEditor } from '@/components/events/EventEditor';
import { JobEditor } from '@/components/jobs/JobEditor';
import { Button } from '@/components/ui/Button';
import { Container } from '@/components/ui/Container';
import { StateMessage } from '@/components/ui/StateMessage';
import { useAuth } from '@/features/auth/AuthContext';
import { useReferenceData } from '@/features/reference/useReferenceData';
import { resubmitSubmission } from '@/features/submissions/api';
import {
  submissionKindLabels,
  submissionStatusLabels,
  type SubmissionKind,
  type SubmissionView,
} from '@/features/submissions/types';
import { useMySubmissions } from '@/features/submissions/useMySubmissions';
import { getErrorMessage } from '@/lib/apiErrors';

import styles from './SubmitPage.module.css';

const composeKinds: SubmissionKind[] = ['Job', 'Event', 'Deal'];

function publicHref(submission: SubmissionView): string {
  const section = submission.kind === 'Job' ? 'jobs' : submission.kind === 'Event' ? 'events' : 'deals';
  return `/${section}/${encodeURIComponent(submission.slug)}`;
}

function statusBadgeClass(status: SubmissionView['status']): string {
  if (status === 'Pending') {
    return `${styles.badge} ${styles.badgeOpen}`;
  }

  if (status === 'Approved') {
    return `${styles.badge} ${styles.badgeApproved}`;
  }

  return `${styles.badge} ${styles.badgeRejected}`;
}

/**
 * The screen a member sends a job, an event or an offer from, and the history of everything
 * they have sent before it. A submission starts unpublished and stays that way until staff
 * approve it, so this page never offers a publish switch — it asks for review, and the row
 * below says where that request stands.
 *
 * The editor is the same form staff use, run through the submission endpoints instead: one
 * body, one queue, and a draft a moderator corrects rather than a second copy of the truth.
 * A turned-down draft can be corrected here and sent again, but only the button in its own
 * row reopens the request — saving a correction alone leaves the decision standing.
 */
export function SubmitPage() {
  const { status } = useAuth();

  if (status === 'anonymous') {
    return (
      <Container as="section" className={styles.page}>
        <StateMessage
          tone="error"
          title="Sign in to send something in"
          description="Anyone with an account can send a job, an event or an offer for review. It reaches the public site once staff approve it."
          action={
            <Link to="/login" className={styles.actionLink}>
              Sign in
            </Link>
          }
        />
      </Container>
    );
  }

  return <SubmitView />;
}

function SubmitView() {
  const [searchParams, setSearchParams] = useSearchParams();
  const editId = searchParams.get('edit');

  const list = useMySubmissions();
  const reference = useReferenceData();

  const [composeKind, setComposeKind] = useState<SubmissionKind>('Job');
  const [editing, setEditing] = useState<{ id: string; kind: SubmissionKind } | null>(null);
  const [formNonce, setFormNonce] = useState(0);
  const [notice, setNotice] = useState<string | null>(null);
  const [rowError, setRowError] = useState<{ id: string; message: string } | null>(null);
  const [busyId, setBusyId] = useState<string | null>(null);

  /*
    The kind comes from the loaded list when the page is opened straight at an edit link,
    and from memory while the list is still reloading — the editor needs to know which of
    the three bodies it is about to build before any of that arrives.
  */
  const editSubmission = editId && list.data ? (list.data.find((row) => row.id === editId) ?? null) : null;
  const editKind = editId
    ? editing?.id === editId
      ? (editing.kind ?? null)
      : (editSubmission?.kind ?? null)
    : null;

  function selectKind(next: SubmissionKind) {
    if (next === composeKind) {
      return;
    }

    const confirmed = window.confirm(
      `Switch to ${submissionKindLabels[next].toLowerCase()}? The form below has not been sent yet and will be cleared.`,
    );

    if (confirmed) {
      setComposeKind(next);
      setFormNonce((current) => current + 1);
    }
  }

  function openEditor(submission: SubmissionView) {
    setRowError(null);
    setNotice(null);
    setEditing({ id: submission.id, kind: submission.kind });

    const params = new URLSearchParams(searchParams);
    params.set('edit', submission.id);
    setSearchParams(params);
  }

  function closeEditor() {
    setEditing(null);

    if (editId) {
      const params = new URLSearchParams(searchParams);
      params.delete('edit');
      setSearchParams(params, { replace: true });
    } else {
      setFormNonce((current) => current + 1);
    }
  }

  function handleSaved(saved: {
    id: string;
    title: string;
    isPublished: boolean;
    submissionId?: string;
  }) {
    list.reload();

    const submissionId = saved.submissionId ?? editId;

    if (submissionId) {
      setEditing({ id: submissionId, kind: editKind ?? composeKind });
    }

    if (editId) {
      setNotice(`Saved your changes to “${saved.title}”.`);
      return;
    }

    setNotice(
      `“${saved.title}” is with staff. It stays off the public site until they approve it, and you can still correct it below.`,
    );

    if (submissionId) {
      setSearchParams({ edit: submissionId }, { replace: true });
    }
  }

  async function handleResubmit(submission: SubmissionView) {
    setBusyId(submission.id);
    setRowError(null);

    try {
      const saved = await resubmitSubmission(submission.id);
      setNotice(`“${saved.title}” is back in the staff queue.`);
      list.reload();
    } catch (error) {
      setRowError({
        id: submission.id,
        message: getErrorMessage(error, 'Could not send it again. Please try later.'),
      });
    } finally {
      setBusyId(null);
    }
  }

  function renderEditor(kind: SubmissionKind | null): ReactNode {
    if (reference.status === 'error') {
      return (
        <StateMessage
          tone="error"
          title="Could not load the location lists"
          description={getErrorMessage(reference.error, 'The form needs them to set a district.')}
          action={
            <Button variant="secondary" size="small" onClick={reference.reload}>
              Try again
            </Button>
          }
        />
      );
    }

    if (reference.status !== 'success') {
      return <StateMessage tone="loading" title="Loading the form…" />;
    }

    const shared = {
      id: null,
      reference: reference.data,
      onCancel: closeEditor,
      onSaved: handleSaved,
      draft: { submissionId: editId },
    };

    if (kind === 'Event') {
      return <EventEditor key={editId ?? `new-event-${formNonce}`} {...shared} />;
    }

    if (kind === 'Deal') {
      return <DealEditor key={editId ?? `new-deal-${formNonce}`} {...shared} />;
    }

    return <JobEditor key={editId ?? `new-job-${formNonce}`} {...shared} />;
  }

  let formBlock: ReactNode;

  if (editId && !editKind) {
    if (list.status === 'loading') {
      formBlock = <StateMessage tone="loading" title="Loading your submissions…" />;
    } else if (list.status === 'error') {
      formBlock = (
        <StateMessage
          tone="error"
          title="Could not load your submissions"
          description={getErrorMessage(list.error, 'Please try again in a moment.')}
          action={
            <Button variant="secondary" size="small" onClick={list.reload}>
              Try again
            </Button>
          }
        />
      );
    } else {
      formBlock = (
        <StateMessage
          tone="empty"
          title="That submission is not here"
          description="It may have been decided already, or it belongs to another account. Everything of yours is listed below."
          action={
            <Button variant="secondary" size="small" onClick={closeEditor}>
              Back to the form
            </Button>
          }
        />
      );
    }
  } else if (editId && editSubmission?.status === 'Rejected') {
    formBlock = (
      <>
        <p className={styles.reviewNote}>
          {editSubmission.reviewNote
            ? `Staff turned this down: “${editSubmission.reviewNote}”`
            : 'Staff turned this one down without a note.'}
        </p>
        <p className={styles.reviewHint}>
          Correct it here, save, then press <strong>Send again</strong> on its row below to put it
          back in the queue.
        </p>
        {renderEditor(editKind)}
      </>
    );
  } else {
    formBlock = renderEditor(editKind ?? composeKind);
  }

  return (
    <Container as="section" className={styles.page}>
      <header className={styles.header}>
        <h1 className={styles.title}>Send something in</h1>
        <p className={styles.subtitle}>
          A job, an event or an offer you would like on Nepal Hub. It is written as a draft and
          stays invisible to readers until staff approve it — you can correct it while it waits,
          and the list below is where every request you have made can be followed.
        </p>
      </header>

      {notice ? (
        <p className={styles.notice} role="status">
          {notice}
        </p>
      ) : null}

      <section className={styles.section}>
        <h2 className={styles.sectionTitle}>{editId ? 'Correct a draft' : 'Write it'}</h2>

        {!editId ? (
          <>
            <div className={styles.tabs} role="group" aria-label="What are you sending">
              {composeKinds.map((value) => (
                <button
                  key={value}
                  type="button"
                  className={value === composeKind ? `${styles.tab} ${styles.tabActive}` : styles.tab}
                  aria-pressed={value === composeKind}
                  onClick={() => selectKind(value)}
                >
                  {submissionKindLabels[value]}
                </button>
              ))}
            </div>
            <p className={styles.tabHint}>
              Whichever you choose, it is sent for review rather than published — the form says
              as much before it goes.
            </p>
          </>
        ) : null}

        {formBlock}
      </section>

      <section className={styles.section}>
        <h2 className={styles.sectionTitle}>What you have sent in</h2>

        {list.status === 'loading' ? (
          <StateMessage tone="loading" title="Loading what you have sent…" />
        ) : null}

        {list.status === 'error' ? (
          <StateMessage
            tone="error"
            title="Could not load your submissions"
            description={getErrorMessage(list.error, 'Please try again in a moment.')}
            action={
              <Button variant="secondary" size="small" onClick={list.reload}>
                Try again
              </Button>
            }
          />
        ) : null}

        {list.status === 'success' && list.data.length === 0 ? (
          <p className={styles.empty}>
            Nothing yet. Everything you send appears here with its standing: waiting for staff,
            approved and live, or turned down with the reason — and a turned-down one can be
            corrected and sent again.
          </p>
        ) : null}

        {list.status === 'success' && list.data.length > 0 ? (
          <ul className={styles.rows}>
            {list.data.map((submission) => (
              <li key={submission.id} className={styles.row}>
                <div className={styles.rowMain}>
                  <p className={styles.rowName}>
                    {submission.isPublished ? (
                      <Link to={publicHref(submission)} className={styles.rowLink}>
                        {submission.title}
                      </Link>
                    ) : (
                      submission.title
                    )}
                  </p>
                  <p className={styles.rowMeta}>
                    {submissionKindLabels[submission.kind]} · sent{' '}
                    {new Date(submission.createdAt).toLocaleDateString()}
                    {submission.reviewedAt
                      ? ` · decided ${new Date(submission.reviewedAt).toLocaleDateString()}`
                      : ''}
                    {submission.reviewNote ? ` — “${submission.reviewNote}”` : ''}
                  </p>
                  {rowError?.id === submission.id ? (
                    <p className={styles.error} role="alert">
                      {rowError.message}
                    </p>
                  ) : null}
                </div>

                <span className={statusBadgeClass(submission.status)}>
                  {submissionStatusLabels[submission.status]}
                </span>

                <div className={styles.rowActions}>
                  {submission.status !== 'Approved' ? (
                    <Button
                      variant="secondary"
                      size="small"
                      onClick={() => openEditor(submission)}
                    >
                      Edit
                    </Button>
                  ) : null}

                  {submission.status === 'Rejected' ? (
                    <Button
                      variant="ghost"
                      size="small"
                      disabled={busyId === submission.id}
                      onClick={() => void handleResubmit(submission)}
                    >
                      {busyId === submission.id ? 'Sending…' : 'Send again'}
                    </Button>
                  ) : null}

                  {submission.isPublished ? (
                    <Link to={publicHref(submission)} className={styles.rowAction}>
                      View
                    </Link>
                  ) : null}
                </div>
              </li>
            ))}
          </ul>
        ) : null}
      </section>
    </Container>
  );
}
