import { useEffect, useState, type FormEvent } from 'react';

import { Button } from '@/components/ui/Button';
import { StateMessage } from '@/components/ui/StateMessage';
import { createResource, updateResource } from '@/features/students/api';
import { useStaffResource } from '@/features/students/useStaffResources';
import {
  categoryLabels,
  educationLevels,
  emptyResourceRequest,
  levelLabels,
  resourceCategories,
  resourceTypes,
  typeLabels,
  type ResourceAdmin,
  type ResourceType,
  type SaveResourceRequest,
} from '@/features/students/types';
import { getErrorMessage, getFieldErrors } from '@/lib/apiErrors';

import styles from './ResourceEditor.module.css';

interface ResourceEditorProps {
  id: string | null;
  onCancel: () => void;
  onSaved: (saved: { id: string; title: string; isPublished: boolean }) => void;
}

function toRequest(detail: ResourceAdmin): SaveResourceRequest {
  return {
    slug: detail.slug,
    title: detail.title,
    summary: detail.summary,
    body: detail.body,
    type: detail.type,
    category: detail.category,
    level: detail.level,
    externalUrl: detail.externalUrl,
    academicYear: detail.academicYear,
    provider: detail.provider,
    awardAmountNpr: detail.awardAmountNpr,
    eligibility: detail.eligibility,
    expiresOn: detail.expiresOn,
    isPublished: detail.isPublished,
  };
}

/** An empty number box means "not set", not zero and not NaN. */
function parseOptionalNumber(value: string): number | null {
  const trimmed = value.trim();

  return trimmed === '' || Number.isNaN(Number(trimmed)) ? null : Number(trimmed);
}

function parseOptionalString(value: string): string | null {
  const trimmed = value.trim();

  return trimmed === '' ? null : trimmed;
}

/**
 * Create and edit in one form, because the API shares one request shape for both.
 *
 * The slug is read-only while editing: the API refuses a changed slug rather than
 * quietly ignoring it, because the slug is the public address and moving it breaks every
 * link already shared.
 *
 * The fields shown depend on the type, and the fields that do not apply are sent as
 * null so the server clears them rather than leaving a year on a note.
 */
export function ResourceEditor({ id, onCancel, onSaved }: ResourceEditorProps) {
  const detail = useStaffResource(id);
  const loaded = detail.status === 'success' ? detail.data : null;
  const loadedId = loaded?.id ?? null;

  const [form, setForm] = useState<SaveResourceRequest>(emptyResourceRequest);
  const [fieldErrors, setFieldErrors] = useState<Record<string, string>>({});
  const [formError, setFormError] = useState<string | null>(null);
  const [isSaving, setIsSaving] = useState(false);

  useEffect(() => {
    setForm(loaded ? toRequest(loaded) : emptyResourceRequest());
  }, [loadedId]);

  function patch(changes: Partial<SaveResourceRequest>) {
    setForm((current) => ({ ...current, ...changes }));
  }

  async function handleSubmit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    setIsSaving(true);
    setFieldErrors({});
    setFormError(null);

    // Only the fields the chosen type actually uses are sent. A note has no year and a
    // scholarship has no external link, so the other set goes as null and the server
    // clears whatever was there before.
    const body: SaveResourceRequest = {
      ...form,
      slug: form.slug.trim(),
      title: form.title.trim(),
      summary: form.summary.trim(),
      body: form.body,
      academicYear: form.type === 'QuestionPaper' ? form.academicYear : null,
      externalUrl: form.type === 'QuestionPaper' ? form.externalUrl : null,
      provider: form.type === 'Scholarship' ? form.provider : null,
      awardAmountNpr: form.type === 'Scholarship' ? form.awardAmountNpr : null,
      eligibility: form.type === 'Scholarship' ? form.eligibility : null,
      expiresOn: form.type === 'Scholarship' ? form.expiresOn : null,
    };

    try {
      const saved = id ? await updateResource(id, body) : await createResource(body);
      onSaved({ id: saved.id, title: saved.title, isPublished: saved.isPublished });
    } catch (error) {
      setFieldErrors(getFieldErrors(error));
      setFormError(getErrorMessage(error, 'The resource could not be saved.'));
    } finally {
      setIsSaving(false);
    }
  }

  if (id && detail.status === 'loading') {
    return <StateMessage tone="loading" title="Loading the resource…" />;
  }

  if (id && detail.status === 'error') {
    return (
      <StateMessage
        tone="error"
        title="Could not load that resource"
        description={getErrorMessage(detail.error, 'It may have been deleted.')}
        action={
          <Button variant="secondary" size="small" onClick={onCancel}>
            Back to the list
          </Button>
        }
      />
    );
  }

  const isQuestionPaper = form.type === 'QuestionPaper';
  const isScholarship = form.type === 'Scholarship';
  const blockers = loaded?.publishBlockers ?? [];

  return (
    <form className={styles.form} onSubmit={handleSubmit} noValidate>
      <h2 className={styles.title}>{id ? 'Edit resource' : 'New resource'}</h2>

      {formError ? (
        <p className={styles.alert} role="alert">
          {formError}
        </p>
      ) : null}

      {blockers.length > 0 ? (
        <div className={styles.blockers} role="status">
          <p className={styles.blockersTitle}>Not publishable yet</p>
          <ul className={styles.blockerList}>
            {blockers.map((blocker) => (
              <li key={blocker}>{blocker}</li>
            ))}
          </ul>
        </div>
      ) : null}

      {id ? (
        <p className={styles.note}>
          The slug is permanent. Create a new resource instead if the address has to change.
        </p>
      ) : null}

      <div className={styles.field}>
        <label className={styles.label} htmlFor="resource-slug">
          Slug
        </label>
        <input
          className={styles.input}
          id="resource-slug"
          name="slug"
          type="text"
          readOnly={id !== null}
          required
          value={form.slug}
          aria-invalid={fieldErrors.slug ? true : undefined}
          aria-describedby={fieldErrors.slug ? 'resource-slug-error' : 'resource-slug-hint'}
          onChange={(event) => patch({ slug: event.target.value })}
        />
        {fieldErrors.slug ? (
          <span className={styles.fieldError} id="resource-slug-error">
            {fieldErrors.slug}
          </span>
        ) : (
          <span className={styles.help} id="resource-slug-hint">
            Lowercase words with single hyphens, for example quadratic-equations-grade-9.
          </span>
        )}
      </div>

      <div className={styles.field}>
        <label className={styles.label} htmlFor="resource-title">
          Title
        </label>
        <input
          className={styles.input}
          id="resource-title"
          name="title"
          type="text"
          required
          value={form.title}
          aria-invalid={fieldErrors.title ? true : undefined}
          onChange={(event) => patch({ title: event.target.value })}
        />
        {fieldErrors.title ? <span className={styles.fieldError}>{fieldErrors.title}</span> : null}
      </div>

      <div className={styles.field}>
        <label className={styles.label} htmlFor="resource-summary">
          Summary
        </label>
        <input
          className={styles.input}
          id="resource-summary"
          name="summary"
          type="text"
          value={form.summary}
          aria-invalid={fieldErrors.summary ? true : undefined}
          aria-describedby="resource-summary-hint"
          onChange={(event) => patch({ summary: event.target.value })}
        />
        <span className={styles.help} id="resource-summary-hint">
          One line for the catalogue card. At least 20 characters to publish.
        </span>
        {fieldErrors.summary ? <span className={styles.fieldError}>{fieldErrors.summary}</span> : null}
      </div>

      <div className={styles.field}>
        <label className={styles.label} htmlFor="resource-body">
          Content
        </label>
        <textarea
          className={styles.textarea}
          id="resource-body"
          name="body"
          rows={10}
          value={form.body}
          aria-invalid={fieldErrors.body ? true : undefined}
          aria-describedby="resource-body-hint"
          onChange={(event) => patch({ body: event.target.value })}
        />
        <span className={styles.help} id="resource-body-hint">
          Shown as plain text, with paragraph breaks kept. At least 80 characters to publish.
        </span>
        {fieldErrors.body ? <span className={styles.fieldError}>{fieldErrors.body}</span> : null}
      </div>

      <div className={styles.pair}>
        <div className={styles.field}>
          <label className={styles.label} htmlFor="resource-record-type">
            Type
          </label>
          <select
            className={styles.input}
            id="resource-record-type"
            name="type"
            value={form.type}
            onChange={(event) => patch({ type: event.target.value as ResourceType })}
          >
            {resourceTypes.map((value) => (
              <option key={value} value={value}>
                {typeLabels[value]}
              </option>
            ))}
          </select>
        </div>

        <div className={styles.field}>
          <label className={styles.label} htmlFor="resource-record-category">
            Subject
          </label>
          <select
            className={styles.input}
            id="resource-record-category"
            name="category"
            value={form.category}
            onChange={(event) =>
              patch({ category: event.target.value as SaveResourceRequest['category'] })
            }
          >
            {resourceCategories.map((value) => (
              <option key={value} value={value}>
                {categoryLabels[value]}
              </option>
            ))}
          </select>
        </div>
      </div>

      <div className={styles.field}>
        <label className={styles.label} htmlFor="resource-record-level">
          Level
        </label>
        <select
          className={styles.input}
          id="resource-record-level"
          name="level"
          value={form.level}
          onChange={(event) =>
            patch({ level: event.target.value as SaveResourceRequest['level'] })
          }
        >
          {educationLevels.map((value) => (
            <option key={value} value={value}>
              {levelLabels[value]}
            </option>
          ))}
        </select>
      </div>

      {isQuestionPaper ? (
        <fieldset className={styles.fieldset}>
          <legend className={styles.legend}>Question paper</legend>

          <div className={styles.pair}>
            <div className={styles.field}>
              <label className={styles.label} htmlFor="resource-year">
                Academic year
              </label>
              <input
                className={styles.input}
                id="resource-year"
                name="academicYear"
                type="number"
                min={2000}
                max={2100}
                value={form.academicYear ?? ''}
                aria-invalid={fieldErrors.academicYear ? true : undefined}
                onChange={(event) =>
                  patch({ academicYear: parseOptionalNumber(event.target.value) })
                }
              />
              {fieldErrors.academicYear ? (
                <span className={styles.fieldError}>{fieldErrors.academicYear}</span>
              ) : (
                <span className={styles.help}>The Bikram Sambat year, for example 2081.</span>
              )}
            </div>

            <div className={styles.field}>
              <label className={styles.label} htmlFor="resource-external-url">
                Link to the paper
              </label>
              <input
                className={styles.input}
                id="resource-external-url"
                name="externalUrl"
                type="url"
                placeholder="https://www.moest.gov.np/"
                value={form.externalUrl ?? ''}
                aria-invalid={fieldErrors.externalUrl ? true : undefined}
                onChange={(event) => patch({ externalUrl: event.target.value })}
              />
              {fieldErrors.externalUrl ? (
                <span className={styles.fieldError}>{fieldErrors.externalUrl}</span>
              ) : (
                <span className={styles.help}>A full https address. Nothing is uploaded here.</span>
              )}
            </div>
          </div>
        </fieldset>
      ) : null}

      {isScholarship ? (
        <fieldset className={styles.fieldset}>
          <legend className={styles.legend}>Scholarship</legend>

          <div className={styles.pair}>
            <div className={styles.field}>
              <label className={styles.label} htmlFor="resource-provider">
                Offered by
              </label>
              <input
                className={styles.input}
                id="resource-provider"
                name="provider"
                type="text"
                value={form.provider ?? ''}
                aria-invalid={fieldErrors.provider ? true : undefined}
                onChange={(event) => patch({ provider: event.target.value })}
              />
              {fieldErrors.provider ? (
                <span className={styles.fieldError}>{fieldErrors.provider}</span>
              ) : null}
            </div>

            <div className={styles.field}>
              <label className={styles.label} htmlFor="resource-award">
                Award amount (NPR)
              </label>
              <input
                className={styles.input}
                id="resource-award"
                name="awardAmountNpr"
                type="number"
                min={0}
                step={1000}
                value={form.awardAmountNpr ?? ''}
                aria-invalid={fieldErrors.awardAmountNpr ? true : undefined}
                onChange={(event) => patch({ awardAmountNpr: parseOptionalNumber(event.target.value) })}
              />
              {fieldErrors.awardAmountNpr ? (
                <span className={styles.fieldError}>{fieldErrors.awardAmountNpr}</span>
              ) : null}
            </div>
          </div>

          <div className={styles.field}>
            <label className={styles.label} htmlFor="resource-deadline">
              Closing date
            </label>
            <input
              className={styles.input}
              id="resource-deadline"
              name="expiresOn"
              type="date"
              value={form.expiresOn ?? ''}
              aria-invalid={fieldErrors.expiresOn ? true : undefined}
              aria-describedby="resource-deadline-hint"
              onChange={(event) =>
                patch({ expiresOn: parseOptionalString(event.target.value) })
              }
            />
            <span className={styles.help} id="resource-deadline-hint">
              In Nepal Standard Time. The listing disappears from the catalogue the day after.
            </span>
            {fieldErrors.expiresOn ? (
              <span className={styles.fieldError}>{fieldErrors.expiresOn}</span>
            ) : null}
          </div>

          <div className={styles.field}>
            <label className={styles.label} htmlFor="resource-eligibility">
              Who can apply
            </label>
            <textarea
              className={styles.textarea}
              id="resource-eligibility"
              name="eligibility"
              rows={3}
              value={form.eligibility ?? ''}
              aria-invalid={fieldErrors.eligibility ? true : undefined}
              onChange={(event) => patch({ eligibility: event.target.value })}
            />
            {fieldErrors.eligibility ? (
              <span className={styles.fieldError}>{fieldErrors.eligibility}</span>
            ) : null}
          </div>
        </fieldset>
      ) : null}

      <div className={styles.checkboxField}>
        <input
          id="resource-published"
          name="isPublished"
          type="checkbox"
          checked={form.isPublished}
          aria-describedby="resource-published-hint"
          onChange={(event) => patch({ isPublished: event.target.checked })}
        />
        <label className={styles.label} htmlFor="resource-published">
          Published
        </label>
      </div>
      <span className={styles.help} id="resource-published-hint">
        A draft is invisible to the public. The list above is what the last saved copy was still
        missing, and a save that is not ready yet reports the exact fields to fix.
      </span>

      <div className={styles.actions}>
        <Button type="submit" disabled={isSaving}>
          {isSaving ? 'Saving…' : id ? 'Save changes' : 'Create resource'}
        </Button>
        <Button type="button" variant="ghost" onClick={onCancel} disabled={isSaving}>
          Cancel
        </Button>
      </div>
    </form>
  );
}
