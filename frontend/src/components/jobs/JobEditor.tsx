import { useEffect, useState, type FormEvent } from 'react';

import { Button } from '@/components/ui/Button';
import { StateMessage } from '@/components/ui/StateMessage';
import { createJob, updateJob } from '@/features/jobs/api';
import { slugifyTitle } from '@/features/jobs/format';
import type { JobAdmin, JobCategory, SaveJobRequest } from '@/features/jobs/types';
import {
  categoryLabels,
  employmentTypeLabels,
  employmentTypes,
  emptyJobRequest,
  jobCategories,
  workModeLabels,
  workModes,
} from '@/features/jobs/types';
import { useStaffJob } from '@/features/jobs/useStaffJobs';
import type { ReferenceData } from '@/features/reference/useReferenceData';
import { getErrorMessage, getFieldErrors } from '@/lib/apiErrors';

import styles from './JobEditor.module.css';

interface JobEditorProps {
  id: string | null;
  reference: ReferenceData;
  onCancel: () => void;
  onSaved: (saved: { id: string; title: string; isPublished: boolean }) => void;
}

function toRequest(detail: JobAdmin): SaveJobRequest {
  return {
    slug: detail.slug,
    title: detail.title,
    organization: detail.organization,
    description: detail.description,
    category: detail.category,
    employmentType: detail.employmentType,
    workMode: detail.workMode,
    districtCode: detail.districtCode,
    applicationUrl: detail.applicationUrl,
    applicationDeadline: detail.applicationDeadline,
    isPublished: detail.isPublished,
  };
}

/**
 * Create and edit in one form, because the API shares one request shape for both.
 *
 * The slug is editable only while the posting is a draft. Once it is published the address is
 * public, so the API refuses a changed slug rather than quietly ignoring it: moving it would
 * break every link already shared. The field is made read-only here so the reason is visible
 * before a save is refused, not after.
 */
export function JobEditor({ id, reference, onCancel, onSaved }: JobEditorProps) {
  const detail = useStaffJob(id);
  const loaded = detail.status === 'success' ? detail.data : null;
  const loadedId = loaded?.id ?? null;

  const [form, setForm] = useState<SaveJobRequest>(emptyJobRequest);
  const [fieldErrors, setFieldErrors] = useState<Record<string, string>>({});
  const [formError, setFormError] = useState<string | null>(null);
  const [isSaving, setIsSaving] = useState(false);

  /**
   * The province is not posted. It is derived from the chosen district, so it is kept here only
   * to drive which district list the select offers, and it is recovered from the district code
   * when a saved row is loaded.
   */
  const [provinceCode, setProvinceCode] = useState('');

  useEffect(() => {
    setForm(loaded ? toRequest(loaded) : emptyJobRequest());
    setProvinceCode(loaded?.provinceCode ?? '');
  }, [loadedId]);

  const districts = provinceCode
    ? (reference.districtsByProvince[provinceCode] ?? [])
    : Object.values(reference.districtsByProvince).flat();

  function patch(changes: Partial<SaveJobRequest>) {
    setForm((current) => ({ ...current, ...changes }));
  }

  function handleTitleChange(title: string) {
    // The slug is only filled in from the title while the posting is a draft and the slug is
    // still untouched, so editing a draft's title does not fight a slug that was set by hand.
    const derived = slugifyTitle(title);
    const slugUnchanged = form.slug === '' || form.slug === slugifyTitle(form.title);

    patch(slugUnchanged ? { title, slug: derived } : { title });
  }

  async function handleSubmit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    setIsSaving(true);
    setFieldErrors({});
    setFormError(null);

    const body: SaveJobRequest = {
      ...form,
      slug: form.slug.trim(),
      title: form.title.trim(),
      organization: form.organization.trim(),
      applicationUrl: form.applicationUrl.trim(),
      districtCode: form.workMode === 'Remote' ? null : form.districtCode || null,
    };

    try {
      const saved = id ? await updateJob(id, body) : await createJob(body);
      onSaved({ id: saved.id, title: saved.title, isPublished: saved.isPublished });
    } catch (error) {
      setFieldErrors(getFieldErrors(error));
      setFormError(getErrorMessage(error, 'The job posting could not be saved.'));
    } finally {
      setIsSaving(false);
    }
  }

  if (id && detail.status === 'loading') {
    return <StateMessage tone="loading" title="Loading the posting…" />;
  }

  if (id && detail.status === 'error') {
    return (
      <StateMessage
        tone="error"
        title="Could not load that posting"
        description={getErrorMessage(detail.error, 'It may have been deleted.')}
        action={
          <Button variant="secondary" size="small" onClick={onCancel}>
            Back to the list
          </Button>
        }
      />
    );
  }

  const isPublished = loaded?.isPublished ?? false;
  const isExpired = loaded?.isExpired ?? false;
  const blockers = loaded?.publishBlockers ?? [];

  return (
    <form className={styles.form} onSubmit={handleSubmit} noValidate>
      <h2 className={styles.title}>{id ? 'Edit posting' : 'New posting'}</h2>

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

      {isPublished ? (
        <p className={styles.note}>
          This posting is live, so its address is permanent. Unpublish it first if the address
          itself has to change.
        </p>
      ) : null}

      <div className={styles.field}>
        <label className={styles.label} htmlFor="job-title">
          Title
        </label>
        <input
          className={styles.input}
          id="job-title"
          name="title"
          type="text"
          required
          value={form.title}
          aria-invalid={fieldErrors.title ? true : undefined}
          onChange={(event) => handleTitleChange(event.target.value)}
        />
        {fieldErrors.title ? <span className={styles.fieldError}>{fieldErrors.title}</span> : null}
      </div>

      <div className={styles.field}>
        <label className={styles.label} htmlFor="job-slug">
          Slug
        </label>
        <div className={styles.slugRow}>
          <input
            className={styles.input}
            id="job-slug"
            name="slug"
            type="text"
            readOnly={isPublished}
            required
            value={form.slug}
            aria-invalid={fieldErrors.slug ? true : undefined}
            aria-describedby="job-slug-hint"
            onChange={(event) => patch({ slug: event.target.value })}
          />
          <Button
            type="button"
            variant="ghost"
            size="small"
            disabled={isPublished}
            onClick={() => patch({ slug: slugifyTitle(form.title) })}
          >
            From title
          </Button>
        </div>
        <span className={styles.help} id="job-slug-hint">
          Lowercase words with single hyphens, for example junior-python-developer-remote. Editable
          while this is a draft.
        </span>
        {fieldErrors.slug ? <span className={styles.fieldError}>{fieldErrors.slug}</span> : null}
      </div>

      <div className={styles.field}>
        <label className={styles.label} htmlFor="job-organization">
          Employer
        </label>
        <input
          className={styles.input}
          id="job-organization"
          name="organization"
          type="text"
          required
          value={form.organization}
          aria-invalid={fieldErrors.organization ? true : undefined}
          onChange={(event) => patch({ organization: event.target.value })}
        />
        {fieldErrors.organization ? (
          <span className={styles.fieldError}>{fieldErrors.organization}</span>
        ) : null}
      </div>

      <div className={styles.field}>
        <label className={styles.label} htmlFor="job-description">
          Description
        </label>
        <textarea
          className={styles.textarea}
          id="job-description"
          name="description"
          rows={10}
          value={form.description}
          aria-invalid={fieldErrors.description ? true : undefined}
          aria-describedby="job-description-hint"
          onChange={(event) => patch({ description: event.target.value })}
        />
        <span className={styles.help} id="job-description-hint">
          Shown as plain text, with paragraph breaks kept. A draft can be saved without it, but a
          posting cannot be published without it.
        </span>
        {fieldErrors.description ? (
          <span className={styles.fieldError}>{fieldErrors.description}</span>
        ) : null}
      </div>

      <div className={styles.pair}>
        <div className={styles.field}>
          <label className={styles.label} htmlFor="job-record-category">
            Sector
          </label>
          <select
            className={styles.input}
            id="job-record-category"
            name="category"
            value={form.category}
            aria-invalid={fieldErrors.category ? true : undefined}
            onChange={(event) => patch({ category: event.target.value as JobCategory })}
          >
            {jobCategories.map((value) => (
              <option key={value} value={value}>
                {categoryLabels[value]}
              </option>
            ))}
          </select>
        </div>

        <div className={styles.field}>
          <label className={styles.label} htmlFor="job-record-employment">
            Employment type
          </label>
          <select
            className={styles.input}
            id="job-record-employment"
            name="employmentType"
            value={form.employmentType}
            aria-invalid={fieldErrors.employmentType ? true : undefined}
            onChange={(event) =>
              patch({ employmentType: event.target.value as SaveJobRequest['employmentType'] })
            }
          >
            {employmentTypes.map((value) => (
              <option key={value} value={value}>
                {employmentTypeLabels[value]}
              </option>
            ))}
          </select>
        </div>
      </div>

      <div className={styles.field}>
        <label className={styles.label} htmlFor="job-work-mode">
          Workplace
        </label>
        <select
          className={styles.input}
          id="job-work-mode"
          name="workMode"
          value={form.workMode}
          aria-invalid={fieldErrors.workMode ? true : undefined}
          onChange={(event) => patch({ workMode: event.target.value as SaveJobRequest['workMode'] })}
        >
          {workModes.map((value) => (
            <option key={value} value={value}>
              {workModeLabels[value]}
            </option>
          ))}
        </select>
      </div>

      {form.workMode === 'Remote' ? (
        <p className={styles.help}>
          A remote posting is saved with no district, so it appears in the nationwide remote list
          rather than under one province.
        </p>
      ) : (
        <>
          <div className={styles.field}>
            <label className={styles.label} htmlFor="job-province">
              Province
            </label>
            <select
              className={styles.input}
              id="job-province"
              name="province"
              value={provinceCode}
              onChange={(event) => {
                setProvinceCode(event.target.value);
                patch({ districtCode: null });
              }}
            >
              <option value="">Choose a province</option>
              {reference.provinces.map((province) => (
                <option key={province.code} value={province.code}>
                  {province.name}
                </option>
              ))}
            </select>
          </div>

          <div className={styles.field}>
            <label className={styles.label} htmlFor="job-district">
              District
            </label>
            <select
              className={styles.input}
              id="job-district"
              name="districtCode"
              value={form.districtCode ?? ''}
              disabled={districts.length === 0}
              aria-invalid={fieldErrors.districtCode ? true : undefined}
              aria-describedby="job-district-hint"
              onChange={(event) => patch({ districtCode: event.target.value || null })}
            >
              <option value="">No district</option>
              {districts.map((district) => (
                <option key={district.code} value={district.code}>
                  {district.name}
                </option>
              ))}
            </select>
            <span className={styles.help} id="job-district-hint">
              The province is read through this, so the two can never disagree.
            </span>
            {fieldErrors.districtCode ? (
              <span className={styles.fieldError}>{fieldErrors.districtCode}</span>
            ) : null}
          </div>
        </>
      )}

      <div className={styles.field}>
        <label className={styles.label} htmlFor="job-application-url">
          Application link
        </label>
        <input
          className={styles.input}
          id="job-application-url"
          name="applicationUrl"
          type="url"
          required
          placeholder="https://"
          value={form.applicationUrl}
          aria-invalid={fieldErrors.applicationUrl ? true : undefined}
          aria-describedby="job-application-url-hint"
          onChange={(event) => patch({ applicationUrl: event.target.value })}
        />
        <span className={styles.help} id="job-application-url-hint">
          A full https address on the employer&rsquo;s own site. Nepal Hub stores the address only
          and never fetches it.
        </span>
        {fieldErrors.applicationUrl ? (
          <span className={styles.fieldError}>{fieldErrors.applicationUrl}</span>
        ) : null}
      </div>

      <div className={styles.field}>
        <label className={styles.label} htmlFor="job-deadline">
          Closing date
        </label>
        <input
          className={styles.input}
          id="job-deadline"
          name="applicationDeadline"
          type="date"
          required
          value={form.applicationDeadline}
          aria-invalid={fieldErrors.applicationDeadline ? true : undefined}
          aria-describedby="job-deadline-hint"
          onChange={(event) => patch({ applicationDeadline: event.target.value })}
        />
        <span className={styles.help} id="job-deadline-hint">
          In Nepal Standard Time. The posting leaves the public board on the day after, and the last
          three days are labelled as closing soon.
        </span>
        {fieldErrors.applicationDeadline ? (
          <span className={styles.fieldError}>{fieldErrors.applicationDeadline}</span>
        ) : null}
      </div>

      <div className={styles.checkboxField}>
        <input
          id="job-published"
          name="isPublished"
          type="checkbox"
          checked={form.isPublished}
          aria-describedby="job-published-hint"
          onChange={(event) => patch({ isPublished: event.target.checked })}
        />
        <label className={styles.label} htmlFor="job-published">
          Published
        </label>
      </div>
      <span className={styles.help} id="job-published-hint">
        {isExpired
          ? 'This posting is past its deadline. It stays off the public board whatever this box says.'
          : 'A draft is invisible to the public. The list above is what the last saved copy was still missing.'}
      </span>

      <div className={styles.actions}>
        <Button type="submit" disabled={isSaving}>
          {isSaving ? 'Saving…' : id ? 'Save changes' : 'Create posting'}
        </Button>
        <Button type="button" variant="ghost" onClick={onCancel} disabled={isSaving}>
          Cancel
        </Button>
      </div>
    </form>
  );
}