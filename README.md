# Nepal Hub

**One useful website for everyday Nepal.**

Nepal Hub will eventually bring Nepal-specific tools and calculators, a daily quiz, a student
hub, jobs, local businesses, deals and events into a single platform — with user accounts, XP and
badges, leaderboards, saved items, business subscriptions and an admin system.

This repository currently contains **the data layer, real accounts and seven public
modules — tools/calculators, the daily quiz, the Student Hub, Jobs, Businesses, Offers and Events**. The
database, authentication, the account pages and all seven modules are live; payments, business accounts,
tickets and bookings are not started, by design.

---

## Status

| Area | State |
| --- | --- |
| React + TypeScript + Vite frontend | Builds clean; home, tools hub, calculator, daily quiz, Student Hub catalogue and detail, job board and job detail, business directory and business detail, offers and offer detail, events calendar and event detail, sign-in, registration, account (with badges), a staff dashboard, and staff calculator/quiz/resource/job/business/deal/event pages |
| ASP.NET Core 8 Web API | Identity cookie auth, CSRF, rate limiting, calculators catalogue, server-side calculation, daily quiz, XP, badges, the Student Hub, the job board, the business directory, offers, the events calendar and the staff dashboard |
| MySQL 8.4 + EF Core | Installed and running on `127.0.0.1:3307`; 24 application tables plus the EF migration-history table, nine migrations applied |
| Reference data | 7 provinces / 77 districts seeded |
| Calculators | 3 seeded (loan EMI, compound interest, percentage); staff CRUD live |
| Daily quiz | 31 seeded questions / 124 options; anonymous play, server-side grading, XP/level/streak; staff CRUD live |
| Badges | 3 seeded definitions; awarded by finishing a quiz, listed on the account page with progress toward the locked ones; pays no XP |
| Staff dashboard | Staff-only counts for every module, a "needs attention" list built from drafts and rows a reader can no longer see, and quick links to each management page |
| Student Hub | 24 seeded resources (10 notes, 7 question papers, 7 scholarships); public catalogue with search, filters and ordering; staff CRUD with draft and expiry handling |
| Tests | 557 integration tests against the real `nepalhub_test` database, plus 14 frontend tests |
| Jobs | 16 seeded postings; public board with search, filters and ordering; staff CRUD with draft and expiry handling |
| Businesses | 25 seeded listings across 12 categories and 7 provinces; public directory with search, filters, ordering and opening hours; staff CRUD with publish and verify |
| Offers | 15 seeded offers, day-granular and inclusive of the last day; public page with search, location filters and `EndingSoon`/`Newest` ordering; staff CRUD with publish blockers |
| Events | 15 seeded events across 8 categories; public calendar with search, location and category filters and `Soonest`/`Newest` ordering; times read as Nepal wall-clock; staff CRUD with publish blockers |
| Payments, business accounts, tickets, bookings, leaderboard, reports queue, audit log and user administration | Not started |

---

## Prerequisites

| Tool | Required version | Checked on this machine |
| --- | --- | --- |
| Node.js | `^20.19.0` or `>=22.12.0` | v22.16.0 |
| npm | 10.x | 10.9.2 |
| .NET SDK | 8.0 (LTS) | 8.0.425 |
| MySQL | 8.4 | 8.4.9, service `MySQLNepalHub84` on port `3307` |

```bash
node --version
npm --version
dotnet --list-sdks
```

---

## Project structure

```
nepalhub/
├── backend/
│   ├── NepalHub.sln
│   ├── NepalHub.Api/          # ASP.NET Core 8 Web API (controllers)
│   │   ├── Program.cs         # DI, Identity, CORS, CSRF, rate limiting
│   │   ├── Common/            # API response envelope, rate limits, CSRF
│   │   ├── Data/              # AppDbContext, entities, seeding, migrations
│   │   └── Features/          # one folder per feature
│   └── NepalHub.Tests/        # xUnit integration tests (real MySQL)
├── frontend/
│   ├── vitest.config.ts    # test runner: jsdom, the `@` alias, src/test/setup.ts
│   └── src/
│       ├── components/
│       │   ├── auth/          # RequireAuth / RequireStaff route guards
│       │   ├── badges/        # badge card
│       │   ├── businesses/    # business card, filters, staff editor
│       │   ├── calculators/   # card, dynamic form, result view, staff editor
│       │   ├── deals/         # offer card, filters, staff editor
│       │   ├── events/        # event card, filters, staff editor
│       │   ├── layout/        # AppShell (header, nav, footer)
│       │   ├── jobs/          # job card, filters, staff editor
│       │   ├── quiz/          # result view, staff question editor
│       │   ├── students/      # resource card, filters, staff editor
│       │   └── ui/            # Container, Button, StateMessage, Pagination
│       ├── config/            # env.ts (VITE_* only)
│       ├── features/          # one folder per feature (api.ts + hooks), incl. badges/, dashboard/, deals/, events/, jobs/ and reference/
│       ├── lib/               # apiClient.ts, apiErrors.ts
│       ├── pages/             # route-level components (tools, calculators, quiz, students, jobs, deals, events, admin, staff dashboard)
│       ├── styles/            # tokens.css (design tokens) + global.css
│       ├── test/              # Vitest setup (Testing Library cleanup after each test)
│       ├── App.tsx
│       ├── main.tsx
│       └── router.tsx
└── docs/
    ├── architecture.md
    ├── database.md
    ├── phase-6-businesses-plan.md
    └── roadmap.md
```

---

## Running the frontend

```bash
cd frontend
npm install
npm run dev          # http://localhost:5173
```

| Script | Purpose |
| --- | --- |
| `npm run dev` | Vite dev server with HMR on port 5173 |
| `npm run build` | Type-check (`tsc -b`) then production build to `dist/` |
| `npm run preview` | Serve the production build locally |
| `npm test` | Run the frontend tests once (Vitest + jsdom + Testing Library) |
| `npm run test:watch` | The same tests, rerun on save |
| `npm run typecheck` | Type-check only |

### Frontend environment variables

Defined in `frontend/.env.development` (committed, contains no secrets) and
`frontend/.env.example`:

| Variable | Purpose |
| --- | --- |
| `VITE_API_BASE_URL` | Base URL for API calls. `/api` (default) keeps calls same-origin so the dev proxy forwards them. |
| `VITE_BACKEND_URL` | Development-only target for the Vite proxy (never sent to the browser). |

> Every `VITE_*` value is compiled into public JavaScript. Never put a database credential,
> API key or JWT secret in a `VITE_*` variable.

---

## Running the backend

```bash
cd backend
dotnet run --project NepalHub.Api
```

| URL | What it is |
| --- | --- |
| `http://localhost:5080/api/health` | JSON health endpoint used by the frontend |
| `http://localhost:5080/health` | Framework liveness check (plain text `Healthy`) |
| `http://localhost:5080/swagger` | Swagger UI (development only) |

`GET /api/health` response:

```json
{
  "success": true,
  "data": {
    "status": "healthy",
    "database": "not-configured",
    "timestamp": "2026-09-30T09:12:44.1234567+00:00"
  },
  "error": null
}
```

`database` is `not-configured` until a connection string exists, then `connected`,
`unreachable` or `timeout`.

### Backend configuration and secrets

`appsettings.json` contains **no credentials**. The connection string is supplied at runtime:

```bash
# Development, per machine, not committed to git
cd backend/NepalHub.Api
dotnet user-secrets set "ConnectionStrings:Default" "Server=127.0.0.1;Port=3307;Database=nepalhub;User Id=nepalhub_app;Password=<strong-password>;"
```

Equivalent environment variable (useful in containers/CI):

```
ConnectionStrings__Default=...
Database__ServerVersion=8.4.0-mysql
Cors__AllowedOrigins__0=http://localhost:5173
Auth__RateLimit__PermitLimit=20
Auth__RateLimit__WindowSeconds=60
Calculate__RateLimit__PermitLimit=60
Calculate__RateLimit__WindowSeconds=60
```

`Database:ServerVersion` is read by both the app and the EF Core design-time factory, so the server
is never auto-detected at startup.

### Entity Framework Core tooling

The `dotnet-ef` CLI is pinned as a local tool in `backend/.config/dotnet-tools.json`:

```bash
cd backend
dotnet tool restore
dotnet ef migrations add <Name> --project NepalHub.Api
dotnet ef database update --project NepalHub.Api
```

The first migration, `20260930092534_InitialCreate`, creates the Identity tables plus the seeded reference data.
The second, `20260930105251_AddCalculators`, creates `calculators`.
The third, `20260930122740_AddQuiz`, creates the six quiz tables.
The fourth, `20260930154447_AddStudentResources`, creates `resources`.
The fifth, `20261001041409_AddJobs`, creates `jobs`.
The sixth, `20261001101158_AddBusinesses`, creates `businesses` and `business_hours`.
The seventh, `20261001142733_AddDealsAndEvents`, creates `deals` and `events`.
The eighth, `20261001155854_AddDealsAndEventsIndexes`, adds the two indexes behind the `Newest` sort.
It is a separate migration because the seventh was already applied to a live development database, and
editing an applied migration to look tidier would leave the two out of step.
The ninth, `20261002162619_AddBadges`, creates `badges` and `user_badges` and seeds the three badge
definitions through `HasData`.

The design-time factory reads `ConnectionStrings__Default` from the environment and falls back to a
passwordless local default, so `dotnet ef database update` needs that variable set (or the API's
user-secrets entry exported) even though the running API itself resolves its connection string from
configuration.

---

## Authentication

Cookie-based, issued by ASP.NET Core Identity. There is no JWT and no token in `localStorage`.

| Endpoint | Auth | Purpose |
| --- | --- | --- |
| `GET /api/auth/csrf` | anonymous | Issues the antiforgery token |
| `POST /api/auth/register` | anonymous | Creates an account, assigns `user`, signs in |
| `POST /api/auth/login` | anonymous | Signs in; 5 failures lock the account for 15 minutes |
| `POST /api/auth/logout` | anonymous | Clears the cookie |
| `GET /api/auth/session` | anonymous | Current session, or `authenticated: false` |
| `GET /api/account/me` | required | The signed-in user |
| `GET /api/admin/ping` | `admin` role | Proves the role policy works |
| `GET /api/reference/provinces` | anonymous | 7 provinces |
| `GET /api/reference/provinces/{code}/districts` | anonymous | Districts for one province |

Every unsafe request (`POST`, `PUT`, `PATCH`, `DELETE`) must carry the `X-CSRF-TOKEN` header, and
the frontend fetches and caches it automatically. The token is bound to the current identity, so it
is dropped after sign-in and sign-out.

Auth endpoints are rate limited to 20 requests per minute per IP
(`Auth:RateLimit:PermitLimit`), and every other endpoint requires a signed-in user unless it opts
out with `[AllowAnonymous]`.

> Accounts are created already confirmed, because no email provider is connected yet. Wiring a real
> mailer belongs to the notifications work, not to this phase.

---

## Calculators

The first public module. A calculator is a database row whose input form is stored as JSON, so adding
one is a data change plus a server-side handler — no frontend code.

| Endpoint | Auth | Purpose |
| --- | --- | --- |
| `GET /api/calculators` | anonymous | Paged catalogue; `page`, `pageSize`, `search`, `category` |
| `GET /api/calculators/{slug}` | anonymous | One published calculator, including its input schema |
| `POST /api/calculators/{slug}/calculate` | anonymous | Runs the calculation; 60/minute/IP |
| `GET /api/admin/calculators` | `staff-only` | Every row, drafts included |
| `GET /api/admin/calculators/{id}` | `staff-only` | One row for the edit form |
| `POST /api/admin/calculators` | `staff-only` | Creates a row |
| `PUT /api/admin/calculators/{id}` | `staff-only` | Updates copy, schema and publish state |
| `DELETE /api/admin/calculators/{id}` | `staff-only` | Deletes a draft; `409` if it has been published |

```bash
# the token is required because every unsafe request is antiforgery-validated
$token = (Invoke-RestMethod http://localhost:5080/api/auth/csrf).data.token
Invoke-RestMethod http://localhost:5080/api/calculators/loan-emi/calculate -Method Post `
  -Headers @{ 'X-CSRF-TOKEN' = $token } -ContentType 'application/json' `
  -Body '{"inputs":{"principal":2000000,"annualRate":11.5,"years":20}}'
```

The arithmetic always runs on the server. A published calculator with no registered
`ICalculatorHandler` is refused, so a half-finished row cannot reach the public catalogue, and a
slug cannot be changed after creation because it is the public URL.

Seeded calculators are inserted by an idempotent startup seeder, so an existing row is never
overwritten. See `docs/database.md` for the schema.

---

## Daily quiz

The second public module. Ten questions a day, the same set for everybody, materialised on the
first request of the day rather than by a scheduled job.

| Endpoint | Auth | Purpose |
| --- | --- | --- |
| `GET /api/quiz/daily` | anonymous | Today's set; carries no answer and no explanation |
| `POST /api/quiz/daily/submit` | anonymous | Grades the whole quiz in one call; 20/minute/IP |
| `GET /api/quiz/daily/result` | required | Today's stored result with the review |
| `GET /api/quiz/stats` | required | Total XP, level, streak, quizzes played |
| `GET /api/admin/quiz/questions` | `staff-only` | Every question, drafts included; paged, searchable, category filter |
| `GET /api/admin/quiz/questions/{id}` | `staff-only` | One question for the edit form, including the answer |
| `POST /api/admin/quiz/questions` | `staff-only` | Creates a question |
| `PUT /api/admin/quiz/questions/{id}` | `staff-only` | Updates the question, its options and its publish state |
| `DELETE /api/admin/quiz/questions/{id}` | `staff-only` | Deletes a question; `409` if it has ever been played |

**Grading is server-side and it is the only place the answer exists.** A pre-answer response has
no `isCorrect` and no `explanation` field, so there is nothing in the payload to read the answer
out of. A submission is validated, graded and written in one transaction: there is no per-question
endpoint, no half-finished attempt and no state to resume, because a whole quiz is submitted at
once or not at all.

**Playing is open, progress is not.** An anonymous visitor gets a real score, the real answers and
the real explanations, but nothing is stored and the API reports `xpAwarded: 0` with `recorded:
false` rather than a misleading zero total. An account earns `10 XP` per correct answer plus a
`20 XP` completion bonus, and the completion bonus is paid even on a zero score — finishing is
worth something, which is the only incentive that survives a bad day.

**One attempt per day, enforced by the database.** `ix_quiz_attempts_user_id_daily_quiz_id` is
unique, so a second submit is a `409` no matter what the service check decided. The service check
exists to produce a friendly error; the index is what makes it true.

**A played question is permanent.** Once a question has appeared in a daily quiz its text and
options are frozen: staff can publish and retract it, but editing the wording or deleting it
returns `409`. Retracting stops it being drawn again without rewriting what a player already saw.
Validation is split in two, and the split is deliberate. A draft needs two to six options, each
with text and no two the same, but it may have **no** correct answer and **no** explanation —
requiring those on a draft would leave no way to save a half-written question. Only publishing
requires exactly one correct option and an explanation of at least ten characters, because those
are what make the question worth answering.

31 questions and 124 options are seeded by an idempotent startup seeder with deterministic ids, so
a staff correction is never reverted by a restart. See `docs/database.md` for the schema.

---

## Student Hub

The third public module: study notes, past question papers and scholarships in one catalogue, served
from a single generic `resources` table. One table rather than three, because a scholarship and a
note are filtered and searched the same way, and a member looking for "physics" should not have to
guess which of three pages to open.

| Endpoint | Auth | Purpose |
| --- | --- | --- |
| `GET /api/students/resources` | anonymous | Paged catalogue; `page`, `pageSize`, `search`, `type`, `category`, `level`, `sort` |
| `GET /api/students/resources/{slug}` | anonymous | One published, unexpired resource |
| `GET /api/admin/students/resources` | `staff-only` | Every row, drafts and expired rows included; `status` filter |
| `GET /api/admin/students/resources/{id}` | `staff-only` | One row for the edit form, with its publish blockers |
| `POST /api/admin/students/resources` | `staff-only` | Creates a row |
| `PUT /api/admin/students/resources/{id}` | `staff-only` | Updates copy, type-specific fields and publish state |
| `DELETE /api/admin/students/resources/{id}` | `staff-only` | Deletes a draft; `409 resource_published` if it has been published |

**One catalogue, three kinds of thing.** A resource is a `Note`, a `QuestionPaper` or a
`Scholarship`; the optional columns (`academic_year`, `external_url`, `provider`, `award_amount_npr`,
`eligibility`, `expires_on`) belong to only one or two of them, and the service clears the ones
that do not apply to the saved type. Switching a row from a question paper to a note therefore
clears its year and link rather than leaving them to show up on a note.

**Expiry is a Nepal-time date, and it is not the server's idea of today.** A scholarship with
`expires_on` set stays publicly listed through the end of that day in Nepal and returns `404`
afterwards — computed against UTC+05:45, so it disappears at the same moment for every reader. Staff
keep seeing it, because a row that quietly vanished is a row nobody can unpublish.

**Publishing is stricter than saving.** A draft may be half-written: no summary, no body, no
deadline. Publishing requires a summary of at least 20 characters, a body of at least 80, and
whatever the chosen type needs. The staff list reports the missing pieces as `publishBlockers`
rather than making staff publish and read the error.

**Two rules protect the catalogue.** A slug is permanent, because it is the public address and
moving it breaks every link already shared. A published row cannot be deleted, only retracted and
then deleted; a resource somebody used to revise for an exam should not disappear from the
archive on an ordinary delete.

**Nothing is uploaded and nothing is fetched.** A question paper links out with a stored HTTPS
address that is validated for shape and never requested by the server, and a body is plain text
rendered as text — no HTML, no Markdown, no file storage. Search matches title and summary only,
with `%`, `_` and `\` escaped so a search for `100%` is a search for the characters rather than a
wildcard.

24 resources (10 notes, 7 question papers, 7 scholarships, two of them already past their deadline)
are seeded by an idempotent startup seeder with deterministic ids and deadlines relative to the
Nepal calendar date, so a re-seed never overwrites a staff correction. See `docs/database.md` for
the schema.

---

## Jobs

The fourth public module: a board of staff-written openings that leave on their own closing date
rather than being taken down by hand.

| Endpoint | Auth | Purpose |
| --- | --- | --- |
| `GET /api/jobs` | anonymous | Paged board; `page`, `pageSize`, `search`, `province`, `district`, `category`, `employmentType`, `workMode`, `sort` |
| `GET /api/jobs/{slug}` | anonymous | One published, unexpired posting |
| `GET /api/admin/jobs` | `staff-only` | Every row, drafts and closed rows included; `status` filter |
| `GET /api/admin/jobs/{id}` | `staff-only` | One row for the edit form, with its publish blockers |
| `POST /api/admin/jobs` | `staff-only` | Creates a posting |
| `PUT /api/admin/jobs/{id}` | `staff-only` | Updates copy, location, application link and publish state |
| `DELETE /api/admin/jobs/{id}` | `staff-only` | Deletes a draft; `409 job_published` if it has been published |

**A posting is public only while it is published and its deadline has not passed on the Nepal
calendar day.** A closed posting returns the same `404` as a slug that never existed, so the public
endpoint cannot be used to discover which slugs are real but no longer open, and it stays visible to
staff because retiring it is their job rather than the reader's.

**Location is a nullable `district_id`, and the province is read through it.** The two therefore
cannot disagree, and a remote posting is saved with no district at all rather than with a placeholder
district that would then show up in every district filter for its province.

**Publishing is stricter than saving, and the slug is frozen once it is public.** A draft may be
half-written and keeps an editable slug, because a staff member who mistyped it has not yet sent
anyone to the wrong address. Publishing requires a title, an employer, a description of at least 80
characters, an application link and a deadline that has not passed; the staff list reports the missing
pieces as `publishBlockers` rather than making staff publish and read the error. Once published the
slug is the address people share, so it can no longer be changed and the row is retracted rather than
deleted.

**Application links are shape-checked absolute `https` URLs and are never fetched.** A `javascript:`
or `data:` link, a bare address such as `https://localhost/jobs`, and a non-`https` scheme are all
refused, and a posting with no way to apply is refused too, because a dead end is not worth
publishing. Descriptions are plain text rendered as text, with no HTML and no file storage.

16 postings are seeded by an idempotent startup seeder with deterministic ids and deadlines relative
to the Nepal calendar date, so a re-seed never overwrites a staff correction. The seeds cover all
seven provinces, all four employment types and all three work modes, and two of them are already
past their deadline so the closing path is visible in a fresh install rather than only in the tests.
See `docs/database.md` for the schema.

---

## Businesses

The fifth public module: a directory of local shops, restaurants, trades and services, written and
checked by staff. It is a directory and nothing else — there are no business accounts yet, because
an owner portal has nothing to attach to until someone can register as a business.

| Endpoint | Auth | Purpose |
| --- | --- | --- |
| `GET /api/businesses` | anonymous | Paged directory; `page`, `pageSize`, `search`, `province`, `district`, `category`, `verified`, `sort` |
| `GET /api/businesses/{slug}` | anonymous | One published listing, with its opening hours |
| `GET /api/admin/businesses` | `staff-only` | Every row, drafts included; `status` filter |
| `GET /api/admin/businesses/{id}` | `staff-only` | One row for the edit form, with its publish blockers |
| `POST /api/admin/businesses` | `staff-only` | Creates a listing |
| `PUT /api/admin/businesses/{id}` | `staff-only` | Updates copy, contact details, hours, and the publish/verify flags |
| `DELETE /api/admin/businesses/{id}` | `staff-only` | Deletes a draft; refused once it has been published |

**A listing is public only while `is_published` is set, and the province is read through a required
`district_id`.** The two can therefore never disagree, and a listing with no district cannot be
found by anyone browsing their own area — which is the one thing a local directory has to get right.

**`is_published` and `is_verified` are independent, on purpose.** Publishing requires verification,
but taking the badge away does not retract the listing, so a detail that turns out to be wrong can be
corrected in place instead of pulling a business off the directory until someone fixes it. The
interesting staff row is the live listing that has lost its badge.

**Hours are a child table of at most seven rows, and a listing may publish none of them.** No hours
means "not listed", which the card and the detail page both say explicitly, rather than a red
"closed" badge the business never agreed to. Each day is either open with two times or explicitly
closed, and a closing time earlier than the opening time is read as running past midnight, because a
bar that empties at 02:00 is an ordinary Nepal schedule.

**Publishing is stricter than saving, and the slug is frozen once it is public.** A draft may be
half-written and keeps an editable slug. Publishing requires a phone number, a description of at
least 80 characters and verification; the staff editor is handed the same list as `publishBlockers`,
so what the form says and what the server enforces cannot drift apart. A live listing is retracted
rather than deleted.

**Website is shape-checked as an absolute `https` URL and is never fetched.** A `javascript:` or
`data:` link, a bare address such as `https://localhost` and a non-`https` scheme are all refused.
Phone numbers are normalised to digits, so one business cannot appear under two spellings of the same
number. Search escapes `LIKE` wildcards, so a reader searching for `%` gets businesses with a
percent sign rather than the whole table.

25 listings are seeded by an idempotent startup seeder with deterministic ids, covering all twelve
categories and all seven provinces. Bagmati is weighted heavily on purpose — fifteen listings are in
Kathmandu valley, which is where most readers are — while every other province still has at least one,
so no province filter returns an empty page. Three seeds exist in states the staff screen must handle:
a draft, a listing checked before it went live, and one that is published without its badge. See
`docs/database.md` for the schema.

Deliberately not in this slice: business accounts, paid subscriptions, featured placement, reviews and
ratings, image uploads, saved businesses, messaging, maps and claims. Deals arrived as the next module;
see below.

---

## Offers

A public page of staff-written discounts, each with the days it runs for. An offer is a *day*-granular
thing — it is redeemed at a counter on a date, not booked for an hour — so its window is two `DateOnly`
values and its headline is `discountText`: the business's own words, because "free chiya with any
thakali set" is a real offer and most Nepalese offers are not expressible as a percentage.

| Endpoint | Auth | Purpose |
| --- | --- | --- |
| `GET /api/deals` | anonymous | Paged offers; `page`, `pageSize`, `search`, `province`, `district`, `sort` |
| `GET /api/deals/{slug}` | anonymous | One live offer |
| `GET /api/admin/deals` | `staff-only` | Every row, drafts and expired offers included; `status` filter |
| `GET /api/admin/deals/{id}` | `staff-only` | One row for the edit form, with its publish blockers |
| `POST /api/admin/deals` | `staff-only` | Creates an offer |
| `PUT /api/admin/deals/{id}` | `staff-only` | Updates copy, dates, business and the publish flag |
| `DELETE /api/admin/deals/{id}` | `staff-only` | Deletes a draft; refused once it has been published |

**An offer is public while it is published and today has not passed its end date**, inclusive, computed
against `NepalDateProvider` on every read — `daysRemaining` is a read-time value and is stored nowhere,
because a stored countdown is wrong by the evening. The province is read through `districtId`, which is
**nullable here and not on an event**: a free-delivery offer that applies nationwide is the case that
separates an offer from a business listing.

**The business name survives; the business link does not always.** A public detail returns
`businessName` whether or not the listing is published, but returns `businessSlug: null` unless it is.
A link to a 404 is worse than no link, and a name with no link is still true. Deleting a business sets
`businessId` to null rather than cascading, so an offer outlives the listing it was attached to.

**`discountText` is the only price on the page.** There is no original/discounted pair, no coupon code
and no redemption count, because an offer in Nepal is usually "show this post" or "ask for the code" and
a numeric discount would be a fabrication. `redemptionUrl` is shape-checked as an absolute `https` URL
and never fetched; the page is honest about sending a reader to the business's own terms.

15 offers are seeded, including a draft, one whose end date has already passed, and a nationwide one, so
every state the staff screen must handle is visible in a fresh install. See `docs/database.md` for the
schema.

---

## Events

A public calendar of things that happen in a district on a date and at an hour, written by staff. The
times are **Nepal wall-clock**, serialised without an offset (`2026-10-04T10:00:00`), and the frontend
displays them as the text they are rather than parsing them as an instant — otherwise a reader in
London or Sydney sees an evening event at lunchtime.

| Endpoint | Auth | Purpose |
| --- | --- | --- |
| `GET /api/events` | anonymous | Paged calendar; `page`, `pageSize`, `search`, `province`, `district`, `category`, `sort` |
| `GET /api/events/{slug}` | anonymous | One event that has not finished |
| `GET /api/admin/events` | `staff-only` | Every row, drafts and finished events included; `status` filter |
| `GET /api/admin/events/{id}` | `staff-only` | One row for the edit form, with its publish blockers |
| `POST /api/admin/events` | `staff-only` | Creates an event |
| `PUT /api/admin/events/{id}` | `staff-only` | Updates copy, times, place, business and the publish flag |
| `DELETE /api/admin/events/{id}` | `staff-only` | Deletes a draft; refused once it has been published |

**`districtId` is required here, unlike an offer.** A reader cannot attend something with no place, and
a province filter over events with no location cannot return anything honest. The province is still read
through the district, so the two can never disagree.

**An event that has finished is gone from the public endpoints; one that is under way is still listed
with `isOngoing: true`.** The finishing time is inclusive, so an event stays on the calendar for the
whole of the day it ends. Like `daysRemaining`, that flag is derived per read rather than stored.

**Categories are a fixed enum** — `Community`, `Cultural`, `Sports`, `Education`, `Religious`,
`Business`, `Exhibition`, `Other` — serialised by name, on the same argument as `BusinessCategory`: a
fixed set of labels is searchable, translatable and impossible to orphan. `registrationUrl` is
shape-checked and never fetched; there is no ticket, price, capacity or booking, because the organiser
collects registrations on their own page.

15 events are seeded across all eight categories and at least four provinces, including a draft and one
that has already finished. See `docs/database.md` for the schema.

Deliberately not in either module: payments, tickets, bookings, RSVPs, reviews, image uploads, saved
offers and notifications. There is nothing to attach them to yet — an offer has no price to pay and an
event has no ticket — and each is recorded in `docs/database.md` as the column it would need.

---

## Badges

Read models over the progression the quiz already records. Three badges ship: finish one daily quiz,
finish one on seven days in a row, finish ten.

| Endpoint | Auth | Purpose |
| --- | --- | --- |
| `GET /api/account/badges` | required | Every badge with this person's progress against it, plus the earned count |

There is no endpoint that grants a badge. A badge is a consequence of a finished quiz, not something a
request can ask for, so the only verb here is a GET and the feature is not subject to antiforgery.

**Awarding runs after the quiz commits, never inside its transaction.** Two reasons. The quiz result is
the thing that matters, so a failure while writing a badge must not roll back a quiz somebody actually
finished. And the submit path already catches every MySQL duplicate-key error to turn a lost race on the
unique `(user, daily_quiz)` index into "you have already played today's quiz"; a badge insert sharing that
save would land in the same handler and be reported as a quiz problem. Keeping the two writes apart means
each one's failure is its own.

**A badge pays no XP.** The definition row is a threshold over a number the quiz already persists — the
count of finished attempts, and the `streak_count` the quiz just wrote — and nothing more. The quiz decides
XP once per attempt; a badge that quietly paid out again would make the total on the account page disagree
with the sum of the quiz results behind it.

**The criterion is a closed enum plus an integer, not a JSON blob.** `BadgeCriteria` is
`QuizzesCompleted` or `ConsecutiveDayStreak`, stored as a string, and `threshold` is compared against
`>=`. A JSON `criteria` column would let the seed describe a rule that nothing evaluates, so a badge could
be listed as unearnable and nobody would find out until a player waited forever for it.

**Granting is idempotent by construction.** The only thing recorded is the award, a badge already held is
skipped, and `ix_user_badges_user_id_badge_id` is unique, so a second row for a badge somebody already has
cannot exist. Reading the badges grants nothing.

**Locked badges are returned, not filtered out.** Someone who has finished one quiz should be able to see
that there is a seventh-day streak to reach. Hiding them would make the first badge arrive as an empty
panel with no hint that anything else exists.

The frontend renders the `icon` key as one text glyph (`trophy` → 🏆), matched against a table in
`features/badges/format.ts` with a neutral fallback for an unknown key. No icon package: the brand mark is
already the letters `NH` and a card's affordance is the character `→`, so three glyphs did not justify a
dependency. See `docs/database.md` for the schema.

Deliberately not here: a leaderboard, staff-editable badge definitions, XP rewards, and badge art. Each is
recorded in `docs/database.md`.

---

## Staff dashboard

One staff-only read of the whole catalogue, for the screen a moderator lands on before deciding what
to open next.

| Endpoint | Auth | Purpose |
| --- | --- | --- |
| `GET /api/admin/dashboard` | `staff-only` | Counts for every module plus the rows that need a decision |

**The eight counts are the management pages' own numbers.** Each one is the `totalCount` of that
module's staff list asked with no filters, so a card that says 16 jobs and the job board's "1–16 of
16" are the same query rather than two queries that happen to agree. Accounts are counted from
`users`, which is the only figure here with no publish state behind it.

**"Needs attention" carries only numbers somebody can act on**, in two groups: rows nobody has
published yet, and rows whose publish flag no longer describes what a reader sees — a job past its
deadline, an offer past its last day, a resource past its expiry, an event that has finished — plus
live listings that are no longer verified, because unverifying does not retract a listing. Rows with
a count of zero are dropped rather than rendered as a row of zeros: on a healthy catalogue most of
them are zero, and a list of zeros trains the eye to ignore the one section that matters when it is
not zero.

**The stale half is `published − visible`, counted through `PublicContentScope` itself.** The date
comparison is not restated in the dashboard, so the section cannot drift from what the public site
is serving: if a rule changes in the scope, this number changes with it.

The endpoint has no write verb, no caching and no new table. It is a handful of count queries, and
a number that changes while a reader is looking at it is worse than one that is a few seconds old.
The frontend is `/admin` behind `RequireStaff`: eight count cards, the attention rows grouped and
linked, and quick links to the seven management pages. Those links are the plain `/admin/*` routes
because those pages keep their filters in component state rather than in the URL — a link carrying
`?status=draft` would land on the unfiltered list and look precise while being nothing of the sort.

---

## Tests

```bash
cd backend
dotnet test
```

The suite is integration-only and uses a **real** MySQL server — there is no in-memory provider, so
the schema, the collation, the Identity stores and the seed data are all exercised. It creates and
migrates `nepalhub_test` and never touches `nepalhub`. Set `ConnectionStrings__Test` to point it
elsewhere; otherwise it derives the test connection string from the API's user-secrets entry.

557 tests pass, of which 60 cover Offers and 54 cover Events:

| File | Tests |
| --- | --- |
| `AuthenticationTests.cs` | 15 |
| `BadgeRulesTests.cs` | 10 |
| `BadgeTests.cs` | 12 |
| `BusinessTests.cs` | 105 |
| `CalculatorTests.cs` | 45 |
| `DealTests.cs` | 60 |
| `EventTests.cs` | 54 |
| `HealthEndpointTests.cs` | 1 |
| `JobsTests.cs` | 86 |
| `QuizTests.cs` | 35 |
| `RateLimitingTests.cs` | 2 |
| `ReferenceDataTests.cs` | 4 |
| `SearchTests.cs` | 31 |
| `StaffDashboardTests.cs` | 9 |
| `StudentHubTests.cs` | 68 |
| `TestDatabaseTests.cs` | 20 |

The frontend has its own runner — Vitest with jsdom and Testing Library, so a page can be rendered
and queried without a server:

```bash
cd frontend
npm test
```

14 tests cover the staff dashboard: the attention list's zero-filtering and its link targets, the
hook's loading / error / retry / unmount behaviour, and the page's counts, quick links, filtered
attention rows and error recovery.

---

## MySQL on this machine

MySQL 8.4.9 is installed as the Windows service `MySQLNepalHub84`, bound to `127.0.0.1:3307` with
its configuration in `C:\ProgramData\MySQL\NepalHub\my.ini`. It was installed **alongside** the
existing XAMPP MariaDB 10.4 rather than replacing it, so XAMPP keeps port `3306` and behaves exactly
as before.

```powershell
Get-Service MySQLNepalHub84
& "C:\Program Files\MySQL\MySQL Server 8.4\bin\mysql.exe" -u nepalhub_app -p -e "SHOW DATABASES;"
```

> XAMPP (`C:\xampp`) ships **MariaDB 10.4**, not MySQL 8. Both work with the same EF Core provider,
> but they are different products with a few behavioural differences. Nepal Hub targets MySQL 8 and
> does not use XAMPP.

See `docs/database.md` for the live schema and the planned tables.

---

## Development rules in force

1. Build incrementally; one module at a time, reviewed before the next.
2. No mock or fake backend for features that should use the real database.
3. No secrets in the frontend; runtime configuration only.
4. Secure defaults from day one (hashing via ASP.NET Core Identity, parameterised SQL via EF Core).
5. Responsive from 320px upward, mobile-first.
6. Reusable components, business logic kept out of presentation components.
7. Explicit loading, empty, error and success states — see `components/ui/StateMessage.tsx`.
8. No unnecessary packages, no heavy animation, no copied design.
9. The API is client-agnostic so a Flutter app can use the same endpoints.

See `docs/architecture.md` for the communication contract and `docs/database.md` for the live schema
and planned tables.
