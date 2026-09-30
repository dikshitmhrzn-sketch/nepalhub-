# Roadmap

Each phase is built, reviewed and approved before the next one starts. A phase is not "done" until
it is verified end to end against a real MySQL database — no mocked persistence.

## Phase 0 — Foundation ✅ current

- [x] Environment audit (Node 22.16.0, .NET SDK 8.0.425, Git)
- [x] Repository, `frontend/`, `backend/`, `docs/`
- [x] React 19 + TypeScript 5.9 + Vite 7 + React Router 7, tokens-based CSS, 320px-first
- [x] ASP.NET Core 8 Web API with CORS, Swagger, health endpoint
- [x] `AppDbContext` + Pomelo MySQL provider + pinned `dotnet-ef` local tool
- [x] Response envelope and typed `apiClient`
- [x] Secrets via user-secrets, zero tables, zero features

## Phase 1 — Database and identity

- [ ] Install MySQL 8.4 LTS (`winget install Oracle.MySQL`), create `nepalhub` database and a
      least-privilege application user
- [ ] First migration: `users` + ASP.NET Core Identity tables + `provinces`/`districts`
- [ ] `POST /api/auth/register`, `POST /api/auth/login`, `POST /api/auth/refresh`, `POST /api/auth/logout`
- [ ] Secure `HttpOnly` cookie JWT, lockout, email verification
- [ ] Sign-up / sign-in UI with validation, loading, error and success states
- [ ] Route protection on the frontend and authorisation policies on the API

## Phase 2 — First real module (tools and calculators)

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
- [ ] Admin and moderator roles, reports queue, audit log
- [ ] Rate limiting and abuse protection across public writes

## Phase 8 — Mobile

- [ ] Flutter app against the same public API
- [ ] Push notifications, offline caching of reference data

## Standing rules

- No feature is built before its data model and API contract are agreed.
- No mocked persistence: features use real MySQL through EF Core.
- New packages are justified in one sentence before being added.
- `npm run typecheck`, `npm run build` and `dotnet build` must pass before any phase is reported done.
