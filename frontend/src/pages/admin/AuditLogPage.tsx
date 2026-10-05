import { useState, type FormEvent } from 'react';
import { useSearchParams } from 'react-router';

import { Button } from '@/components/ui/Button';
import { Container } from '@/components/ui/Container';
import { Pagination } from '@/components/ui/Pagination';
import { StateMessage } from '@/components/ui/StateMessage';
import { useAuditLog } from '@/features/audit/useAuditLog';
import type { AuditLogEntry } from '@/features/audit/types';
import { getErrorMessage } from '@/lib/apiErrors';

import styles from './AuditLogPage.module.css';

const PAGE_SIZE = 25;

/**
 * The trail of what administrators and moderators did to other accounts and reports.
 * Both filters are exact matches, because the question this screen answers is "what
 * happened to this subject" — a search box would promise fuzzy matching it cannot do.
 *
 * The before/after pair is shown as the API stored it, JSON and all: it is the record of
 * what changed, and prettifying it into friendlier prose would be a second version of the
 * truth nobody signed off on.
 */
export function AuditLogPage() {
  const [searchParams, setSearchParams] = useSearchParams();

  const page = Math.max(Number(searchParams.get('page') ?? '1') || 1, 1);
  const action = searchParams.get('action') ?? '';
  const targetType = searchParams.get('targetType') ?? '';

  const [actionInput, setActionInput] = useState(action);
  const [targetInput, setTargetInput] = useState(targetType);

  const log = useAuditLog({
    page,
    pageSize: PAGE_SIZE,
    action: action || undefined,
    targetType: targetType || undefined,
  });

  function updateParams(changes: Record<string, string | null>) {
    const next = new URLSearchParams(searchParams);

    for (const [key, value] of Object.entries(changes)) {
      if (value) {
        next.set(key, value);
      } else {
        next.delete(key);
      }
    }

    setSearchParams(next, { replace: true });
  }

  function handleFilterSubmit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    updateParams({
      page: null,
      action: actionInput.trim() || null,
      targetType: targetInput.trim() || null,
    });
  }

  function handleClear() {
    setActionInput('');
    setTargetInput('');
    updateParams({ page: null, action: null, targetType: null });
  }

  return (
    <Container as="section" className={styles.page}>
      <header className={styles.header}>
        <h1 className={styles.title}>Audit log</h1>
        <p className={styles.subtitle}>
          Who did what to which account or report, newest first. Entries are appended and never
          edited, so this page is a record rather than a screen anyone can change.
        </p>
      </header>

      <form className={styles.filters} onSubmit={handleFilterSubmit}>
        <label className={styles.field}>
          <span className={styles.filterLabel}>Action</span>
          <input
            className={styles.input}
            type="text"
            value={actionInput}
            placeholder="user.deactivated"
            onChange={(event) => setActionInput(event.target.value)}
          />
        </label>

        <label className={styles.field}>
          <span className={styles.filterLabel}>Subject</span>
          <input
            className={styles.input}
            type="text"
            value={targetInput}
            placeholder="report or user"
            onChange={(event) => setTargetInput(event.target.value)}
          />
        </label>

        <Button type="submit" size="small" variant="secondary">
          Filter
        </Button>
        <Button type="button" size="small" variant="ghost" onClick={handleClear}>
          Clear
        </Button>
      </form>

      {log.status === 'loading' ? <StateMessage tone="loading" title="Loading the log" /> : null}

      {log.status === 'error' ? (
        <StateMessage
          tone="error"
          title="Could not load the audit log"
          description={getErrorMessage(log.error, 'Please try again in a moment.')}
          action={
            <Button variant="secondary" size="small" onClick={log.reload}>
              Try again
            </Button>
          }
        />
      ) : null}

      {log.status === 'success' && log.data.items.length === 0 ? (
        <StateMessage
          tone="empty"
          title="Nothing matches that filter"
          description="Try a different action or subject, or clear the filters to see everything."
        />
      ) : null}

      {log.status === 'success' && log.data.items.length > 0 ? (
        <>
          <ul className={styles.list}>
            {log.data.items.map((entry) => (
              <AuditRow key={entry.id} entry={entry} />
            ))}
          </ul>

          <Pagination
            page={log.data.page}
            totalPages={log.data.totalPages}
            totalCount={log.data.totalCount}
            label="entries"
            onPageChange={(next) => updateParams({ page: String(next) })}
          />
        </>
      ) : null}
    </Container>
  );
}

/** The before/after pair, made readable when it is JSON and shown as-is when it is not. */
function pretty(value: string | null): string {
  if (!value) {
    return '—';
  }

  try {
    return JSON.stringify(JSON.parse(value), null, 2);
  } catch {
    return value;
  }
}

function AuditRow({ entry }: { entry: AuditLogEntry }) {
  const hasChange = Boolean(entry.before || entry.after);

  return (
    <li className={styles.row}>
      <div className={styles.head}>
        <code className={styles.action}>{entry.action}</code>
        <span className={styles.admin}>{entry.adminName ?? 'Unknown account'}</span>
        <span className={styles.when}>{new Date(entry.createdAt).toLocaleString()}</span>
      </div>

      <p className={styles.subject}>
        <span className={styles.subjectType}>{entry.targetType}</span>
        <code className={styles.subjectId}>{entry.targetId}</code>
        {entry.ip ? <span className={styles.ip}>from {entry.ip}</span> : null}
      </p>

      {hasChange ? (
        <details className={styles.change}>
          <summary>What changed</summary>
          <div className={styles.changeGrid}>
            <div>
              <p className={styles.changeLabel}>Before</p>
              <pre className={styles.changeValue}>{pretty(entry.before)}</pre>
            </div>
            <div>
              <p className={styles.changeLabel}>After</p>
              <pre className={styles.changeValue}>{pretty(entry.after)}</pre>
            </div>
          </div>
        </details>
      ) : null}
    </li>
  );
}
