import { useEffect, useState, type FormEvent } from 'react';

import { Button } from '@/components/ui/Button';
import { StateMessage } from '@/components/ui/StateMessage';
import { createBusiness, updateBusiness } from '@/features/businesses/api';
import { fromInputTime, slugifyName, toInputTime } from '@/features/businesses/format';
import type { BusinessAdmin, BusinessCategory, SaveBusinessHours, SaveBusinessRequest } from '@/features/businesses/types';
import {
  businessCategories,
  categoryLabels,
  daysOfWeek,
  emptyBusinessRequest,
  emptyWeek,
} from '@/features/businesses/types';
import { useStaffBusiness } from '@/features/businesses/useStaffBusinesses';
import type { ReferenceData } from '@/features/reference/useReferenceData';
import { getErrorMessage, getFieldErrors } from '@/lib/apiErrors';

import styles from './BusinessEditor.module.css';

interface BusinessEditorProps {
  id: string | null;
  reference: ReferenceData;
  onCancel: () => void;
  onSaved: (saved: { id: string; name: string; isPublished: boolean }) => void;
}

function toHoursRequest(detail: BusinessAdmin): SaveBusinessHours[] {
  // A listing that saved no hours comes back as an empty list, and posting that empty list back
  // is correct: it means "hours still not stated". A week of rows is only built when the saved
  // listing actually has some, so a partial week cannot be padded with invented opening times.
  if (detail.openingHours.length === 0) {
    return [];
  }

  const byDay = new Map(detail.openingHours.map((day) => [day.dayOfWeek, day]));

  return daysOfWeek.map((dayOfWeek) => {
    const saved = byDay.get(dayOfWeek);

    return saved
      ? {
          dayOfWeek,
          isClosed: saved.isClosed,
          opensAt: saved.opensAt,
          closesAt: saved.closesAt,
        }
      : { dayOfWeek, isClosed: true, opensAt: null, closesAt: null };
  });
}

function toRequest(detail: BusinessAdmin): SaveBusinessRequest {
  return {
    slug: detail.slug,
    name: detail.name,
    description: detail.description,
    category: detail.category,
    districtCode: detail.districtCode,
    address: detail.address,
    phone: detail.phone,
    email: detail.email,
    website: detail.website,
    isPublished: detail.isPublished,
    isVerified: detail.isVerified,
    openingHours: toHoursRequest(detail),
  };
}

/**
 * Create and edit in one form, because the API shares one request shape for both.
 *
 * The slug behaves the way it does on the job board: editable while the listing is a draft,
 * frozen once it is live, because by then the address is something people and search engines have
 * recorded. It is made read-only here so the reason is visible before a save is refused.
 *
 * The hours table is the one place this form differs from the job editor. A week is edited day by
 * day rather than as one field, and a day is either open with two times or explicitly closed,
 * because "closed" is not the same statement as "we did not record it".
 */
export function BusinessEditor({ id, reference, onCancel, onSaved }: BusinessEditorProps) {
  const detail = useStaffBusiness(id);
  const loaded = detail.status === 'success' ? detail.data : null;
  const loadedId = loaded?.id ?? null;

  const [form, setForm] = useState<SaveBusinessRequest>(emptyBusinessRequest);
  const [hours, setHours] = useState<SaveBusinessHours[]>([]);
  const [fieldErrors, setFieldErrors] = useState<Record<string, string>>({});
  const [formError, setFormError] = useState<string | null>(null);
  const [isSaving, setIsSaving] = useState(false);

  // The province is not posted, because the API reads it through the district. It is kept only to
  // decide which district list the select offers, and is recovered from the saved row.
  const [provinceCode, setProvinceCode] = useState('');

  useEffect(() => {
    if (loaded) {
      setForm(toRequest(loaded));
      setHours(toHoursRequest(loaded));
      setProvinceCode(loaded.provinceCode);
    } else if (!id) {
      setForm(emptyBusinessRequest());
      setHours(emptyWeek());
      setProvinceCode('');
    }
  }, [loadedId, id]);

  const districts = provinceCode
    ? (reference.districtsByProvince[provinceCode] ?? [])
    : Object.values(reference.districtsByProvince).flat();

  function patch(changes: Partial<SaveBusinessRequest>) {
    setForm((current) => ({ ...current, ...changes }));
  }

  function patchDay(dayOfWeek: SaveBusinessHours['dayOfWeek'], changes: Partial<SaveBusinessHours>) {
    setHours((current) =>
      current.map((day) => (day.dayOfWeek === dayOfWeek ? { ...day, ...changes } : day)),
    );
  }

  function handleNameChange(name: string) {
    // The slug only tracks the name while it is still untouched, so renaming a draft does not
    // overwrite a slug a staff member set by hand.
    const derived = slugifyName(name);
    const slugUnchanged = form.slug === '' || form.slug === slugifyName(form.name);

    patch(slugUnchanged ? { name, slug: derived } : { name });
  }

  function handleDayToggle(dayOfWeek: SaveBusinessHours['dayOfWeek'], isClosed: boolean) {
    // Closing a day clears its times rather than leaving them stored and hidden, so a day that is
    // reopened starts from a stated schedule instead of a stale one.
    patchDay(dayOfWeek, isClosed ? { isClosed, opensAt: null, closesAt: null } : { isClosed });
  }

  async function handleSubmit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    setIsSaving(true);
    setFieldErrors({});
    setFormError(null);

    const body: SaveBusinessRequest = {
      ...form,
      slug: form.slug.trim(),
      name: form.name.trim(),
      description: form.description.trim(),
      address: form.address?.trim() || null,
      phone: form.phone?.trim() || null,
      email: form.email?.trim() || null,
      website: form.website?.trim() || null,
      districtCode: form.districtCode || null,
      openingHours: hours.map((day) => ({
        dayOfWeek: day.dayOfWeek,
        isClosed: day.isClosed,
        opensAt: fromInputTime(toInputTime(day.opensAt)),
        closesAt: fromInputTime(toInputTime(day.closesAt)),
      })),
    };

    try {
      const saved = id ? await updateBusiness(id, body) : await createBusiness(body);
      onSaved({ id: saved.id, name: saved.name, isPublished: saved.isPublished });
    } catch (error) {
      setFieldErrors(getFieldErrors(error));
      setFormError(getErrorMessage(error, 'The business listing could not be saved.'));
    } finally {
      setIsSaving(false);
    }
  }

  if (id && detail.status === 'loading') {
    return <StateMessage tone="loading" title="Loading the listing…" />;
  }

  if (id && detail.status === 'error') {
    return (
      <StateMessage
        tone="error"
        title="Could not load that listing"
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
  const isVerified = loaded?.isVerified ?? false;
  const blockers = loaded?.publishBlockers ?? [];

  return (
    <form className={styles.form} onSubmit={handleSubmit} noValidate>
      <h2 className={styles.title}>{id ? 'Edit listing' : 'New listing'}</h2>

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
          This listing is live, so its address is permanent. Unpublish it first if the address
          itself has to change.
        </p>
      ) : null}

      <div className={styles.field}>
        <label className={styles.label} htmlFor="business-name">
          Name
        </label>
        <input
          className={styles.input}
          id="business-name"
          name="name"
          type="text"
          required
          value={form.name}
          aria-invalid={fieldErrors.name ? true : undefined}
          onChange={(event) => handleNameChange(event.target.value)}
        />
        {fieldErrors.name ? <span className={styles.fieldError}>{fieldErrors.name}</span> : null}
      </div>

      <div className={styles.field}>
        <label className={styles.label} htmlFor="business-slug">
          Slug
        </label>
        <div className={styles.slugRow}>
          <input
            className={styles.input}
            id="business-slug"
            name="slug"
            type="text"
            readOnly={isPublished}
            required
            value={form.slug}
            aria-invalid={fieldErrors.slug ? true : undefined}
            aria-describedby="business-slug-hint"
            onChange={(event) => patch({ slug: event.target.value })}
          />
          <Button
            type="button"
            variant="ghost"
            size="small"
            disabled={isPublished}
            onClick={() => patch({ slug: slugifyName(form.name) })}
          >
            From name
          </Button>
        </div>
        <span className={styles.help} id="business-slug-hint">
          Lowercase words with single hyphens, for everest-web-studio-lalitpur. Editable while this
          is a draft.
        </span>
        {fieldErrors.slug ? <span className={styles.fieldError}>{fieldErrors.slug}</span> : null}
      </div>

      <div className={styles.field}>
        <label className={styles.label} htmlFor="business-description">
          Description
        </label>
        <textarea
          className={styles.textarea}
          id="business-description"
          name="description"
          rows={8}
          value={form.description}
          aria-invalid={fieldErrors.description ? true : undefined}
          aria-describedby="business-description-hint"
          onChange={(event) => patch({ description: event.target.value })}
        />
        <span className={styles.help} id="business-description-hint">
          Shown as plain text, with paragraph breaks kept. A draft can be saved without it, but a
          listing cannot be published without it.
        </span>
        {fieldErrors.description ? (
          <span className={styles.fieldError}>{fieldErrors.description}</span>
        ) : null}
      </div>

      <fieldset className={styles.section}>
        <legend className={styles.sectionLegend}>Category and location</legend>

        <div className={styles.field}>
          <label className={styles.label} htmlFor="business-category">
            Category
          </label>
          <select
            className={styles.input}
            id="business-category"
            name="category"
            value={form.category}
            aria-invalid={fieldErrors.category ? true : undefined}
            onChange={(event) => patch({ category: event.target.value as BusinessCategory })}
          >
            {businessCategories.map((value) => (
              <option key={value} value={value}>
                {categoryLabels[value]}
              </option>
            ))}
          </select>
        </div>

        <div className={styles.field}>
          <label className={styles.label} htmlFor="business-province">
            Province
          </label>
          <select
            className={styles.input}
            id="business-province"
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
          <label className={styles.label} htmlFor="business-district">
            District
          </label>
          <select
            className={styles.input}
            id="business-district"
            name="districtCode"
            value={form.districtCode ?? ''}
            disabled={districts.length === 0}
            aria-invalid={fieldErrors.districtCode ? true : undefined}
            aria-describedby="business-district-hint"
            onChange={(event) => patch({ districtCode: event.target.value || null })}
          >
            <option value="">No district</option>
            {districts.map((district) => (
              <option key={district.code} value={district.code}>
                {district.name}
              </option>
            ))}
          </select>
          <span className={styles.help} id="business-district-hint">
            Required before publishing, and the province is read through it so the two can never
            disagree.
          </span>
          {fieldErrors.districtCode ? (
            <span className={styles.fieldError}>{fieldErrors.districtCode}</span>
          ) : null}
        </div>

        <div className={styles.field}>
          <label className={styles.label} htmlFor="business-address">
            Address
          </label>
          <input
            className={styles.input}
            id="business-address"
            name="address"
            type="text"
            value={form.address ?? ''}
            aria-invalid={fieldErrors.address ? true : undefined}
            aria-describedby="business-address-hint"
            onChange={(event) => patch({ address: event.target.value })}
          />
          <span className={styles.help} id="business-address-hint">
            Free text, as a reader would say it out loud. Not geocoded.
          </span>
          {fieldErrors.address ? <span className={styles.fieldError}>{fieldErrors.address}</span> : null}
        </div>
      </fieldset>

      <fieldset className={styles.section}>
        <legend className={styles.sectionLegend}>Contact</legend>

        <div className={styles.pair}>
          <div className={styles.field}>
            <label className={styles.label} htmlFor="business-phone">
              Phone
            </label>
            <input
              className={styles.input}
              id="business-phone"
              name="phone"
              type="tel"
              value={form.phone ?? ''}
              aria-invalid={fieldErrors.phone ? true : undefined}
              aria-describedby="business-phone-hint"
              onChange={(event) => patch({ phone: event.target.value })}
            />
            {fieldErrors.phone ? <span className={styles.fieldError}>{fieldErrors.phone}</span> : null}
          </div>

          <div className={styles.field}>
            <label className={styles.label} htmlFor="business-email">
              Email
            </label>
            <input
              className={styles.input}
              id="business-email"
              name="email"
              type="email"
              value={form.email ?? ''}
              aria-invalid={fieldErrors.email ? true : undefined}
              onChange={(event) => patch({ email: event.target.value })}
            />
            {fieldErrors.email ? <span className={styles.fieldError}>{fieldErrors.email}</span> : null}
          </div>
        </div>

        <span className={styles.help} id="business-phone-hint">
          Stored as digits only, so one business cannot appear under two spellings of the same
          number. Required before publishing.
        </span>

        <div className={styles.field}>
          <label className={styles.label} htmlFor="business-website">
            Website
          </label>
          <input
            className={styles.input}
            id="business-website"
            name="website"
            type="url"
            placeholder="https://"
            value={form.website ?? ''}
            aria-invalid={fieldErrors.website ? true : undefined}
            aria-describedby="business-website-hint"
            onChange={(event) => patch({ website: event.target.value })}
          />
          <span className={styles.help} id="business-website-hint">
            A full https address on the business&rsquo;s own site. Nepal Hub stores the address only
            and never fetches it.
          </span>
          {fieldErrors.website ? (
            <span className={styles.fieldError}>{fieldErrors.website}</span>
          ) : null}
        </div>
      </fieldset>

      <fieldset className={styles.section}>
        <legend className={styles.sectionLegend}>Opening hours</legend>

        <p className={styles.help}>
          Leave every day open and the listing says nothing about its hours, which is honest. To
          publish a schedule, close the days it does not open. A closing time earlier than the
          opening time is read as running past midnight.
        </p>

        <div className={styles.hours}>
          {hours.map((day) => (
            <div key={day.dayOfWeek} className={styles.hourRow}>
              <span className={styles.hourDay}>{day.dayOfWeek}</span>

              <div className={styles.hourControls}>
                <div className={styles.checkboxField}>
                  <input
                    id={`business-hours-${day.dayOfWeek}`}
                    type="checkbox"
                    checked={!day.isClosed}
                    onChange={(event) => handleDayToggle(day.dayOfWeek, !event.target.checked)}
                  />
                  <label className={styles.label} htmlFor={`business-hours-${day.dayOfWeek}`}>
                    Open
                  </label>
                </div>

                <input
                  className={styles.timeInput}
                  type="time"
                  aria-label={`${day.dayOfWeek} opening time`}
                  disabled={day.isClosed}
                  value={toInputTime(day.opensAt)}
                  onChange={(event) =>
                    patchDay(day.dayOfWeek, { opensAt: fromInputTime(event.target.value) })
                  }
                />

                <span className={styles.hourSeparator}>to</span>

                <input
                  className={styles.timeInput}
                  type="time"
                  aria-label={`${day.dayOfWeek} closing time`}
                  disabled={day.isClosed}
                  value={toInputTime(day.closesAt)}
                  onChange={(event) =>
                    patchDay(day.dayOfWeek, { closesAt: fromInputTime(event.target.value) })
                  }
                />
              </div>
            </div>
          ))}
        </div>
      </fieldset>

      <fieldset className={styles.section}>
        <legend className={styles.sectionLegend}>Status</legend>

        <div className={styles.checkboxField}>
          <input
            id="business-verified"
            name="isVerified"
            type="checkbox"
            checked={form.isVerified}
            aria-describedby="business-verified-hint"
            onChange={(event) => patch({ isVerified: event.target.checked })}
          />
          <label className={styles.label} htmlFor="business-verified">
            Verified
          </label>
        </div>
        <span className={styles.help} id="business-verified-hint">
          Whether a staff member has checked this listing. Publishing requires it, but taking the
          badge away does not retract the listing, so a wrong detail can be corrected in place.
        </span>

        <div className={styles.checkboxField}>
          <input
            id="business-published"
            name="isPublished"
            type="checkbox"
            checked={form.isPublished}
            aria-describedby="business-published-hint"
            onChange={(event) => patch({ isPublished: event.target.checked })}
          />
          <label className={styles.label} htmlFor="business-published">
            Published
          </label>
        </div>
        <span className={styles.help} id="business-published-hint">
          A draft is invisible to the public. The list above is what the last saved copy was still
          missing.
        </span>
        {isPublished && !isVerified ? (
          <p className={styles.note}>
            This listing is live without its verified badge. That is allowed on purpose, so a
            correction can be made without pulling the listing off the directory.
          </p>
        ) : null}
      </fieldset>

      <div className={styles.actions}>
        <Button type="submit" disabled={isSaving}>
          {isSaving ? 'Saving…' : id ? 'Save changes' : 'Create listing'}
        </Button>
        <Button type="button" variant="ghost" onClick={onCancel} disabled={isSaving}>
          Cancel
        </Button>
      </div>
    </form>
  );
}
