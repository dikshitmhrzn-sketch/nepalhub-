# Architecture

## 1. Shape of the system

```
┌────────────────────────┐        HTTP/JSON        ┌──────────────────────────┐
│  React 19 + TS + Vite  │ ──────────────────────► │  ASP.NET Core 8 Web API  │
│  frontend/             │   /api/*  (JSON only)   │  backend/NepalHub.Api    │
│  (browser, later SPA)  │ ◄────────────────────── │  (controllers + services)│
└────────────────────────┘                         └────────────┬─────────────┘
                                                                 │ EF Core (Pomelo)
                                                                 ▼
                                                        ┌─────────────────┐
                                                        │   MySQL 8       │
                                                        └─────────────────┘
```

The frontend is a pure API consumer. It shares no code, no build output and no database access with
the backend. That is what allows a Flutter mobile app to be added later without changing the API.

## 2. Backend layout (single project, folder-layered)

One project keeps solo development cheap: no extra solutions, no multi-project build steps, no
layer-plumbing ceremony. Folders give the same navigational clarity.

```
NepalHub.Api/
├── Program.cs          composition root: DI, CORS, Swagger, exception handling, rate limiting
├── Common/             cross-cutting primitives: ApiResponse envelope, PagedResult/PageRequest,
│                       LikePattern (escaped LIKE for every search box)
├── Data/               AppDbContext, design-time factory, entity configurations, seeding, migrations
└── Features/<Module>/  vertical slices, one folder per product module
    ├── Auth/
    ├── Health/
    ├── Quiz/             the daily quiz and progression
    │   ├── QuizController.cs           public daily, submit, result, stats
    │   ├── QuizAdminController.cs      staff-only question CRUD
    │   ├── QuizService.cs              selection, grading, progression, staff rules
    │   │                               and QuizRateLimitOptions, the submit quota
    │   ├── QuizContracts.cs            request/response records
    │   ├── QuizProgression.cs          XP, levels and streaks as one unit
    │   └── NepalDateProvider.cs        the Nepal calendar day (UTC+05:45)
    ├── Students/         the Student Hub: notes, question papers and scholarships
    │   ├── StudentsController.cs              public catalogue and detail
    │   ├── StudentResourcesAdminController.cs  staff-only CRUD
    │   ├── StudentResourceService.cs          visibility, expiry, publishing, slug rules
    │   ├── StudentResourceContracts.cs        public and staff DTOs, sort/filter enums
    │   └── ExternalUrlPolicy.cs               https shape check for outbound links
    ├── Jobs/             the job board: staff-written postings with a closing date
    │   ├── JobsController.cs                 public board and detail, anonymous
    │   ├── JobsAdminController.cs            staff-only CRUD
    │   ├── JobService.cs                     visibility, closing soon, publishing, slug rules
    │   ├── JobContracts.cs                   public and staff DTOs, sort/filter enums
    │   └── (reuses Students/ExternalUrlPolicy.cs for the application link)
    ├── Businesses/       the local directory: staff-written listings with opening hours
    │   ├── BusinessesController.cs           public directory and detail, anonymous
    │   ├── BusinessAdminController.cs        staff-only CRUD and verification
    │   ├── BusinessService.cs                visibility, publish blockers, slug rules
    │   ├── BusinessContracts.cs              public and staff DTOs, category enum
    │   └── BusinessHours.cs                  the seven-day schedule as one value
    ├── Deals/            the offers page: staff-written discounts with an end date
    │   ├── DealsController.cs                public offers and detail, anonymous
    │   ├── DealsAdminController.cs           staff-only CRUD
    │   ├── DealService.cs                    visibility, days remaining, slug rules
    │   ├── DealContracts.cs                  public and staff DTOs, DealSort enum
    │   └── (reuses Students/ExternalUrlPolicy.cs for the redemption link)
    ├── Events/           the events calendar: staff-written happenings in a district
    │   ├── EventsController.cs               public calendar and detail, anonymous
    │   ├── EventsAdminController.cs          staff-only CRUD
    │   ├── EventService.cs                   visibility, ongoing, slug rules
    │   ├── EventContracts.cs                 public and staff DTOs, EventCategory/EventSort enums
    │   └── (reuses Students/ExternalUrlPolicy.cs for the registration link)
    ├── Badges/          the badges on someone's own account: awarded by finishing a quiz
    │   ├── AccountBadgesController.cs  the whole set with progress, authenticated, GET only
    │   ├── BadgeAwardService.cs        idempotent granting after the quiz commits, and the list
    │   ├── BadgeRules.cs               the criteria as pure arithmetic, no database and no clock
    │   └── BadgeContracts.cs           the read model, locked badges included
    ├── Admin/           the staff dashboard, the audit trail and admin-only accounts
    │   ├── AdminController.cs              admin-only ping, the proof the stricter policy works
    │   ├── StaffDashboardController.cs     staff-only counts over every module, and the rows
    │   │                                   that need a decision (stale measured through
    │   │                                   Data/PublicContentScope, never restated)
    │   ├── AuditLogController.cs           admin-only read of the trail, exact-match filters
    │   ├── AuditLogService.cs              IAuditLog: appends to the caller's SaveChanges,
    │   │                                   and the paged read of what it has recorded
    │   ├── AuditLogContracts.cs            the trail's DTOs and AuditActions' action names
    │   ├── UsersAdminController.cs         admin-only list, activate/deactivate, role changes
    │   └── UserAdminService.cs             the mutations, the self-guard and their audit rows
    ├── Leaderboard/      the ranking over the XP the quiz already records
    │   ├── LeaderboardController.cs        anonymous GET, period parsed case-insensitively
    │   ├── LeaderboardService.cs           read-time ranking; no table and no refresh service
    │   └── LeaderboardContracts.cs         entries and the all/today/week period
    ├── Moderation/       member reports and the staff queue
    │   ├── ReportsController.cs            signed-in filing, rate-limited per member
    │   ├── ReportsAdminController.cs       staff queue, resolve and dismiss
    │   ├── ReportService.cs                visibility rules, the unique-target rule,
    │   │                                   queue ordering, and closing beside its audit row
    │   └── ReportContracts.cs              reasons, statuses and the queue's read model
    ├── Reference/       provinces and districts, the only place location lists come from
    ├── Ownership/       who manages a listing — asserted by a person, decided by staff
    │   ├── BusinessClaimController.cs          signed-in filing, rate-limited per member
    │   ├── BusinessClaimsAdminController.cs    staff-only queue; approval writes the owner
    │   ├── AccountBusinessesController.cs      the owner's own listings: read and save
    │   ├── OwnershipService.cs                 claim rules, rival settlement, owner scoping
    │   └── OwnershipContracts.cs               claim, queue and owner-editor DTOs
    ├── Subscriptions/   plans and granted windows: what a listing buys
    │   ├── PlansController.cs                  the catalogue, anonymous
    │   ├── FeaturedBusinessesController.cs     the strip above the directory, anonymous
    │   ├── AccountSubscriptionController.cs    state and a plan request for one listing
    │   ├── SubscriptionRequestsAdminController.cs  the request queue (admin-only)
    │   ├── SubscriptionsAdminController.cs     direct grant and cancel (admin-only)
    │   ├── SubscriptionService.cs              entitlement from dates, the extension rule
    │   ├── SubscriptionRules.cs                the shared live-featured predicate
    │   └── SubscriptionContracts.cs            catalogue, state and queue DTOs
    ├── Bookings/        one person holding a place at an event, free or paid
    │   ├── EventBookingsController.cs          book and cancel, rate-limited per member
    │   ├── EventBookingsAdminController.cs     staff-only attendance list
    │   ├── BookingService.cs                   the atomic latch against registration_count
    │   └── BookingContracts.cs                 booking read model
    ├── Payments/        7f: where money is asked for, never stored
    │   ├── PaymentsController.cs               config, checkout, verify, one reference's state
    │   ├── AccountPaymentsController.cs        the payer's own history
    │   ├── PaymentsAdminController.cs          the list, plus "check with the provider"
    │   ├── PaymentService.cs                   one open checkout, the settlement latch, refunds
    │   ├── PaymentContracts.cs                 filter, row and checkout DTOs
    │   ├── IPaymentGateway.cs                  the seam 7e left: initiate and status-check
    │   ├── EsewaPaymentGateway.cs              eSewa ePay v2, signed form out, status in
    │   ├── EsewaSignature.cs                   HMAC-SHA256 over signed_field_names
    │   └── RejectingPaymentGateway.cs          "no provider" as a real answer
    └── Calculators/    a second, larger reference example
        ├── CalculatorsController.cs        public catalogue, detail, calculate
        ├── CalculatorAdminController.cs    staff-only CRUD
        ├── CalculatorService.cs            validation and the calculation pipeline
        ├── CalculatorContracts.cs          request/response records
        ├── InputFieldDefinition.cs         the stored form schema
        ├── ICalculatorHandler.cs           one handler per calculator
        └── Handlers/                       LoanEmi, CompoundInterest, Percentage
```

**Vertical slices, not layer folders.** When a module is added it gets its own `Features/<Module>/`
folder holding its controller, DTOs, services and data access. A request can be traced without
jumping between five directories. `Features/Health` is the smallest example,
`Features/Calculators` the largest and `Features/Quiz` the one with the most invariants.

**The quiz is graded, never predicted.** A pre-answer DTO has no answer field, so a daily set can
be fetched and re-fetched without leaking the result. All the answers leave the database only
inside a `QuizAnswerReview`, which is built after a submission has been graded. Anonymous play is
therefore allowed: the score and the review are honest, and only the progression is withheld.

**Calculators are described by data, not code.** A calculator row stores its whole form as JSON in
`calculators.inputs_json`, and the frontend renders whatever the API returns. The arithmetic lives
server-side in an `ICalculatorHandler` registered in DI, so a published calculator without a handler
is refused rather than left broken, and the browser never computes a financial figure.

**The Student Hub is one slice, one table, three public shapes.** Notes, question papers and
scholarships share a table and a service because they are searched, filtered, paged and expired
identically; only the optional columns differ, and the service clears the ones that do not belong to
the saved type. The split that matters is *save* versus *publish*: a draft may be half-written, and
the staff contract reports `publishBlockers` so an unfinished row is visible as unfinished rather
than discovered by attempting to publish it. Visibility is a single predicate — published, and not
past its date on the Nepal calendar — used by the public list, the public detail and the seeded
counts, so the catalogue and the detail page can never disagree about whether a row is live.

**A job posting's status is derived, never stored.** The board has no `status` column and no expiry
flag: public visibility is published, plus a deadline that has not passed on the Nepal calendar, and
`Open` versus `ClosingSoon` is that deadline compared against today at read time. Both answers stop
being true on their own as a deadline approaches, so a stored value would be wrong the next morning —
which is the same reason `NepalDateProvider` exists and the reason the Student Hub has no `is_expired`
column. A draft's slug is editable and a published posting's is not, because the slug is the public
address and moving it breaks every link already shared. Location is a nullable `district_id` with the
province read through it, so a posting cannot claim to be in two provinces at once and a remote posting
stores nothing rather than a placeholder. Applications, CVs and salary ranges are deferred: each needs
either a moderation surface or a verification rule this phase did not have.

**Offers and events are three slices sharing one argument.** Deals, events and businesses are the
same shape of problem — staff-written public copy that is filtered, paged and expires — so each gets its
own folder and its own service rather than a generic "listing" abstraction that would have to grow a
flag per module. What they share they share deliberately: `NepalDateProvider` for "is this still
current", `Students/ExternalUrlPolicy` for the outbound link, `Reference` for the district list, and
the `ApiResponse`/`PagedResponse` envelope. A fourth module with the same shape should follow the same
pattern rather than inventing one.

**An offer and an event both have a clock, and neither stores a countdown.** `deals` carries
`starts_on`/`ends_on` as `DateOnly` and `events` carries `starts_at`/`ends_at` as Nepal wall-clock
`DateTime`; `days_remaining` and `is_ongoing` are computed per read. A stored flag is wrong by the
evening, which is the same argument as the job board's derived `ClosingSoon` and the Student Hub's
missing `is_expired`. The two modules differ only in granularity — an offer is redeemed on a day, an
event happens at an hour — and the wire format follows: a bare `yyyy-MM-dd` is safe to read anywhere,
while a bare local time must be displayed as the text it is rather than parsed as an instant, or every
reader outside Nepal sees it shifted by five hours and three quarters.

**Events are dated, not located.** A deal may be nationwide (`district_id` null) because free delivery
that applies everywhere is real; an event may not, because a reader cannot attend something with no
place. Neither table stores a latitude, for the reason the business directory does not: a district is
the location a reader filters by, and a pin the server cannot verify is worse than no pin.

**An offer or event outlives the listing it was attached to.** Both take a nullable `business_id` with
`ON DELETE SET NULL`, so retracting a business leaves its offers and events standing rather than
deleting public copy a reader may have saved. The business *name* survives with them; the public
`businessSlug` is withheld for an unpublished business, because that is a read-model decision rather
than a column — a link to a 404 is worse than no link, but a name with no link is still true.

**A deal collects nothing; an event collects seats, and since 7f a price.** There is no payment,
ticket or coupon for an offer — a deal is redeemed at a counter and money has nowhere to go — the
useful thing this site can offer is a trustworthy link out, shape-checked as `https` and never
fetched. Events are the deliberate exception to "collect nothing": `capacity` and a stored
`registration_count` let a reader hold a place here, because seat availability the organiser's
own page cannot answer for, and `ticket_price` decides whether that place is free or costs money.
Payment is eSewa's problem, not the database's: a signed form leaves, a signed payload and a status
check come back, and the row that settles is a reference and an amount. The same reason there is
no cover image — image storage has not had its disk-versus-object-storage decision yet — is why
a receipt image would be the next thing not to store.

**Reference data is fetched, not hard-coded.** The province and district lists come from
`Features/Reference` and are the single source for the board's location filters and the editor's
district select. A hard-coded list of Nepal's districts is a thing that silently goes stale, and the
seeder that validates every seeded district code against the same table is what caught the two codes
in the first seed draft that did not exist.

**A badge is derived from the quiz, and is granted after the quiz has committed.** `Features/Badges`
stores no fact about a user that the quiz has not already written. A criterion is a closed enum plus an
integer threshold — `QuizzesCompleted` or `ConsecutiveDayStreak`, compared with `>=` — read against the
count of `quiz_attempts` rows and the `streak_count` the quiz just persisted. Both rejected alternatives
are worth naming: a JSON `criteria` blob would let the seed declare a rule nothing evaluates, so a badge
could sit on the page as unearnable with nothing to report it; and an `xp_reward` column would pay XP a
second time, so the account total would stop agreeing with the sum of the quiz results behind it.

The grant deliberately runs *after* the quiz transaction commits, not inside it, for two reasons that
only one of which is about atomicity. A failure while writing a badge must not roll back a quiz somebody
actually finished. More specifically, `QuizService` catches every MySQL 1062 to turn a lost race on the
unique `(user, daily_quiz)` index into "you have already played today's quiz" — so a badge insert sharing
that save would be caught by the same handler and reported as a quiz problem the player never retried.
After the commit, each write's failure is its own.

Idempotency is structural rather than checked: the award row is the only thing written, a badge already
held is skipped, and `ix_user_badges_user_id_badge_id` is unique. Reading the list therefore grants
nothing, which the tests assert against the table rather than against the response — a second row that
the UI happened not to show is still a bug. The streak criterion reads the stored `StreakCount` rather
than recounting days, so a badge and the streak shown on the account page cannot disagree about whether
seven days were consecutive.

**Ownership is asserted, never assigned.** Staff still create, publish and verify every listing —
the owner column changes what happens *after* publication. A signed-in person files a claim
(`already_claimed` / `already_owner` are refused in the service, where the reason can be named), and
the claim grants no access of its own: approval is the single thing that moves `owner_user_id`, and
it does so by writing the owner, granting the `business` role, settling the rival claims and
appending `business.claim_approved` to the audit trail in one `SaveChanges`, so a rolled-back approval
leaves no half-state. The owner's editor is a whitelist of contact-and-hours fields rather than the
staff editor, because "may edit this listing" and "may publish or verify it" are different grants.
There is no unique index on (business, claimant): a rejected claimant who corrects their evidence
must be able to try again, and a database that refuses the second row gives no reason at all.

**Entitlement is derived from the Nepal day, exactly like every other clock in this system.**
Whether a listing is featured is `ends_on >= today` read through `INepalDateProvider` — the same
argument as the job board's `ClosingSoon` and the events' `is_ongoing`, and for the same reason: a
stored liveness flag is wrong the morning after. At most one row per business can be live, enforced
in the service because two overlapping rows would make the answer depend on which one a query
happened to find; re-granting extends by `max(current_end, today) + period_days` rather than
stacking. The featured strip is capped and sorted alphabetically so a slot is a position, not a rank,
and it renders nothing when the fetch fails rather than a stale top of the directory. The purchase
UX is still a queue, not a checkout: `POST` a request and an admin approves it — and since 7f a
paid plan opens that same request, sends the reader to eSewa, and on a verified status check
approves it from `PaymentService` with the reference in `review_note`
(`Features/Payments/PaymentService.cs`). Approval logic is written once, in one service, with one
audit line (`payment.succeeded` replacing the approval pair) whether a person clicked Approve or a
gateway answered. Plans are rows edited through an
idempotent startup seeder rather than `HasData`, because staff are meant to change the price without
a migration.

**A booking is a latch, not a check.** Availability is decided by one atomic conditional `UPDATE`
against `events.registration_count` that either succeeds or does not, with the counter released if
the claim fails; a read-then-write would let two tabs both take the last seat. The unique
(event, attendee) index means a person has one row for all time — cancelling stamps it, rebooking
reactivates it — so a place can never be held twice by one account even racing itself. Bookings take
no audit rows on purpose: the trail exists for admin and staff actions, and a reader taking a seat is
neither. Two rate limits guard the write (`Claim` 10/hour, `Booking` 20/hour per IP), running before
authentication so an anonymous flood cannot spend the budget a signed-in person needs.

Rules that keep this maintainable:

- Entities are not exposed as API models. Each feature defines its own request/response records.
- Business rules live in services, not controllers. Controllers only map HTTP ⇄ service calls.
- One `DbContext` (`AppDbContext`); entity configuration uses `IEntityTypeConfiguration<T>` in
  `Data/Configurations/`, registered by `ApplyConfigurationsFromAssembly`.
- No repository abstraction over EF Core unless a query genuinely needs it. `DbContext` is safe to
  inject directly and the extra layer usually just adds noise.

## 3. Frontend layout

```
src/
├── components/     presentational, reusable: layout/ (AppShell), auth/ (RequireAuth, RequireStaff),
│                   ui/ (Container, Button, StateMessage, Pagination) and per-module components
│                   (calculators/, quiz/, students/, jobs/, businesses/, deals/, events/, badges/,
│                   claims/, subscriptions/, bookings/)
├── config/env.ts   reads VITE_* configuration
├── features/       per-module logic: api.ts (requests) + hooks (state) — no JSX
├── lib/            cross-cutting infrastructure: apiClient.ts, apiErrors.ts
├── pages/          route-level components that compose features and components
├── styles/         tokens.css (all design values) + global.css (reset and base elements)
├── test/           Vitest setup: Testing Library cleanup after every test
├── App.tsx         RouterProvider
├── main.tsx        entry point
└── router.tsx      route table
```

Business logic stays in `features/*` and `lib/*`. Pages and components render what they are given.
Fetching, caching, retries and error normalisation belong to the hook layer, not to a component's
`useEffect`.

A page that is mostly a list over a paged endpoint keeps its list state in the URL rather than in
`useState`, so a filtered catalogue can be shared, bookmarked and survived by the back button. The
one exception is the search box, which is local state submitted by the form: firing a request per
keystroke would be both noisy and rude to the API. The public Student Hub is the reference example
of that split.

CSS: plain CSS with design tokens (`styles/tokens.css`) plus CSS Modules for component scope. No
utility framework, no runtime CSS-in-JS. Dark mode and any future rebrand are a single-file change
because every colour is a variable. Component styles never hard-code a colour, space or duration.

Tests: Vitest with jsdom and Testing Library, configured in `vitest.config.ts` and cleaned up by
`src/test/setup.ts`, run with `npm test`. Everything a page knows arrives through `api.ts`, so the
API module is mocked and the tests assert on what a reader would see — counts, link targets, the
error state and its retry — without a server. Pure logic such as the dashboard's attention-list
filtering is tested directly, where it lives, rather than through the component that renders it.

Route-level code splitting (`React.lazy`) is intentionally deferred until the route count makes the
main bundle measurably heavy; add it per module as modules arrive.

## 4. How React talks to ASP.NET Core

`src/lib/apiClient.ts` is the single HTTP boundary. Every request goes through it, so timeout
handling, JSON headers, error normalisation and base-URL resolution are implemented exactly once.

```
component/page → feature hook (features/*) → api.ts → apiClient (lib/) → HTTP
```

**Base URL.** `VITE_API_BASE_URL` defaults to `/api`, so the browser issues same-origin requests and
the Vite dev proxy forwards `/api` and `/health` to `http://localhost:5080`. No CORS preflight in
development and no backend hostname in the source. For a deployed API, set the variable to the
absolute URL. CORS is still configured on the backend (origins from `Cors:AllowedOrigins`) so
direct cross-origin use works if a deployment needs it.

**Error handling.** `apiClient` unwraps the success envelope and throws a typed `ApiError` carrying
`status`, `code`, `message` and `details`. `lib/apiErrors.ts` turns `details` into a field → message
map so a form can show the server's own message next to the right input, and picks a human sentence
for transport failures. Hooks translate an `ApiError` into UI state, and `StateMessage` renders
loading / empty / error / info states. Components never call `fetch` and never inspect HTTP status
codes.

**Timeouts and cancellation.** Every request gets a 15-second default timeout via `AbortController`
(8 seconds for health). Hooks abort in-flight requests on unmount, so a fast navigation or reload
never leaves a stale response to overwrite fresh state.

## 5. API conventions

**Envelope.** Every endpoint returns the same shape, so the future Flutter client has one decoder.

```jsonc
// success
{ "success": true, "data": { }, "error": null }

// failure
{ "success": false, "data": null, "error": { "code": "not_found", "message": "…", "details": null } }
```

**Rules**

- JSON only, camelCase property names. Timestamps are ISO-8601 without an offset (`2026-10-04T10:00:00`)
  and are read as **Nepal wall-clock**, not UTC; audit columns (`createdAt`, `updatedAt`) are server
  values the client never displays. A `DateOnly` is a bare `yyyy-MM-dd`. See "an offer and an event
  both have a clock" in section 2 for why none of them carry a `Z`.
- Versioned under `/api/...` from day one; breaking changes get a new major version prefix.
- Resource nouns in paths, HTTP verbs for actions: `GET /api/students/resources`,
  `GET/POST /api/admin/jobs`, `GET /api/jobs/{slug}`.
- Public reads are `[AllowAnonymous]` at class level and are safe because every filter argument is bound
  to a fixed property with a whitelist, never concatenated into a query.
- List endpoints are paged (`page`, `pageSize`, capped) and return a total count. See "one paging
  convention" below.
- Errors use stable machine-readable `code` values; `message` is for humans and may change.
- Validation failures are `400` with per-field details; auth `401`/`403`; missing rows `404`.
- Swagger/OpenAPI is generated and available at `/swagger` in development.

### One paging convention

Every list endpoint, public and staff, returns `PagedResult<T>` from
`NepalHub.Api.Common`. `Items`, `Page`, `PageSize` and `TotalCount` are stored; `TotalPages`,
`HasPrevious` and `HasNext` are derived, so no endpoint can report a page count that disagrees with
its own total.

- `PageRequest.Normalise` is applied to the caller's arguments first: a page below 1 becomes the
  first page, and a page size above `MaxPageSize` (100) is reduced rather than rejected, so a client
  cannot ask the database for an unbounded set.
- `PageRequest.ClampTo(totalCount)` is applied *after* the count query and before the `Skip`.
  Whether a page is past the end is not knowable until the rows have been counted, and the answer is
  the last page rather than an empty one — an empty page beside a `TotalPages` of 1 is a response
  that contradicts itself, and it is a worse answer than the one the caller was obviously reaching
  for. A result set with no rows has no last page to fall back to, so an empty list is page 1, which
  keeps `HasPrevious` false in the only case where there is nothing to read.
- Each service clamps for itself. The shared type makes the convention uniform; it does not reach
  into six services to enforce it, because a helper that re-ran the filter would be a second query
  for a decision one line can make.

`GET /api/search` reuses `PageRequest` but caps its own page size at 30, because a search page is
six queries rather than one and the extra rows serve nobody who would scroll them. That cap is
applied in the controller rather than through `Normalise`, which would silently hand back 24-row
pages.

### Global search

`GET /api/search` is `[AllowAnonymous]`. The header search box has to work before someone has an
account, and the endpoint cannot reach anything private in any case, because it has no other way to
read a row: every module's public list and detail, and search itself, filter through
`PublicContentScope`. One place states what a reader may see, so a visibility rule cannot be changed
in a module and missed in search — the failure mode being a search result for a page that no longer
serves it, which looks fine in isolation from both sides.

Six tables with six different column lists cannot be unioned in SQL, so `SearchService` takes at most
`SearchLimits.CandidateCap` (50) bounded candidates from each source, ranks them together in memory,
and pages the merged set. No source is counted: a `COUNT` over the whole matched set is the one query
here that grows without limit as the catalogue does, so `TotalCount` is the number of candidates
returned, not the number of matches in existence.

That is why `SearchResponse.Truncated` exists. It is measured on the rows SQL returned rather than on
the rows that survived ranking — ranking drops rows matching only a low-value haystack, so a source
that filled its cap and ranked just under it has still hidden matches. A client rendering "4 matches"
when four were shown is telling the truth; "4 results in Nepal Hub" is a claim the server cannot
support.

Ranking and filtering agree on what a match is because both go through `SearchText.Normalise`, which
strips diacritics and lowercases. MySQL matches on `utf8mb4_0900_ai_ci`, so a reader typing `CAFÉ`
is meant to find `cafe`; an `Ordinal` comparison would score that row as a non-match and push it off
the page.

## 6. Authentication and security (implemented for Phase 1)

- **Passwords:** ASP.NET Core Identity — PBKDF2 hashing, lockout, security-stamp invalidation. No
  hand-rolled crypto.
- **Session:** a server-side Identity authentication cookie, `Secure`, `HttpOnly`,
  `SameSite=Lax`, issued under the `Identity.Application` scheme. **No JWT and no token in
  `localStorage`**, so an XSS bug cannot read the session. A native Flutter app uses the same cookie
  jar, so the contract stays identical for both clients.
- **CSRF:** every unsafe request (`POST`, `PUT`, `PATCH`, `DELETE`) to `/api` is validated by
  `AntiforgeryMiddleware` against the `X-CSRF-TOKEN` header and the `.NepalHub.Csrf` cookie, and a
  mismatch is `400 csrf_failed`. `GET /api/auth/csrf` issues the token. The token is bound to the
  current identity, so the frontend discards it after sign-in and sign-out.
- **Authorisation:** role-based policies (`user`, `business`, `moderator`, `admin`) enforced by
  `[Authorize(Policy = ...)]` with `admin-only` and `staff-only` policies, plus resource-level
  ownership checks in services. Business accounts can only edit their own listings. The queues are
  split by consequence: the claims queue is `staff-only` (it moves ownership, a fact staff already
  curate), while subscription requests and direct grant/cancel are `admin-only` (they are the site's
  money-equivalent), and event attendance is `staff-only`.
- **Default deny:** a fallback authorisation policy means every endpoint requires a signed-in user
  unless it opts out with `[AllowAnonymous]`, so a new controller is protected the moment it is added.
- **Input safety:** EF Core parameterises all SQL; request DTOs are validated with DataAnnotations;
  file uploads are validated by content type and size and stored outside the web root.
- **Rate limiting:** a fixed-window limiter per policy, returning `429 rate_limited`. Auth endpoints
  get 20 requests/minute/IP (`Auth:RateLimit:PermitLimit`) and the public calculation endpoint gets 60
  requests/minute/IP (`Calculate:RateLimit:PermitLimit`), so one visitor cannot burn the server on
  arithmetic while still letting a person work through a calculator normally. Logout gets its own 20/
  minute (`Logout:RateLimit:PermitLimit`) because it is an unsafe request that tears down a session,
  and report filing gets 10/hour (`Report:RateLimit:PermitLimit`) because it is the public write a
  signed-in member can repeat — three in the suite's window is already a queue nobody can read. Claim
  filing gets 10/hour (`Claim:RateLimit:PermitLimit`) and event booking 20/hour
  (`Booking:RateLimit:PermitLimit`), both per IP and both registered *before* authentication, so an
  anonymous flood cannot spend the budget a signed-in person needs. Payment checkout and verify get
  20/hour each (`Payment:RateLimit:PermitLimit`) for the same reason a browser will happily re-POST a
  return URL, and `GET /payments/config` is anonymous because it only decides whether a button is
  shown. Read-only endpoints are not
  limited. Public write endpoints (quiz answers) reuse the same mechanism.
- **Errors:** Identity failures are translated into stable field codes (`email_taken`,
  `password_too_short`, `password_too_weak`) so the UI can put the message next to the right input.
  A wrong password and an unknown account return the same `401 invalid_credentials`.
- **Email:** accounts are created already confirmed because no mail provider is connected yet.
  Verification and password-reset emails arrive with the notifications work.
- **Admin actions** that change other users' data are written to an audit log: moderation, user
  administration, claim approvals and subscription grants/cancels append through `IAuditLog.Record`
  into `admin_audit_logs` inside the same `SaveChanges` as the change itself, with JSON
  `before`/`after` snapshots, and the rows are never updated. Owner edits, claim filings, plan
  requests and event bookings are deliberately *not* audited — the trail is for decisions staff make
  about other people's accounts, not for a person acting on their own. **Money is the one exception
  to "the payer writes nothing":** `payment.succeeded` and `payment.refunded` are appended by
  `PaymentService` inside the same `SaveChanges` as the settlement, with the payer as the actor, so
  the person whose entitlement appeared can be traced without a second, staff-written approval row
  saying the same thing. The log is read back only by
  `admin-only` at `GET /api/admin/audit-log`.

## 7. Deployment target

One host, one origin, no reverse-proxy complexity to debug at first:

1. `npm run build` in `frontend/` → static `dist/`
2. `dotnet publish` the API
3. The API serves `dist/` as static files with SPA fallback to `index.html`
4. Secrets and the connection string come from environment variables only
