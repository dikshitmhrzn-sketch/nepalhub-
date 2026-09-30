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

## Phase 2 — First real module (tools and calculators) — not started

Smallest end-to-end feature that proves calculators can be added one by one.

- [ ] Calculator catalogue table + CRUD behind roles
- [ ] `/api/calculators` (paged, searchable) and `/api/calculators/{slug}`
- [ ] Tools hub page, calculator detail page, mobile-first numeric inputs
- [ ] Server-side calculation endpoint for a calculator that needs it (loan EMI, tax)

## Phase 3 — Daily quiz and XP

- [ ] Quiz tables, seed data, `POST /api/quiz/daily`, leaderboard endpoint
- [ ] XP, level and streak rules as one service, unit-tested
- [ ] Badges and leaderboard page
- [ ] Rate limiting on answers

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

## Phase 7 — Global search and admin

- [ ] Cross-module search index
- [ ] Admin screens, reports queue and audit log (the `admin` and `moderator` roles and the
      `admin-only` / `staff-only` policies already exist)
- [ ] Rate limiting and abuse protection across public writes

## Phase 8 — Mobile

- [ ] Flutter app against the same public API
- [ ] Push notifications, offline caching of reference data

## Standing rules

- No feature is built before its data model and API contract are agreed.
- No mocked persistence: features use real MySQL through EF Core.
- New packages are justified in one sentence before being added.
- `npm run typecheck`, `npm run build`, `dotnet build` and `dotnet test` must pass before any phase is reported done.
