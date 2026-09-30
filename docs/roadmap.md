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

## Phase 4 — Student hub

- [ ] Notes, past papers, calculators scoped to the student audience
- [ ] Search and filters, saved items

## Phase 5 — Jobs

- [ ] Job posting lifecycle (draft → pending → live → expired), applications
- [ ] Job search with filters, district and remote/onsite, salary
- [ ] Business-owned job management

## Phase 6 — Businesses, deals and events

- [ ] Business registration and verification flow
- [ ] Business profiles, hours, photos, reviews and ratings
- [ ] Subscription plans and payment provider adapter
- [ ] Deals and events modules

## Phase 7 — Global search, gamification and admin

- [ ] Cross-module search index
- [ ] Badges and a leaderboard, both read models over the XP the quiz already records
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
