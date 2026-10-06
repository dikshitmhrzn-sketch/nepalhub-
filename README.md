# Nepal Hub

**One useful website for everyday Nepal.**

Nepal Hub will eventually bring Nepal-specific tools and calculators, a daily quiz, a student
hub, jobs, local businesses, deals and events into a single platform — with user accounts, XP and
badges, leaderboards, saved items, business subscriptions and an admin system.

This repository currently contains **the data layer, real accounts and seven public
modules - tools/calculators, the daily quiz, the Student Hub, Jobs, Businesses, Offers and Events**. The
database, authentication, the account pages, all seven modules, the leaderboard, member reports and the
staff tools behind them are live, along with ownership claims, plan requests, free event bookings
and eSewa checkout for paid plans and priced events.

---

## Status

| Area | State |
| --- | --- |
| React + TypeScript + Vite frontend | Builds clean; home, tools hub, calculator, daily quiz, Student Hub catalogue and detail, job board and job detail, business directory (with the featured strip) and business detail (with the claim panel), offers and offer detail, events calendar and event detail (with the booking panel), sign-in, registration, account (with badges), a leaderboard, a staff dashboard, a my-businesses list and owner editor, a payment history at `/account/payments` and a provider
return page, and staff calculator/quiz/resource/job/business/deal/event/report/claim pages plus
admin-only account, audit-log, plan-request and payments pages |
| ASP.NET Core 8 Web API | Identity cookie auth, CSRF, rate limiting, calculators catalogue, server-side calculation, daily quiz, XP, badges, the Student Hub, the job board, the business directory, offers, the events calendar, the staff dashboard, the leaderboard, member reports, the audit trail, ownership claims, plans and subscriptions, event bookings, and eSewa payments |
| MySQL 8.4 + EF Core | Installed and running on `127.0.0.1:3307`; 32 application tables plus the EF migration-history table, fifteen migrations applied |
| Reference data | 7 provinces / 77 districts seeded |
| Calculators | 3 seeded (loan EMI, compound interest, percentage); staff CRUD live |
| Daily quiz | 31 seeded questions / 124 options; anonymous play, server-side grading, XP/level/streak; staff CRUD live |
| Badges | 3 seeded definitions; awarded by finishing a quiz, listed on the account page with progress toward the locked ones; pays no XP |
| Staff dashboard | Staff-only counts for every module, a "needs attention" list built from drafts, rows a reader can no longer see, open reports, unreviewed claims and plan requests, and quick links to each management page |
| Student Hub | 24 seeded resources (10 notes, 7 question papers, 7 scholarships); public catalogue with search, filters and ordering; staff CRUD with draft and expiry handling |
| Leaderboard | `GET /api/leaderboard` over the XP the quiz records: `all`, `today` or the Saturday-to-Friday Nepal week, computed at read time, no table of its own; in the primary nav |
| Moderation | Signed-in members report any of the five listing types; staff work the queue at `/admin/reports` and resolve or dismiss with an audit row written in the same save |
| Accounts and audit log | Admin-only account list with activate/deactivate and role grant/revoke, plus the append-only trail at `/admin/audit-log` |
| Tests | 631 integration tests against the real `nepalhub_test` database, plus 61 frontend tests |
| Jobs | 16 seeded postings; public board with search, filters and ordering; staff CRUD with draft and expiry handling |
| Businesses | 25 seeded listings across 12 categories and 7 provinces; public directory with search, filters, ordering and opening hours; staff CRUD with publish and verify |
| Offers | 15 seeded offers, day-granular and inclusive of the last day; public page with search, location filters and `EndingSoon`/`Newest` ordering; staff CRUD with publish blockers |
| Events | 15 seeded events across 8 categories; public calendar with search, location and category filters and `Soonest`/`Newest` ordering; times read as Nepal wall-clock; staff CRUD with publish blockers |
| Ownership and claims | A signed-in person claims a staff-written listing; staff approve or reject at `/admin/claims`, and approval writes the owner, grants the `business` role and settles rival claims in one save; the owner edits contact details and hours at `/my-business` |
| Plans and featured listings | Free and Featured plans (30 days each), seeded as rows staff can edit; an owner requests a plan and an admin approves at `/admin/subscription-requests` (or grants directly); live featured listings get a labelled strip atop the directory, capped at six and alphabetical |
| Event bookings | Free bookings of 1–4 places against an atomic capacity counter, paid ones through eSewa when `ticket_price` is set; book and release on the event detail page before the start time, staff read the attendee list, and event creation carries a `Capacity` and a `Ticket price` field |
| Payments | eSewa ePay v2 behind `IPaymentGateway`, off until `Payments:Provider` is set; one open checkout per listing or seat, settlement only from the signed return plus a server-to-server status check, refunds discovered through the admin status check, and history at `/account/payments` / `/admin/payments` |

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
│       │   ├── auth/          # RequireAuth / RequireStaff / RequireAdmin route guards
│       │   ├── badges/        # badge card
│       │   ├── bookings/      # booking panel (stepper, remaining spots)
│       │   ├── businesses/    # business card, filters, staff editor, featured strip
│       │   ├── calculators/   # card, dynamic form, result view, staff editor
│       │   ├── claims/        # claim panel on a listing, staff queue card
│       │   ├── deals/         # offer card, filters, staff editor
│       │   ├── events/        # event card, filters, staff editor
│       │   ├── layout/        # AppShell (header, nav, footer)
│       │   ├── jobs/          # job card, filters, staff editor
│       │   ├── quiz/          # result view, staff question editor
│       │   ├── reports/       # member report button, staff queue card
│       │   ├── students/      # resource card, filters, staff editor
│       │   ├── subscriptions/ # plan request card
│       │   └── ui/            # Container, Button, StateMessage, Pagination
│       ├── config/            # env.ts (VITE_* only)
│       ├── features/          # one folder per feature (api.ts + hooks), incl. audit/, badges/, dashboard/, leaderboard/, ownership/, payments/, reports/, subscriptions/, useradmin/ ...
│       ├── lib/               # apiClient.ts, apiErrors.ts
│       ├── pages/             # route-level components (tools, calculators, quiz, students, jobs, deals, events, leaderboard, my-business, payment return/history, admin, staff dashboard)
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
The tenth, `20261005042009_AddModeration`, creates `reports` and `admin_audit_logs`.
The eleventh, `20261005042034_AddQuizAttemptCompletedAtIndex`, adds the single-column index the
leaderboard's today/week boards filter on - separate from the tenth for the same reason the eighth
was separate: it is an index, not a table, and a migration named for one job does one job.
The twelfth, `20261005103010_AddBusinessOwnership`, adds `businesses.owner_user_id`.
The thirteenth, `20261005104909_AddBusinessSubscriptions`, creates `plans`,
`business_subscriptions` and `subscription_requests`.
The fourteenth, `20261005104931_AddEventBookings`, creates `event_bookings` and gives `events` its
`capacity` and `registration_count`.
The fifteenth, `20261006035723_AddPayments`, creates `payment_transactions` and gives `events` its
`ticket_price`.

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
checked by staff. Staff still create, publish and verify every row; what Phase 7e added is the
attachment point — a claimed listing has an owner who edits its contact details and hours, and a
featured plan buys a labelled position atop the directory (see "Ownership, plans and bookings"
below).

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
claims, plan requests and the featured strip arrived with Phase 7e — see below; reviews, ratings and
images are still open.

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
shape-checked and never fetched; there is no ticket or price, because the organiser collects
registrations on their own page — and, since Phase 7e, a reader can also hold a free place right
here, against the event's own `capacity` (see "Ownership, plans and bookings" below).

15 events are seeded across all eight categories and at least four provinces, including a draft and one
that has already finished. See `docs/database.md` for the schema.

Deliberately not in either module: payments, tickets, RSVPs, reviews, image uploads, saved
offers and notifications - there is nothing to attach them to yet (an offer has no price to pay and
an event's ticket price would have nowhere to go), and each is recorded in `docs/database.md` as the
column it would need. Free bookings did arrive, as `event_bookings` in Phase 7e; paid ones wait for
a payment provider.

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

Deliberately not here: staff-editable badge definitions, XP rewards, and badge art. Each is
recorded in `docs/database.md`. The leaderboard reads the XP these badges sit beside but adds no way
to earn any of it.

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

**"Needs attention" carries only numbers somebody can act on**, in three groups: open decisions
first — reports, since a complaint has a reader waiting on the other side of it, then ownership
claims and plan requests — then rows nobody has published yet, and rows whose publish
flag no longer describes what a reader sees - a job past its
deadline, an offer past its last day, a resource past its expiry, an event that has finished - plus
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
linked, and quick links to the ten management pages - the seven content modules, the report queue,
the claims queue and the plan-request queue. Those links are the plain `/admin/*` routes
because those pages keep their filters in component state rather than in the URL - a link carrying
`?status=draft` would land on the unfiltered list and look precise while being nothing of the sort.
The report queue, the account list, the audit log, the claims queue and the plan-request queue are
the opposite: their filters live in the URL, so a filtered view is a link somebody can paste to a
colleague.

---

## Leaderboard

The XP the daily quiz already records, read back as a ranking - anonymous, because a board a visitor
cannot see before signing up is a board nobody checks.

| Endpoint | Auth | Purpose |
| --- | --- | --- |
| `GET /api/leaderboard` | anonymous | The top of a window: `period=all` (the default), `today` or `week`, 100 entries per window |

**There is no leaderboard table.** `all` is a seek over `ix_users_level_xp` among active accounts;
`today` and `week` group `quiz_attempts.XpAwarded` since the window opened, served by
`ix_quiz_attempts_completed_at`. The plan this phase replaced proposed cached `leaderboard_entries`
rows refreshed by a background service; they were dropped because every read here is bounded by the
page size and the count, and a refresh job would only buy staleness for a figure that is already
correct on demand. The reversal is recorded in `docs/roadmap.md` rather than quietly deleted.

**The week runs Saturday to Friday on Nepal's clock** - the weekend-first week a Kathmandu reader
expects - converted to UTC exactly once per query by `INepalDateProvider.UtcStartOfDay` instead of
each row comparing against a local clock it cannot have. A period that is not one of the three is
`400 period_invalid` rather than a silent fall back to `all`.

**Ranks are absolute positions, not positions within the page**: the page offset plus the row index,
so page two starts at the number it actually occupies. Deactivated accounts are excluded by the
query itself - switching an account off removes it from the board without every reader having to
remember to filter - and ties break deterministically (XP, then when the account was created, then
the id), so two readers comparing rows see the same order.

The frontend is `/leaderboard`, in the primary nav. The period tabs own `?period=`, so "this week"
is a link somebody can send. The board shows the display name only: never an email address as a
stand-in for a row that somehow has none - that falls back to `Member`.

---

## Ownership, plans and bookings

Phase 7e's three moves: a person can take over a listing they recognise, a listing can buy a
position, and a reader can hold a place at an event. The first two still run through the
plan-request queue, and the third still latches a seat — 7f made two of them cost money without
adding a second approval path (see **Payments** below).

| Endpoint | Auth | Purpose |
| --- | --- | --- |
| `POST /api/businesses/{slug}/claim` | required | File a claim with up to 500 chars of evidence; `409 already_owner` / `already_claimed` / `already_owned`; 10/hour per IP |
| `GET /api/admin/business-claims` | `staff-only` | The queue: pending first, `status` filter |
| `POST /api/admin/business-claims/{id}/approve` | `staff-only` | Writes the owner, grants `business`, settles rival claims, audits — one save |
| `POST /api/admin/business-claims/{id}/reject` | `staff-only` | Closes it with an optional review note, recorded on the claim row |
| `GET /api/account/businesses` | required | The listings this account owns, plus its own claims in flight |
| `GET`/`PUT /api/account/businesses/{slug}` | required | The owner's read and save: description, address, phone, email, website and hours — never publish or verify |
| `GET /api/plans` | anonymous | The catalogue: Free and Featured, prices staff can edit without a deployment |
| `GET /api/businesses/featured` | anonymous | The strip: published, verified and live-featured listings, capped at six, alphabetical |
| `GET /api/account/businesses/{slug}/subscription` | required | This listing's plan state against the Nepal day |
| `POST /api/account/businesses/{slug}/subscription-requests` | required | Ask for a plan; `409 request_pending` while one waits |
| `GET`/`POST /api/admin/subscription-requests` | `admin-only` | The queue and its approve/reject |
| `POST /api/admin/subscriptions/grant`, `POST /api/admin/subscriptions/{id}/cancel` | `admin-only` | Direct grant and cancel, both audited |
| `POST`/`DELETE /api/events/{slug}/booking` | required | Take or release 1–4 places, 20/hour per IP, only before the event starts |
| `GET /api/admin/events/{id}/bookings` | `staff-only` | The attendee list |

**A claim grants nothing; approval is the single thing that moves ownership.** Staff still create,
publish and verify every listing — filing a claim only puts a row in the queue
(`GET /api/admin/business-claims`, staff-only, pending first and newest first). Approving writes
`businesses.owner_user_id`, grants the `business` role (effective at the next sign-in, because role
claims live in the cookie), rejects the rival claims for the same listing and appends
`business.claim_approved` to the audit trail — all in one `SaveChanges`, so a rolled-back approval
leaves no half-owner. There is no unique index on (business, claimant): a rejected claimant who
corrects their evidence can try again, and the service names each refusal rather than the database
swallowing the row. The owner's screen is `/my-business` — a list of owned listings beside the
claims in flight, and an editor whose whitelist is contact details and hours. The claim panel on a
listing page shows the person's own state (filed, approved, rejected) rather than a button that
would be refused.

**Entitlement is derived, never a flag.** Whether a listing is featured is `ends_on >= today`
against `INepalDateProvider`, so an expired row reports itself with no background job; one row per
business can be live, and re-granting extends by `max(current_end, today) + 30 days` rather than
stacking. Plans are rows (`Free` 30 days / NPR 0, `Featured` 30 days / NPR 3,000) seeded through an
idempotent startup seeder, so staff can change a price without a migration. Purchasing is a request:
an owner asks at `/my-business` and an admin decides at `/admin/subscription-requests`
(**admin-only** — this phase's stand-in for money — with direct grant and cancel at
`/api/admin/subscriptions`), and each decision writes one `subscription.*` audit entry naming the
queue row and the subscription it created. The featured strip above the directory is labelled,
capped at six and sorted alphabetically, so a slot is a position rather than a rank; it renders
nothing when the fetch fails rather than a stale top of the page.

**A booking is a latch, not a check.** Availability is one atomic conditional update against
`events.registration_count`; the unique (event, attendee) index means cancelling stamps the row and
rebooking reactivates it, so a place can never be held twice by the same account even from two tabs.
Booking and releasing both refuse after the event has started (Nepal clock), quantity is 1–4, and
event creation carries a `Capacity` that cannot be lowered below the places already held. Attendees
take no audit rows — the trail is for staff decisions, not a reader taking a seat — and both verbs
are rate-limited per IP (20/hour). Event detail carries `capacity`, `registrationCount` and a
`remainingSpots` that never goes negative, with a booking panel beside them; the staff attendee list
is `GET /api/admin/events/{id}/bookings`.

---

## Payments

eSewa ePay v2, off until `Payments:Provider` is set to `esewa` with `Payments:ProductCode`,
`Payments:SecretKey` and `Payments:ReturnBaseUrl` alongside it — all four are required by
`PaymentSettings.IsConfigured`, so a half-configured deployment answers `available: false` instead
of sending a buyer somewhere that cannot take the money. `SecretKey` comes from user-secrets or the
environment only (`Payments__SecretKey`), never from a committed file; `Payments:Environment` is
`sandbox` or `production` and picks the hosts (sandbox `rc-epay.esewa.com.np` / `rc.esewa.com.np`,
production `epay.esewa.com.np` / `esewa.com.np`) rather than storing them. When nothing is
configured, DI registers `RejectingPaymentGateway`, so the seam is never a null reference. The seam
7e left, `Features/Payments/IPaymentGateway.cs`, is the whole of what a second provider would have
to implement: `Initiate` and `CheckStatus`.

| Endpoint | Auth | Purpose |
| --- | --- | --- |
| `GET /api/payments/config` | anonymous | `{ available, provider }` — enough to hide a button, never a merchant code or a gateway url |
| `POST /api/payments/checkout` | required | Open or resume one checkout for a plan request or 1–4 seats; returns the signed form fields to post; 20/hour per IP |
| `POST /api/payments/verify` | required | The signed return, plus a server-to-server status check; 20/hour per IP |
| `GET /api/payments/{reference}` | required | This account's row for one reference |
| `GET /api/account/payments` | required | The buyer's own history, newest first, paged with a `status` filter |
| `GET /api/admin/payments` | admin-only | The list: paged, `status` and `kind` filters, buyer name per row |
| `POST /api/admin/payments/{reference}/check-status` | admin-only | Ask eSewa again — how a settlement nobody returned from, and a portal refund, are found |

**Nothing settles on a redirect.** The browser comes back to `/payments/return/{reference}` carrying
eSewa's Base64 `data` and `signature`, and `POST /api/payments/verify` believes them only insofar as
the status check agrees about the total: an answer about a different amount is recorded as
`amount_mismatch` and grants nothing, an unreachable provider leaves the row `Pending`, and a bad
signature is `payment_signature_invalid` rather than a plausible URL. Settlement moves
`Pending → Succeeded` through one conditional `UPDATE`, so two answers grant once and write one
`payment.succeeded` audit row (actor: the payer, not the gateway).

A paid plan opens the same `subscription_requests` row a free one does and approves *that row* on
success, with the provider's reference in `review_note` — approval logic stays in one service with
one audit trail instead of a second "system approved it" path. A paid seat latches the seat before
the provider is asked (release on failure or refund), confirms it on success, and parking a
cancelled paid booking at `refund_due` is how eSewa's lack of a refund API is handled: requesting
one from the provider's portal is an administrator's call, and the next status check records it as
`payment.refunded`. Free events are unaffected — `ticket_price` is 0 by default, and the booking
panel never mentions money until it is above zero.

The buyer's history is `/account/payments`, and `/admin/payments` (admin-only, URL-owned status
tabs) is where staff see every row and press **Check with provider**. Both are rate-limited by the
same `Payment:RateLimit` policy as checkout and verify (20/hour per IP).

---

## Reports and moderation

A signed-in member can complain about any of the five listing types, and staff work the complaints
in one queue.

| Endpoint | Auth | Purpose |
| --- | --- | --- |
| `POST /api/reports` | authenticated | File one report: target, reason, optional note |
| `GET /api/admin/reports` | staff-only | The queue: open work first, `status=all\|open\|resolved\|dismissed` |
| `POST /api/admin/reports/{id}/resolve` | staff-only | Close it as acted on, with an optional resolution note |
| `POST /api/admin/reports/{id}/dismiss` | staff-only | Close it as not acted on, same note |

**The target must be publicly visible.** Filing validates through `PublicContentScope` - published,
unexpired, unfinished - so a report can only point at something a reader could actually see, and a
target that has since gone is `404 target_not_found`, which the form shows as its own sentence
rather than as a generic failure.

**One report per person per target, enforced by the database.** The unique index on
`(reporter_user_id, target_type, target_id)` means a double-click, a retried request or a second tab
ends in one row and a `409 already_reported` for the person to read - and a repeat after staff closed
the first one is refused too: the record of what they thought is already in the queue. Filing sits
behind its own rate limit as well (10 per hour per member, `Report:RateLimit`), because this is the
public write a member can repeat.

**Closing a report writes its audit row in the same save.** A report stores a pointer and a reason,
never a copy of the content it complains about; the queue joins each target's title and slug at read
time. Resolve and dismiss append `report.resolved` / `report.dismissed` with the note beside it in
the same `SaveChanges` as the status change, so a rolled-back close cannot leave an entry claiming
it happened.

The staff screen is `/admin/reports` behind `RequireStaff`, and the open total is a row on the
dashboard itself. The member's side is a Report button on all five detail pages, rendered only for a
signed-in visitor, with the duplicate and vanished-target answers written out as sentences.

---

## Accounts and the audit log

Admin-only: the accounts behind the site, and the record of what staff did to them.

| Endpoint | Auth | Purpose |
| --- | --- | --- |
| `GET /api/admin/users` | admin-only | The account list: `search`, `status=active\|inactive`, `role`, `page` |
| `POST /api/admin/users/{id}/activate` | admin-only | Let the account sign in again |
| `POST /api/admin/users/{id}/deactivate` | admin-only | Block sign-in - an admin cannot do this to themselves (`409 cannot_modify_self`) |
| `POST /api/admin/users/{id}/roles/grant` | admin-only | Add `user`, `business`, `moderator` or `admin` |
| `POST /api/admin/users/{id}/roles/revoke` | admin-only | Remove one - also self-guarded |
| `GET /api/admin/audit-log` | admin-only | The trail: exact-match `action` and `targetType` filters |

**Roles live in the auth cookie**, so a granted or revoked role takes effect the next time the
member signs in; the API does not pretend the change is instant. Deactivation is immediate, though:
sign-in reads `IsActive`, so the next attempt is `403 account_disabled` and stays that way until an
admin activates the account again.

**Every mutation writes to `admin_audit_logs` beside the change itself** - `user.activated`,
`user.deactivated`, `user.role_granted`, `user.role_revoked`, alongside the moderation actions. The
trail stores JSON `before`/`after` snapshots rather than per-subject columns, so it can record a
future subject without a schema change, and rows are appended and never updated. Reading it is
`admin-only`, not `staff-only`: the log oversees the moderators as much as it records them.

The screens are `/admin/users` and `/admin/audit-log`, behind a `RequireAdmin` route guard nested
under the staff routes, and both keep their filters in the URL like every other list on the site.

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

631 tests pass, of which 60 cover Offers, 54 cover Events and 17 cover Payments:

| File | Tests |
| --- | --- |
| `AbuseProtectionTests.cs` | 2 |
| `AuthenticationTests.cs` | 15 |
| `BadgeRulesTests.cs` | 10 |
| `BadgeTests.cs` | 12 |
| `BookingTests.cs` | 5 |
| `BusinessTests.cs` | 105 |
| `CalculatorTests.cs` | 45 |
| `DealTests.cs` | 60 |
| `EventTests.cs` | 54 |
| `JobsTests.cs` | 86 |
| `LeaderboardTests.cs` | 9 |
| `ModerationTests.cs` | 10 |
| `OwnershipTests.cs` | 8 |
| `PaymentTests.cs` | 17 |
| `QuizTests.cs` | 35 |
| `RateLimitingTests.cs` | 4 |
| `ReferenceDataTests.cs` | 5 |
| `SearchTests.cs` | 31 |
| `StaffDashboardTests.cs` | 11 |
| `StudentHubTests.cs` | 68 |
| `SubscriptionTests.cs` | 12 |
| `TestDatabaseTests.cs` | 20 |
| `UserAdminTests.cs` | 7 |

The frontend has its own runner — Vitest with jsdom and Testing Library, so a page can be rendered
and queried without a server:

```bash
cd frontend
npm test
```

61 tests cover the staff dashboard's attention filtering, links and hook behaviour, the leaderboard's
period tabs and its loading and empty states, the report button's lifecycle - filing, the
duplicate and vanished-target answers it shows as sentences, and cancelling without sending
anything - plus the claim panel's four answers, the claim queue card, the featured strip's
loading/empty/error silence, the booking panel from stepper to sold-out and release (including the
priced path and why a checkout that cannot start says so), the payment return page's five answers,
and `checkout.ts` - the signed form it builds and posts, and every sentence an error code can
produce.

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
