# Nepal Hub

**One useful website for everyday Nepal.**

Nepal Hub will eventually bring Nepal-specific tools and calculators, a daily quiz, a student
hub, jobs, local businesses, deals and events into a single platform — with user accounts, XP and
badges, leaderboards, saved items, business subscriptions and an admin system.

This repository currently contains **Phase 1: the foundation only**. No product features are built
yet, by design.

---

## Status

| Area | State |
| --- | --- |
| React + TypeScript + Vite frontend | Scaffolding, builds clean, responsive shell only |
| ASP.NET Core 8 Web API | Scaffolding, one `GET /api/health` endpoint |
| MySQL 8 + EF Core | Configured (Pomelo provider), **not installed yet**, zero tables |
| Auth, calculators, quiz, jobs, businesses, payments, admin | Not started |

---

## Prerequisites

| Tool | Required version | Checked on this machine |
| --- | --- | --- |
| Node.js | `^20.19.0` or `>=22.12.0` | v22.16.0 |
| npm | 10.x | 10.9.2 |
| .NET SDK | 8.0 (LTS) | 8.0.425 |
| MySQL | 8.0 / 8.4 | **not installed** |

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
│   └── NepalHub.Api/          # ASP.NET Core 8 Web API (controllers)
│       ├── Program.cs         # DI, CORS, Swagger, health checks
│       ├── Common/            # shared API response envelope
│       ├── Data/              # AppDbContext, design-time factory
│       └── Features/          # one folder per feature (Health/ lives here)
├── frontend/
│   └── src/
│       ├── components/
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
dotnet user-secrets set "ConnectionStrings:Default" "Server=localhost;Port=3306;Database=nepalhub;User=<app-user>;Password=<strong-password>;"
```

Equivalent environment variable (useful in containers/CI):

```
ConnectionStrings__Default=...
Database__ServerVersion=8.4.0-mysql
Cors__AllowedOrigins__0=http://localhost:5173
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

No migrations exist yet — the first one arrives with the first real feature.

---

## Setting up MySQL 8

MySQL is not installed on this machine. Recommended install (8.4 LTS) and setup:

```bash
winget install Oracle.MySQL

# Then create the database and a least-privilege application user
mysql -u root -p
#   CREATE DATABASE nepalhub CHARACTER SET utf8mb4 COLLATE utf8mb4_0900_ai_ci;
#   CREATE USER 'nepalhub_app'@'localhost' IDENTIFIED BY '<strong-password>';
#   GRANT ALL PRIVILEGES ON nepalhub.* TO 'nepalhub_app'@'localhost';
#   FLUSH PRIVILEGES;
```

> XAMPP (`C:\xampp`) ships **MariaDB 10.4**, not MySQL 8. It can talk to the same EF Core provider,
> but it is a different product with a few behavioural differences. Nepal Hub targets MySQL 8.

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

See `docs/architecture.md` for the communication contract and `docs/database.md` for the proposed
schema.
