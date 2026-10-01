import { useState, type FormEvent } from 'react';
import { Link } from 'react-router';

import { JobEditor } from '@/components/jobs/JobEditor';
import { Button } from '@/components/ui/Button';
import { Container } from '@/components/ui/Container';
import { Pagination } from '@/components/ui/Pagination';
import { StateMessage } from '@/components/ui/StateMessage';
import { deleteJob } from '@/features/jobs/api';
import { categoryLabel, describeJob, formatDeadline, formatLocation } from '@/features/jobs/format';
import {
  employmentTypes,
  employmentTypeLabels,
  jobAdminFilters,
  jobCategories,
  adminFilterLabels,
  type JobAdminSummary,
} from '@/features/jobs/types';
import { useStaffJobList } from '@/features/jobs/useStaffJobs';
import { useReferenceData } from '@/features/reference/useReferenceData';
import { getErrorMessage } from '@/lib/apiErrors';

import styles from './JobsAdminPage.module.css';

const PAGE_SIZE = 20;

/**
 * Three states, not two. A published posting past its deadline is neither live nor a draft,
 * and it is the row that needs a decision, so it gets its own badge rather than being filed
 * under published and left to rot.
 */
function StatusBadge({ job }: { job: JobAdminSummary }) {
  if (!job.isPublished) {
    return <span className={`${styles.badge} ${styles.badgeDraft}`}>Draft</span>;
  }

  if (job.isExpired) {
    return <span className={`${styles.badge} ${styles.badgeExpired}`}>Closed</span>;
  }

  return <span className={`${styles.badge} ${styles.badgePublished}`}>Live</span>;
}

export function JobsAdminPage() {
  const [page, setPage] = useState(1);
  const [searchInput, setSearchInput] = useState('');
  const [search, setSearch] = useState('');
  const [category, setCategory] = useState('');
  const [employmentType, setEmploymentType] = useState('');
  const [status, setStatus] = useState('');
  const [editingId, setEditingId] = useState<string | null>(null);
  const [isCreating, setIsCreating] = useState(false);
  const [rowError, setRowError] = useState<{ id: string; message: string } | null>(null);
  const [notice, setNotice] = useState<string | null>(null);

  const reference = useReferenceData();
  const isEditing = isCreating || editingId !== null;

  const list = useStaffJobList({
    page,
    pageSize: PAGE_SIZE,
    search: search || undefined,
    category: category ? (jobCategories.find((value) => value === category) ?? undefined) : undefined,
    employmentType: employmentType
      ? (employmentTypes.find((value) => value === employmentType) ?? undefined)
      : undefined,
    status: status ? (jobAdminFilters.find((value) => value === status) ?? undefined) : undefined,
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

  async function handleDelete(job: JobAdminSummary) {
    const confirmed = window.confirm(`Delete the draft “${job.title}”? This cannot be undone.`);

    if (!confirmed) {
      return;
    }

    setRowError(null);
    setNotice(null);

    try {
      await deleteJob(job.id);
      setNotice(`Deleted the draft “${job.title}”.`);
      list.reload();
    } catch (error) {
      setRowError({
        id: job.id,
        message: getErrorMessage(error, 'The posting could not be deleted.'),
      });
    }
  }

  return (
    <Container as="section" className={styles.page}>
      <header className={styles.header}>
        <div>
          <h1 className={styles.title}>Manage jobs</h1>
          <p className={styles.subtitle}>
            A posting leaves the public board on the day after its closing date whether or not
            anyone remembers it. Closed rows stay here so they can be retired deliberately, and a
            published posting is retracted by unpublishing rather than deleted, because the record
            of what was public is worth keeping.
          </p>
        </div>

        {!isEditing ? (
          <Button onClick={() => setIsCreating(true)}>New posting</Button>
        ) : null}
      </header>

      {notice ? (
        <p className={styles.notice} role="status">
          {notice}
        </p>
      ) : null}

      {isEditing ? (
        reference.status === 'success' ? (
          <JobEditor
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
            <label className={styles.label} htmlFor="admin-job-search">
              Search postings
            </label>
            <input
              className={styles.input}
              id="admin-job-search"
              type="search"
              value={searchInput}
              placeholder="Title, employer or keyword…"
              onChange={(event) => setSearchInput(event.target.value)}
            />
          </div>

          <Button type="submit" variant="secondary" size="small">
            Search
          </Button>
        </form>

        <div className={styles.filters}>
          <div className={styles.filterField}>
            <label className={styles.label} htmlFor="admin-job-category">
              Sector
            </label>
            <select
              className={styles.input}
              id="admin-job-category"
              value={category}
              onChange={(event) => {
                setCategory(event.target.value);
                setPage(1);
              }}
            >
              <option value="">All sectors</option>
              {jobCategories.map((value) => (
                <option key={value} value={value}>
                  {categoryLabel(value)}
                </option>
              ))}
            </select>
          </div>

          <div className={styles.filterField}>
            <label className={styles.label} htmlFor="admin-job-employment">
              Employment
            </label>
            <select
              className={styles.input}
              id="admin-job-employment"
              value={employmentType}
              onChange={(event) => {
                setEmploymentType(event.target.value);
                setPage(1);
              }}
            >
              <option value="">Any employment</option>
              {employmentTypes.map((value) => (
                <option key={value} value={value}>
                  {employmentTypeLabels[value]}
                </option>
              ))}
            </select>
          </div>

          <div className={styles.filterField}>
            <label className={styles.label} htmlFor="admin-job-status">
              Status
            </label>
            <select
              className={styles.input}
              id="admin-job-status"
              value={status}
              onChange={(event) => {
                setStatus(event.target.value);
                setPage(1);
              }}
            >
              {jobAdminFilters.map((value) => (
                <option key={value} value={value}>
                  {adminFilterLabels[value]}
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
          title="No postings here yet"
          description="Create the first one to get started."
        />
      ) : null}

      {list.status === 'success' && list.data.items.length > 0 ? (
        <>
          <ul className={styles.list}>
            {list.data.items.map((job) => {
              const location = formatLocation(null, job.districtName);

              return (
                <li className={styles.row} key={job.id}>
                  <div className={styles.rowMain}>
                    <p className={styles.rowName}>{job.title}</p>
                    <p className={styles.rowMeta}>
                      <code>{job.slug}</code> · {job.organization} ·{' '}
                      {describeJob(job.employmentType, job.workMode)} ·{' '}
                      {categoryLabel(job.category)}
                      {location ? ` · ${location}` : ''} · closes{' '}
                      {formatDeadline(job.applicationDeadline)}
                    </p>
                    {rowError?.id === job.id ? (
                      <p className={styles.rowError} role="alert">
                        {rowError.message}
                      </p>
                    ) : null}
                  </div>

                  <div className={styles.rowActions}>
                    <StatusBadge job={job} />

                    {job.isPublished && !job.isExpired ? (
                      <Link to={`/jobs/${job.slug}`} className={styles.viewLink}>
                        View
                      </Link>
                    ) : null}

                    <Button
                      variant="secondary"
                      size="small"
                      onClick={() => {
                        setIsCreating(false);
                        setEditingId(job.id);
                      }}
                    >
                      Edit
                    </Button>

                    {job.isPublished ? null : (
                      <Button
                        variant="ghost"
                        size="small"
                        onClick={() => void handleDelete(job)}
                      >
                        Delete
                      </Button>
                    )}
                  </div>
                </li>
              );
            })}
          </ul>

          <Pagination
            page={list.data.page}
            totalPages={list.data.totalPages}
            totalCount={list.data.totalCount}
            label="postings"
            onPageChange={setPage}
          />
        </>
      ) : null}
    </Container>
  );
}