import { useEffect, useState, type FormEvent } from 'react';

import { Button } from '@/components/ui/Button';
import { StateMessage } from '@/components/ui/StateMessage';
import { createEvent, updateEvent } from '@/features/events/api';
import { slugifyTitle } from '@/features/events/format';
import type { EventAdmin, EventCategory, SaveEventRequest } from '@/features/events/types';
import { categoryLabels, emptyEventRequest, eventCategories } from '@/features/events/types';
import { useEventBusinessOptions, useStaffEvent } from '@/features/events/useStaffEvents';
import type { ReferenceData } from '@/features/reference/useReferenceData';
import { getErrorMessage, getFieldErrors } from '@/lib/apiErrors';

import styles from './EventEditor.module.css';

interface EventEditorProps {
  id: string | null;
  reference: ReferenceData;
  onCancel: () => void;
  onSaved: (saved: { id: string; title: string; isPublished: boolean }) => void;
}

/**
 * A saved time arrives as a bare `2026-03-14T10:00:00` and the input wants `2026-03-14T10:00`, so
 * it is truncated rather than parsed and re-serialised. Parsing would read the value in the browser's
 * own zone and hand the input a time that is hours away from the one that was saved, which is how an
 * event silently moves.
 */
function toInputTime(value: string): string {
  const match = /^(\d{4}-\d{2}-\d{2}T\d{2}:\d{2})/.exec(value);

  return match ? match[1] : '';
}

function toRequest(detail: EventAdmin): SaveEventRequest {
  return {
    slug: detail.slug,
    title: detail.title,
    summary: detail.summary,
    description: detail.description,
    category: detail.category,
    organiser: detail.organiser,
    businessSlug: detail.businessSlug,
    districtCode: detail.districtCode,
    venue: detail.venue,
    registrationUrl: detail.registrationUrl,
    startsAt: detail.startsAt,
    endsAt: detail.endsAt,
    isPublished: detail.isPublished,
  };
}

/**
 * Create and edit in one form, because the API shares one request shape for both.
 *
 * The times are Nepal wall-clock and are labelled as such in the form, because a datetime-local input
 * has no zone of its own: whatever the staff member's machine is set to, the value is read as Nepal
 * time. That is the only reading that means anything for an event in Nepal, and it is why the offset
 * is never appended on the way to the API.
 *
 * The slug is editable while the event is a draft and frozen once it is live, matching every other
 * editor in the app.
 */
export function EventEditor({ id, reference, onCancel, onSaved }: EventEditorProps) {
  const detail = useStaffEvent(id);
  const loaded = detail.status === 'success' ? detail.data : null;
  const loadedId = loaded?.id ?? null;

  const [form, setForm] = useState<SaveEventRequest>(emptyEventRequest);
  const [fieldErrors, setFieldErrors] = useState<Record<string, string>>({});
  const [formError, setFormError] = useState<string | null>(null);
  const [isSaving, setIsSaving] = useState(false);
  const [businessSearch, setBusinessSearch] = useState('');
  const businessOptions = useEventBusinessOptions(businessSearch);

  // The province is not posted, because the API reads it through the district. It is kept only to
  // decide which district list the select offers, and is recovered from the saved row.
  const [provinceCode, setProvinceCode] = useState('');

  useEffect(() => {
    if (loaded) {
      setForm(toRequest(loaded));
      setProvinceCode(loaded.provinceCode ?? '');
    } else if (!id) {
      setForm(emptyEventRequest());
      setProvinceCode('');
    }
  }, [loadedId, id]);

  const districts = provinceCode
    ? (reference.districtsByProvince[provinceCode] ?? [])
    : Object.values(reference.districtsByProvince).flat();

  function patch(changes: Partial<SaveEventRequest>) {
    setForm((current) => ({ ...current, ...changes }));
  }

  function handleTitleChange(title: string) {
    // The slug only tracks the title while it is still untouched, so rewriting a draft does not
    // overwrite a slug a staff member set by hand.
    const derived = slugifyTitle(title);
    const slugUnchanged = form.slug === '' || form.slug === slugifyTitle(form.title);

    patch(slugUnchanged ? { title, slug: derived } : { title });
  }

  async function handleSubmit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    setIsSaving(true);
    setFieldErrors({});
    setFormError(null);

    const body: SaveEventRequest = {
      ...form,
      slug: form.slug.trim(),
      title: form.title.trim(),
      summary: form.summary?.trim() || null,
      description: form.description.trim(),
      organiser: form.organiser?.trim() || null,
      venue: form.venue?.trim() || null,
      registrationUrl: form.registrationUrl?.trim() || null,
      districtCode: form.districtCode || null,
    };

    try {
      const saved = id ? await updateEvent(id, body) : await createEvent(body);
      onSaved({ id: saved.id, title: saved.title, isPublished: saved.isPublished });
    } catch (error) {
      setFieldErrors(getFieldErrors(error));
      setFormError(getErrorMessage(error, 'The event could not be saved.'));
    } finally {
      setIsSaving(false);
    }
  }

  if (id && detail.status === 'loading') {
    return <StateMessage tone="loading" title="Loading the event…" />;
  }

  if (id && detail.status === 'error') {
    return (
      <StateMessage
        tone="error"
        title="Could not load that event"
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
  const blockers = loaded?.publishBlockers ?? [];

  return (
    <form className={styles.form} onSubmit={handleSubmit} noValidate>
      <h2 className={styles.title}>{id ? 'Edit event' : 'New event'}</h2>

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
          This event is live, so its address is permanent. Unpublish it first if the address itself
          has to change.
        </p>
      ) : null}

      <div className={styles.field}>
        <label className={styles.label} htmlFor="event-title">
          Title
        </label>
        <input
          className={styles.input}
          id="event-title"
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
        <label className={styles.label} htmlFor="event-slug">
          Slug
        </label>
        <div className={styles.slugRow}>
          <input
            className={styles.input}
            id="event-slug"
            name="slug"
            type="text"
            readOnly={isPublished}
            required
            value={form.slug}
            aria-invalid={fieldErrors.slug ? true : undefined}
            aria-describedby="event-slug-hint"
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
        <span className={styles.help} id="event-slug-hint">
          Lowercase words with single hyphens, for example bhajan-sandhya-birendranagar. Editable
          while this is a draft.
        </span>
        {fieldErrors.slug ? <span className={styles.fieldError}>{fieldErrors.slug}</span> : null}
      </div>

      <div className={styles.field}>
        <label className={styles.label} htmlFor="event-summary">
          Summary
        </label>
        <textarea
          className={styles.textarea}
          id="event-summary"
          name="summary"
          rows={2}
          value={form.summary ?? ''}
          aria-invalid={fieldErrors.summary ? true : undefined}
          aria-describedby="event-summary-hint"
          onChange={(event) => patch({ summary: event.target.value })}
        />
        <span className={styles.help} id="event-summary-hint">
          One or two lines shown on the card. Required before publishing.
        </span>
        {fieldErrors.summary ? (
          <span className={styles.fieldError}>{fieldErrors.summary}</span>
        ) : null}
      </div>

      <div className={styles.field}>
        <label className={styles.label} htmlFor="event-description">
          Description
        </label>
        <textarea
          className={styles.textarea}
          id="event-description"
          name="description"
          rows={8}
          value={form.description}
          aria-invalid={fieldErrors.description ? true : undefined}
          aria-describedby="event-description-hint"
          onChange={(event) => patch({ description: event.target.value })}
        />
        <span className={styles.help} id="event-description-hint">
          The full detail, shown as plain text with paragraph breaks kept. A draft can be saved without
          it, but an event cannot be published without it.
        </span>
        {fieldErrors.description ? (
          <span className={styles.fieldError}>{fieldErrors.description}</span>
        ) : null}
      </div>

      <fieldset className={styles.section}>
        <legend className={styles.sectionLegend}>What and where</legend>

        <div className={styles.field}>
          <label className={styles.label} htmlFor="event-category">
            Category
          </label>
          <select
            className={styles.input}
            id="event-category"
            name="category"
            value={form.category}
            aria-invalid={fieldErrors.category ? true : undefined}
            onChange={(changeEvent) =>
              patch({ category: changeEvent.target.value as EventCategory })
            }
          >
            {eventCategories.map((value) => (
              <option key={value} value={value}>
                {categoryLabels[value]}
              </option>
            ))}
          </select>
        </div>

        <div className={styles.field}>
          <label className={styles.label} htmlFor="event-organiser">
            Organiser
          </label>
          <input
            className={styles.input}
            id="event-organiser"
            name="organiser"
            type="text"
            value={form.organiser ?? ''}
            aria-invalid={fieldErrors.organiser ? true : undefined}
            aria-describedby="event-organiser-hint"
            onChange={(changeEvent) => patch({ organiser: changeEvent.target.value })}
          />
          <span className={styles.help} id="event-organiser-hint">
            The group or committee running it, as it would be written on a poster. Required before
            publishing.
          </span>
          {fieldErrors.organiser ? (
            <span className={styles.fieldError}>{fieldErrors.organiser}</span>
          ) : null}
        </div>

        <div className={styles.pair}>
          <div className={styles.field}>
            <label className={styles.label} htmlFor="event-province">
              Province
            </label>
            <select
              className={styles.input}
              id="event-province"
              name="province"
              value={provinceCode}
              onChange={(changeEvent) => {
                setProvinceCode(changeEvent.target.value);
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
            <label className={styles.label} htmlFor="event-district">
              District
            </label>
            <select
              className={styles.input}
              id="event-district"
              name="districtCode"
              value={form.districtCode ?? ''}
              disabled={districts.length === 0}
              required
              aria-invalid={fieldErrors.districtCode ? true : undefined}
              aria-describedby="event-district-hint"
              onChange={(changeEvent) => patch({ districtCode: changeEvent.target.value || null })}
            >
              <option value="">Choose a district</option>
              {districts.map((district) => (
                <option key={district.code} value={district.code}>
                  {district.name}
                </option>
              ))}
            </select>
          </div>
        </div>
        <span className={styles.help} id="event-district-hint">
          Required, unlike an offer&rsquo;s district: an event happens somewhere specific, so there is
          no nationwide version of it. The province is read through this, so the two can never
          disagree.
        </span>
        {fieldErrors.districtCode ? (
          <span className={styles.fieldError}>{fieldErrors.districtCode}</span>
        ) : null}

        <div className={styles.field}>
          <label className={styles.label} htmlFor="event-venue">
            Venue
          </label>
          <input
            className={styles.input}
            id="event-venue"
            name="venue"
            type="text"
            placeholder="Pokhara Stadium, gate 2"
            value={form.venue ?? ''}
            aria-invalid={fieldErrors.venue ? true : undefined}
            aria-describedby="event-venue-hint"
            onChange={(changeEvent) => patch({ venue: changeEvent.target.value })}
          />
          <span className={styles.help} id="event-venue-hint">
            Free text, as a reader would say it out loud. Not geocoded and not mapped.
          </span>
          {fieldErrors.venue ? <span className={styles.fieldError}>{fieldErrors.venue}</span> : null}
        </div>
      </fieldset>

      <fieldset className={styles.section}>
        <legend className={styles.sectionLegend}>When</legend>

        <div className={styles.pair}>
          <div className={styles.field}>
            <label className={styles.label} htmlFor="event-starts-at">
              Starts
            </label>
            <input
              className={styles.input}
              id="event-starts-at"
              name="startsAt"
              type="datetime-local"
              required
              value={toInputTime(form.startsAt)}
              aria-invalid={fieldErrors.startsAt ? true : undefined}
              aria-describedby="event-times-hint"
              onChange={(changeEvent) => patch({ startsAt: changeEvent.target.value })}
            />
          </div>

          <div className={styles.field}>
            <label className={styles.label} htmlFor="event-ends-at">
              Ends
            </label>
            <input
              className={styles.input}
              id="event-ends-at"
              name="endsAt"
              type="datetime-local"
              required
              value={toInputTime(form.endsAt)}
              aria-invalid={fieldErrors.endsAt ? true : undefined}
              aria-describedby="event-times-hint"
              onChange={(changeEvent) => patch({ endsAt: changeEvent.target.value })}
            />
          </div>
        </div>
        <span className={styles.help} id="event-times-hint">
          Nepal time, whatever your machine&rsquo;s clock says. An event still running is shown as
          happening now until its finishing time passes.
        </span>
        {fieldErrors.startsAt ? (
          <span className={styles.fieldError}>{fieldErrors.startsAt}</span>
        ) : null}
        {fieldErrors.endsAt ? <span className={styles.fieldError}>{fieldErrors.endsAt}</span> : null}
      </fieldset>

      <fieldset className={styles.section}>
        <legend className={styles.sectionLegend}>Business and registration</legend>

        <div className={styles.field}>
          <label className={styles.label} htmlFor="event-business-search">
            Find a business
          </label>
          <input
            className={styles.input}
            id="event-business-search"
            type="search"
            value={businessSearch}
            aria-describedby="event-business-hint"
            onChange={(changeEvent) => setBusinessSearch(changeEvent.target.value)}
          />
          <select
            className={styles.input}
            name="businessSlug"
            value={form.businessSlug ?? ''}
            aria-invalid={fieldErrors.businessSlug ? true : undefined}
            aria-describedby="event-business-hint"
            onChange={(changeEvent) => patch({ businessSlug: changeEvent.target.value || null })}
          >
            <option value="">No business attached</option>
            {businessOptions.data.map((option) => (
              <option key={option.slug} value={option.slug}>
                {option.name}
              </option>
            ))}
          </select>
          <span className={styles.help} id="event-business-hint">
            Only published businesses can be attached, and the search looks through the directory.
            Optional: most events are put on by somebody who has no listing here.
          </span>
          {businessOptions.status === 'loading' ? (
            <span className={styles.help}>Searching the directory…</span>
          ) : null}
          {businessOptions.status === 'error' ? (
            <span className={styles.fieldError}>
              {getErrorMessage(businessOptions.error, 'The directory could not be searched.')}
            </span>
          ) : null}
          {fieldErrors.businessSlug ? (
            <span className={styles.fieldError}>{fieldErrors.businessSlug}</span>
          ) : null}
        </div>

        <div className={styles.field}>
          <label className={styles.label} htmlFor="event-registration-url">
            Registration link
          </label>
          <input
            className={styles.input}
            id="event-registration-url"
            name="registrationUrl"
            type="url"
            placeholder="https://"
            value={form.registrationUrl ?? ''}
            aria-invalid={fieldErrors.registrationUrl ? true : undefined}
            aria-describedby="event-registration-hint"
            onChange={(changeEvent) => patch({ registrationUrl: changeEvent.target.value })}
          />
          <span className={styles.help} id="event-registration-hint">
            A full https address on the organiser&rsquo;s own page. Nepal Hub stores the address only
            and never fetches it, and nothing is collected on this site.
          </span>
          {fieldErrors.registrationUrl ? (
            <span className={styles.fieldError}>{fieldErrors.registrationUrl}</span>
          ) : null}
        </div>
      </fieldset>

      <fieldset className={styles.section}>
        <legend className={styles.sectionLegend}>Status</legend>

        <div className={styles.checkboxField}>
          <input
            id="event-published"
            name="isPublished"
            type="checkbox"
            checked={form.isPublished}
            aria-describedby="event-published-hint"
            onChange={(changeEvent) => patch({ isPublished: changeEvent.target.checked })}
          />
          <label className={styles.label} htmlFor="event-published">
            Published
          </label>
        </div>
        <span className={styles.help} id="event-published-hint">
          A draft is invisible to the public. The list above is what the last saved copy was still
          missing.
        </span>
      </fieldset>

      <div className={styles.actions}>
        <Button type="submit" disabled={isSaving}>
          {isSaving ? 'Saving…' : id ? 'Save changes' : 'Create event'}
        </Button>
        <Button type="button" variant="ghost" onClick={onCancel} disabled={isSaving}>
          Cancel
        </Button>
      </div>
    </form>
  );
}