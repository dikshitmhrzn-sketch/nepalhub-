# Nepal Hub

**One useful website for everyday Nepal.**

Nepal Hub will eventually bring Nepal-specific tools and calculators, a daily quiz, a student
hub, jobs, local businesses, deals and events into a single platform — with user accounts, XP and
badges, leaderboards, saved items, business subscriptions and an admin system.

This repository currently contains **Phase 3: the data layer, real accounts, the first public
module and the daily quiz**. The database, authentication, the account pages, the
tools/calculators module and the daily quiz are live; the remaining product modules (student hub,
jobs, businesses, payments) are not started, by design.

---

## Status

| Area | State |
| --- | --- |
| React + TypeScript + Vite frontend | Builds clean; home, tools hub, calculator, daily quiz, sign-in, registration, account, and staff calculator/quiz pages |
| ASP.NET Core 8 Web API | Identity cookie auth, CSRF, rate limiting, calculators catalogue, server-side calculation, daily quiz and XP |
| MySQL 8.4 + EF Core | Installed and running on `127.0.0.1:3307`; 16 tables, three migrations applied |
| Reference data | 7 provinces / 77 districts seeded |
| Calculators | 3 seeded (loan EMI, compound interest, percentage); staff CRUD live |
| Daily quiz | 31 seeded questions / 124 options; anonymous play, server-side grading, XP/level/streak; staff CRUD live |
| Tests | 99 integration tests against the real `nepalhub_test` database |
| Student hub, jobs, businesses, payments, badges, leaderboard, admin screens beyond calculators and quiz | Not started |

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
│   └── src/
│       ├── components/
│       │   ├── auth/          # RequireAuth / RequireStaff route guards
│       │   ├── calculators/   # card, dynamic form, result view, staff editor
│       │   ├── layout/        # AppShell (header, nav, footer)
│       │   ├── quiz/          # result view, staff question editor
│       │   └── ui/            # Container, Button, StateMessage, Pagination
│       ├── config/            # env.ts (VITE_* only)
│       ├── features/          # one folder per feature (api.ts + hooks)
│       ├── lib/               # apiClient.ts, apiErrors.ts
│       ├── pages/             # route-level components (tools, calculators, quiz, admin)
│       ├── styles/            # tokens.css (design tokens) + global.css
│       ├── App.tsx
│       ├── main.tsx
│       └── router.tsx
└── docs/
    ├── architecture.md
    ├── database.md
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

## Tests

```bash
cd backend
dotnet test
```

The suite is integration-only and uses a **real** MySQL server — there is no in-memory provider, so
the schema, the collation, the Identity stores and the seed data are all exercised. It creates and
migrates `nepalhub_test` and never touches `nepalhub`. Set `ConnectionStrings__Test` to point it
elsewhere; otherwise it derives the test connection string from the API's user-secrets entry.

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
