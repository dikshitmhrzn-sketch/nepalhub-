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

## Phase 6c — Business registration and verification (live)

The rule that held through 6a and 6b — a listing is staff-written until there is an owner to
attach to — now has its second half: an owner can put a draft of their own into the system and
ask for it. Publication stays a staff decision, so the two ways in (claim, register) end at the
same place and neither of them publishes anything.

- [x] `business_verification_requests` (`20261006093754_AddBusinessVerifications`): who asked,
      their note, `Pending`/`Approved`/`Rejected`, who decided, when and the review note; no
      unique index on the business (a corrected draft must be askable again), with the queue
      index and the one-open-request index instead
- [x] `POST /api/account/businesses` registers a listing owned by the caller: born unpublished and
      unchecked, 404 on the public site, `409 slug_taken` for a taken address, `400
      validation_failed` with a field error for a reserved one or an unknown district
- [x] The owner's save gains identity while the listing is a draft — slug, name, category and
      district — omitted from the request once published rather than sent and refused; contact
      details and the week were already editable either way
- [x] `POST /api/account/businesses/{slug}/verification` opens one request (`verification_pending`
      while one waits, `already_published` when there is nothing left to decide) and puts the note
      on the row it creates
- [x] `GET /api/admin/business-verifications` (pending first, `status` filter) with approve and
      reject: approval runs `CollectPublishBlockers` and refuses `409 not_publishable` naming each
      blocker, or publishes, stamps `is_verified`, grants the `business` role and audits
      `business.verification_approved`; rejection audits `business.verification_rejected` and
      leaves the draft where it was. Both settle from the row's own status, so a second decision
      is `409 verification_closed`
- [x] `Business:RateLimit` (10/hour per IP) on register and submit, because a stranger can mint
      rows and then ask staff to review them; one policy covers both verbs
- [x] Frontend: the registration form and verification list on `/my-business`, the draft identity
      fields and the ask-for-a-check panel in the owner editor, `/admin/verifications` with
      URL-owned status windows, a nav item, the dashboard's `pendingVerifications` count and quick
      link. Gates: `dotnet build` 0/0, **641/641** backend tests, frontend typecheck and build
      clean, **61/61** frontend tests

Subscription plans and the payment provider adapter were not built in this phase: 7e and 7f
delivered them instead, once there was a queue and a listing to attach a plan to.

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

### Phase 7c — Staff dashboard ✅

One staff-only screen over every module: the counts a moderator asks for before deciding what to
open, and the rows that have a decision waiting. No new table, no migration, no write verb, and no
second copy of a rule the site already keeps somewhere else.

- [x] `GET /api/admin/dashboard`, `staff-only`, the only dashboard endpoint. The `admin-only`
      `ping` stays the proof that the stricter policy works; this one is the screen itself
- [x] Eight counts — users, businesses, jobs, offers, events, resources, quiz questions,
      calculators — each taken from that module's own staff-list `TotalCount` asked with no
      filters, so a card and the management page under it are the same query rather than two
      queries that agree today
- [x] "Needs attention" in two groups: rows nobody has published yet, and rows whose publish flag
      no longer describes what a reader sees. Rows at zero are dropped instead of rendered, so the
      section stays worth reading on a healthy catalogue
- [x] The stale half is `published − visible`, counted through `PublicContentScope` itself. The
      date comparison is not restated, so the section cannot drift from what the public site serves
- [x] Unverifying does not retract a listing, so a live row that lost its verification is reported
      too — the one case where a published row a reader is being told to trust is one staff has
      stopped standing behind
- [x] `/admin` behind `RequireStaff`, a staff-only header link, eight count cards, the attention
      rows grouped and linked, and quick links to the seven management pages
- [x] Quick links are the plain `/admin/*` routes: those pages keep their filters in component
      state rather than in the URL, so a link carrying `?status=draft` would land on the
      unfiltered list while looking precise
- [x] 9 integration tests: 401 for an anonymous reader, 403 for a signed-in non-staff account,
      both staff roles allowed, every count checked against the management page's own total, the
      draft and stale counts checked against the tables through the scope, the seeded catalogue
      actually reporting something, and a draft created, published, retracted and deleted with the
      counts moving and then coming back (557 total)
- [x] The frontend's first test runner: Vitest with jsdom and Testing Library, added because there
      was none — justified in one sentence under the standing rule on new packages. 14 tests cover
      the attention list's zero-filtering, the hook's loading/error/retry/unmount behaviour, and
      the page's counts, links and error recovery

### Phase 7d — Leaderboard, moderation and abuse protection ✅

The read model over the XP the quiz already records, the moderation queue the staff dashboard was
counting towards, and the rate limits the two new public writes needed.

- [x] `GET /api/leaderboard`, anonymous: `period=all|today|week`, 100 entries per window. Computed at
      read time rather than from cached rows — the earlier plan's `leaderboard_entries` table and its
      refresh service are dropped, and the reversal is recorded here rather than quietly deleted:
      all-time is a seek over `ix_users_level_xp`, the period boards group `quiz_attempts` over
      `ix_quiz_attempts_completed_at`, and a refresh job would only buy staleness for a figure that
      is already correct on demand. The week runs Saturday to Friday on Nepal's clock — Nepal's own
      weekend-first week — via `INepalDateProvider.UtcStartOfDay`, converted to UTC exactly once per
      query. Ranks come from the page offset plus the row index, so page two starts at the number it
      actually occupies; deactivated accounts are excluded in the query, and a blank display name
      falls back to `Member` rather than to an email address the board must not publish
- [x] Member reports (`POST /api/reports`): signed-in only, five listing types, closed reason set,
      optional 500-character note. The target must be publicly visible (`PublicContentScope`), a
      repeat against the same target is `409 already_reported` — enforced by a unique index, so a
      double-click cannot produce two rows even if the requests race — and filing sits behind its own
      rate-limit policy (10 per hour, `Report:RateLimit`). The report stores a pointer and a reason,
      never a copy of the content it complains about
- [x] The staff queue (`GET /api/admin/reports`, plus resolve/dismiss): open work first, newest
      first, with the target's title and slug joined at read time and an exact status filter
      (`all` is open-first order, not "ignore everything"). Closing a report writes
      `report.resolved` / `report.dismissed` to the audit trail in the same `SaveChanges` as the
      decision, so a half-applied close cannot leave a log entry for an action that did not happen
- [x] The audit trail (`admin_audit_logs`): appended through `IAuditLog.Record` by moderation and
      user administration only — the subject there is somebody else's account rather than the
      site's own catalogue — with `before`/`after` as JSON snapshots and rows that are never
      updated. `GET /api/admin/audit-log` reads it admin-only with exact-match `action` /
      `targetType` filters
- [x] User administration (`/api/admin/users`): list with search/status/role filters, activate and
      deactivate, role grant and revoke. An admin cannot disable or demote themselves
      (`409 cannot_modify_self`), unknown or repeated roles get their own codes, and because roles
      live in the auth cookie the affected member signs in again to see the change. Every mutation
      writes `user.*` to the trail beside the change itself
- [x] The staff dashboard carries the queue: `needsAttention.openReports` counts open reports, so
      moderation is a row on `/admin` like every other piece of pending work, and the report queue
      joins the quick links
- [x] Logout is rate-limited too (`Logout:RateLimit`, 20/minute): it is an unsafe request that
      invalidates a session, and it was the remaining public write with no policy of its own
- [x] Frontend: `/leaderboard` in the primary nav with its period tabs owned by the URL; a Report
      button on all five listing detail pages, rendered only for a signed-in member, with the 409
      and 404 answers written out as sentences rather than shown as generic errors; `/admin/reports`
      behind `RequireStaff`; `/admin/users` and `/admin/audit-log` behind a new `RequireAdmin`.
      25 frontend tests, including the report form's lifecycle and its two special-case answers

### Phase 7e — Monetisation ✅

Ownership, plans and bookings: the three ways a listing or an event earns something, built around
the queues staff already work through. No money moves in this phase — a request and an approval
replace checkout — and the payment seam exists so 7f can wire a provider without rewriting any
of these decisions.

- [x] The `businesses.owner_user_id` column (`AddBusinessOwnership`): one owner per listing as a
      service-level rule, because MySQL has no partial unique index, and approval is the only thing
      that moves ownership — a filed claim grants no access of its own
- [x] Claims: `POST /api/businesses/{slug}/claim` (signed in, `Claim:RateLimit` 10/hour per IP),
      optional 500-character evidence; a draft and an unknown slug both answer 404 so the endpoint
      cannot be used to discover unpublished listings, and the conflicts `already_owner` /
      `already_owned` / `already_claimed` each get their own code. The staff queue
      (`GET /api/admin/business-claims`, approve/reject, `StaffOnly`) settles rivals, grants the
      `business` role and writes `business.claim_*` to the audit trail in the same save; the review
      note lives on the claim row, not in the audit `after`
- [x] The owner's screen (`GET`/`PUT /api/account/businesses/{slug}`, signed in, scoped to the
      caller's own listings so someone else's answers the same 404): description, contact details
      and the whole opening week only — name, slug, category, district, publish and verify are
      staff's, shown read-only. Owner saves are deliberately not audited: the trail is for actions
      on other people's rows
- [x] Plans (`PlanSeeder`: Free 30 days / NPR 0, Featured 30 days / NPR 3,000): the public
      `GET /api/plans` catalogue; per-listing state derived on read against `INepalDateProvider`, so
      an expired window reports itself without a job; one `Active` row per business and the
      extension rule `max(current_end, today) + period`. Free is granted on the spot; Featured lands
      in `GET/POST /api/admin/subscription-requests` (**AdminOnly**, until a provider's webhook
      resolves into the same queue) and approval opens or rolls the window with a single
      `subscription.*` audit entry naming both rows. Direct grant/cancel at
      `/api/admin/subscriptions` (AdminOnly)
- [x] The featured strip: `GET /api/businesses/featured` — published ∧ verified ∧ a live featured
      window, up to six, ordered `Name` then `Id` so nobody bought a ranking, on the reserved slug
      `featured`
- [x] Bookings: `POST`/`DELETE /api/events/{slug}/booking` (signed in, `Booking:RateLimit`
      20/hour per IP), quantity 1–4, an atomic conditional-UPDATE latch on `registration_count` that
      releases on failure, and book/cancel only while the Nepal start time is ahead. Cancelling
      stamps the row rather than deleting it and rebooking reactivates the same row, so the
      attendee can never hold two places. **Bookings write no audit entries** — the trail stays
      admin/staff actions by design. Event detail carries `capacity`, `registrationCount` and
      `remainingSpots` (never negative), and capacity cannot be lowered below places already held
- [x] Staff attendance (`GET /api/admin/events/{id}/bookings`, `StaffOnly`): the live counter
      plus rows, cancelled included, because "someone cancelled" is half the questions staff ask
- [x] The dashboard carries both queues: `needsAttention.pendingClaims` and
      `subscriptionRequests` sit under "Waiting for a decision" beside open reports, and both
      queues join the quick links
- [x] Three migrations (`AddBusinessOwnership`, `AddBusinessSubscriptions`, `AddEventBookings`)
      applied to the dev database; payments remain behind `IPaymentGateway` + `PaymentSettings`
      with no provider wired (7f)
- [x] Frontend: `features/{ownership,subscriptions,bookings}`; `/my-business` (list, editor with the
      whole week, plan panel) behind `RequireAuth`; a claim panel on the listing page whose four
      answers are written out as sentences; the labelled featured strip above the directory that
      renders nothing when nobody holds a slot or the fetch fails; a booking panel with the 1–4
      stepper, remaining spots and the release path; `/admin/claims` (RequireStaff) and
      `/admin/subscription-requests` (RequireAdmin) with URL-owned windows; the capacity field in
      the staff event editor. Gates: `dotnet build` 0/0, **614/614** backend tests, frontend
      typecheck and build clean, **50/50** frontend tests

### Phase 7f — Payments (eSewa)

- [x] `payment_transactions` (`AddPayments`) and `events.ticket_price` applied to the dev database;
      whole-rupee amounts, the same shape as `plans.price_npr`. No card data in this database —
      the browser posts a signed form and only a signed payload plus a status check come back
- [x] eSewa ePay v2 behind 7e's seam: `EsewaPaymentGateway` signs the outgoing form and verifies
      the return payload with `EsewaSignature` (HMAC-SHA256 over the ordered `signed_field_names`),
      `RejectingPaymentGateway` answers "no provider", and `Payments:Provider = "none"` keeps the
      whole thing off by default. ePay v2 has no webhook — the signed redirect settles nothing, so
      every answer is a server-to-server status check against a provider that never saw our session
- [x] `PaymentService`: one open checkout per listing or seat (a second click resumes it), the seat
      is latched before the provider is asked, and settlement runs behind a conditional `UPDATE` so
      two answers about the same reference grant once and audit once. A reply about a different
      total is recorded as `amount_mismatch` and believed about nothing; an unreachable provider
      leaves the row exactly as it was
- [x] Paid plans settle *into* 7e's queue rather than beside it: `SettlePaidRequestAsync` approves
      the request the checkout opened, carries the reference in `review_note`, and writes
      `payment.succeeded` instead of a second approval audit. Paid tickets take
      `events.ticket_price × places`, confirm the seat on success, release it on failure or refund,
      and mark the payment `refund_due` when the buyer cancels
- [x] eSewa has no refund API, so a portal refund is *discovered*: the admin status check is also
      how a settlement nobody returned from is found, and how `FULL_REFUND` flips a row to
      `refunded` (subscriptions are not auto-revoked — rolling windows make that the
      administrator's call, written to the audit trail as `payment.refunded`)
- [x] `Payment:RateLimit` (20/hour per IP) on checkout and verify; `GET /payments/config` is
      anonymous and says only `available` + `provider` — never a merchant code or a gateway url
- [x] Free event bookings stay free: a zero price books exactly as before, and `payment_not_required`
      is the refusal for a checkout against one. `BookStatus.PaymentRequired` is the 409 a priced
      event gives to a plain booking
- [x] Frontend: `features/payments` (config, checkout, verify, history, admin list) posting the
      signed form from the browser; `/payments/{return,failed}/:reference` asks the provider and
      repeats nothing the url claims; `/account/payments`; `/admin/payments` with URL-owned status
      windows and a per-row check; the plan panel's `Pay NPR …` button and the booking panel's
      priced path; the ticket price field in the staff event editor. Gates: `dotnet build` 0/0,
      **631/631** backend tests, frontend typecheck and build clean, **61/61** frontend tests

## Phase 8 — Mobile

- [x] Flutter app against the same public API: an Android-first `mobile/` app with module list and
      detail for every module, global search, sign in and registration, event booking, paid tickets
      through a web view checkout that posts the provider form and verifies by reference, listing
      reports, and an account page carrying badges, XP and payment history. No second backend, no
      `/admin/**` screens, and no JWT — the app speaks the cookie plus `X-CSRF-TOKEN` pair the API
      already uses
- [x] Offline caching of reference data: provinces, districts, plans and the calculator catalogue
      are written on a successful load and served back when the request fails, opened once from
      `main()` by `ReferenceCache.open()`
- [ ] Push notifications, deferred for V1: the API runs `Push:Provider=none` with nothing sending,
      so registering a device token would have no consumer
- [x] Gates: `flutter analyze` clean, **89/89** Flutter tests, `flutter build apk --debug` and
      `--release` green, backend `dotnet build` 0/0 and **641/641** tests, frontend typecheck and
      **61/61** tests

## Standing rules

- No feature is built before its data model and API contract are agreed.
- No mocked persistence: features use real MySQL through EF Core.
- New packages are justified in one sentence before being added.
- `npm run typecheck`, `npm run build`, `npm test`, `dotnet build` and `dotnet test` must pass
  before any phase is reported done.
