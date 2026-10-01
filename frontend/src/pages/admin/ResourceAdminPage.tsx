import { useState, type FormEvent } from 'react';
import { Link } from 'react-router';

import { ResourceEditor } from '@/components/students/ResourceEditor';
import { Button } from '@/components/ui/Button';
import { Container } from '@/components/ui/Container';
import { Pagination } from '@/components/ui/Pagination';
import { StateMessage } from '@/components/ui/StateMessage';
import { deleteResource } from '@/features/students/api';
import { formatDeadline, typeLabel } from '@/features/students/format';
import { useStaffResourceList } from '@/features/students/useStaffResources';
import {
  categoryLabels,
  resourceAdminFilters,
  resourceCategories,
  resourceTypes,
  type ResourceAdminFilter,
  type ResourceAdminSummary,
} from '@/features/students/types';
import { getErrorMessage } from '@/lib/apiErrors';

import styles from './ResourceAdminPage.module.css';

const PAGE_SIZE = 20;

const filterLabels: Record<ResourceAdminFilter, string> = {
  All: 'Everything',
  Published: 'Published',
  Draft: 'Drafts',
  Expired: 'Past their date',
};

/**
 * Three states, not two. A published row that has passed its deadline is neither live
 * nor a draft, and it is the row that needs a decision, so it gets its own badge rather
 * than being filed under published and left to rot.
 */
function StatusBadge({ resource }: { resource: ResourceAdminSummary }) {
  if (!resource.isPublished) {
    return <span className={`${styles.badge} ${styles.badgeDraft}`}>Draft</span>;
  }

  if (resource.isExpired) {
    return <span className={`${styles.badge} ${styles.badgeExpired}`}>Expired</span>;
  }

  return <span className={`${styles.badge} ${styles.badgePublished}`}>Live</span>;
}

export function ResourceAdminPage() {
  const [page, setPage] = useState(1);
  const [searchInput, setSearchInput] = useState('');
  const [search, setSearch] = useState('');
  const [type, setType] = useState('');
  const [category, setCategory] = useState('');
  const [status, setStatus] = useState('');
  const [editingId, setEditingId] = useState<string | null>(null);
  const [isCreating, setIsCreating] = useState(false);
  const [rowError, setRowError] = useState<{ id: string; message: string } | null>(null);
  const [notice, setNotice] = useState<string | null>(null);

  const list = useStaffResourceList({
    page,
    pageSize: PAGE_SIZE,
    search: search || undefined,
    type: type ? (resourceTypes.find((value) => value === type) ?? undefined) : undefined,
    category: category ? (resourceCategories.find((value) => value === category) ?? undefined) : undefined,
    status: status
      ? (resourceAdminFilters.find((value) => value === status) ?? undefined)
      : undefined,
  });
  const isEditing = isCreating || editingId !== null;

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

  async function handleDelete(resource: ResourceAdminSummary) {
    const confirmed = window.confirm(
      `Delete the draft “${resource.title}”? This cannot be undone.`,
    );

    if (!confirmed) {
      return;
    }

    setRowError(null);
    setNotice(null);

    try {
      await deleteResource(resource.id);
      setNotice(`Deleted the draft “${resource.title}”.`);
      list.reload();
    } catch (error) {
      setRowError({
        id: resource.id,
        message: getErrorMessage(error, 'The resource could not be deleted.'),
      });
    }
  }

  return (
    <Container as="section" className={styles.page}>
      <header className={styles.header}>
        <div>
          <h1 className={styles.title}>Manage resources</h1>
          <p className={styles.subtitle}>
            Drafts are invisible to the public, and a published resource can only be deleted
            after it has been retracted. A scholarship past its closing date stays listed here
            so it can be taken down deliberately rather than quietly forgotten.
          </p>
        </div>

        {!isEditing ? (
          <Button onClick={() => setIsCreating(true)}>New resource</Button>
        ) : null}
      </header>

      {notice ? (
        <p className={styles.notice} role="status">
          {notice}
        </p>
      ) : null}

      {isEditing ? (
        <ResourceEditor
          key={editingId ?? 'new'}
          id={editingId}
          onCancel={closeEditor}
          onSaved={handleSaved}
        />
      ) : null}

      <div className={styles.searchRow}>
        <form className={styles.search} onSubmit={handleSearchSubmit} role="search">
          <div className={styles.searchField}>
            <label className={styles.label} htmlFor="admin-resource-search">
              Search resources
            </label>
            <input
              className={styles.input}
              id="admin-resource-search"
              type="search"
              value={searchInput}
              placeholder="Title or summary…"
              onChange={(event) => setSearchInput(event.target.value)}
            />
          </div>

          <Button type="submit" variant="secondary" size="small">
            Search
          </Button>
        </form>

        <div className={styles.filters}>
          <div className={styles.filterField}>
            <label className={styles.label} htmlFor="admin-resource-type">
              Type
            </label>
            <select
              className={styles.input}
              id="admin-resource-type"
              value={type}
              onChange={(event) => {
                setType(event.target.value);
                setPage(1);
              }}
            >
              <option value="">All types</option>
              {resourceTypes.map((value) => (
                <option key={value} value={value}>
                  {typeLabel(value)}
                </option>
              ))}
            </select>
          </div>

          <div className={styles.filterField}>
            <label className={styles.label} htmlFor="admin-resource-category">
              Subject
            </label>
            <select
              className={styles.input}
              id="admin-resource-category"
              value={category}
              onChange={(event) => {
                setCategory(event.target.value);
                setPage(1);
              }}
            >
              <option value="">All subjects</option>
              {resourceCategories.map((value) => (
                <option key={value} value={value}>
                  {categoryLabels[value]}
                </option>
              ))}
            </select>
          </div>

          <div className={styles.filterField}>
            <label className={styles.label} htmlFor="admin-resource-status">
              Status
            </label>
            <select
              className={styles.input}
              id="admin-resource-status"
              value={status}
              onChange={(event) => {
                setStatus(event.target.value);
                setPage(1);
              }}
            >
              {resourceAdminFilters.map((value) => (
                <option key={value} value={value}>
                  {filterLabels[value]}
                </option>
              ))}
            </select>
          </div>
        </div>
      </div>

      {list.status === 'loading' ? (
        <StateMessage tone="loading" title="Loading the list…" />
      ) : null}

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
          title="No resources here yet"
          description="Create the first one to get started."
        />
      ) : null}

      {list.status === 'success' && list.data.items.length > 0 ? (
        <>
          <ul className={styles.list}>
            {list.data.items.map((resource) => (
              <li className={styles.row} key={resource.id}>
                <div className={styles.rowMain}>
                  <p className={styles.rowName}>{resource.title}</p>
                  <p className={styles.rowMeta}>
                    <code>{resource.slug}</code> · {typeLabel(resource.type)} ·{' '}
                    {categoryLabels[resource.category]}
                    {resource.expiresOn
                      ? ` · closes ${formatDeadline(resource.expiresOn)}`
                      : ''}
                  </p>
                  {rowError?.id === resource.id ? (
                    <p className={styles.rowError} role="alert">
                      {rowError.message}
                    </p>
                  ) : null}
                </div>

                <div className={styles.rowActions}>
                  <StatusBadge resource={resource} />

                  {resource.isPublished && !resource.isExpired ? (
                    <Link
                      to={`/students/resources/${resource.slug}`}
                      className={styles.viewLink}
                    >
                      View
                    </Link>
                  ) : null}

                  <Button
                    variant="secondary"
                    size="small"
                    onClick={() => {
                      setIsCreating(false);
                      setEditingId(resource.id);
                    }}
                  >
                    Edit
                  </Button>

                  {resource.isPublished ? null : (
                    <Button
                      variant="ghost"
                      size="small"
                      onClick={() => void handleDelete(resource)}
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
            label="resources"
            onPageChange={setPage}
          />
        </>
      ) : null}
    </Container>
  );
}
