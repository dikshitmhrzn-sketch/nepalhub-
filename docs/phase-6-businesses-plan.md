# Phase 6 plan — Businesses / Local Services (first slice)

Status: **plan only.** Nothing in this document has been implemented. No migration has been
generated, neither database has been touched, and no package has been added.

This is a public Nepal-focused directory of useful local businesses and services, staff-managed.
Business-owner accounts, subscriptions, reviews, uploads, deals, events, payments, maps and claims
are all explicitly deferred — see "Out of scope" at the end.

---

## 0. Decisions taken

| Decision | Choice | Consequence |
| --- | --- | --- |
| Categories | Flat `BusinessCategory` enum, 12 + `Other` | Amends `docs/database.md:342`, which currently plans a seeded tree via `parent_id` |
| Verification | `is_verified` independent of `is_published`; unverify does **not** unpublish | Both remaining 2×2 corners stay reachable, which is what makes the second flag worth having |
| Opening hours | `business_hours` child table, max 7 rows | Keeps the shape already committed at `docs/database.md:348` |
| Contact fields | `phone` **and** `email` | Honours the committed field list at `docs/database.md:344` |

The reference implementation for every convention below is the Jobs module added in Phase 5
(`dd68b84`).

---

## 1. Database model

New table `businesses`, created by a migration applied to both `nepalhub` and `nepalhub_test`.

| Column | Type | Rule |
| --- | --- | --- |
| `id` | `char(36)` PK | `ValueGeneratedNever`, deterministic from a new `ReferenceDataIds.Business(slug)` |
| `slug` | `varchar(160)` | unique `ix_businesses_slug`; editable as a draft, frozen once published |
| `name` | `varchar(160)` | required |
| `category` | `int` | `HasConversion<int>()`, required |
| `description` | `longtext` | required; character ceiling enforced by the service, as in `JobConfiguration.cs` |
| `district_id` | `char(36)` FK | **non-nullable**, `Restrict`, `businesses_fk_district_id` |
| `address` | `varchar(240)` nullable | free text |
| `phone` | `varchar(20)` | required to publish |
| `email` | `varchar(254)` nullable | shape-checked only |
| `website` | `varchar(500)` nullable | `ExternalUrlPolicy` |
| `is_published` | `tinyint(1)` default 0 | |
| `is_verified` | `tinyint(1)` default 0 | |
| `verified_at` | `datetime(6)` nullable | set on verify, cleared on unverify |
| `created_at`, `updated_at` | `datetime(6)` | `CURRENT_TIMESTAMP(6)` defaults |
| `created_by_user_id` | `char(36)` nullable FK | `SetNull`, `businesses_fk_created_by_user_id` |

`district_id` is **non-nullable, unlike Jobs.** A remote job legitimately has no location; a local
business directory entry with no location is not a listing. The province is read through the
district rather than stored beside it, so a row cannot claim to be in two provinces at once — the
same decision and the same reasoning as `Job.cs:42-52`.

**Phone is deliberately not unique.** Two shops on one floor share a landline. `users.phone_number`
is unique (`docs/database.md:71`) but that is an identity rule, not a directory rule.

Indexes, each leading with `is_published` because every public read filters on it first (the
reasoning at `JobConfiguration.cs:66-70` applies verbatim):

- `ix_businesses_is_published_name`
- `ix_businesses_is_published_category`
- `ix_businesses_is_published_verified`
- `ix_businesses_is_published_created_at`
- `ix_businesses_district_id_is_published`
- `ix_businesses_created_by_user_id`

### Category enum

`Data/Entities/BusinessCategory.cs`, one file per enum, explicit ordinals, and a doc comment
justifying enum-over-lookup-table the way `JobCategory.cs:3-13` does — the hot list query needs no
join to read a category filter.

```
Restaurants = 0, Cafes, HotelsAndAccommodation, GymsAndFitness, SalonsAndBeauty,
EducationAndTuition, RepairAndMaintenance, Shops, InformationTechnology,
ProfessionalServices, HealthcareServices, Other
```

Deliberately **not** added: `business_categories`, `business_images`, `business_subscriptions`.

### Opening hours table

| Column | Type | Rule |
| --- | --- | --- |
| `business_id` | `char(36)` PK/FK | `Cascade` — hours have no meaning without the row, and cascading from the owning row is safe in a way cascading from reference data is not |
| `day_of_week` | `int` PK | 0 = Sunday … 6 = Saturday |
| `is_closed` | `tinyint(1)` default 0 | |
| `opens_at` | `time` nullable | |
| `closes_at` | `time` nullable | |

The composite primary key `(business_id, day_of_week)` makes "at most one row per day" a schema
guarantee rather than a service check.

**Overnight shifts are legal.** A bar that opens at 22:00 and closes at 02:00 is ordinary, so
`closes_at <= opens_at` is refused only when the two are *equal* on a day that is not closed.

---

## 2. Public API

`[Route("api/businesses")]`, `[AllowAnonymous]` at class level. That is safe here because every
action on the controller is a public read — there is no per-visitor state in this slice. The
argument is written out at `JobsController.cs:8-17` and should be repeated.

### `GET /api/businesses`

Query parameters: `page`, `pageSize`, `search`, `province`, `district`, `category`, `verified`,
`sort`.

Public visibility is one predicate, simpler than Jobs because nothing here expires:

```csharp
private IQueryable<Business> PublicScope() =>
    dbContext.Businesses.AsNoTracking().Where(b => b.IsPublished);
```

No expiry rule means no `today` parameter and no `INepalDateProvider` dependency in this service.

Filters, search and paging follow `JobService.cs:439-526` exactly: `is { } x` pattern matching on
nullable filters, `else if` so **district wins over province**, `PageRequest.Normalise` for paging,
and a sort chosen from a fixed enum applied as a real `OrderBy` with `.ThenBy(Id)` as the
stability tiebreak so a row cannot appear on two pages.

`BusinessSummaryResponse` omits `description`, for the same single-projection reason as
`JobSummaryResponse` (`JobContracts.cs:94-106`).

### `GET /api/businesses/{slug}`

Same scope, so a draft and a slug that never existed are indistinguishable: one
`404 business_not_found`. That non-disclosure is deliberate and should be documented in the README.

### Search fields

Search covers **name, description and address only.** Phone and email are deliberately *not*
searchable: making a contact number keyword-searchable turns the directory into an enumeration
surface for the contact details of small businesses.

---

## 3. Staff API

`[Route("api/admin/businesses")]`, `[Authorize(Policy = AuthPolicies.StaffOnly)]`.

**No antiforgery attribute.** `AntiforgeryMiddleware` already validates every unsafe `/api` request
(`Common/AntiforgeryMiddleware.cs:46-48`); adding a per-action attribute would be the second
handling that the Jobs admin class doc explicitly warns against.

| Verb | Route | Notes |
| --- | --- | --- |
| GET | `/api/admin/businesses` | adds `status` (`All`/`Published`/`Draft`) and `verified`; ordered `UpdatedAt desc, Id asc`, fixed |
| GET | `/api/admin/businesses/{id:guid}` | includes `publishBlockers` |
| POST | `/api/admin/businesses` | `201 Created` |
| PUT | `/api/admin/businesses/{id:guid}` | `409 slug_immutable` when a published slug moves |
| DELETE | `/api/admin/businesses/{id:guid}` | `204`; `409 business_published` while published |

Error codes reuse the established vocabulary: `business_not_found`, `slug_taken`, `slug_immutable`,
`business_published`, `validation_failed`, `{field}_invalid`, plus `csrf_failed` and `rate_limited`
from the shared layers.

**The one new rule: publishing requires `is_verified = true` on the incoming request.** A listing
nobody checked does not go out. Unverify leaves `is_published` untouched, so `(published,
unverified)` and `(draft, verified)` are both reachable — reachable only by the independent flag, which
is the argument for having two flags rather than one combined status column.

Publish, unpublish, verify and unverify are all boolean fields on `SaveBusinessRequest`; there are
no separate toggle actions, matching Jobs.

---

## 4. Validation and security

- **Website** — `ExternalUrlPolicy.TryNormalise(raw, "website", out normalised, out error)`, the
  field-name overload added in Phase 5. Absolute `https` only, dotted non-IP host, canonical
  `Uri.AbsoluteUri` stored, never fetched.
- **Email** — optional; length ≤ 254, exactly one `@`, non-empty local and domain, domain contains a
  dot. No RFC 5322 parser: one dependency is not justified for an optional contact field.
- **Phone** — strip spaces, dashes, parentheses and a leading `+977`; require 7–10 remaining digits;
  reject letters; store the digit-only canonical form in `varchar(20)`. Deliberately does **not**
  validate Nepali mobile prefixes or landline area codes — those conventions are not settled, and a
  wrong rejection blocks a legitimate listing.
- **Slug** — `^[a-z0-9]+(?:-[a-z0-9]+)*$` (`JobSlug.Pattern`), a uniqueness pre-check plus the MySQL
  `1062` backstop (`JobService.cs:332-347`) so a concurrent duplicate is reported as the conflict it
  is rather than as a server error.
- **Search** — `LikePattern.Contains` with `LikePattern.Escape` passed explicitly as the third
  argument to `EF.Functions.Like`, so a `NO_BACKSLASH_ESCAPES` server cannot break it.
- **Description** — plain text, rendered as text, split on `/\n\s*\n/` client-side. No
  `dangerouslySetInnerHTML` anywhere; keep it that way.
- **Authorization** — the fallback policy already denies by default, so the public controller must
  opt out with `[AllowAnonymous]` and nothing else does.
- **Not sensitive data** — a business phone number is a published business phone number. Nothing
  here is personal data about a person.

### Opening hours validation

- At most 7 rows, and the whole set is replaced on each save.
- `is_closed = false` requires both `opens_at` and `closes_at`.
- Equal open and close times on an open day are refused.
- Overnight (close earlier than open) is allowed.
- Unknown day values are `validation_failed`.

---

## 5. Opening hours — the simple structure

One row per weekday, at most seven, with the set replaced wholesale on save so duplicates are
impossible and no merge logic is needed.

Detail responses return an ordered `IReadOnlyList<BusinessHoursResponse>`. The list summary carries
only a derived `isOpenNow` bool, computed server-side against Nepal time, which is the single thing
worth projecting onto a card.

This is the most machinery in the slice and it earns its place: it is the only structure that can
answer "open now", and it handles Saturday-off and split shifts without special cases. A free-text
line would be less code and would support none of that, and a JSON column would leave the shape
unvalidated by the schema.

---

## 6. Seed data

**20 businesses**, via a new `BusinessSeeder` and `BusinessSeedData`, mirroring `JobSeeder.cs`
exactly: load known ids into a `HashSet`, `known.Add(id)` decides insert-or-skip so a staff edit is
never undone on restart, and resolve every district code once up front from a dictionary so a typo
throws a clear message at startup rather than becoming a foreign-key error.

Deterministic ids from `ReferenceDataIds.Business(slug)`.

Coverage: all 13 categories with at least one entry each, all 7 provinces, and at least two in
Bagmati so the province filter behaves differently from the district filter.

Deliberate fixtures, so each state is visible in a fresh install rather than only in tests:

- one **draft, unverified** — exercises the staff-only list
- one **published but unverified** — the state unverify creates
- one **verified but unpublished** — exercises verify-before-publish
- one **published, verified, no opening hours** — proves hours are optional
- one **closed every Saturday** — proves the `is_closed` path

---

## 7. Tests

`BusinessTests.cs`, four classes mirroring `JobsTests.cs`:

| Class | Fixture | Covers |
| --- | --- | --- |
| `BusinessDirectoryTests` | `IClassFixture<NepalHubApiFactory>` | public reads |
| `BusinessAdminTests` | `IClassFixture<NepalHubApiFactory>` | staff writes |
| `BusinessOpeningHoursTests` | `IClassFixture<NepalHubApiFactory>` | hours rules |
| `BusinessLocationFilterTests` | `IClassFixture<NepalHubApiFactory>` | district/province integrity |

Target roughly 65 tests, taking the suite from 249 to about 315.

**The already-used test database is the governing constraint.** The rule at `JobsTests.cs:13-21`
applies in full: *every count assertion is scoped to a unique slug or a search term, never to a
whole-directory total*, because the staff tests create rows and do not remove them, so the seeds
drift onto later pages between runs. The `ReadAllAdminRowsAsync` paging helper (`JobsTests.cs:140-168`)
is reused verbatim for the two tests that must see every row.

Coverage list:

- Public list readable anonymously; seeded rows present; every category and province has a seed.
- Search by name, by description, by address; blank search ignored; wildcard search (`%`, `_`, `\`)
  matches nothing rather than everything.
- Each category, province and district filter; filters combine; unrecognised filter values produce a
  named 400.
- Sorting by name and by newest, stable across calls.
- Paging with no row on two pages; out-of-range page clamped.
- Summary omits description, detail carries it.
- A draft is hidden from readers and visible to staff.
- Anonymous → 401 on every staff verb; standard account → 403 on a read *and* a write; missing CSRF
  → 400.
- Full CRUD lifecycle; duplicate slug 409; malformed slug 400; a draft's slug is correctable; a
  published slug is frozen; updating a nonexistent id is 404.
- Unknown district code refused rather than silently dropped.
- Website refused for `http:`, `javascript:`, `data:`, a bare IP and a bare host; a missing website
  is allowed.
- Email shape refused when wrong; phone normalised, and letters or a bad length refused.
- **Publishing without verification is refused**; unverify keeps the row published; publish blockers
  clear once the draft is filled in.
- Opening hours: at most seven rows; a closed day needs no times; equal times on an open day refused;
  an overnight shift accepted; an unknown day refused; hours are optional.
- Deleting a published business is 409, then retracting and deleting a draft is 204.
- Staff list `status` and `verified` filters; the author is reported; an admin and a moderator can
  both write.
- The seeder is idempotent across two hosts.

Reuse the `JobApi.Body` / `ReadCreated` / `Details` shape as `BusinessApi.*`, with a unique token in
the fixture's business name so each row can be found again by text search, and `TestData.UniqueEmail()`
for accounts.

---

## 8. Frontend

New files following the Jobs layout exactly:

```
frontend/src/features/businesses/     api.ts · types.ts · useBusinesses.ts · useStaffBusinesses.ts · format.ts
frontend/src/components/businesses/   BusinessCard · BusinessFilters · BusinessEditor  (+ .module.css each)
frontend/src/pages/                   BusinessesPage · BusinessPage
frontend/src/pages/admin/             BusinessesAdminPage  (+ .module.css)
```

`features/` holds logic and no JSX; that boundary is documented at `docs/architecture.md:124`.

**`/businesses`** — the URL owns `page`, `search`, `province`, `district`, `category`, `verified` and
`sort`. Each value is whitelisted against the runtime enum list or the reference-data lists before
being sent (`JobsPage.tsx:40-53`), so a hand-edited `?category=Nonsense` degrades to "no filter"
instead of producing a 400. Every filter change resets to page 1, and `setSearchParams` uses
`{ replace: true }` so filtering does not fill the back button. The search box stays local component
state committed on submit, for the reason given at `docs/architecture.md:137-141`.

**`BusinessCard`** — name, category label, district and province, address, phone, an `Open now` /
`Closed` badge and a `Verified` badge. 320px-first: one column, then two, then three, using only
tokens from `styles/tokens.css`.

**`/businesses/:slug`** — description as plain-text paragraphs split on blank lines; a `<dl>` of
facts; opening hours as a seven-row table, or "Hours not listed"; `tel:` and `mailto:` links; and the
website link with `target="_blank" rel="noreferrer noopener"` plus the mandatory disclaimer that
Nepal Hub stores the address and never fetches it.

**`/admin/businesses`** — `useState` rather than URL state, because it is a staff screen
(`JobsAdminPage.tsx` is the reference). `key={editingId ?? 'new'}` remounts the editor per row.
Rows are a `<ul>`/`<li>` flex list, not a `<table>`, for the same mobile-first reason. Delete uses
`window.confirm`, per-row failures render with `role="alert"`, and the badge has three states
(Draft / Live / Verified). The Delete button is hidden for published rows rather than relying on the
409 alone, which is what the Jobs admin page does.

**Routing and navigation** — `router.tsx` gains `/businesses`, `/businesses/:slug` and, under
`RequireAuth` + `RequireStaff`, `/admin/businesses`. `AppShell` gains a `Businesses` nav item and a
staff `Manage businesses` item, and `plannedModules` drops `'Businesses'`. The `HomePage` hero and
the `AppShell` footer mention the directory alongside Jobs.

**Sort is `Name` and `Newest` only.** A "verified first" ordering would be a ranking nudge that
reads as promotion, and featured placement is explicitly out of scope.

---

## 9. Documentation

- `docs/roadmap.md` — a Phase 6 section with the checklist, plus an explicit
  "deliberately not in this phase" paragraph naming the deferred items.
- `docs/database.md` — replace the `business_categories` line with the enum rationale; rewrite the
  `businesses` column list to the real one; keep `business_hours`; mark `business_images` and
  `business_subscriptions` deferred; record that phone and email are not searchable.
- `docs/architecture.md` — a Businesses module section after the Jobs one, recording the
  directory-specific decisions.
- `README.md` — status table, project tree, migration list, test count, and a `## Businesses`
  section with the endpoint table and behaviour rules, matching the `## Jobs` section.

---

## 10. Sequencing

1. Enums, entity, EF configurations, `ReferenceDataIds.Business`. Build only; no database touched.
2. Migration, applied to `nepalhub` and then `nepalhub_test`.
3. Service, contracts, controllers, `Program.cs` registration and seeder call.
4. Seeder and seed data.
5. Build, then start the API once so `nepalhub` seeds; verify 20 rows and that quiz, resource and
   calculator counts are unchanged.
6. Tests until green, then the full gate: `dotnet build`, `dotnet test`, `npm run typecheck`,
   `npm run build`.
7. Frontend, then runtime smoke tests against the live API.
8. Documentation, then diff review and a single commit.

---

## Out of scope for this slice

Business-owner accounts and the owner portal · paid subscriptions · featured or promoted listings ·
reviews and ratings · online ordering · payments · analytics and view counts · image or file uploads ·
deals · events · saved businesses · messaging · maps API integration · business claims and ownership
requests · latitude and longitude.

On coordinates specifically: `docs/database.md:352` currently plans `latitude`/`longitude` with a
composite index. This plan proposes **against** that, for the reason already written at
`docs/database.md:353-355` — a district is the location a reader filters by, and the live `jobs`
table deliberately has no pin for the same reason. Distance-sorted search, if it is ever wanted, is a
separate migration plus a geocoding decision for Nepal rather than a column to add now.

Note also that `Roles.Business` (`"business"`) is already seeded and is still referenced by nothing
in the codebase. This slice does not use it. Leaving it unused is correct given staff-managed
listings, and it remains ready for the owner portal.

Finally: there is no email verification anywhere in this product, so a staff-entered business email
is unverified by definition. That is equally true of the phone number, which is why `is_verified`
means "a person checked this" and not "the business proved it".