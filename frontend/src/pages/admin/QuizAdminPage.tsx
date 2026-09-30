import { useState, type FormEvent } from 'react';

import { QuizQuestionEditor } from '@/components/quiz/QuizQuestionEditor';
import { Button } from '@/components/ui/Button';
import { Container } from '@/components/ui/Container';
import { Pagination } from '@/components/ui/Pagination';
import { StateMessage } from '@/components/ui/StateMessage';
import { deleteStaffQuizQuestion } from '@/features/quiz/api';
import {
  quizCategories,
  quizCategoryLabels,
  type QuizCategory,
  type QuizQuestionAdminSummary,
} from '@/features/quiz/types';
import { useStaffQuizQuestionList } from '@/features/quiz/useStaffQuiz';
import { getErrorMessage } from '@/lib/apiErrors';

import styles from './QuizAdminPage.module.css';

const PAGE_SIZE = 20;

function StatusBadge({ question }: { question: QuizQuestionAdminSummary }) {
  if (question.isPublished) {
    return <span className={`${styles.badge} ${styles.badgePublished}`}>Published</span>;
  }

  return <span className={`${styles.badge} ${styles.badgeDraft}`}>Draft</span>;
}

export function QuizAdminPage() {
  const [page, setPage] = useState(1);
  const [searchInput, setSearchInput] = useState('');
  const [search, setSearch] = useState('');
  const [category, setCategory] = useState<QuizCategory | ''>('');
  const [editingId, setEditingId] = useState<string | null>(null);
  const [isCreating, setIsCreating] = useState(false);
  const [rowError, setRowError] = useState<{ id: string; message: string } | null>(null);
  const [notice, setNotice] = useState<string | null>(null);

  const list = useStaffQuizQuestionList({
    page,
    pageSize: PAGE_SIZE,
    search: search || undefined,
    category: category || undefined,
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

  function handleSaved(saved: { id: string; text: string }) {
    closeEditor();
    setNotice(`Saved “${saved.text}”.`);
    list.reload();
  }

  async function handleDelete(question: QuizQuestionAdminSummary) {
    const confirmed = window.confirm('Delete this question? This cannot be undone.');

    if (!confirmed) {
      return;
    }

    setRowError(null);
    setNotice(null);

    try {
      await deleteStaffQuizQuestion(question.id);
      setNotice('Deleted the question.');
      list.reload();
    } catch (error) {
      setRowError({
        id: question.id,
        message: getErrorMessage(error, 'The question could not be deleted.'),
      });
    }
  }

  return (
    <Container as="section" className={styles.page}>
      <header className={styles.header}>
        <div>
          <h1 className={styles.title}>Manage quiz questions</h1>
          <p className={styles.subtitle}>
            Drafts are never drawn into a daily quiz and may be incomplete. Once a question has
            been played its text and options are permanent, because a player has already seen
            them; retract it instead of rewriting it.
          </p>
        </div>

        {!isEditing ? <Button onClick={() => setIsCreating(true)}>New question</Button> : null}
      </header>

      {notice ? (
        <p className={styles.notice} role="status">
          {notice}
        </p>
      ) : null}

      {isEditing ? (
        <QuizQuestionEditor
          key={editingId ?? 'new'}
          id={editingId}
          onCancel={closeEditor}
          onSaved={handleSaved}
        />
      ) : null}

      <form className={styles.search} onSubmit={handleSearchSubmit} role="search">
        <div className={styles.searchField}>
          <label className={styles.label} htmlFor="quiz-admin-search">
            Search questions
          </label>
          <input
            className={styles.input}
            id="quiz-admin-search"
            type="search"
            value={searchInput}
            placeholder="Question text…"
            onChange={(event) => setSearchInput(event.target.value)}
          />
        </div>

        <div className={styles.searchField}>
          <label className={styles.label} htmlFor="quiz-admin-category">
            Category
          </label>
          <select
            className={styles.input}
            id="quiz-admin-category"
            value={category}
            onChange={(event) => {
              setPage(1);
              setCategory(event.target.value as QuizCategory | '');
            }}
          >
            <option value="">All categories</option>
            {quizCategories.map((value) => (
              <option key={value} value={value}>
                {quizCategoryLabels[value]}
              </option>
            ))}
          </select>
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
          title="No questions here yet"
          description="Create the first one to get started."
        />
      ) : null}

      {list.status === 'success' && list.data.items.length > 0 ? (
        <>
          <ul className={styles.list}>
            {list.data.items.map((question) => (
              <li className={styles.row} key={question.id}>
                <div className={styles.rowMain}>
                  <p className={styles.rowName}>{question.text}</p>
                  <p className={styles.rowMeta}>
                    {quizCategoryLabels[question.category]} · {question.optionCount} options ·{' '}
                    {question.correctOptionCount} correct · updated{' '}
                    {new Date(question.updatedAt).toLocaleDateString()}
                  </p>
                  {question.hasBeenPlayed ? (
                    <p className={styles.rowNote}>
                      Already played, so the wording and options can no longer be changed.
                    </p>
                  ) : null}
                  {rowError?.id === question.id ? (
                    <p className={styles.rowError} role="alert">
                      {rowError.message}
                    </p>
                  ) : null}
                </div>

                <div className={styles.rowActions}>
                  <StatusBadge question={question} />

                  <Button
                    variant="secondary"
                    size="small"
                    onClick={() => {
                      setIsCreating(false);
                      setEditingId(question.id);
                    }}
                  >
                    Edit
                  </Button>

                  <Button variant="ghost" size="small" onClick={() => void handleDelete(question)}>
                    Delete
                  </Button>
                </div>
              </li>
            ))}
          </ul>

          <Pagination
            page={list.data.page}
            totalPages={list.data.totalPages}
            totalCount={list.data.totalCount}
            label="questions"
            onPageChange={setPage}
          />
        </>
      ) : null}
    </Container>
  );
}
