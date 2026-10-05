# Roadmap

Each phase is built, reviewed and approved before the next one starts. A phase is not "done" until
it is verified end to end against a real MySQL database — no mocked persistence.

## Phase 0 — Foundation ✅

- [x] Environment audit (Node 22.16.0, .NET SDK 8.0.425, Git)
- [x] Repository, `frontend/`, `backend/`, `docs/`
- [x] React 19 + TypeScript 5.9 + Vite 7 + React Router 7, tokens-based CSS, 320px-first
- [x] ASP.NET Core 8 Web API with CORS, Swagger, health endpoint
- [x] `AppDbContext` + Pomelo MySQL provider + pinned `dotnet-ef` local tool
- [x] Response envelope and typed `apiClient`
- [x] Secrets via user-secrets, zero tables, zero features

## Phase 1 — Database and identity ✅ complete

- [x] MySQL 8.4.9 installed as `MySQLNepalHub84` on `127.0.0.1:3307`, alongside an untouched XAMPP
- [x] `nepalhub` and `nepalhub_test` databases, least-privilege `nepalhub_app` user
- [x] `20260930092534_InitialCreate`: Identity tables (`users`, `roles`, `user_roles`, `user_claims`,
      `user_logins`, `user_tokens`, `role_claims`) plus `provinces` / `districts`
- [x] 7 provinces and 77 districts seeded with deterministic ids
- [x] ASP.NET Core Identity with `HttpOnly` cookie, lockout, password policy, no password stored in
      the browser
- [x] `POST /api/auth/register`, `POST /api/auth/login`, `POST /api/auth/logout`,
      `GET /api/auth/session`
- [x] Antiforgery validation on every unsafe `/api` request (`X-CSRF-TOKEN`)
- [x] Rate limiting on auth endpoints (20/minute/IP) and `admin-only` / `staff-only` policies
- [x] Sign-up / sign-in / account UI with validation, loading, error and success states
- [x] Frontend route protection and a CSRF-aware `apiClient`
- [x] 22 integration tests against the real `nepalhub_test` database

Deliberately deferred to a later phase: email verification and a `refresh` endpoint. There is no mail
provider yet, so accounts are created already confirmed, and the cookie is a session that the browser
keeps until it expires — there is no access token to refresh.

## Phase 2 — First real module (tools and calculators) ✅ complete

Smallest end-to-end feature that proves calculators can be added one by one.

- [x] `calculators` table: slug, copy, category, sort order, publish flag and a JSON input schema
- [x] `20260930105251_AddCalculators` applied to `nepalhub` and `nepalhub_test`
- [x] `GET /api/calculators` (paged, searchable, category filter) and `GET /api/calculators/{slug}`,
      both anonymous
- [x] `POST /api/calculators/{slug}/calculate`, rate limited to 60/minute/IP, on a dedicated
      `calculate` policy
- [x] `staff-only` CRUD at `/api/admin/calculators` — immutable slug, draft-only delete, and a
      published row is retracted rather than removed
- [x] Every calculation runs server-side through `ICalculatorHandler`; publishing without a
      registered handler is refused
- [x] Tools hub page with URL-driven search, category and paging, plus a calculator page whose form
      is rendered from the stored schema
- [x] Staff management page: create, edit, publish/unpublish and delete drafts
- [x] Three calculators seeded by an idempotent startup seeder: loan EMI, compound interest and
  percentage
- [x] 60 integration tests against the real `nepalhub_test` database

Deliberately not in this phase: a tax calculator, saved results, and result history. The
`CalculatorCategory` enum already carries the categories later modules will need.

## Phase 3 — Daily quiz and XP ✅ complete

- [x] Six quiz tables: `quiz_questions`, `quiz_options`, `daily_quizzes`, `daily_quiz_questions`,
      `quiz_attempts`, `quiz_attempt_answers`
- [x] `20260930122740_AddQuiz` applied to `nepalhub` and `nepalhub_test`
- [x] 31 questions / 124 options seeded by an idempotent startup seeder with deterministic ids, so
      staff edits survive a reseed
- [x] `GET /api/quiz/daily`, anonymous, with no answer or explanation in the payload, and a
      deterministic category-balanced set materialised on first use for a given Nepal date
- [x] `POST /api/quiz/daily/submit`, anonymous and rate limited to 20/minute/IP, antiforgery
      protected, graded server-side in one transaction
- [x] One attempt per user per day, enforced by a unique index rather than only by a service check
- [x] `GET /api/quiz/daily/result` and `GET /api/quiz/stats`, both authenticated
- [x] XP, level and streak rules as one service writing `users.xp` / `level` / `streak_count` /
      `last_active_on`; anonymous play is graded and reviewed but records nothing
- [x] `staff-only` CRUD at `/api/admin/quiz/questions` — a draft needs 2-6 distinct non-empty
      options but may have no correct answer and no explanation; publishing requires exactly one
      correct option and an explanation; a question that has been played can be retracted but never
      rewritten or deleted
- [x] Daily quiz page with the question set, the per-day XP pot, a post-submit review with
      explanations, and an XP/level/streak summary
- [x] Staff management page: create, edit, publish/retract, delete drafts, search and category
      filter
- [x] 99 integration tests against the real `nepalhub_test` database (64 carried forward)

Deliberately not in this phase: badges, a leaderboard, resume, per-question submits, and
anonymous leaderboard eligibility. Grading is already atomic, so there is no half-finished attempt
to resume and no state to reconstruct. Badges and the leaderboard need their own read models and
are listed under Phase 7.

## Phase 4 — Student Hub ✅ complete

Study notes, past question papers and scholarships in one catalogue, from one generic table.

- [x] One `resources` table with `type` (Note / QuestionPaper / Scholarship), subject `category`,
      `level` and the optional columns each type needs
- [x] `20260930154447_AddStudentResources` applied to `nepalhub` and `nepalhub_test`
- [x] 24 resources seeded by an idempotent startup seeder with deterministic ids: 10 notes, 7 question
      papers and 7 scholarships, two of them already past their deadline
- [x] `GET /api/students/resources` (paged, searchable, type/category/level filters, newest /
      expiring / title order) and `GET /api/students/resources/{slug}`, both anonymous
- [x] A resource is public only while it is published and its date has not passed **on the Nepal
      calendar day**; an expired row returns `404` publicly and stays visible to staff
- [x] `staff-only` CRUD at `/api/admin/students/resources` — immutable slug, publish blockers
      reported on the row, and a published resource retracted rather than deleted
- [x] A draft may be half-written; publishing requires a summary of 20+ characters, a body of 80+
      and the fields the chosen type needs
- [x] Outbound links are shape-checked absolute `https` URLs and are never fetched; bodies are
      plain text rendered as text
- [x] Public catalogue page with URL-driven search, filters, ordering and paging; detail page; staff
      management page with create, edit, publish/retract, delete drafts, search and status filter
- [x] 67 integration tests against the real `nepalhub_test` database (166 total)

Deliberately not in this phase: file uploads, saved items, a rich-text editor and a "calculators
scoped to the student audience" page. A question paper links out to a stored address instead of
hosting a file, the body is plain text because an HTML surface is a moderation problem this phase
did not need to take on, and saved items belong with the account's own data rather than with a
public catalogue. Search covers title and summary across all three types, which is the part that
gets used.

## Phase 5 — Jobs ✅ complete

Openings in one board, staff-written, leaving on their own closing date.

- [x] One `jobs` table added by `20261001041409_AddJobs`, applied to `nepalhub` and `nepalhub_test`
- [x] `category` (15 sectors), `employment_type` (full time / part time / contract / internship) and
      `work_mode` (on site / remote / hybrid), plus a required `application_url` and
      `application_deadline`
- [x] Location is a nullable `district_id` and the province is read through it, so the two cannot
      disagree; a remote posting is saved with no district
- [x] 16 postings seeded by an idempotent startup seeder with deterministic ids, one in each of the
      seven provinces, two of them already past their deadline
- [x] `GET /api/jobs` (paged, searchable, province/district/sector/employment/workplace filters,
      newest / closing-soonest order) and `GET /api/jobs/{slug}`, both anonymous
- [x] A posting is public only while it is published and its deadline has not passed **on the Nepal
      calendar day**; a closed posting returns `404` publicly and stays visible to staff
- [x] `staff-only` CRUD at `/api/admin/jobs` — a draft's slug is editable and becomes permanent once
      published, publish blockers reported on the row, and a published posting retracted rather than
      deleted
- [x] A draft may be half-written; publishing requires a title, an employer, a description, an
      application link and a deadline that has not passed
- [x] Application links are shape-checked absolute `https` URLs and are never fetched; descriptions
      are plain text rendered as text
- [x] Public board page with URL-driven search, filters, ordering and paging; detail page; staff
      management page with create, edit, publish/retract, delete drafts, search and status filter
- [x] 83 integration tests against the real `nepalhub_test` database (249 total)

Deliberately not in this phase: applications, saved postings, CVs, salary ranges, employer accounts
and featured listings. Applications need a moderation surface and a place to store what somebody
sent, and a posting that a business owns needs the business module to attach to; a job board a
reader cannot apply through is still worth having, because the reader follows the employer's own
link. Salary was left out because a range nobody verifies is worse than none, and Nepal's
conventions for publishing one are not settled.

## Phase 6 — Businesses (first slice, live)

A public directory of staff-written local listings. The scope is deliberately a directory and
nothing else: no business accounts, because an owner portal has nothing to attach to until someone can
register as a business.

- [x] `businesses` and `business_hours` added by `20261001101158_AddBusinesses`; hours is a child
      table keyed `(business_id, day_of_week)` with an explicit `is_closed`, so a listing that
      publishes no hours is a normal state rather than seven null columns
- [x] `BusinessCategory` enum of twelve labels rather than a category table, so the set is searchable,
      translatable and impossible to orphan
- [x] The province is read through a required `district_id` rather than stored beside it, so the two
      cannot disagree; `is_published` and `is_verified` are independent flags, so unverifying does not
      retract a live listing
- [x] Public list with search, category, province, district and verification filters, `Name`/`Newest`
      ordering and paging; detail page by slug; staff list including drafts
- [x] Staff create, edit, publish/retract, verify/unverify, delete drafts only
- [x] Publishing requires a phone number, a description of at least 80 characters and verification;
      the staff editor is given the same list as `publishBlockers`, so what the form says and what the
      server enforces cannot drift apart
- [x] Website is shape-checked as an absolute `https` URL and never fetched; phone is normalised to
      digits so one business cannot appear under two spellings of the same number; search escapes
      `LIKE` wildcards
- [x] Public directory page, detail page, staff management page and editor, all with the seven-day
      hours table Sunday first
- [x] 25 seeded listings across all twelve categories and all seven provinces (72 integration tests,
      354 total)

Deliberately not in this slice: business accounts and an owner portal, paid subscriptions, featured
placement, reviews and ratings, image uploads, saved businesses, messaging, map integration
and claims. `docs/database.md` records the schema note for each; the one that gates the most of them
is `businesses.owner_user_id`, which does not exist yet by design. Deals arrived separately as
Phase 6b, on the same rule that a listing is staff-written until there is an owner to attach to.

## Phase 6b — Deals and events (live)

A public offers page and a public events calendar, both staff-written and both linking into the
business directory rather than replacing it. An offer is a discount with an end date; an event is a
scheduled thing that happens in one district. Neither is user-submitted, because neither has a
submitter: both are staff entries with a beginning and an end, which is why they can be useful
without an owner account.

- [x] `deals` and `events` added by `20261001142733_AddDealsAndEvents`, with
      `20261001155854_AddDealsAndEventsIndexes` adding the two indexes behind the `Newest` sort; both
      take a nullable `business_id` that is set null on delete, so an offer or event still reads after
      the listing it was attached to is retracted
- [x] A deal is `starts_on`/`ends_on` as `DateOnly`, day-granular and inclusive, because an offer is
      redeemed on a day rather than at an instant; `days_remaining` is computed against the current
      Nepal date on every read and stored nowhere, so it cannot go stale
- [x] A deal's headline is `discount_text` — the business's own words, 120 characters — rather than a
      percentage or a price pair, because "free chiya with any thakali set" is a real offer and most
      Nepalese offers are not expressible as a number
- [x] `DealSort.EndingSoon`/`Newest` and `EventSort.Soonest`/`Newest`; enums are serialised by name so
      the wire contract is readable and cannot silently change meaning when a member is reordered
- [x] An event's `starts_at`/`ends_at` are `DateTime` read as Nepal wall-clock, and `is_ongoing` is
      derived per read. Public reads drop finished events and keep an event that is still running until
      its finishing time passes, inclusive
- [x] A deal's `district_id` is nullable — a nationwide offer is the case that separates an offer from
      a business listing — while an event's is required, because a reader cannot attend something with
      no location
- [x] Public list with search, province, district, category and ordering filters plus paging; detail by
      slug; staff list including drafts, expired deals and finished events
- [x] Staff create, edit, publish/retract, delete drafts only; a published row cannot be deleted, so the
      record of what was public is kept
- [x] Redemption and registration links are shape-checked as absolute `https` URLs and never fetched;
      public search escapes `LIKE` wildcards, so searching `%` matches a literal `%` and nothing else
- [x] Public `businessSlug` is emitted only for a published business. The name always survives; the
      slug does not, because a slug is only useful when it resolves to a page and a link to a 404 is
      worse than no link
- [x] Public offers page, offer detail page, events page, event detail page, staff management pages and
      editors. Event times are read as text rather than as an instant, so ten o'clock reads as ten
      o'clock in every reader's browser
- [x] 15 seeded deals and 15 seeded events, including a draft, an already-expired offer and a finished
      event, so every state is visible in a fresh install rather than only in the tests (114
      integration tests for the two modules, 506 total)

Deliberately not in this slice: payments, tickets, bookings, RSVPs, reviews, image uploads, owner
portals, claims, subscriptions, saved offers, notifications and maps. An offer has no price to pay
and an event has no ticket, so a payment provider would have nothing to attach to yet; `docs/database.md`
records the column each of those would need.

## Phase 6c — Business registration and monetisation

- [ ] Business registration and verification flow, once there is an owner column to attach it to
- [ ] Subscription plans and payment provider adapter

## Phase 7 — Global search, gamification and admin

### Phase 7a — Global search and shared pagination ✅

One search box that reaches every public module, plus the shared pagination convention the earlier
phases were written towards and the test-database isolation the suite turned out to need.

- [x] `PublicContentScope` (`NepalHub.Api/Data/PublicContentScope.cs`) states once what a reader may
      see — published, unexpired, unfinished — and every module's public list and detail routes
      through it. Search has no other way to read a row, so a search result cannot drift from the
      module page serving it. The reasoning for each rule moved with the rule rather than staying
      behind with the service that first needed it
- [x] `GET /api/search`, anonymous by design: the header box has to work before someone has an
      account, and every module it reads is already public. `q` of 2–100 characters after
      normalisation, optional `module`, `page`/`pageSize`. A term that normalises to nothing is a
      missing query, not a search for nothing
- [x] Six tables with six different column lists cannot be unioned in SQL — there is no shared key
      and each projects different columns — so the work is three steps: at most
      `SearchLimits.CandidateCap` (50) candidates from each source, rank them together, page the
      merged result. No source is counted, because a `COUNT` over the whole matched set is the one
      query here that grows without limit as the catalogue does; a request therefore touches at most
      six bounded, indexed queries
- [x] `truncated` is measured on the rows SQL returned, not on the rows that survived ranking.
      Ranking drops rows that matched only in a low-value haystack, so a source that filled its cap
      and ranked just under it has still hidden matches from the page; measuring after ranking would
      under-report truncation in exactly the case the flag exists to warn about. The UI reads it and
      renders "N strongest matches" rather than claiming a total it cannot support
- [x] Ranking compares diacritic-stripped, lowercased text on both sides, because MySQL matches on
      `utf8mb4_0900_ai_ci` and a reader typing `CAFÉ` is meant to find `cafe`. Comparing the same
      strings with `Ordinal` would score a row the database had already matched as a non-match and
      push it off the page
- [x] `LIKE` wildcards in a public term are escaped, so searching `%` matches a literal `%` and
      nothing else. Queries run sequentially, not concurrently, because an `AppDbContext` is not
      thread-safe and six at once would throw rather than save real time at this size
- [x] Search caps its page size at 30 rather than the shared 100, because a page here is six
      queries and four extra pages of work serves no reader who would scroll it. It reuses
      `PageRequest` for the rest rather than inventing a second paging convention
- [x] Every list service now applies `PageRequest.ClampTo(totalCount)` between the count query and
      the `Skip`, so a request for page 999 of a two-page list is answered with the last page rather
      than an empty page beside a `TotalPages` of 1 — a response that contradicts itself. Applied to
      calculators, resources, jobs, businesses, deals and events, public and staff alike
- [x] `Search` rate-limit policy at 60/minute/IP, configurable through `Search:RateLimit:*`. Harder
      than calculation on purpose: a calculation is one row of arithmetic, whereas one search is six
      queries, and an unthrottled loop of them is the cheapest way to make the whole API slow
- [x] Header search box on every page and a `/search` results page with grouped results and a module
      tab strip. The header box is suppressed on `/search` itself, so the page never shows two
      identical search boxes, and search is a footer link rather than a tenth item in a nav that
      already wraps on a phone
- [x] 31 integration tests for search, covering every module's visibility boundary, the truncation
      contract, wildcards, term normalisation, paging and the rate limit
- [x] The test database is dropped and rebuilt from the migrations once per run, then reseeded by
      the application. Without it every run left its own published rows behind, the catalogue grew
      without bound, and a coverage assertion asked of the first page started failing for reasons
      that had nothing to do with the seed data. Rebuilding also re-dates the seed set, which is
      inserted once and never re-dated, so its expiry windows stay valid
- [x] That reset is guarded, and the guard is tested: `nepalhub` is refused however it was reached,
      including through an explicitly mis-set `ConnectionStrings__Test`, and anything other than
      `nepalhub_test` has to have been named by hand. `TestDatabaseTests.cs` exercises the decision
      as a pure function of its arguments, taking no fixture, so 20 tests can cover it without any
      of them connecting to a server. `NepalHubTests__KeepData=1` skips the reset

Cross-module search is delivered as a live six-query search rather than a materialised index: there
is no search-index table, and one is not warranted until a query is slow enough to notice.

### Phase 7b — Badges ✅

Read models over the progression the quiz already records. No new fact about a user is stored, and no
second place for XP or a quiz count is introduced.

- [x] Two tables: `badges` (the definitions) and `user_badges` (the awards), the latter unique per
      user+badge so a badge cannot be granted twice
- [x] `20261002162619_AddBadges` applied to `nepalhub` and `nepalhub_test`
- [x] Three definitions seeded through `HasData` with deterministic ids — first quiz, seven-day
      streak, ten quizzes — so a fresh environment reproduces them and a migration cannot overwrite
      a real edit the way an idempotent startup seeder would not
- [x] The criterion is a closed `BadgeCriteria` enum plus an integer `threshold`, not a JSON blob. A
      JSON criterion would let the seed describe a rule that nothing evaluates, so a badge could be
      listed as unearnable and nobody would find out until a player waited forever for it
- [x] No `xp_reward` column. The quiz decides XP once per attempt; a badge that quietly paid out
      again would make the total on the account page disagree with the sum of the quiz results
      behind it. The numbers a badge reads are the count of `quiz_attempts` rows and the
      `streak_count` the quiz just wrote — never a tally kept alongside them
- [x] The streak criterion reads the stored `StreakCount` rather than recounting days, so a badge
      and the streak on the account page cannot disagree about whether seven days were consecutive
- [x] Awarding runs **after** the quiz commits, not inside its transaction: a failure writing a badge
      must not roll back a quiz somebody finished, and the submit path's duplicate-key handler
      catches every MySQL 1062 to report "you have already played", so a badge insert sharing that
      save would be reported as a quiz problem. Each write's failure is now its own
- [x] Idempotent by construction — the award is the only row written, a held badge is skipped, and
      the unique index makes a second row impossible. Reading the badges grants nothing, which is
      asserted against the table rather than against the response
- [x] `GET /api/account/badges`, authenticated, the only verb. A badge is a consequence of a
      finished quiz, not something a request can ask for, so the feature is not subject to
      antiforgery and there is no way to grant one over HTTP
- [x] Locked badges are returned rather than filtered out, each carrying `current`/`threshold`, so an
      account with nothing yet is shown what there is to reach instead of an empty panel
- [x] Account page section: an "N of M earned" heading, a card per badge, a progress meter and the
      same count as text, earned ones dated in the Nepal wall clock the award was written in
- [x] The `icon` key renders as one text glyph from a table in `features/badges/format.ts`, with a
      neutral fallback for an unknown key. No icon package: the brand mark is already `NH` and a
      card's affordance is `→`, so three glyphs did not justify a dependency
- [x] `BadgeRules` as pure arithmetic with no database and no clock, asserted on both sides of every
      threshold, plus a test that the two criteria are not interchangeable
- [x] 22 integration tests: the endpoint's authorisation and ordering, anonymous play awarding
      nothing, a repeated play awarding nothing extra, repeated reads never writing a second row, no
      badge paying extra XP, the award timestamp being the quiz's, and both streak cases driven by a
      pinned clock — seven days in a row earning it, and seven plays with a gap never earning it

### Phase 7c — Leaderboard, admin and monetisation (not started)

- [ ] Leaderboard, the other read model over the XP the quiz already records. Cached ranking rows
      per period, refreshed by a background service rather than recomputed on every request
- [ ] Admin screens, reports queue and audit log (the `admin` and `moderator` roles and the
      `admin-only` / `staff-only` policies already exist)
- [ ] Abuse protection across the remaining public writes

## Phase 8 — Mobile

- [ ] Flutter app against the same public API
- [ ] Push notifications, offline caching of reference data

## Standing rules

- No feature is built before its data model and API contract are agreed.
- No mocked persistence: features use real MySQL through EF Core.
- New packages are justified in one sentence before being added.
- `npm run typecheck`, `npm run build`, `dotnet build` and `dotnet test` must pass before any phase is reported done.
