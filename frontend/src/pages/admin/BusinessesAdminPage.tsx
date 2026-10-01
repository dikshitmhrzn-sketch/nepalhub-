import { useState, type FormEvent } from 'react';
import { Link } from 'react-router';

import { BusinessEditor } from '@/components/businesses/BusinessEditor';
import { Button } from '@/components/ui/Button';
import { Container } from '@/components/ui/Container';
import { Pagination } from '@/components/ui/Pagination';
import { StateMessage } from '@/components/ui/StateMessage';
import { deleteBusiness } from '@/features/businesses/api';
import { categoryLabel } from '@/features/businesses/format';
import {
  adminFilterLabels,
  businessAdminFilters,
  businessCategories,
  categoryLabels,
  type BusinessAdminSummary,
} from '@/features/businesses/types';
import { useStaffBusinessList } from '@/features/businesses/useStaffBusinesses';
import { useReferenceData } from '@/features/reference/useReferenceData';
import { getErrorMessage } from '@/lib/apiErrors';

import styles from './BusinessesAdminPage.module.css';

const PAGE_SIZE = 20;

/**
 * Publication and verification are shown as two badges rather than one status, because they are
 * two independent facts and the interesting row is the one where they disagree: a live listing
 * that has lost its badge, which is allowed so a wrong detail can be corrected without pulling the
 * listing off the directory.
 */
function StatusBadge({ business }: { business: BusinessAdminSummary }) {
  return (
    <>
      <span
        className={`${styles.badge} ${
          business.isPublished ? styles.badgePublished : styles.badgeDraft
        }`}
      >
        {business.isPublished ? 'Live' : 'Draft'}
      </span>

      {business.isVerified ? null : (
        <span className={`${styles.badge} ${styles.badgeUnverified}`}>Not checked</span>
      )}
    </>
  );
}

export function BusinessesAdminPage() {
  const [page, setPage] = useState(1);
  const [searchInput, setSearchInput] = useState('');
  const [search, setSearch] = useState('');
  const [category, setCategory] = useState('');
  const [status, setStatus] = useState('');
  const [verified, setVerified] = useState('');
  const [editingId, setEditingId] = useState<string | null>(null);
  const [isCreating, setIsCreating] = useState(false);
  const [rowError, setRowError] = useState<{ id: string; message: string } | null>(null);
  const [notice, setNotice] = useState<string | null>(null);

  const reference = useReferenceData();
  const isEditing = isCreating || editingId !== null;

  const list = useStaffBusinessList({
    page,
    pageSize: PAGE_SIZE,
    search: search || undefined,
    category: category
      ? (businessCategories.find((value) => value === category) ?? undefined)
      : undefined,
    status: status ? (businessAdminFilters.find((value) => value === status) ?? undefined) : undefined,
    verified: verified === 'true' ? true : verified === 'false' ? false : undefined,
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

  function handleSaved(saved: { id: string; name: string; isPublished: boolean }) {
    closeEditor();
    setNotice(
      saved.isPublished
        ? `Published “${saved.name}”.`
        : `Saved “${saved.name}” as a draft.`,
    );
    list.reload();
  }

  /**
   * Deletion is offered on drafts only, and the API refuses it on a published row as well. A
   * listing people have already been shown is retracted by unpublishing, which keeps the record of
   * what was public rather than discarding it.
   */
  async function handleDelete(business: BusinessAdminSummary) {
    const confirmed = window.confirm(
      `Delete the draft “${business.name}”? This cannot be undone.`,
    );

    if (!confirmed) {
      return;
    }

    setRowError(null);
    setNotice(null);

    try {
      await deleteBusiness(business.id);
      setNotice(`Deleted the draft “${business.name}”.`);
      list.reload();
    } catch (error) {
      setRowError({
        id: business.id,
        message: getErrorMessage(error, 'The listing could not be deleted.'),
      });
    }
  }

  return (
    <Container as="section" className={styles.page}>
      <header className={styles.header}>
        <div>
          <h1 className={styles.title}>Manage businesses</h1>
          <p className={styles.subtitle}>
            Every listing here is written by our staff; there are no business accounts yet. A draft
            is invisible to the public. Publishing needs a name, a district, a description and a
            phone number, and the editor lists anything still missing. A live listing is retracted by
            unpublishing rather than deleted, so the record of what was public is worth keeping.
          </p>
        </div>

        {!isEditing ? <Button onClick={() => setIsCreating(true)}>New listing</Button> : null}
      </header>

      {notice ? (
        <p className={styles.notice} role="status">
          {notice}
        </p>
      ) : null}

      {isEditing ? (
        reference.status === 'success' ? (
          <BusinessEditor
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
            <label className={styles.label} htmlFor="admin-business-search">
              Search listings
            </label>
            <input
              className={styles.input}
              id="admin-business-search"
              type="search"
              value={searchInput}
              placeholder="Name, service or place…"
              onChange={(event) => setSearchInput(event.target.value)}
            />
          </div>

          <Button type="submit" variant="secondary" size="small">
            Search
          </Button>
        </form>

        <div className={styles.filters}>
          <div className={styles.filterField}>
            <label className={styles.label} htmlFor="admin-business-category">
              Category
            </label>
            <select
              className={styles.input}
              id="admin-business-category"
              value={category}
              onChange={(event) => {
                setCategory(event.target.value);
                setPage(1);
              }}
            >
              <option value="">All categories</option>
              {businessCategories.map((value) => (
                <option key={value} value={value}>
                  {categoryLabels[value]}
                </option>
              ))}
            </select>
          </div>

          <div className={styles.filterField}>
            <label className={styles.label} htmlFor="admin-business-status">
              Status
            </label>
            <select
              className={styles.input}
              id="admin-business-status"
              value={status}
              onChange={(event) => {
                setStatus(event.target.value);
                setPage(1);
              }}
            >
              {businessAdminFilters.map((value) => (
                <option key={value} value={value}>
                  {adminFilterLabels[value]}
                </option>
              ))}
            </select>
          </div>

          <div className={styles.filterField}>
            <label className={styles.label} htmlFor="admin-business-verified">
              Checked
            </label>
            <select
              className={styles.input}
              id="admin-business-verified"
              value={verified}
              onChange={(event) => {
                setVerified(event.target.value);
                setPage(1);
              }}
            >
              <option value="">Any listing</option>
              <option value="true">Checked only</option>
              <option value="false">Not yet checked</option>
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
          title="No listings here yet"
          description="Create the first one to get started."
        />
      ) : null}

      {list.status === 'success' && list.data.items.length > 0 ? (
        <>
          <ul className={styles.list}>
            {list.data.items.map((business) => (
              <li className={styles.row} key={business.id}>
                <div className={styles.rowMain}>
                  <p className={styles.rowName}>{business.name}</p>
                  <p className={styles.rowMeta}>
                    <code>{business.slug}</code> · {categoryLabel(business.category)} ·{' '}
                    {business.districtName}
                    {business.createdByEmail ? ` · added by ${business.createdByEmail}` : ''}
                  </p>
                  {rowError?.id === business.id ? (
                    <p className={styles.rowError} role="alert">
                      {rowError.message}
                    </p>
                  ) : null}
                </div>

                <div className={styles.rowActions}>
                  <StatusBadge business={business} />

                  {business.isPublished ? (
                    <Link to={`/businesses/${business.slug}`} className={styles.viewLink}>
                      View
                    </Link>
                  ) : null}

                  <Button
                    variant="secondary"
                    size="small"
                    onClick={() => {
                      setIsCreating(false);
                      setEditingId(business.id);
                    }}
                  >
                    Edit
                  </Button>

                  {business.isPublished ? null : (
                    <Button
                      variant="ghost"
                      size="small"
                      onClick={() => void handleDelete(business)}
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
            label="listings"
            onPageChange={setPage}
          />
        </>
      ) : null}
    </Container>
  );
}
