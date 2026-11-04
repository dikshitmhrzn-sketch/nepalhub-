import { useEffect, useState, type FormEvent } from 'react';

import { Button } from '@/components/ui/Button';
import { StateMessage } from '@/components/ui/StateMessage';
import { createDeal, updateDeal } from '@/features/deals/api';
import { slugifyTitle } from '@/features/deals/format';
import type { DealAdmin, SaveDealRequest } from '@/features/deals/types';
import { emptyDealRequest } from '@/features/deals/types';
import { useDealBusinessOptions, useStaffDeal } from '@/features/deals/useStaffDeals';
import { findProvinceCode, type ReferenceData } from '@/features/reference/useReferenceData';
import { submitDealSubmission, updateSubmission } from '@/features/submissions/api';
import { useDirectoryBusinessOptions } from '@/features/submissions/useDirectoryBusinessOptions';
import { useSubmissionDraft } from '@/features/submissions/useSubmissionDraft';
import { getErrorMessage, getFieldErrors } from '@/lib/apiErrors';

import styles from './DealEditor.module.css';

interface DealEditorProps {
  id: string | null;
  reference: ReferenceData;
  onCancel: () => void;
  onSaved: (saved: {
    id: string;
    title: string;
    isPublished: boolean;
    submissionId?: string;
  }) => void;
  /**
   * Present means this is a member's submission draft rather than a staff row: the form
   * loads through the submission detail endpoint, saves through the submission endpoints,
   * shows the author's note beside the body, and never offers the publish checkbox —
   * going live is what the queue decides. A null `submissionId` composes a brand-new one.
   */
  draft?: { submissionId: string | null };
}

function toRequest(detail: DealAdmin): SaveDealRequest {
  return {
    slug: detail.slug,
    title: detail.title,
    summary: detail.summary,
    description: detail.description,
    discountText: detail.discountText,
    businessSlug: detail.businessSlug,
    districtCode: detail.districtCode,
    redemptionUrl: detail.redemptionUrl,
    startsOn: detail.startsOn,
    endsOn: detail.endsOn,
    isPublished: detail.isPublished,
  };
}

/**
 * Create and edit in one form, because the API shares one request shape for both.
 *
 * The slug behaves the way it does everywhere else in the app: editable while the offer is a draft,
 * frozen once it is live, because by then the address is something people and search engines have
 * recorded. It is made read-only here so the reason is visible before a save is refused.
 *
 * The business picker searches on the server rather than listing every listing, for two reasons.
 * Only published businesses can be linked, and the endpoint is capped at 100 — so a business that
 * exists but has never been picked is genuinely not in the list until the search narrows it. The
 * selected slug is kept as a hidden input so a search that returns nothing cannot silently detach
 * the business the offer is already attached to.
 *
 * The same fields write a member's submission: with the `draft` prop the save runs through the
 * queue instead of the staff board, the author's note travels beside the body, the picker reads
 * the public directory rather than the staff endpoint, and the publish checkbox is gone —
 * publishing is the decision this form exists to ask for.
 */
export function DealEditor({ id, reference, onCancel, onSaved, draft }: DealEditorProps) {
  const isDraft = draft !== undefined;
  const draftId = draft?.submissionId ?? null;

  const detail = useStaffDeal(isDraft ? null : id);
  const loaded = detail.status === 'success' ? detail.data : null;
  const loadedId = loaded?.id ?? null;

  const draftDetail = useSubmissionDraft(isDraft ? draftId : null);
  const draftLoaded = draftDetail.status === 'success' ? draftDetail.data : null;
  const draftLoadedId = draftLoaded?.submission.id ?? null;

  const [form, setForm] = useState<SaveDealRequest>(emptyDealRequest);
  const [fieldErrors, setFieldErrors] = useState<Record<string, string>>({});
  const [formError, setFormError] = useState<string | null>(null);
  const [isSaving, setIsSaving] = useState(false);
  const [note, setNote] = useState('');
  const [businessSearch, setBusinessSearch] = useState('');
  const staffBusinessOptions = useDealBusinessOptions(businessSearch, !isDraft);
  const directoryBusinessOptions = useDirectoryBusinessOptions(businessSearch, isDraft);
  const businessOptions = isDraft ? directoryBusinessOptions : staffBusinessOptions;

  // The province is not posted, because the API reads it through the district. It is kept only to
  // decide which district list the select offers, and is recovered from the saved row.
  const [provinceCode, setProvinceCode] = useState('');

  useEffect(() => {
    if (isDraft) {
      // A submission's body arrives already in the request shape — it went out as one —
      // so the only projection is the publish flag, which this form may never express.
      const content = draftLoaded?.deal ?? null;

      if (content) {
        setForm({ ...content, isPublished: false });
        setNote(draftLoaded?.submission.note ?? '');
        setProvinceCode(findProvinceCode(reference, content.districtCode));
      } else if (!draftId) {
        setForm(emptyDealRequest());
        setNote('');
        setProvinceCode('');
      }

      return;
    }

    if (loaded) {
      setForm(toRequest(loaded));
      setProvinceCode(loaded.provinceCode ?? '');
    } else if (!id) {
      setForm(emptyDealRequest());
      setProvinceCode('');
    }
  }, [loadedId, draftLoadedId, isDraft, draftId, id]);

  const districts = provinceCode
    ? (reference.districtsByProvince[provinceCode] ?? [])
    : Object.values(reference.districtsByProvince).flat();

  function patch(changes: Partial<SaveDealRequest>) {
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

    const body: SaveDealRequest = {
      ...form,
      slug: form.slug.trim(),
      title: form.title.trim(),
      summary: form.summary?.trim() || null,
      description: form.description.trim(),
      discountText: form.discountText?.trim() || null,
      redemptionUrl: form.redemptionUrl?.trim() || null,
      districtCode: form.districtCode || null,
      isPublished: isDraft ? false : form.isPublished,
    };

    try {
      if (isDraft) {
        const saved = draftId
          ? await updateSubmission(draftId, {
              kind: 'Deal',
              deal: body,
              note: note.trim() || null,
            })
          : await submitDealSubmission({ content: body, note: note.trim() || null });
        onSaved({
          id: saved.itemId,
          title: saved.title,
          isPublished: saved.isPublished,
          submissionId: saved.id,
        });
      } else {
        const saved = id ? await updateDeal(id, body) : await createDeal(body);
        onSaved({ id: saved.id, title: saved.title, isPublished: saved.isPublished });
      }
    } catch (error) {
      setFieldErrors(getFieldErrors(error));
      setFormError(getErrorMessage(error, 'The offer could not be saved.'));
    } finally {
      setIsSaving(false);
    }
  }

  if (!isDraft && id && detail.status === 'loading') {
    return <StateMessage tone="loading" title="Loading the offer…" />;
  }

  if (!isDraft && id && detail.status === 'error') {
    return (
      <StateMessage
        tone="error"
        title="Could not load that offer"
        description={getErrorMessage(detail.error, 'It may have been deleted.')}
        action={
          <Button variant="secondary" size="small" onClick={onCancel}>
            Back to the list
          </Button>
        }
      />
    );
  }

  if (isDraft && draftId && draftDetail.status === 'loading') {
    return <StateMessage tone="loading" title="Loading your draft…" />;
  }

  if (isDraft && draftId && draftDetail.status === 'error') {
    return (
      <StateMessage
        tone="error"
        title="Could not load that submission"
        description={getErrorMessage(draftDetail.error, 'It may have been deleted.')}
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
      <h2 className={styles.title}>
        {isDraft
          ? draftId
            ? 'Edit your draft'
            : 'Send an offer for review'
          : id
            ? 'Edit offer'
            : 'New offer'}
      </h2>

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
          This offer is live, so its address is permanent. Unpublish it first if the address itself
          has to change.
        </p>
      ) : null}

      <div className={styles.field}>
        <label className={styles.label} htmlFor="deal-title">
          Title
        </label>
        <input
          className={styles.input}
          id="deal-title"
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
        <label className={styles.label} htmlFor="deal-slug">
          Slug
        </label>
        <div className={styles.slugRow}>
          <input
            className={styles.input}
            id="deal-slug"
            name="slug"
            type="text"
            readOnly={isPublished}
            required
            value={form.slug}
            aria-invalid={fieldErrors.slug ? true : undefined}
            aria-describedby="deal-slug-hint"
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
        <span className={styles.help} id="deal-slug-hint">
          Lowercase words with single hyphens, for example free-chiya-with-thakali-set. Editable
          while this is a draft.
        </span>
        {fieldErrors.slug ? <span className={styles.fieldError}>{fieldErrors.slug}</span> : null}
      </div>

      <div className={styles.field}>
        <label className={styles.label} htmlFor="deal-summary">
          Summary
        </label>
        <textarea
          className={styles.textarea}
          id="deal-summary"
          name="summary"
          rows={2}
          value={form.summary ?? ''}
          aria-invalid={fieldErrors.summary ? true : undefined}
          aria-describedby="deal-summary-hint"
          onChange={(event) => patch({ summary: event.target.value })}
        />
        <span className={styles.help} id="deal-summary-hint">
          One or two lines shown on the card, in the business&rsquo;s own words. Required before
          publishing.
        </span>
        {fieldErrors.summary ? (
          <span className={styles.fieldError}>{fieldErrors.summary}</span>
        ) : null}
      </div>

      <div className={styles.field}>
        <label className={styles.label} htmlFor="deal-discount">
          Discount
        </label>
        <input
          className={styles.input}
          id="deal-discount"
          name="discountText"
          type="text"
          placeholder="20% off with a Thakali set"
          value={form.discountText ?? ''}
          aria-invalid={fieldErrors.discountText ? true : undefined}
          aria-describedby="deal-discount-hint"
          onChange={(event) => patch({ discountText: event.target.value })}
        />
        <span className={styles.help} id="deal-discount-hint">
          The offer in short, exactly as it should be read on the card. No prices, no codes, no
          images — this is text, and anything more belongs in the description.
        </span>
        {fieldErrors.discountText ? (
          <span className={styles.fieldError}>{fieldErrors.discountText}</span>
        ) : null}
      </div>

      <div className={styles.field}>
        <label className={styles.label} htmlFor="deal-description">
          Description
        </label>
        <textarea
          className={styles.textarea}
          id="deal-description"
          name="description"
          rows={8}
          value={form.description}
          aria-invalid={fieldErrors.description ? true : undefined}
          aria-describedby="deal-description-hint"
          onChange={(event) => patch({ description: event.target.value })}
        />
        <span className={styles.help} id="deal-description-hint">
          The terms of the offer, shown as plain text with paragraph breaks kept. A draft can be
          saved without it, but an offer cannot be published without it.
        </span>
        {fieldErrors.description ? (
          <span className={styles.fieldError}>{fieldErrors.description}</span>
        ) : null}
      </div>

      <fieldset className={styles.section}>
        <legend className={styles.sectionLegend}>Where and when</legend>

        <div className={styles.field}>
          <label className={styles.label} htmlFor="deal-province">
            Province
          </label>
          <select
            className={styles.input}
            id="deal-province"
            name="province"
            value={provinceCode}
            onChange={(event) => {
              setProvinceCode(event.target.value);
              patch({ districtCode: null });
            }}
          >
            <option value="">Nationwide</option>
            {reference.provinces.map((province) => (
              <option key={province.code} value={province.code}>
                {province.name}
              </option>
            ))}
          </select>
          <span className={styles.help}>
            Optional. An offer with no district applies everywhere, which is the one thing a business
            listing cannot do.
          </span>
        </div>

        <div className={styles.field}>
          <label className={styles.label} htmlFor="deal-district">
            District
          </label>
          <select
            className={styles.input}
            id="deal-district"
            name="districtCode"
            value={form.districtCode ?? ''}
            disabled={districts.length === 0}
            aria-invalid={fieldErrors.districtCode ? true : undefined}
            aria-describedby="deal-district-hint"
            onChange={(event) => patch({ districtCode: event.target.value || null })}
          >
            <option value="">Applies nationwide</option>
            {districts.map((district) => (
              <option key={district.code} value={district.code}>
                {district.name}
              </option>
            ))}
          </select>
          <span className={styles.help} id="deal-district-hint">
            The province is read through this, so the two can never disagree.
          </span>
          {fieldErrors.districtCode ? (
            <span className={styles.fieldError}>{fieldErrors.districtCode}</span>
          ) : null}
        </div>

        <div className={styles.pair}>
          <div className={styles.field}>
            <label className={styles.label} htmlFor="deal-starts-on">
              Starts on
            </label>
            <input
              className={styles.input}
              id="deal-starts-on"
              name="startsOn"
              type="date"
              required
              value={form.startsOn}
              aria-invalid={fieldErrors.startsOn ? true : undefined}
              onChange={(event) => patch({ startsOn: event.target.value })}
            />
          </div>

          <div className={styles.field}>
            <label className={styles.label} htmlFor="deal-ends-on">
              Ends on
            </label>
            <input
              className={styles.input}
              id="deal-ends-on"
              name="endsOn"
              type="date"
              required
              value={form.endsOn}
              aria-invalid={fieldErrors.endsOn ? true : undefined}
              aria-describedby="deal-ends-hint"
              onChange={(event) => patch({ endsOn: event.target.value })}
            />
          </div>
        </div>
        <span className={styles.help} id="deal-ends-hint">
          Whole days, counted in Nepal time. The offer runs through the end day, so an offer ending
          today is still readable today.
        </span>
        {fieldErrors.startsOn ? (
          <span className={styles.fieldError}>{fieldErrors.startsOn}</span>
        ) : null}
        {fieldErrors.endsOn ? <span className={styles.fieldError}>{fieldErrors.endsOn}</span> : null}
      </fieldset>

      <fieldset className={styles.section}>
        <legend className={styles.sectionLegend}>Business and redemption</legend>

        <div className={styles.field}>
          <label className={styles.label} htmlFor="deal-business-search">
            Find a business
          </label>
          <input
            className={styles.input}
            id="deal-business-search"
            type="search"
            value={businessSearch}
            aria-describedby="deal-business-hint"
            onChange={(event) => setBusinessSearch(event.target.value)}
          />
          <select
            className={styles.input}
            name="businessSlug"
            value={form.businessSlug ?? ''}
            aria-invalid={fieldErrors.businessSlug ? true : undefined}
            aria-describedby="deal-business-hint"
            onChange={(event) => patch({ businessSlug: event.target.value || null })}
          >
            <option value="">No business attached</option>
            {businessOptions.data.map((option) => (
              <option key={option.slug} value={option.slug}>
                {option.name}
              </option>
            ))}
          </select>
          <span className={styles.help} id="deal-business-hint">
            Only published businesses can be attached, and the search looks through the directory
            rather than the offers. Optional: an offer can exist before the business is listed here.
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
          <label className={styles.label} htmlFor="deal-redemption-url">
            Redemption link
          </label>
          <input
            className={styles.input}
            id="deal-redemption-url"
            name="redemptionUrl"
            type="url"
            placeholder="https://"
            value={form.redemptionUrl ?? ''}
            aria-invalid={fieldErrors.redemptionUrl ? true : undefined}
            aria-describedby="deal-redemption-hint"
            onChange={(event) => patch({ redemptionUrl: event.target.value })}
          />
          <span className={styles.help} id="deal-redemption-hint">
            A full https address, on the business&rsquo;s own site or the brand&rsquo;s campaign page.
            Nepal Hub stores the address only and never fetches it.
          </span>
          {fieldErrors.redemptionUrl ? (
            <span className={styles.fieldError}>{fieldErrors.redemptionUrl}</span>
          ) : null}
        </div>
      </fieldset>

      {!isDraft ? (
        <fieldset className={styles.section}>
          <legend className={styles.sectionLegend}>Status</legend>

          <div className={styles.checkboxField}>
            <input
              id="deal-published"
              name="isPublished"
              type="checkbox"
              checked={form.isPublished}
              aria-describedby="deal-published-hint"
              onChange={(event) => patch({ isPublished: event.target.checked })}
            />
            <label className={styles.label} htmlFor="deal-published">
              Published
            </label>
          </div>
          <span className={styles.help} id="deal-published-hint">
            A draft is invisible to the public. The list above is what the last saved copy was still
            missing.
          </span>
        </fieldset>
      ) : (
        <fieldset className={styles.section}>
          <legend className={styles.sectionLegend}>Note to staff</legend>

          <div className={styles.field}>
            <label className={styles.label} htmlFor="deal-submission-note">
              Anything they should know (optional)
            </label>
            <input
              className={styles.input}
              id="deal-submission-note"
              name="note"
              type="text"
              maxLength={500}
              value={note}
              aria-invalid={fieldErrors.note ? true : undefined}
              aria-describedby="deal-submission-note-hint"
              placeholder="Which shop this was agreed with, and until when."
              onChange={(event) => setNote(event.target.value)}
            />
            <span className={styles.help} id="deal-submission-note-hint">
              Up to 500 characters, read beside this draft in the staff queue. The offer stays
              invisible to the public until staff approve it.
            </span>
            {fieldErrors.note ? (
              <span className={styles.fieldError}>{fieldErrors.note}</span>
            ) : null}
          </div>
        </fieldset>
      )}

      <div className={styles.actions}>
        <Button type="submit" disabled={isSaving}>
          {isSaving
            ? 'Saving…'
            : isDraft
              ? draftId
                ? 'Save changes'
                : 'Send for review'
              : id
                ? 'Save changes'
                : 'Create offer'}
        </Button>
        <Button type="button" variant="ghost" onClick={onCancel} disabled={isSaving}>
          Cancel
        </Button>
      </div>
    </form>
  );
}