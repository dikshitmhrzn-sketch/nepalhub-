import { useEffect, useState, type FormEvent } from 'react';

import { Button } from '@/components/ui/Button';
import { StateMessage } from '@/components/ui/StateMessage';
import { createStaffQuizQuestion, updateStaffQuizQuestion } from '@/features/quiz/api';
import {
  quizCategories,
  quizCategoryLabels,
  type QuizCategory,
  type QuizQuestionAdmin,
  type SaveQuizOption,
  type SaveQuizQuestionRequest,
} from '@/features/quiz/types';
import { useStaffQuizQuestion } from '@/features/quiz/useStaffQuiz';
import { getErrorMessage, getFieldErrors } from '@/lib/apiErrors';

import styles from './QuizQuestionEditor.module.css';

interface QuizQuestionEditorProps {
  id: string | null;
  onCancel: () => void;
  onSaved: (saved: { id: string; text: string }) => void;
}

const MIN_OPTIONS = 2;
const MAX_OPTIONS = 6;
const MIN_EXPLANATION_LENGTH = 10;

function emptyOption(): SaveQuizOption {
  return { text: '', isCorrect: false };
}

function emptyRequest(): SaveQuizQuestionRequest {
  return {
    text: '',
    explanation: '',
    category: 'Nepal',
    isPublished: false,
    options: [emptyOption(), emptyOption()],
  };
}

function toRequest(detail: QuizQuestionAdmin): SaveQuizQuestionRequest {
  return {
    text: detail.text,
    explanation: detail.explanation,
    category: detail.category,
    isPublished: detail.isPublished,
    options: detail.options.map((option) => ({ ...option })),
  };
}

/**
 * Create and edit in one form because the API shares one request shape. The rules a
 * draft does not have to satisfy are checked here rather than in the browser's own
 * validation, so an incomplete question can be saved while it is still being written.
 */
export function QuizQuestionEditor({ id, onCancel, onSaved }: QuizQuestionEditorProps) {
  const detail = useStaffQuizQuestion(id);
  const loaded = detail.status === 'success' ? detail.data : null;
  const loadedId = loaded?.id ?? null;

  const [form, setForm] = useState<SaveQuizQuestionRequest>(emptyRequest);
  const [fieldErrors, setFieldErrors] = useState<Record<string, string>>({});
  const [formError, setFormError] = useState<string | null>(null);
  const [isSaving, setIsSaving] = useState(false);

  useEffect(() => {
    setForm(loaded ? toRequest(loaded) : emptyRequest());
  }, [loadedId]);

  function updateOption(index: number, patch: Partial<SaveQuizOption>) {
    setForm((current) => ({
      ...current,
      options: current.options.map((row, position) =>
        position === index ? { ...row, ...patch } : row,
      ),
    }));
  }

  /** The answer is a single choice, so choosing one has to clear the others. */
  function markCorrect(index: number) {
    setForm((current) => ({
      ...current,
      options: current.options.map((row, position) => ({ ...row, isCorrect: position === index })),
    }));
  }

  function addOption() {
    setForm((current) => ({
      ...current,
      options: [...current.options, emptyOption()],
    }));
  }

  function removeOption(index: number) {
    setForm((current) => {
      const removed = current.options[index];
      const options = current.options.filter((_, position) => position !== index);

      // Never leave the form pointing at a removed row as the answer.
      if (removed?.isCorrect) {
        return { ...current, options: options.map((row, position) => ({ ...row, isCorrect: position === 0 })) };
      }

      return { ...current, options };
    });
  }

  /**
   * The same conditions the service enforces, checked early so staff get a readable message
   * instead of a round trip. The split mirrors the server: the shape of the question always
   * applies, while the answer and the explanation are only required to publish.
   */
  function validate(request: SaveQuizQuestionRequest): string[] {
    const problems: string[] = [];
    const filled = request.options.filter((option) => option.text.trim() !== '');

    if (filled.length < MIN_OPTIONS) {
      problems.push(`A question needs at least ${MIN_OPTIONS} options with text.`);
    }

    if (request.options.length > MAX_OPTIONS) {
      problems.push(`A question can have at most ${MAX_OPTIONS} options.`);
    }

    if (filled.length !== request.options.length) {
      problems.push('Every option needs some text.');
    }

    if (new Set(filled.map((option) => option.text.trim().toLowerCase())).size !== filled.length) {
      problems.push('Two options have the same text.');
    }

    if (!request.isPublished) {
      return problems;
    }

    const correct = request.options.filter((option) => option.isCorrect).length;

    if (correct !== 1) {
      problems.push(
        correct === 0
          ? 'Mark exactly one option as the correct answer before publishing.'
          : `Only one option can be correct, but ${correct} are marked.`,
      );
    }

    if (request.explanation.trim().length < MIN_EXPLANATION_LENGTH) {
      problems.push(
        `A published question needs an explanation of at least ${MIN_EXPLANATION_LENGTH} characters.`,
      );
    }

    return problems;
  }

  async function handleSubmit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    setIsSaving(true);
    setFieldErrors({});
    setFormError(null);

    const body: SaveQuizQuestionRequest = {
      text: form.text.trim(),
      explanation: form.explanation.trim(),
      category: form.category,
      isPublished: form.isPublished,
      options: form.options.map((option) => ({ ...option, text: option.text.trim() })),
    };

    if (body.text.length < 10) {
      setFieldErrors({ text: 'The question must be at least 10 characters.' });
      setFormError(null);
      setIsSaving(false);

      return;
    }

    const problems = validate(body);

    if (problems.length > 0) {
      setFormError(problems.join(' '));
      setIsSaving(false);

      return;
    }

    try {
      const saved = id
        ? await updateStaffQuizQuestion(id, body)
        : await createStaffQuizQuestion(body);
      onSaved({ id: saved.id, text: saved.text });
    } catch (error) {
      setFieldErrors(getFieldErrors(error));
      setFormError(getErrorMessage(error, 'The question could not be saved.'));
    } finally {
      setIsSaving(false);
    }
  }

  if (id && detail.status === 'loading') {
    return <StateMessage tone="loading" title="Loading the question…" />;
  }

  if (id && detail.status === 'error') {
    return (
      <StateMessage
        tone="error"
        title="Could not load that question"
        description={getErrorMessage(detail.error, 'It may have been deleted.')}
        action={
          <Button variant="secondary" size="small" onClick={onCancel}>
            Back to the list
          </Button>
        }
      />
    );
  }

  const correctCount = form.options.filter((option) => option.isCorrect).length;

  return (
    <form className={styles.form} onSubmit={handleSubmit} noValidate>
      <h2 className={styles.title}>{id ? 'Edit question' : 'New question'}</h2>

      {formError ? (
        <p className={styles.alert} role="alert">
          {formError}
        </p>
      ) : null}

      <div className={styles.field}>
        <label className={styles.label} htmlFor="question-text">
          Question
        </label>
        <textarea
          className={styles.textarea}
          id="question-text"
          name="text"
          rows={3}
          required
          value={form.text}
          aria-invalid={fieldErrors.text ? true : undefined}
          onChange={(event) => setForm((current) => ({ ...current, text: event.target.value }))}
        />
        {fieldErrors.text ? (
          <span className={styles.fieldError}>{fieldErrors.text}</span>
        ) : (
          <span className={styles.help}>10 to 600 characters. Keep it a single clear question.</span>
        )}
      </div>

      <div className={styles.field}>
        <label className={styles.label} htmlFor="question-category">
          Category
        </label>
        <select
          className={styles.input}
          id="question-category"
          name="category"
          value={form.category}
          onChange={(event) =>
            setForm((current) => ({ ...current, category: event.target.value as QuizCategory }))
          }
        >
          {quizCategories.map((value) => (
            <option key={value} value={value}>
              {quizCategoryLabels[value]}
            </option>
          ))}
        </select>
      </div>

      <div className={styles.field}>
        <label className={styles.label} htmlFor="question-explanation">
          Explanation
        </label>
        <textarea
          className={styles.textarea}
          id="question-explanation"
          name="explanation"
          rows={4}
          value={form.explanation}
          aria-invalid={fieldErrors.explanation ? true : undefined}
          onChange={(event) =>
            setForm((current) => ({ ...current, explanation: event.target.value }))
          }
        />
        {fieldErrors.explanation ? (
          <span className={styles.fieldError}>{fieldErrors.explanation}</span>
        ) : (
          <span className={styles.help}>
            Shown only in the review after someone answers. Required to publish.
          </span>
        )}
      </div>

      <div className={styles.checkboxField}>
        <input
          id="question-published"
          name="isPublished"
          type="checkbox"
          checked={form.isPublished}
          aria-invalid={fieldErrors.isPublished ? true : undefined}
          aria-describedby="question-published-hint"
          onChange={(event) =>
            setForm((current) => ({ ...current, isPublished: event.target.checked }))
          }
        />
        <label className={styles.label} htmlFor="question-published">
          Published
        </label>
      </div>
      {fieldErrors.isPublished ? (
        <span className={styles.fieldError}>{fieldErrors.isPublished}</span>
      ) : (
        <span className={styles.help} id="question-published-hint">
          Only published questions can be drawn into a daily quiz. A draft still needs two to six
          options with text, but needs no correct answer and no explanation.
        </span>
      )}

      <fieldset className={styles.fieldset}>
        <legend className={styles.legend}>Options</legend>

        {fieldErrors.options ? <p className={styles.fieldError}>{fieldErrors.options}</p> : null}

        {form.options.map((option, index) => {
          const inputId = `option-${index}-text`;
          const correctId = `option-${index}-correct`;

          return (
            <div className={styles.optionRow} key={index}>
              <div className={styles.field}>
                <label className={styles.label} htmlFor={inputId}>
                  Option {index + 1}
                </label>
                <input
                  className={styles.input}
                  id={inputId}
                  type="text"
                  value={option.text}
                  onChange={(event) => updateOption(index, { text: event.target.value })}
                />
              </div>

              <div className={styles.optionFooter}>
                <div className={styles.checkboxField}>
                  <input
                    id={correctId}
                    name="correctOption"
                    type="radio"
                    checked={option.isCorrect}
                    onChange={() => markCorrect(index)}
                  />
                  <label className={styles.label} htmlFor={correctId}>
                    Correct answer
                  </label>
                </div>

                <Button
                  variant="ghost"
                  size="small"
                  onClick={() => removeOption(index)}
                  disabled={form.options.length <= MIN_OPTIONS}
                >
                  Remove
                </Button>
              </div>
            </div>
          );
        })}

        <div className={styles.optionFooter}>
          <Button
            variant="secondary"
            size="small"
            onClick={addOption}
            disabled={form.options.length >= MAX_OPTIONS}
          >
            Add option
          </Button>
          <span className={styles.help}>
            {correctCount === 1
              ? 'One answer marked.'
              : 'No answer marked yet, which is fine for a draft and required to publish.'}
          </span>
        </div>
      </fieldset>

      <div className={styles.actions}>
        <Button type="submit" disabled={isSaving}>
          {isSaving ? 'Saving…' : id ? 'Save changes' : 'Create question'}
        </Button>
        <Button type="button" variant="ghost" onClick={onCancel} disabled={isSaving}>
          Cancel
        </Button>
      </div>
    </form>
  );
}
