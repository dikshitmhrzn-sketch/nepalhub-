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
├── Program.cs          composition root: DI, CORS, Swagger, exception handling, health checks
├── Common/             cross-cutting primitives (ApiResponse envelope)
├── Data/               AppDbContext, design-time factory, entity configurations
└── Features/<Module>/  vertical slices, one folder per product module
    └── Health/
        ├── HealthController.cs
        └── HealthResponse.cs
```

**Vertical slices, not layer folders.** When a module is added it gets its own `Features/<Module>/`
folder holding its controller, DTOs, services and data access. A request can be traced without
jumping between five directories. `Features/Health` is the reference example.

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
├── components/     presentational, reusable: layout/ (AppShell) and ui/ (Container, Button, StateMessage)
├── config/env.ts   reads VITE_* configuration
├── features/       per-module logic: api.ts (requests) + hooks (state) — no JSX
├── lib/            cross-cutting infrastructure: apiClient.ts
├── pages/          route-level components that compose features and components
├── styles/         tokens.css (all design values) + global.css (reset and base elements)
├── App.tsx         RouterProvider
├── main.tsx        entry point
└── router.tsx      route table
```

Business logic stays in `features/*` and `lib/*`. Pages and components render what they are given.
Fetching, caching, retries and error normalisation belong to the hook layer, not to a component's
`useEffect`.

CSS: plain CSS with design tokens (`styles/tokens.css`) plus CSS Modules for component scope. No
utility framework, no runtime CSS-in-JS. Dark mode and any future rebrand are a single-file change
because every colour is a variable. Component styles never hard-code a colour, space or duration.

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
`status`, `code`, `message` and `details`. Hooks translate that into UI state, and
`StateMessage` renders loading / empty / error / info states. Components never call `fetch` and
never inspect HTTP status codes.

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

- JSON only, camelCase property names, UTC timestamps in ISO-8601.
- Versioned under `/api/...` from day one; breaking changes get a new major version prefix.
- Resource nouns in paths, HTTP verbs for actions: `GET/POST /api/jobs`, `GET /api/jobs/{id}`.
- List endpoints are paged (`page`, `pageSize`, capped) and return a total count.
- Errors use stable machine-readable `code` values; `message` is for humans and may change.
- Validation failures are `400` with per-field details; auth `401`/`403`; missing rows `404`.
- Swagger/OpenAPI is generated and available at `/swagger` in development.
- Pagination, sorting and filtering follow one convention across all list endpoints so the global
  search feature can reuse them.

## 6. Authentication and security (design only, not implemented yet)

- **Passwords:** ASP.NET Core Identity — PBKDF2 hashing, lockout, security-stamp invalidation. No
  hand-rolled crypto.
- **Tokens:** short-lived JWT access token + rotating refresh token, issued as `Secure`,
  `HttpOnly`, `SameSite=Lax` cookies. This works identically for the SPA and a native Flutter app.
- **Authorisation:** role-based policies (`user`, `business`, `admin`, `moderator`) enforced in
  controllers via `[Authorize]`, with resource-level ownership checks in services. Business accounts
  can only edit their own listings.
- **Input safety:** EF Core parameterises all SQL; request DTOs are validated with DataAnnotations
  or a validator of choice; file uploads are validated by content type and size and stored outside
  the web root.
- **Rate limiting and abuse protection** will be added before any public write endpoint (quiz
  answers, sign-up, business claims).
- **Admin actions** that change other users' data are written to an audit log.

## 7. Deployment target

One host, one origin, no reverse-proxy complexity to debug at first:

1. `npm run build` in `frontend/` → static `dist/`
2. `dotnet publish` the API
3. The API serves `dist/` as static files with SPA fallback to `index.html`
4. Secrets and the connection string come from environment variables only
