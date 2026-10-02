import { useState, type FormEvent } from 'react';
import { Link } from 'react-router';

import { DealEditor } from '@/components/deals/DealEditor';
import { Button } from '@/components/ui/Button';
import { Container } from '@/components/ui/Container';
import { Pagination } from '@/components/ui/Pagination';
import { StateMessage } from '@/components/ui/StateMessage';
import { deleteDeal } from '@/features/deals/api';
import { describeStaffRow, formatNepalDate } from '@/features/deals/format';
import { adminFilterLabels, dealAdminFilters, type DealAdminSummary } from '@/features/deals/types';
import { useStaffDealList } from '@/features/deals/useStaffDeals';
import { useReferenceData } from '@/features/reference/useReferenceData';
import { getErrorMessage } from '@/lib/apiErrors';

import styles from './DealsAdminPage.module.css';

const PAGE_SIZE = 20;

/**
 * Status is shown as one of three badges rather than published or not, because "live" is not the
 * same as "readable". A published offer whose end date has passed is still in this board and is not
 * on the public page, and "published" alone would hide the one case a staff member is usually here to
 * look for.
 */
function StatusBadge({ deal }: { deal: DealAdminSummary }) {
  const state = describeStaffRow(deal);

  return (
    <span
      className={`${styles.badge} ${
        state === 'Live' ? styles.badgePublished : state === 'Draft' ? styles.badgeDraft : styles.badgeExpired
      }`}
    >
      {state}
    </span>
  );
}

export function DealsAdminPage() {
  const [page, setPage] = useState(1);
  const [searchInput, setSearchInput] = useState('');
  const [search, setSearch] = useState('');
  const [status, setStatus] = useState('');
  const [province, setProvince] = useState('');
  const [editingId, setEditingId] = useState<string | null>(null);
  const [isCreating, setIsCreating] = useState(false);
  const [rowError, setRowError] = useState<{ id: string; message: string } | null>(null);
  const [notice, setNotice] = useState<string | null>(null);

  const reference = useReferenceData();
  const isEditing = isCreating || editingId !== null;

  const knownProvince = reference.data?.provinces.find((item) => item.code === province);

  const list = useStaffDealList({
    page,
    pageSize: PAGE_SIZE,
    search: search || undefined,
    province: knownProvince?.code,
    status: status ? (dealAdminFilters.find((value) => value === status) ?? undefined) : undefined,
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
   * Deletion is offered on drafts only, and the API refuses it on a published row as well. An offer
   * people have already been shown is retired by unpublishing or by letting its end date pass, which
   * keeps the record of what was public rather than discarding it.
   */
  async function handleDelete(deal: DealAdminSummary) {
    const confirmed = window.confirm(`Delete the draft “${deal.title}”? This cannot be undone.`);

    if (!confirmed) {
      return;
    }

    setRowError(null);
    setNotice(null);

    try {
      await deleteDeal(deal.id);
      setNotice(`Deleted the draft “${deal.title}”.`);
      list.reload();
    } catch (error) {
      setRowError({
        id: deal.id,
        message: getErrorMessage(error, 'The offer could not be deleted.'),
      });
    }
  }

  return (
    <Container as="section" className={styles.page}>
      <header className={styles.header}>
        <div>
          <h1 className={styles.title}>Manage offers</h1>
          <p className={styles.subtitle}>
            Discounts and offers, written by our staff; there are no business accounts yet. A draft is
            invisible to the public. Publishing needs a title, a summary, a discount line, a
            description and end dates, and the editor lists anything still missing. An offer is
            retired by unpublishing or by letting its end date pass, never by deleting it, so the
            record of what was public is worth keeping.
          </p>
        </div>

        {!isEditing ? <Button onClick={() => setIsCreating(true)}>New offer</Button> : null}
      </header>

      {notice ? (
        <p className={styles.notice} role="status">
          {notice}
        </p>
      ) : null}

      {isEditing ? (
        reference.status === 'success' ? (
          <DealEditor
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
            <label className={styles.label} htmlFor="admin-deal-search">
              Search offers
            </label>
            <input
              className={styles.input}
              id="admin-deal-search"
              type="search"
              value={searchInput}
              placeholder="Title, discount or detail…"
              onChange={(event) => setSearchInput(event.target.value)}
            />
          </div>

          <Button type="submit" variant="secondary" size="small">
            Search
          </Button>
        </form>

        <div className={styles.filters}>
          <div className={styles.filterField}>
            <label className={styles.label} htmlFor="admin-deal-status">
              Status
            </label>
            <select
              className={styles.input}
              id="admin-deal-status"
              value={status}
              onChange={(event) => {
                setStatus(event.target.value);
                setPage(1);
              }}
            >
              {dealAdminFilters.map((value) => (
                <option key={value} value={value}>
                  {adminFilterLabels[value]}
                </option>
              ))}
            </select>
          </div>

          <div className={styles.filterField}>
            <label className={styles.label} htmlFor="admin-deal-province">
              Province
            </label>
            <select
              className={styles.input}
              id="admin-deal-province"
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
          title="No offers here yet"
          description="Create the first one to get started."
        />
      ) : null}

      {list.status === 'success' && list.data.items.length > 0 ? (
        <>
          <ul className={styles.list}>
            {list.data.items.map((deal) => (
              <li className={styles.row} key={deal.id}>
                <div className={styles.rowMain}>
                  <p className={styles.rowName}>{deal.title}</p>
                  <p className={styles.rowMeta}>
                    <code>{deal.slug}</code> · {deal.districtName ?? 'Nationwide'} · ends{' '}
                    {formatNepalDate(deal.endsOn)}
                    {deal.businessName ? ` · ${deal.businessName}` : ''}
                    {deal.createdByEmail ? ` · added by ${deal.createdByEmail}` : ''}
                  </p>
                  {rowError?.id === deal.id ? (
                    <p className={styles.rowError} role="alert">
                      {rowError.message}
                    </p>
                  ) : null}
                </div>

                <div className={styles.rowActions}>
                  <StatusBadge deal={deal} />

                  {deal.isPublished ? (
                    <Link to={`/deals/${deal.slug}`} className={styles.viewLink}>
                      View
                    </Link>
                  ) : null}

                  <Button
                    variant="secondary"
                    size="small"
                    onClick={() => {
                      setIsCreating(false);
                      setEditingId(deal.id);
                    }}
                  >
                    Edit
                  </Button>

                  {deal.isPublished ? null : (
                    <Button
                      variant="ghost"
                      size="small"
                      onClick={() => void handleDelete(deal)}
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
            label="offers"
            onPageChange={setPage}
          />
        </>
      ) : null}
    </Container>
  );
}