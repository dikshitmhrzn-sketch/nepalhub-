import { useEffect, useState, type FormEvent } from 'react';
import { Link, useParams } from 'react-router';

import { Button } from '@/components/ui/Button';
import { Container } from '@/components/ui/Container';
import { StateMessage } from '@/components/ui/StateMessage';
import {
  categoryLabel,
} from '@/features/businesses/format';
import {
  shortDayNames,
  type SaveBusinessHours,
} from '@/features/businesses/types';
import { useAuth } from '@/features/auth/AuthContext';
import {
  saveMyBusiness,
} from '@/features/ownership/api';
import {
  useMyBusiness,
  useMyBusinesses,
} from '@/features/ownership/useMyBusinesses';
import {
  claimStatusLabels,
  type SaveOwnerBusinessRequest,
} from '@/features/ownership/types';
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
 * The ownership screen: what this person manages, where each claim stands, and the editor
 * for one listing — details on the left, plan on the right.
 *
 * The editor only offers what the API lets an owner touch: description, contact details and
 * the week. Name, category, district and the publish and verify flags belong to staff, so
 * they are shown read-only — an owner corrects what a listing says, not what it is.
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

  const { owned, claims } = list.data;

  return (
    <Container as="section" className={styles.page}>
      <header className={styles.header}>
        <h1 className={styles.title}>My businesses</h1>
        <p className={styles.subtitle}>
          The listings you manage and the claims you have filed. Owners can correct contact
          details, the description and opening hours; publishing and verification stay with our
          staff.
        </p>
      </header>

      <section className={styles.section}>
        <h2 className={styles.sectionTitle}>Listings you manage</h2>

        {owned.length === 0 ? (
          <p className={styles.empty}>
            You do not manage a listing yet. Find yours in the directory and claim it from its
            page.
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

  const [form, setForm] = useState<SaveOwnerBusinessRequest | null>(null);
  const [saving, setSaving] = useState(false);
  const [saved, setSaved] = useState(false);
  const [formError, setFormError] = useState<string | null>(null);
  const [fieldErrors, setFieldErrors] = useState<FieldError[]>([]);
  const [planBusy, setPlanBusy] = useState(false);
  const [planMessage, setPlanMessage] = useState<string | null>(null);
  const [planError, setPlanError] = useState<string | null>(null);

  const detail = business.status === 'success' ? business.data : null;

  useEffect(() => {
    if (!detail) {
      return;
    }

    setForm({
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

    // The early return above guarantees a form, but TypeScript resets that narrowing inside
    // a function declaration, so the guard is restated where the value is used.
    if (!form) {
      return;
    }

    const body = form;
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
      } else if (saveError instanceof ApiError && saveError.status === 404) {
        setFormError('That listing is no longer yours to edit.');
      } else {
        setFormError(getErrorMessage(saveError, 'Could not save the listing. Please try again.'));
      }
    } finally {
      setSaving(false);
    }
  }

  async function handleChoosePlan(tier: 'Free' | 'Featured') {
    setPlanBusy(true);
    setPlanMessage(null);
    setPlanError(null);

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
                Name, category, district, publishing and verification are kept by our staff —
                ask us through the report link if any of those are wrong.
              </span>
            </div>
          </form>
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
                          onClick={() => void handleChoosePlan(plan.tier)}
                        >
                          {planBusy
                            ? 'Sending…'
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
                labelled on the directory. No payment is taken on Nepal Hub yet — approving a
                request is what turns the placement on.
              </p>
            </div>
          ) : null}
        </section>
      </div>
    </>
  );
}
