import { useState, type FormEvent } from 'react';
import { Link } from 'react-router';

import { CalculatorEditor } from '@/components/calculators/CalculatorEditor';
import { Button } from '@/components/ui/Button';
import { Container } from '@/components/ui/Container';
import { Pagination } from '@/components/ui/Pagination';
import { StateMessage } from '@/components/ui/StateMessage';
import { deleteCalculator } from '@/features/calculators/api';
import { useStaffCalculatorList } from '@/features/calculators/useStaffCalculators';
import type { CalculatorAdminSummary } from '@/features/calculators/types';
import { getErrorMessage } from '@/lib/apiErrors';

import styles from './CalculatorAdminPage.module.css';

const PAGE_SIZE = 20;

function StatusBadge({ calculator }: { calculator: CalculatorAdminSummary }) {
  return calculator.isPublished ? (
    <span className={`${styles.badge} ${styles.badgePublished}`}>Published</span>
  ) : (
    <span className={`${styles.badge} ${styles.badgeDraft}`}>Draft</span>
  );
}

export function CalculatorAdminPage() {
  const [page, setPage] = useState(1);
  const [searchInput, setSearchInput] = useState('');
  const [search, setSearch] = useState('');
  const [editingId, setEditingId] = useState<string | null>(null);
  const [isCreating, setIsCreating] = useState(false);
  const [rowError, setRowError] = useState<{ id: string; message: string } | null>(null);
  const [notice, setNotice] = useState<string | null>(null);

  const list = useStaffCalculatorList({ page, pageSize: PAGE_SIZE, search: search || undefined });
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

  function handleSaved(saved: { id: string; name: string }) {
    closeEditor();
    setNotice(`Saved “${saved.name}”.`);
    list.reload();
  }

  async function handleDelete(calculator: CalculatorAdminSummary) {
    const confirmed = window.confirm(
      `Delete the draft “${calculator.name}”? This cannot be undone.`,
    );

    if (!confirmed) {
      return;
    }

    setRowError(null);
    setNotice(null);

    try {
      await deleteCalculator(calculator.id);
      setNotice(`Deleted the draft “${calculator.name}”.`);
      list.reload();
    } catch (error) {
      setRowError({
        id: calculator.id,
        message: getErrorMessage(error, 'The calculator could not be deleted.'),
      });
    }
  }

  return (
    <Container as="section" className={styles.page}>
      <header className={styles.header}>
        <div>
          <h1 className={styles.title}>Manage calculators</h1>
          <p className={styles.subtitle}>
            Drafts are invisible to the public. A calculator can only be deleted while it
            is a draft, because a published row has already been used and shared.
          </p>
        </div>

        {!isEditing ? (
          <Button onClick={() => setIsCreating(true)}>New calculator</Button>
        ) : null}
      </header>

      {notice ? (
        <p className={styles.notice} role="status">
          {notice}
        </p>
      ) : null}

      {isEditing ? (
        <CalculatorEditor
          key={editingId ?? 'new'}
          id={editingId}
          onCancel={closeEditor}
          onSaved={handleSaved}
        />
      ) : null}

      <form className={styles.search} onSubmit={handleSearchSubmit} role="search">
        <div className={styles.searchField}>
          <label className={styles.label} htmlFor="admin-search">
            Search calculators
          </label>
          <input
            className={styles.input}
            id="admin-search"
            type="search"
            value={searchInput}
            placeholder="Name or slug…"
            onChange={(event) => setSearchInput(event.target.value)}
          />
        </div>

        <Button type="submit" variant="secondary" size="small">
          Search
        </Button>
      </form>

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
          title="No calculators here yet"
          description="Create the first one to get started."
        />
      ) : null}

      {list.status === 'success' && list.data.items.length > 0 ? (
        <>
          <ul className={styles.list}>
            {list.data.items.map((calculator) => (
              <li className={styles.row} key={calculator.id}>
                <div className={styles.rowMain}>
                  <p className={styles.rowName}>{calculator.name}</p>
                  <p className={styles.rowMeta}>
                    <code>{calculator.slug}</code> · {calculator.category} · sort{' '}
                    {calculator.sortOrder}
                  </p>
                  {rowError?.id === calculator.id ? (
                    <p className={styles.rowError} role="alert">
                      {rowError.message}
                    </p>
                  ) : null}
                </div>

                <div className={styles.rowActions}>
                  <StatusBadge calculator={calculator} />

                  {calculator.isPublished ? (
                    <Link to={`/calculators/${calculator.slug}`} className={styles.viewLink}>
                      View
                    </Link>
                  ) : null}

                  <Button
                    variant="secondary"
                    size="small"
                    onClick={() => {
                      setIsCreating(false);
                      setEditingId(calculator.id);
                    }}
                  >
                    Edit
                  </Button>

                  {calculator.isPublished ? (
                    <Button
                      variant="ghost"
                      size="small"
                      onClick={() => {
                        setIsCreating(false);
                        setEditingId(calculator.id);
                      }}
                      title="Unpublish from the edit form"
                    >
                      Unpublish
                    </Button>
                  ) : (
                    <Button variant="ghost" size="small" onClick={() => void handleDelete(calculator)}>
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
            label="calculators"
            onPageChange={setPage}
          />
        </>
      ) : null}
    </Container>
  );
}
