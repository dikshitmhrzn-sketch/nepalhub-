# Nepal Hub

**One useful website for everyday Nepal.**

Nepal Hub will eventually bring Nepal-specific tools and calculators, a daily quiz, a student
hub, jobs, local businesses, deals and events into a single platform — with user accounts, XP and
badges, leaderboards, saved items, business subscriptions and an admin system.

This repository currently contains **Phase 2: the data layer and real accounts**. The database,
authentication and the account pages are live; the product modules (calculators, quiz, jobs,
businesses, payments) are not started, by design.

---

## Status

| Area | State |
| --- | --- |
| React + TypeScript + Vite frontend | Builds clean; home, sign-in, registration and account pages |
| ASP.NET Core 8 Web API | Identity cookie auth, CSRF, rate limiting, 14 endpoints |
| MySQL 8.4 + EF Core | Installed and running on `127.0.0.1:3307`; 9 tables, migration applied |
| Reference data | 7 provinces / 77 districts seeded |
| Tests | 22 integration tests against the real `nepalhub_test` database |
| Calculators, quiz, jobs, businesses, payments, admin screens | Not started |

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
│       │   ├── auth/          # RequireAuth route guard
│       │   ├── layout/        # AppShell (header, nav, footer)
│       │   └── ui/            # Container, Button, StateMessage
│       ├── config/            # env.ts (VITE_* only)
│       ├── features/          # one folder per feature (api.ts + hooks)
│       ├── lib/               # apiClient.ts
│       ├── pages/             # route-level components
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
