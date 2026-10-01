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
    ├── Reference/       provinces and districts, the only place location lists come from
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

**Reference data is fetched, not hard-coded.** The province and district lists come from
`Features/Reference` and are the single source for the board's location filters and the editor's
district select. A hard-coded list of Nepal's districts is a thing that silently goes stale, and the
seeder that validates every seeded district code against the same table is what caught the two codes
in the first seed draft that did not exist.

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
│                   (calculators/, quiz/, students/, jobs/)
├── config/env.ts   reads VITE_* configuration
├── features/       per-module logic: api.ts (requests) + hooks (state) — no JSX
├── lib/            cross-cutting infrastructure: apiClient.ts, apiErrors.ts
├── pages/          route-level components that compose features and components
├── styles/         tokens.css (all design values) + global.css (reset and base elements)
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

- JSON only, camelCase property names, UTC timestamps in ISO-8601.
- Versioned under `/api/...` from day one; breaking changes get a new major version prefix.
- Resource nouns in paths, HTTP verbs for actions: `GET /api/students/resources`,
  `GET/POST /api/admin/jobs`, `GET /api/jobs/{slug}`.
- List endpoints are paged (`page`, `pageSize`, capped) and return a total count.
- Errors use stable machine-readable `code` values; `message` is for humans and may change.
- Validation failures are `400` with per-field details; auth `401`/`403`; missing rows `404`.
- Swagger/OpenAPI is generated and available at `/swagger` in development.
- Pagination, sorting and filtering follow one convention across all list endpoints so the global
  search feature can reuse them.

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
  ownership checks in services. Business accounts can only edit their own listings.
- **Default deny:** a fallback authorisation policy means every endpoint requires a signed-in user
  unless it opts out with `[AllowAnonymous]`, so a new controller is protected the moment it is added.
- **Input safety:** EF Core parameterises all SQL; request DTOs are validated with DataAnnotations;
  file uploads are validated by content type and size and stored outside the web root.
- **Rate limiting:** a fixed-window limiter per policy, returning `429 rate_limited`. Auth endpoints
  get 20 requests/minute/IP (`Auth:RateLimit:PermitLimit`) and the public calculation endpoint gets 60
  requests/minute/IP (`Calculate:RateLimit:PermitLimit`), so one visitor cannot burn the server on
  arithmetic while still letting a person work through a calculator normally. Read-only endpoints are
  not limited. Public write endpoints (quiz answers, business claims) reuse the same mechanism.
- **Errors:** Identity failures are translated into stable field codes (`email_taken`,
  `password_too_short`, `password_too_weak`) so the UI can put the message next to the right input.
  A wrong password and an unknown account return the same `401 invalid_credentials`.
- **Email:** accounts are created already confirmed because no mail provider is connected yet.
  Verification and password-reset emails arrive with the notifications work.
- **Admin actions** that change other users' data are written to an audit log.

## 7. Deployment target

One host, one origin, no reverse-proxy complexity to debug at first:

1. `npm run build` in `frontend/` → static `dist/`
2. `dotnet publish` the API
3. The API serves `dist/` as static files with SPA fallback to `index.html`
4. Secrets and the connection string come from environment variables only
