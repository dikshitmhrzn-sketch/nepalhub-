import { useEffect, useState, type FormEvent } from 'react';
import { Link, useParams } from 'react-router';

import { Button } from '@/components/ui/Button';
import { Container } from '@/components/ui/Container';
import { StateMessage } from '@/components/ui/StateMessage';
import {
  categoryLabel,
  slugifyName,
} from '@/features/businesses/format';
import {
  businessCategories,
  categoryLabels,
  shortDayNames,
  type BusinessCategory,
  type SaveBusinessHours,
} from '@/features/businesses/types';
import { useAuth } from '@/features/auth/AuthContext';
import {
  registerBusiness,
  saveMyBusiness,
  submitVerification,
} from '@/features/ownership/api';
import {
  useMyBusiness,
  useMyBusinesses,
} from '@/features/ownership/useMyBusinesses';
import {
  claimStatusLabels,
  verificationStatusLabels,
  type RegisterBusinessRequest,
  type SaveOwnerBusinessRequest,
} from '@/features/ownership/types';
import { useReferenceData } from '@/features/reference/useReferenceData';
import { startCheckout } from '@/features/payments/api';
import { checkoutErrorMessage, openProviderCheckout } from '@/features/payments/checkout';
import { usePaymentConfig } from '@/features/payments/usePayments';
import { requestPlan } from '@/features/subscriptions/api';
import {
  describeSubscription,
  planTierLabels,
} from '@/features/subscriptions/types';
import {
  usePlans,
  useSubscriptionState,
} from '@/features/subscriptions/useSubscriptions';
import { ApiError } from '@/lib/apiClient';
import { getErrorMessage } from '@/lib/apiErrors';
import type { FieldError } from '@/features/businesses/types';

import styles from './MyBusinessPage.module.css';

/**
 * The ownership screen: what this person manages, where each claim and verification stands,
 * the form for registering a listing of their own, and the editor for one listing — details
 * on the left, plan on the right.
 *
 * The editor only offers what the API lets an owner touch: while the listing is a draft,
 * the address it will live at, its name, category and district — the identity a typo is
 * made in — plus description, contact details and the week. Once it is published those
 * belong to staff, and the panel says so rather than letting an edit fail on save.
 */
export function MyBusinessPage() {
  const { slug } = useParams();
  const { status } = useAuth();

  if (status === 'anonymous') {
    return (
      <Container as="section" className={styles.page}>
        <StateMessage
          tone="error"
          title="Sign in to see your businesses"
          description="The ownership screen is for signed-in members. It lists the listings you manage and every claim you have filed."
          action={
            <Link to="/login" className={styles.actionLink}>
              Sign in
            </Link>
          }
        />
      </Container>
    );
  }

  return slug ? <EditorView slug={slug} /> : <ListView />;
}

function ListView() {
  const list = useMyBusinesses();

  if (list.status === 'loading') {
    return (
      <Container as="section" className={styles.page}>
        <StateMessage tone="loading" title="Loading your businesses…" />
      </Container>
    );
  }

  if (list.status === 'error') {
    return (
      <Container as="section" className={styles.page}>
        <StateMessage
          tone="error"
          title="Could not load your businesses"
          description={getErrorMessage(list.error, 'Please try again in a moment.')}
          action={
            <Button variant="secondary" size="small" onClick={list.reload}>
              Try again
            </Button>
          }
        />
      </Container>
    );
  }

  const { owned, claims, verifications } = list.data;

  return (
    <Container as="section" className={styles.page}>
      <header className={styles.header}>
        <h1 className={styles.title}>My businesses</h1>
        <p className={styles.subtitle}>
          The listings you manage, the claims you have filed, and every request you have made
          for a listing to be checked. A listing you register here starts as a draft — it
          reaches the directory when our staff approve it, and that approval is also what
          gives it the verified badge.
        </p>
      </header>

      <section className={styles.section}>
        <h2 className={styles.sectionTitle}>Listings you manage</h2>

        {owned.length === 0 ? (
          <p className={styles.empty}>
            You do not manage a listing yet. Register yours in the form below, or find it in
            the directory and claim it from its page.
          </p>
        ) : (
          <ul className={styles.rows}>
            {owned.map((business) => (
              <li key={business.id} className={styles.row}>
                <div className={styles.rowMain}>
                  <p className={styles.rowName}>
                    <Link
                      to={`/my-business/${encodeURIComponent(business.slug)}`}
                      className={styles.rowLink}
                    >
                      {business.name}
                    </Link>
                  </p>
                  <p className={styles.rowMeta}>
                    {categoryLabel(business.category)} · {business.districtName}
                  </p>
                </div>

                <div className={styles.rowBadges}>
                  <span
                    className={
                      business.isPublished
                        ? `${styles.badge} ${styles.badgeOn}`
                        : `${styles.badge} ${styles.badgeOff}`
                    }
                  >
                    {business.isPublished ? 'Published' : 'Draft'}
                  </span>
                  <span
                    className={
                      business.isVerified
                        ? `${styles.badge} ${styles.badgeOn}`
                        : `${styles.badge} ${styles.badgeOff}`
                    }
                  >
                    {business.isVerified ? 'Verified' : 'Unchecked'}
                  </span>
                </div>

                <Link
                  to={`/my-business/${encodeURIComponent(business.slug)}`}
                  className={styles.rowAction}
                >
                  Manage
                </Link>
              </li>
            ))}
          </ul>
        )}
      </section>

      <RegisterForm onRegistered={list.reload} />

      <section className={styles.section}>
        <h2 className={styles.sectionTitle}>Verification requests</h2>

        {verifications.length === 0 ? (
          <p className={styles.empty}>
            No requests yet. Register the listing above, or open one of your drafts and ask us
            to check it — approval is what publishes it and gives it the verified badge.
          </p>
        ) : (
          <ul className={styles.rows}>
            {verifications.map((request) => (
              <li key={request.id} className={styles.row}>
                <div className={styles.rowMain}>
                  <p className={styles.rowName}>
                    <Link
                      to={`/my-business/${encodeURIComponent(request.slug)}`}
                      className={styles.rowLink}
                    >
                      {request.businessName}
                    </Link>
                  </p>
                  <p className={styles.rowMeta}>
                    Asked {new Date(request.createdAt).toLocaleDateString()}
                    {request.reviewNote ? ` — “${request.reviewNote}”` : ''}
                  </p>
                </div>

                <span
                  className={
                    request.status === 'Pending'
                      ? `${styles.badge} ${styles.badgeOn}`
                      : `${styles.badge} ${styles.badgeOff}`
                  }
                >
                  {verificationStatusLabels[request.status]}
                </span>
              </li>
            ))}
          </ul>
        )}
      </section>

      <section className={styles.section}>
        <h2 className={styles.sectionTitle}>Claims you have filed</h2>

        {claims.length === 0 ? (
          <p className={styles.empty}>
            No claims yet. If a listing in the directory is yours, open it and claim it.
          </p>
        ) : (
          <ul className={styles.rows}>
            {claims.map((claim) => (
              <li key={claim.id} className={styles.row}>
                <div className={styles.rowMain}>
                  <p className={styles.rowName}>
                    <Link
                      to={`/businesses/${encodeURIComponent(claim.slug)}`}
                      className={styles.rowLink}
                    >
                      {claim.businessName}
                    </Link>
                  </p>
                  <p className={styles.rowMeta}>
                    Filed {new Date(claim.createdAt).toLocaleDateString()}
                    {claim.reviewNote ? ` — “${claim.reviewNote}”` : ''}
                  </p>
                </div>

                <span
                  className={
                    claim.status === 'Pending'
                      ? `${styles.badge} ${styles.badgeOn}`
                      : `${styles.badge} ${styles.badgeOff}`
                  }
                >
                  {claimStatusLabels[claim.status]}
                </span>
              </li>
            ))}
          </ul>
        )}
      </section>
    </Container>
  );
}

interface EditorViewProps {
  slug: string;
}

/**
 * Registering a listing the directory has never seen. The row this creates is born a
 * draft — owned, unpublished and unchecked — so the form asks only for what the API
 * requires to place it: the address it will live at, its name, category and district.
 * Everything else, including the week, is corrected in the editor afterwards.
 */
function RegisterForm({ onRegistered }: { onRegistered: () => void }) {
  const reference = useReferenceData();
  const [name, setName] = useState('');
  const [slug, setSlug] = useState('');
  const [slugTouched, setSlugTouched] = useState(false);
  const [category, setCategory] = useState<BusinessCategory>('Restaurants');
  const [districtCode, setDistrictCode] = useState('');
  const [description, setDescription] = useState('');
  const [phone, setPhone] = useState('');
  const [busy, setBusy] = useState(false);
  const [formError, setFormError] = useState<string | null>(null);
  const [fieldErrors, setFieldErrors] = useState<FieldError[]>([]);
  const [savedName, setSavedName] = useState<string | null>(null);

  function errorFor(field: string): string | undefined {
    return fieldErrors.find((item) => item.field === field)?.message;
  }

  function handleNameChange(next: string) {
    setName(next);

    // The slug follows the name until it has been edited by hand, so the address a person
    // wants is offered to them rather than something they must retype.
    if (!slugTouched) {
      setSlug(slugifyName(next));
    }
  }

  async function handleRegister(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();

    const body: RegisterBusinessRequest = {
      slug: slug.trim(),
      name: name.trim(),
      description,
      category,
      districtCode,
      address: null,
      phone: phone.trim() || null,
      email: null,
      website: null,
      openingHours: [],
    };

    setBusy(true);
    setFormError(null);
    setFieldErrors([]);
    setSavedName(null);

    try {
      const created = await registerBusiness(body);
      setSavedName(created.name);
      setName('');
      setSlug('');
      setSlugTouched(false);
      setDescription('');
      setPhone('');
      onRegistered();
    } catch (registerError) {
      if (registerError instanceof ApiError && registerError.code === 'slug_taken') {
        setFormError('That address is already used by another listing. Choose a different one.');
      } else if (registerError instanceof ApiError && registerError.code === 'validation_failed') {
        setFieldErrors(
          Array.isArray(registerError.details) ? (registerError.details as FieldError[]) : [],
        );
        setFormError('Some fields need attention before the listing can be registered.');
      } else if (registerError instanceof ApiError && registerError.status === 429) {
        setFormError('Too many registrations in a short time. Please try again later.');
      } else {
        setFormError(
          getErrorMessage(registerError, 'Could not register the listing. Please try again.'),
        );
      }
    } finally {
      setBusy(false);
    }
  }

  return (
    <section className={styles.section}>
      <h2 className={styles.sectionTitle}>Register a listing</h2>
      <p className={styles.hoursNote}>
        Your business is not on Nepal Hub yet? Register it here. It starts as a draft that
        nobody but you can see, and it appears on the directory once our staff approve it.
      </p>

      <form className={styles.form} onSubmit={(event) => void handleRegister(event)}>
        <div className={styles.grid}>
          <label className={styles.field}>
            <span className={styles.label}>Name</span>
            <input
              className={styles.input}
              type="text"
              maxLength={160}
              required
              value={name}
              disabled={busy}
              aria-invalid={errorFor('name') ? true : undefined}
              onChange={(event) => handleNameChange(event.target.value)}
            />
            {errorFor('name') ? (
              <span className={styles.fieldError}>{errorFor('name')}</span>
            ) : null}
          </label>

          <label className={styles.field}>
            <span className={styles.label}>Address (slug)</span>
            <input
              className={styles.input}
              type="text"
              maxLength={160}
              required
              value={slug}
              disabled={busy}
              aria-invalid={errorFor('slug') ? true : undefined}
              onChange={(event) => {
                setSlugTouched(true);
                setSlug(event.target.value);
              }}
            />
            <span className={styles.hoursNote}>
              Lowercase words with single hyphens, for example newa-momo-house-lalitpur.
            </span>
            {errorFor('slug') ? (
              <span className={styles.fieldError}>{errorFor('slug')}</span>
            ) : null}
          </label>

          <label className={styles.field}>
            <span className={styles.label}>Category</span>
            <select
              className={styles.input}
              value={category}
              disabled={busy}
              aria-invalid={errorFor('category') ? true : undefined}
              onChange={(event) => setCategory(event.target.value as BusinessCategory)}
            >
              {businessCategories.map((value) => (
                <option key={value} value={value}>
                  {categoryLabels[value]}
                </option>
              ))}
            </select>
            {errorFor('category') ? (
              <span className={styles.fieldError}>{errorFor('category')}</span>
            ) : null}
          </label>

          <label className={styles.field}>
            <span className={styles.label}>District</span>
            <select
              className={styles.input}
              value={districtCode}
              disabled={busy || reference.status !== 'success'}
              aria-invalid={errorFor('districtCode') ? true : undefined}
              onChange={(event) => setDistrictCode(event.target.value)}
            >
              <option value="">Choose a district</option>
              {reference.status === 'success'
                ? reference.data.provinces.map((province) => (
                    <optgroup key={province.code} label={province.name}>
                      {(reference.data.districtsByProvince[province.code] ?? []).map(
                        (district) => (
                          <option key={district.code} value={district.code}>
                            {district.name}
                          </option>
                        ),
                      )}
                    </optgroup>
                  ))
                : null}
            </select>
            {errorFor('districtCode') ? (
              <span className={styles.fieldError}>{errorFor('districtCode')}</span>
            ) : null}
          </label>

          <label className={styles.field}>
            <span className={styles.label}>Phone</span>
            <input
              className={styles.input}
              type="text"
              maxLength={20}
              value={phone}
              disabled={busy}
              aria-invalid={errorFor('phone') ? true : undefined}
              onChange={(event) => setPhone(event.target.value)}
            />
            {errorFor('phone') ? (
              <span className={styles.fieldError}>{errorFor('phone')}</span>
            ) : null}
          </label>
        </div>

        <label className={styles.field}>
          <span className={styles.label}>Description</span>
          <textarea
            className={styles.textarea}
            rows={6}
            maxLength={20000}
            value={description}
            disabled={busy}
            aria-invalid={errorFor('description') ? true : undefined}
            onChange={(event) => setDescription(event.target.value)}
          />
          {errorFor('description') ? (
            <span className={styles.fieldError}>{errorFor('description')}</span>
          ) : null}
        </label>

        {formError ? (
          <p className={styles.error} role="alert">
            {formError}
          </p>
        ) : null}

        {savedName ? (
          <p className={styles.saved} role="status">
            {savedName} is registered as a draft. Open it below to finish it and ask us to
            check it.
          </p>
        ) : null}

        <div className={styles.actions}>
          <Button type="submit" disabled={busy}>
            {busy ? 'Registering…' : 'Register the listing'}
          </Button>
          <span className={styles.readonlyNote}>
            A registration grants authorship of a draft only — publishing and the verified
            badge come from the staff decision on your request.
          </span>
        </div>
      </form>
    </section>
  );
}

function EditorView({ slug }: EditorViewProps) {
  return (
    <Container as="section" className={styles.page}>
      <Link to="/my-business" className={styles.backLink}>
        ← Back to my businesses
      </Link>

      <Editor slug={slug} />
    </Container>
  );
}

function Editor({ slug }: { slug: string }) {
  const business = useMyBusiness(slug);
  const subscription = useSubscriptionState(slug);
  const plans = usePlans();
  const paymentConfig = usePaymentConfig();
  const reference = useReferenceData();

  const [form, setForm] = useState<SaveOwnerBusinessRequest | null>(null);
  const [saving, setSaving] = useState(false);
  const [saved, setSaved] = useState(false);
  const [formError, setFormError] = useState<string | null>(null);
  const [fieldErrors, setFieldErrors] = useState<FieldError[]>([]);
  const [planBusy, setPlanBusy] = useState(false);
  const [planMessage, setPlanMessage] = useState<string | null>(null);
  const [planError, setPlanError] = useState<string | null>(null);
  const [verifyNote, setVerifyNote] = useState('');
  const [verifying, setVerifying] = useState(false);
  const [verifyError, setVerifyError] = useState<string | null>(null);
  const [verifyMessage, setVerifyMessage] = useState<string | null>(null);

  const detail = business.status === 'success' ? business.data : null;

  useEffect(() => {
    if (!detail) {
      return;
    }

    setForm({
      slug: detail.slug,
      name: detail.name,
      category: detail.category,
      districtCode: detail.districtCode,
      description: detail.description,
      address: detail.address,
      phone: detail.phone,
      email: detail.email,
      website: detail.website,
      openingHours: detail.openingHours.map((day) => ({ ...day })),
    });
    setSaved(false);
    setFormError(null);
    setFieldErrors([]);
  }, [detail]);

  if (business.status === 'loading') {
    return <StateMessage tone="loading" title="Loading the listing…" />;
  }

  if (business.status === 'error') {
    const isMissing = business.error.status === 404;

    return (
      <StateMessage
        tone="error"
        title={isMissing ? 'That listing is not yours to edit' : 'Could not load the listing'}
        description={
          isMissing
            ? 'It may have been taken off the directory, or it may belong to a different account.'
            : getErrorMessage(business.error, 'Please try again in a moment.')
        }
        action={
          <Link to="/my-business" className={styles.actionLink}>
            Back to my businesses
          </Link>
        }
      />
    );
  }

  if (!form || !detail) {
    return (
      <StateMessage tone="loading" title="Loading the listing…" />
    );
  }

  function errorFor(field: string): string | undefined {
    return fieldErrors.find((item) => item.field === field)?.message;
  }

  function updateDay(dayOfWeek: SaveBusinessHours['dayOfWeek'], changes: Partial<SaveBusinessHours>) {
    setForm((current) =>
      current
        ? {
            ...current,
            openingHours: current.openingHours.map((day) =>
              day.dayOfWeek === dayOfWeek ? { ...day, ...changes } : day,
            ),
          }
        : current,
    );
  }

  async function handleSave(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();

    // The early return above guarantees a form and a detail, but TypeScript resets that
    // narrowing inside a function declaration, so the guard is restated where they are used.
    if (!form || !detail) {
      return;
    }

    // The identity fields travel only while the listing is a draft. On a published row the
    // API would refuse a change to any of them, so they are not even offered — omitting
    // them is how the API is told to leave them alone.
    const identity = detail.isPublished
      ? {}
      : {
          slug: form.slug?.trim() || null,
          name: form.name?.trim() || null,
          category: form.category,
          districtCode: form.districtCode?.trim() || null,
        };
    const body: SaveOwnerBusinessRequest = { ...form, ...identity };
    setSaving(true);
    setSaved(false);
    setFormError(null);
    setFieldErrors([]);

    try {
      await saveMyBusiness(slug, body);
      setSaved(true);
      subscription.reload();
      business.reload();
    } catch (saveError) {
      if (saveError instanceof ApiError && saveError.code === 'validation_failed') {
        setFieldErrors(Array.isArray(saveError.details) ? (saveError.details as FieldError[]) : []);
        setFormError('Some fields need attention before this can be saved.');
      } else if (saveError instanceof ApiError && saveError.code === 'slug_taken') {
        setFormError('That address is already used by another listing. Choose a different one.');
      } else if (saveError instanceof ApiError && saveError.status === 404) {
        setFormError('That listing is no longer yours to edit.');
      } else {
        setFormError(getErrorMessage(saveError, 'Could not save the listing. Please try again.'));
      }
    } finally {
      setSaving(false);
    }
  }

  // Whether checkout exists at all. A screen that has not loaded that answer yet keeps to
  // the request path, and the server refuses any checkout it cannot serve.
  const canPay = paymentConfig.status === 'success' && paymentConfig.data.available;

  async function handleChoosePlan(tier: 'Free' | 'Featured', priceNpr: number) {
    setPlanBusy(true);
    setPlanMessage(null);
    setPlanError(null);

    // A plan with a price and a provider to take it goes to the provider: the checkout
    // opens the question in the queue, the money answers it, and the browser is on its
    // way to eSewa before anything else happens here. Without a provider the request
    // path below stands, which is what the site did before payment existed.
    if (priceNpr > 0 && canPay) {
      try {
        const handshake = await startCheckout({ kind: 'Subscription', slug, tier });
        openProviderCheckout(handshake);
      } catch (payError) {
        setPlanError(
          checkoutErrorMessage(payError, 'Could not start the payment. Please try again.'),
        );
        setPlanBusy(false);
      }

      // Either the browser is leaving, or the reason it could not is on screen.
      return;
    }

    try {
      await requestPlan(slug, tier, null);
      setPlanMessage(
        tier === 'Free'
          ? 'The free plan is on your listing now.'
          : 'Your featured request is with our staff. It appears on the directory once approved.',
      );
      subscription.reload();
    } catch (requestError) {
      if (requestError instanceof ApiError && requestError.code === 'request_pending') {
        setPlanError('A request for this listing is already waiting for a decision.');
      } else if (requestError instanceof ApiError && requestError.code === 'plan_unavailable') {
        setPlanError('That plan cannot be granted right now.');
      } else {
        setPlanError(getErrorMessage(requestError, 'Could not send the request. Please try again.'));
      }
    } finally {
      setPlanBusy(false);
    }
  }

  // Asking staff to check this draft. Approval is what publishes it, so the panel reads the
  // newest state back off the listing rather than trusting the click that just happened.
  async function handleVerify(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();

    setVerifying(true);
    setVerifyError(null);
    setVerifyMessage(null);

    try {
      await submitVerification(slug, verifyNote.trim() || null);
      setVerifyNote('');
      setVerifyMessage(
        'Your request is with our staff. The listing appears on the directory, carrying the verified badge, once they approve it.',
      );
      business.reload();
    } catch (verifyFailure) {
      if (verifyFailure instanceof ApiError && verifyFailure.code === 'verification_pending') {
        setVerifyError('A request for this listing is already waiting for a decision.');
        business.reload();
      } else if (verifyFailure instanceof ApiError && verifyFailure.code === 'already_published') {
        setVerifyError('This listing is already on the directory.');
        business.reload();
      } else if (verifyFailure instanceof ApiError && verifyFailure.status === 404) {
        setVerifyError('That listing is no longer yours to manage.');
      } else if (verifyFailure instanceof ApiError && verifyFailure.status === 429) {
        setVerifyError('Too many requests in a short time. Please try again later.');
      } else {
        setVerifyError(
          getErrorMessage(verifyFailure, 'Could not send the request. Please try again.'),
        );
      }
    } finally {
      setVerifying(false);
    }
  }

  const state = subscription.status === 'success' ? subscription.data : null;
  const planList = plans.status === 'success' ? plans.data : [];

  return (
    <>
      <header className={styles.header}>
        <div className={styles.headerRow}>
          <h1 className={styles.title}>{detail.name}</h1>
          <div className={styles.rowBadges}>
            <span
              className={
                detail.isPublished
                  ? `${styles.badge} ${styles.badgeOn}`
                  : `${styles.badge} ${styles.badgeOff}`
              }
            >
              {detail.isPublished ? 'Published' : 'Draft'}
            </span>
            <span
              className={
                detail.isVerified
                  ? `${styles.badge} ${styles.badgeOn}`
                  : `${styles.badge} ${styles.badgeOff}`
              }
            >
              {detail.isVerified ? 'Verified' : 'Unchecked'}
            </span>
          </div>
        </div>
        <p className={styles.subtitle}>
          {categoryLabel(detail.category)} · {detail.districtName} · Save time{' '}
          {new Date(detail.updatedAt).toLocaleString()}
        </p>
      </header>

      <div className={styles.columns}>
        <section className={styles.section}>
          <h2 className={styles.sectionTitle}>Listing details</h2>

          <form className={styles.form} onSubmit={(event) => void handleSave(event)}>
            {detail.isPublished ? null : (
              <>
                <p className={styles.hoursNote}>
                  The address this listing will live at, its name, category and district are
                  yours to correct while it is a draft. Once it is published they belong to
                  our staff.
                </p>

                <div className={styles.grid}>
                  <label className={styles.field}>
                    <span className={styles.label}>Name</span>
                    <input
                      className={styles.input}
                      type="text"
                      maxLength={160}
                      value={form.name ?? ''}
                      disabled={saving}
                      aria-invalid={errorFor('name') ? true : undefined}
                      onChange={(event) => {
                        const name = event.target.value;
                        // The address follows the name until it has been edited by hand, so a
                        // draft renamed from its own typo does not keep the typo's address.
                        const slugUntouched =
                          !form.slug || form.slug === slugifyName(form.name ?? '');
                        setForm({
                          ...form,
                          name,
                          slug: slugUntouched ? slugifyName(name) : form.slug,
                        });
                      }}
                    />
                    {errorFor('name') ? (
                      <span className={styles.fieldError}>{errorFor('name')}</span>
                    ) : null}
                  </label>

                  <label className={styles.field}>
                    <span className={styles.label}>Address (slug)</span>
                    <input
                      className={styles.input}
                      type="text"
                      maxLength={160}
                      value={form.slug ?? ''}
                      disabled={saving}
                      aria-invalid={errorFor('slug') ? true : undefined}
                      onChange={(event) =>
                        setForm({ ...form, slug: event.target.value || null })
                      }
                    />
                    <span className={styles.hoursNote}>
                      Lowercase words with single hyphens, for example newa-momo-house-lalitpur.
                    </span>
                    {errorFor('slug') ? (
                      <span className={styles.fieldError}>{errorFor('slug')}</span>
                    ) : null}
                  </label>

                  <label className={styles.field}>
                    <span className={styles.label}>Category</span>
                    <select
                      className={styles.input}
                      value={form.category ?? ''}
                      disabled={saving}
                      aria-invalid={errorFor('category') ? true : undefined}
                      onChange={(event) =>
                        setForm({
                          ...form,
                          category: event.target.value as BusinessCategory,
                        })
                      }
                    >
                      {businessCategories.map((value) => (
                        <option key={value} value={value}>
                          {categoryLabels[value]}
                        </option>
                      ))}
                    </select>
                    {errorFor('category') ? (
                      <span className={styles.fieldError}>{errorFor('category')}</span>
                    ) : null}
                  </label>

                  <label className={styles.field}>
                    <span className={styles.label}>District</span>
                    <select
                      className={styles.input}
                      value={form.districtCode ?? ''}
                      disabled={saving || reference.status !== 'success'}
                      aria-invalid={errorFor('districtCode') ? true : undefined}
                      onChange={(event) =>
                        setForm({ ...form, districtCode: event.target.value || null })
                      }
                    >
                      <option value="">Choose a district</option>
                      {reference.status === 'success'
                        ? reference.data.provinces.map((province) => (
                            <optgroup key={province.code} label={province.name}>
                              {(reference.data.districtsByProvince[province.code] ?? []).map(
                                (district) => (
                                  <option key={district.code} value={district.code}>
                                    {district.name}
                                  </option>
                                ),
                              )}
                            </optgroup>
                          ))
                        : null}
                    </select>
                    <span className={styles.hoursNote}>
                      The province is read through the district, so the two cannot disagree.
                    </span>
                    {errorFor('districtCode') ? (
                      <span className={styles.fieldError}>{errorFor('districtCode')}</span>
                    ) : null}
                  </label>
                </div>
              </>
            )}

            <label className={styles.field}>
              <span className={styles.label}>Description</span>
              <textarea
                className={styles.textarea}
                rows={8}
                maxLength={20000}
                value={form.description}
                disabled={saving}
                onChange={(event) =>
                  setForm({ ...form, description: event.target.value })
                }
              />
              {errorFor('description') ? (
                <span className={styles.fieldError}>{errorFor('description')}</span>
              ) : null}
            </label>

            <div className={styles.grid}>
              <label className={styles.field}>
                <span className={styles.label}>Address</span>
                <input
                  className={styles.input}
                  type="text"
                  maxLength={240}
                  value={form.address ?? ''}
                  disabled={saving}
                  onChange={(event) =>
                    setForm({ ...form, address: event.target.value || null })
                  }
                />
                {errorFor('address') ? (
                  <span className={styles.fieldError}>{errorFor('address')}</span>
                ) : null}
              </label>

              <label className={styles.field}>
                <span className={styles.label}>Phone</span>
                <input
                  className={styles.input}
                  type="text"
                  maxLength={20}
                  value={form.phone ?? ''}
                  disabled={saving}
                  onChange={(event) =>
                    setForm({ ...form, phone: event.target.value || null })
                  }
                />
                {errorFor('phone') ? (
                  <span className={styles.fieldError}>{errorFor('phone')}</span>
                ) : null}
              </label>

              <label className={styles.field}>
                <span className={styles.label}>Email</span>
                <input
                  className={styles.input}
                  type="email"
                  maxLength={254}
                  value={form.email ?? ''}
                  disabled={saving}
                  onChange={(event) =>
                    setForm({ ...form, email: event.target.value || null })
                  }
                />
                {errorFor('email') ? (
                  <span className={styles.fieldError}>{errorFor('email')}</span>
                ) : null}
              </label>

              <label className={styles.field}>
                <span className={styles.label}>Website</span>
                <input
                  className={styles.input}
                  type="url"
                  maxLength={500}
                  value={form.website ?? ''}
                  disabled={saving}
                  onChange={(event) =>
                    setForm({ ...form, website: event.target.value || null })
                  }
                />
                {errorFor('website') ? (
                  <span className={styles.fieldError}>{errorFor('website')}</span>
                ) : null}
              </label>
            </div>

            <h3 className={styles.subheading}>Opening hours</h3>
            <p className={styles.hoursNote}>
              The whole week is sent every time you save: a day left off here is a day the
              listing will show as closed.
            </p>

            <ul className={styles.hours}>
              {form.openingHours.map((day) => (
                <li key={day.dayOfWeek} className={styles.hoursRow}>
                  <span className={styles.hoursDay}>{shortDayNames[day.dayOfWeek]}</span>
                  <label className={styles.hoursClosed}>
                    <input
                      type="checkbox"
                      checked={day.isClosed}
                      disabled={saving}
                      onChange={(event) =>
                        updateDay(day.dayOfWeek, { isClosed: event.target.checked })
                      }
                    />
                    Closed
                  </label>
                  <input
                    className={styles.timeInput}
                    type="time"
                    step={1}
                    aria-label={`${day.dayOfWeek} opens`}
                    value={day.opensAt ? day.opensAt.slice(0, 5) : ''}
                    disabled={saving || day.isClosed}
                    onChange={(event) =>
                      updateDay(day.dayOfWeek, {
                        opensAt: event.target.value ? `${event.target.value}:00` : null,
                      })
                    }
                  />
                  <span className={styles.hoursDash}>to</span>
                  <input
                    className={styles.timeInput}
                    type="time"
                    step={1}
                    aria-label={`${day.dayOfWeek} closes`}
                    value={day.closesAt ? day.closesAt.slice(0, 5) : ''}
                    disabled={saving || day.isClosed}
                    onChange={(event) =>
                      updateDay(day.dayOfWeek, {
                        closesAt: event.target.value ? `${event.target.value}:00` : null,
                      })
                    }
                  />
                </li>
              ))}
            </ul>
            {errorFor('openingHours') ? (
              <span className={styles.fieldError}>{errorFor('openingHours')}</span>
            ) : null}

            {formError ? (
              <p className={styles.error} role="alert">
                {formError}
              </p>
            ) : null}

            {saved ? (
              <p className={styles.saved} role="status">
                Saved. The listing now shows these details.
              </p>
            ) : null}

            <div className={styles.actions}>
              <Button type="submit" disabled={saving}>
                {saving ? 'Saving…' : 'Save changes'}
              </Button>
              <span className={styles.readonlyNote}>
                {detail.isPublished
                  ? 'Name, category, district, publishing and verification are kept by our staff — ask us through the report link if any of those are wrong.'
                  : 'Contact details and the week can be corrected at any time; the fields above are yours while this is a draft, and publishing comes from the verification below.'}
              </span>
            </div>
          </form>
        </section>

        <section className={styles.section}>
          <h2 className={styles.sectionTitle}>Verification</h2>

          {detail.isPublished ? (
            <p className={styles.planWindow}>
              This listing is on the directory and is checked by our staff. The verified badge
              follows from that check.
            </p>
          ) : detail.verification?.status === 'Pending' ? (
            <div className={styles.planBox}>
              <p className={styles.planState}>
                Asked on {new Date(detail.verification.createdAt).toLocaleDateString()} —
                waiting for a staff decision.
              </p>
              <p className={styles.planWindow}>
                Our staff are checking this listing. It appears on the directory, with the
                verified badge, once it is approved.
              </p>
              {detail.verification.note ? (
                <p className={styles.planWindow}>Your note: “{detail.verification.note}”</p>
              ) : null}
            </div>
          ) : (
            <div className={styles.planBox}>
              {detail.verification?.status === 'Rejected' ? (
                <>
                  <p className={styles.planState}>
                    Turned down on{' '}
                    {new Date(detail.verification.reviewedAt ?? detail.verification.createdAt).toLocaleDateString()}
                    .
                  </p>
                  {detail.verification.reviewNote ? (
                    <p className={styles.planWindow}>
                      The staff note: “{detail.verification.reviewNote}”
                    </p>
                  ) : null}
                  <p className={styles.planWindow}>
                    Correct the listing above and ask again — a turned-down draft can be sent
                    back as often as it takes.
                  </p>
                </>
              ) : (
                <p className={styles.planState}>
                  This draft has not been sent for a check yet.
                </p>
              )}

              <form className={styles.form} onSubmit={(event) => void handleVerify(event)}>
                <label className={styles.field}>
                  <span className={styles.label}>Note for the staff checking it</span>
                  <textarea
                    className={styles.textarea}
                    rows={3}
                    maxLength={500}
                    value={verifyNote}
                    disabled={verifying}
                    placeholder="Anything you would like them to know — optional."
                    onChange={(event) => setVerifyNote(event.target.value)}
                  />
                </label>

                {verifyError ? (
                  <p className={styles.error} role="alert">
                    {verifyError}
                  </p>
                ) : null}

                {verifyMessage ? (
                  <p className={styles.saved} role="status">
                    {verifyMessage}
                  </p>
                ) : null}

                <div className={styles.actions}>
                  <Button type="submit" disabled={verifying}>
                    {verifying ? 'Sending…' : 'Ask us to check it'}
                  </Button>
                  <span className={styles.readonlyNote}>
                    Save the listing details first — the staff check what is on the draft when
                    they open it.
                  </span>
                </div>
              </form>
            </div>
          )}
        </section>

        <section className={styles.section}>
          <h2 className={styles.sectionTitle}>Plan</h2>

          {subscription.status === 'loading' ? (
            <StateMessage tone="loading" title="Loading the plan…" />
          ) : null}

          {subscription.status === 'error' ? (
            <StateMessage
              tone="error"
              title="Could not load the plan"
              description={getErrorMessage(subscription.error, 'Please try again in a moment.')}
              action={
                <Button variant="secondary" size="small" onClick={subscription.reload}>
                  Try again
                </Button>
              }
            />
          ) : null}

          {state ? (
            <div className={styles.planBox}>
              <p className={styles.planState}>{describeSubscription(state)}</p>
              {state.startsOn && state.endsOn ? (
                <p className={styles.planWindow}>
                  {state.startsOn} to {state.endsOn}
                </p>
              ) : null}

              {planMessage ? (
                <p className={styles.saved} role="status">
                  {planMessage}
                </p>
              ) : null}

              {planError ? (
                <p className={styles.error} role="alert">
                  {planError}
                </p>
              ) : null}

              {state.pendingRequestId ? (
                <p className={styles.planWindow}>
                  Nothing to choose while a request is waiting for a decision.
                </p>
              ) : (
                <ul className={styles.planList}>
                  {plans.status === 'loading' ? (
                    <li className={styles.planRow}>Loading the plans…</li>
                  ) : (
                    planList.map((plan) => (
                      <li key={plan.id} className={styles.planRow}>
                        <div className={styles.planMain}>
                          <p className={styles.planName}>{plan.name}</p>
                          <p className={styles.planMeta}>
                            {plan.periodDays} days ·{' '}
                            {plan.priceNpr === 0
                              ? 'Free'
                              : `NPR ${plan.priceNpr.toLocaleString()}`}
                          </p>
                          {plan.description ? (
                            <p className={styles.planDescription}>{plan.description}</p>
                          ) : null}
                        </div>
                        <Button
                          type="button"
                          size="small"
                          variant={
                            state.tier === plan.tier && state.isActive ? 'ghost' : 'secondary'
                          }
                          disabled={planBusy}
                          onClick={() => void handleChoosePlan(plan.tier, plan.priceNpr)}
                        >
                          {planBusy
                            ? 'Sending…'
                            : plan.priceNpr > 0 && canPay
                              ? `Pay NPR ${plan.priceNpr.toLocaleString()}`
                              : state.tier === plan.tier && state.isActive
                                ? 'Extend'
                                : state.tier === plan.tier
                                  ? 'Renew'
                                  : 'Choose'}
                        </Button>
                      </li>
                    ))
                  )}
                </ul>
              )}

              <p className={styles.planNote}>
                {planTierLabels.Featured} placements are approved by an administrator and are
                labelled on the directory.{' '}
                {canPay
                  ? 'A paid plan is settled through eSewa: the placement opens as soon as the provider confirms the money, and the queue row settles itself with it.'
                  : 'No payment is taken on Nepal Hub yet — approving a request is what turns the placement on.'}
              </p>
            </div>
          ) : null}
        </section>
      </div>
    </>
  );
}
