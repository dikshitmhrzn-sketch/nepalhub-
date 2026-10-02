import { useState, type FormEvent } from 'react';
import { Link } from 'react-router';

import { EventEditor } from '@/components/events/EventEditor';
import { Button } from '@/components/ui/Button';
import { Container } from '@/components/ui/Container';
import { Pagination } from '@/components/ui/Pagination';
import { StateMessage } from '@/components/ui/StateMessage';
import { deleteEvent } from '@/features/events/api';
import { categoryLabel, describeStaffRow, formatEventDate } from '@/features/events/format';
import {
  adminFilterLabels,
  categoryLabels,
  eventAdminFilters,
  eventCategories,
  type EventAdminSummary,
} from '@/features/events/types';
import { useStaffEventList } from '@/features/events/useStaffEvents';
import { useReferenceData } from '@/features/reference/useReferenceData';
import { getErrorMessage } from '@/lib/apiErrors';

import styles from './EventsAdminPage.module.css';

const PAGE_SIZE = 20;

/**
 * Status is shown as one of three badges rather than published or not, because "live" is not the same
 * as "readable". A published event whose finishing time has passed is still in this board and is not
 * on the public page, and "published" alone would hide the one case a staff member is usually here to
 * look for.
 */
function StatusBadge({ event }: { event: EventAdminSummary }) {
  const state = describeStaffRow(event);

  return (
    <span
      className={`${styles.badge} ${
        state === 'Live'
          ? styles.badgePublished
          : state === 'Draft'
            ? styles.badgeDraft
            : styles.badgeFinished
      }`}
    >
      {state}
    </span>
  );
}

export function EventsAdminPage() {
  const [page, setPage] = useState(1);
  const [searchInput, setSearchInput] = useState('');
  const [search, setSearch] = useState('');
  const [status, setStatus] = useState('');
  const [category, setCategory] = useState('');
  const [province, setProvince] = useState('');
  const [editingId, setEditingId] = useState<string | null>(null);
  const [isCreating, setIsCreating] = useState(false);
  const [rowError, setRowError] = useState<{ id: string; message: string } | null>(null);
  const [notice, setNotice] = useState<string | null>(null);

  const reference = useReferenceData();
  const isEditing = isCreating || editingId !== null;

  const knownProvince = reference.data?.provinces.find((item) => item.code === province);

  const list = useStaffEventList({
    page,
    pageSize: PAGE_SIZE,
    search: search || undefined,
    province: knownProvince?.code,
    category: category ? (eventCategories.find((value) => value === category) ?? undefined) : undefined,
    status: status ? (eventAdminFilters.find((value) => value === status) ?? undefined) : undefined,
  });

  function handleSearchSubmit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    setPage(1);
    setSearch(searchInput.trim());
  }

  function closeEditor() {
    setEditingId(null);
    setIsCreating(false);
  }

  function handleSaved(saved: { id: string; title: string; isPublished: boolean }) {
    closeEditor();
    setNotice(
      saved.isPublished
        ? `Published “${saved.title}”.`
        : `Saved “${saved.title}” as a draft.`,
    );
    list.reload();
  }

  /**
   * Deletion is offered on drafts only, and the API refuses it on a published row as well. An event
   * people have already been shown is retired by unpublishing or by letting it finish, which keeps the
   * record of what was public rather than discarding it.
   */
  async function handleDelete(event: EventAdminSummary) {
    const confirmed = window.confirm(`Delete the draft “${event.title}”? This cannot be undone.`);

    if (!confirmed) {
      return;
    }

    setRowError(null);
    setNotice(null);

    try {
      await deleteEvent(event.id);
      setNotice(`Deleted the draft “${event.title}”.`);
      list.reload();
    } catch (error) {
      setRowError({
        id: event.id,
        message: getErrorMessage(error, 'The event could not be deleted.'),
      });
    }
  }

  return (
    <Container as="section" className={styles.page}>
      <header className={styles.header}>
        <div>
          <h1 className={styles.title}>Manage events</h1>
          <p className={styles.subtitle}>
            Fairs, festivals, training and gatherings, put on by our staff; there are no organiser
            accounts yet. A draft is invisible to the public. Publishing needs a title, a summary, a
            description, an organiser, a district and times that have not already passed, and the
            editor lists anything still missing. Times are Nepal Standard Time throughout. An event is
            retired by unpublishing or by letting it finish, never by deleting it, so the record of
            what was public is worth keeping.
          </p>
        </div>

        {!isEditing ? <Button onClick={() => setIsCreating(true)}>New event</Button> : null}
      </header>

      {notice ? (
        <p className={styles.notice} role="status">
          {notice}
        </p>
      ) : null}

      {isEditing ? (
        reference.status === 'success' ? (
          <EventEditor
            key={editingId ?? 'new'}
            id={editingId}
            reference={reference.data}
            onCancel={closeEditor}
            onSaved={handleSaved}
          />
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

      <div className={styles.searchRow}>
        <form className={styles.search} onSubmit={handleSearchSubmit} role="search">
          <div className={styles.searchField}>
            <label className={styles.label} htmlFor="admin-event-search">
              Search events
            </label>
            <input
              className={styles.input}
              id="admin-event-search"
              type="search"
              value={searchInput}
              placeholder="Title, organiser or venue…"
              onChange={(event) => setSearchInput(event.target.value)}
            />
          </div>

          <Button type="submit" variant="secondary" size="small">
            Search
          </Button>
        </form>

        <div className={styles.filters}>
          <div className={styles.filterField}>
            <label className={styles.label} htmlFor="admin-event-category">
              Category
            </label>
            <select
              className={styles.input}
              id="admin-event-category"
              value={category}
              onChange={(event) => {
                setCategory(event.target.value);
                setPage(1);
              }}
            >
              <option value="">All categories</option>
              {eventCategories.map((value) => (
                <option key={value} value={value}>
                  {categoryLabels[value]}
                </option>
              ))}
            </select>
          </div>

          <div className={styles.filterField}>
            <label className={styles.label} htmlFor="admin-event-province">
              Province
            </label>
            <select
              className={styles.input}
              id="admin-event-province"
              value={province}
              disabled={reference.status === 'loading'}
              onChange={(event) => {
                setProvince(event.target.value);
                setPage(1);
              }}
            >
              <option value="">All provinces</option>
              {(reference.data?.provinces ?? []).map((item) => (
                <option key={item.code} value={item.code}>
                  {item.name}
                </option>
              ))}
            </select>
          </div>

          <div className={styles.filterField}>
            <label className={styles.label} htmlFor="admin-event-status">
              Status
            </label>
            <select
              className={styles.input}
              id="admin-event-status"
              value={status}
              onChange={(event) => {
                setStatus(event.target.value);
                setPage(1);
              }}
            >
              {eventAdminFilters.map((value) => (
                <option key={value} value={value}>
                  {adminFilterLabels[value]}
                </option>
              ))}
            </select>
          </div>
        </div>
      </div>

      {list.status === 'loading' ? <StateMessage tone="loading" title="Loading the list…" /> : null}

      {list.status === 'error' ? (
        <StateMessage
          tone="error"
          title="Could not load the list"
          description={getErrorMessage(list.error, 'Please try again in a moment.')}
          action={
            <Button variant="secondary" size="small" onClick={list.reload}>
              Try again
            </Button>
          }
        />
      ) : null}

      {list.status === 'success' && list.data.items.length === 0 ? (
        <StateMessage
          tone="empty"
          title="No events here yet"
          description="Create the first one to get started."
        />
      ) : null}

      {list.status === 'success' && list.data.items.length > 0 ? (
        <>
          <ul className={styles.list}>
            {list.data.items.map((event) => (
              <li className={styles.row} key={event.id}>
                <div className={styles.rowMain}>
                  <p className={styles.rowName}>{event.title}</p>
                  <p className={styles.rowMeta}>
                    <code>{event.slug}</code> · {categoryLabel(event.category)} ·{' '}
                    {formatEventDate(event.startsAt)} · {event.districtName}
                    {event.createdByEmail ? ` · added by ${event.createdByEmail}` : ''}
                  </p>
                  {rowError?.id === event.id ? (
                    <p className={styles.rowError} role="alert">
                      {rowError.message}
                    </p>
                  ) : null}
                </div>

                <div className={styles.rowActions}>
                  <StatusBadge event={event} />

                  {event.isPublished && !event.isFinished ? (
                    <Link to={`/events/${event.slug}`} className={styles.viewLink}>
                      View
                    </Link>
                  ) : null}

                  <Button
                    variant="secondary"
                    size="small"
                    onClick={() => {
                      setIsCreating(false);
                      setEditingId(event.id);
                    }}
                  >
                    Edit
                  </Button>

                  {event.isPublished ? null : (
                    <Button
                      variant="ghost"
                      size="small"
                      onClick={() => void handleDelete(event)}
                    >
                      Delete
                    </Button>
                  )}
                </div>
              </li>
            ))}
          </ul>

          <Pagination
            page={list.data.page}
            totalPages={list.data.totalPages}
            totalCount={list.data.totalCount}
            label="events"
            onPageChange={setPage}
          />
        </>
      ) : null}
    </Container>
  );
}