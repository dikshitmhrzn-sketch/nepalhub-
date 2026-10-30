# Deployment

One host, one origin: the API serves the built SPA itself, MySQL sits beside it, and a reverse
proxy in front terminates TLS. This is the runbook for running it that way.

## What you need

- .NET 8 SDK to build, ASP.NET Core 8 runtime to run
- Node 20+ for the frontend build
- MySQL 8 with a database and a user holding `ALL` on exactly that database (the dev grants on
  `nepalhub` and `nepalhub_test` are the shape to copy - a dump user needs no more)
- `mysqldump` and `mysql` clients for the backup scripts
- a reverse proxy that sets `X-Forwarded-For` and `X-Forwarded-Proto` (nginx shown below)

## Configuration

Production reads configuration from the environment; `appsettings.Production.json` holds only the
non-secret defaults, and no secret is ever committed.

| Variable | What it does |
| --- | --- |
| `ASPNETCORE_ENVIRONMENT` | `Production` |
| `ConnectionStrings__Default` | the MySQL connection string - secret, environment only |
| `Seeding__DemoContent` | `false` by default in Production: the demo businesses, jobs, offers and events never seed. Reference data, calculators, plans, quiz questions and student resources still do, because the site cannot run without them |
| `Database__AutoMigrate` | `true` by default: pending migrations are applied on startup, before seeding. Set `false` to apply them yourself (see below) |
| `Cors__AllowedOrigins` | empty means same-origin only, which is the normal case behind one host |
| `ForwardedHeaders__KnownProxies__0` | an extra proxy IP to believe beyond the loopback default - only needed when there is more than one hop in front |
| `ForwardedHeaders__TrustAnyProxy` | `true` believes whatever address is in front, for a platform whose edge rewrites `X-Forwarded-For` wholesale but arrives from router addresses too dynamic to name; only safe where that platform is the sole route in |
| `DataProtection__KeyPath` | a folder for the data-protection key ring that survives redeploys - preferred whenever the host has a real disk |
| `DataProtection__Provider` | `database` keeps the same ring in the `data_protection_keys` table instead, for hosts whose filesystem is wiped on every start; a set `KeyPath` still wins |
| `ASPNETCORE_HTTPS_PORTS` | `443` when the app itself should redirect plain http to https |
| `Payments__Provider`, `Payments__ProductCode`, `Payments__SecretKey`, `Payments__ReturnBaseUrl`, `Payments__Environment`, `Payments__ReconcileIntervalSeconds` | eSewa; the payments section below covers when and how |
| `Email__Provider`, `Email__RequireVerified`, `Email__From`, `Email__WebBaseUrl`, `Email__Smtp__*` | outbound mail; the email section below covers when and how |
| `BootstrapAdmin__Email` | one-time - remove after the first sign-in: the address of the first administrator, granted the `admin` role on the next boot. The first-administrator section below is the procedure |
| `BootstrapAdmin__Password` | one-time, optional - remove together with the address: only alongside `BootstrapAdmin__Email`, and only when no account for the address exists yet, to create it directly instead of registering first |

## The first administrator

Every management screen - publishing a job, approving a listing, granting a role - sits behind an
account that already holds the `admin` role, and a fresh database has none: the development
seeder refuses to run outside Development, and the role-grant endpoint itself needs an
administrator to call it. `BootstrapAdmin__Email` is the one deliberate way out:

1. Register the account you will administer with, on the site itself, and sign in once so you
   know the password works.
2. Set `BootstrapAdmin__Email` to that address (no password variable needed) and deploy.
3. The next boot grants the role. Sign out and sign back in - role claims are baked into the
   cookie at sign-in - and the admin screens open.
4. Remove `BootstrapAdmin__Email` and deploy again. While it stays set, every boot logs a
   warning: a grant that silently re-applies forever is a standing administrator nobody
   remembers creating.

If no account exists for the address yet, the seeder only logs what to do next rather than
guessing a password. To create the account directly instead of registering first, also set
`BootstrapAdmin__Password` (one-time; remove both together). When both are set and the account
already exists, the password must sign in to it first: registration needs no mail confirmation,
so the password is what proves the account is yours rather than someone who claimed the address
first.

## Build and publish

```bash
cd frontend
npm ci && npm run build                       # writes dist/
cp -r dist ../backend/NepalHub.Api/wwwroot    # or point Frontend:DistPath somewhere else

cd ../backend
dotnet publish NepalHub.Api.csproj -c Release -o publish
```

The API serves `Frontend:DistPath` (default `wwwroot` under the content root) as static files
with SPA fallback to `index.html`, and an unknown `/api/...` path still answers the JSON envelope
rather than the page. A publish without a dist still runs - it just answers JSON only, which is
what a pure-API host wants anyway.

## Running it

A systemd unit is enough:

```ini
[Unit]
Description=Nepal Hub API
After=network.target mysqld.service

[Service]
WorkingDirectory=/srv/nepalhub/publish
Environment=ASPNETCORE_ENVIRONMENT=Production
Environment=ConnectionStrings__Default=server=127.0.0.1;port=3306;user=nepalhub_app;password=...;database=nepalhub
Environment=ASPNETCORE_URLS=http://127.0.0.1:5080
ExecStart=/usr/bin/dotnet /srv/nepalhub/publish/NepalHub.Api.dll
Restart=always
RestartSec=5

[Install]
WantedBy=multi-user.target
```

Secrets can come from an `EnvironmentFile=` (`/etc/nepalhub.env`, mode 600) instead of the unit
file itself - either way they are outside the repository and outside the process arguments.

## Applying migrations

No manual step: on startup the API applies whatever pending EF Core migrations it finds, before
any seeder reads the schema (`Database:AutoMigrate`, default `true`). The deploy order that makes
that safe is:

1. take the nightly backup (or run `backup-db.sh` by hand) - the backup and the migration are the
   two things that touch data, and backup-then-migrate is what makes a bad migration recoverable
2. publish the new build and restart the service
3. watch the logs for the migration and check `GET /health` afterwards

A host that wants the schema change applied deliberately - reviewed, and applied while the old
build is still running - sets `Database__AutoMigrate=false`, runs
`dotnet ef database update` from a checkout with the same `ConnectionStrings__Default`, then
starts the new build. Either way the application never runs against a schema it does not
understand: with auto-migrate off, applying the migrations is the operator's half of the same
contract.

## TLS and the proxy

The API binds plain http on loopback; the proxy holds the certificate:

```nginx
server {
    listen 443 ssl http2;
    server_name nepalhub.example.com;

    location / {
        proxy_pass http://127.0.0.1:5080;
        proxy_set_header Host $host;
        proxy_set_header X-Forwarded-For $proxy_add_x_forwarded_for;
        proxy_set_header X-Forwarded-Proto $scheme;
    }
}
```

What that buys, because the API reads those headers before anything else:

- the rate limiter buckets by the **real client IP** rather than the proxy's address, so one
  abusive visitor cannot hide behind the proxy's single bucket and one honest visitor cannot be
  caught by it
- both auth cookies get `Secure` (their `SameAsRequest` policy sees https through the forwarded
  scheme) and plain-localhost development keeps working untouched
- HSTS answers for the real host and never for `localhost`, which is on its excluded list

The API believes forwarded headers only from loopback by default - a forged `X-Forwarded-For`
arriving from the open internet is dropped. A second hop (a CDN in front of the proxy) is added
with `ForwardedHeaders__KnownProxies__0=<ip>`. For the redirect itself, either set
`ASPNETCORE_HTTPS_PORTS=443` so the API redirects, or redirect in the proxy; both are fine.

## Logs and health

Production logs are **one JSON object per line**: level, UTC timestamp, message and fields, with
the request id in a scope. Development keeps the plain console, because that log is read by eye.

Every request gets one line - `"<id> GET /path answered 200 in 3.1ms"` - at Information, a
429 is logged at Warning (that is the abuse signal) and a 5xx at Error. The same id comes back
as the `X-Request-Id` response header; a proxy that sends `X-Request-Id` is echoed when the value
is safe to reflect, and replaced when it is not, so a bug report can quote the id that ties it
to the log line.

Two probes, for two jobs:

| Endpoint | For | Answer |
| --- | --- | --- |
| `GET /health` | load balancer / supervisor | plain text, `200 Healthy` or `503 Unhealthy` when the database is unreachable or not configured |
| `GET /api/health` | the app itself | JSON with `status` and `database` detail |

Point liveness checks at `/health`. If logs go to a file rather than journald, rotate them.

## Email

Mail ships **off**: `appsettings.json` carries `Email:Provider = none`, which logs and drops every
message, and accounts start confirmed — enforcement is the conjunction of a configured provider and
`Email:RequireVerified`, so with no provider the second half never bites and a fresh clone behaves
exactly as it did before this module existed. Turning mail on is the variables below; the section is
validated at boot, so a half-configured relay stops the host with one sentence instead of starting
to quietly lose messages (or, worse, letting `file` write mail to disk in Production, where nobody
would ever read it — that combination is refused).

| Variable | What it does |
| --- | --- |
| `Email__Provider` | `none` (default: log and drop), `file` (development only - writes an `.html`/`.txt` pair per message under `Email__File__Directory`, defaults to the temp folder), `smtp` (MailKit) |
| `Email__RequireVerified` | `true` (the default) with a provider makes registration wait for the link and login answer `403 email_not_confirmed`; `false` still sends the mail but lets an unconfirmed address sign in |
| `Email__From` | the envelope address the mail arrives from - required with `smtp`, a mailbox on your own sending domain, not a secret |
| `Email__FromName` | the name beside the address (default `Nepal Hub`) |
| `Email__WebBaseUrl` | the absolute origin the site is served from (`https://nepalhub.example.com`); every link in every message is built from it and a configured provider refuses to boot without it |
| `Email__ConfirmTokenLifetimeMinutes` | how long a confirmation link works (default `1440`, one day) |
| `Email__ResetTokenLifetimeMinutes` | how long a reset link works (default `60`) - deliberately shorter than confirmation, because the reset link is the one that hands over the account |
| `Email__SendCooldownSeconds` | quiet window per address between two sends (default `60`, `0` off); a repeat inside the window is logged and dropped rather than delivered |
| `Email__Smtp__Host`, `Email__Smtp__Port` | the relay (`smtp.provider.example`, `587` by default) |
| `Email__Smtp__Encryption` | `auto` (default: upgrades when the server offers it), `starttls` (insists), `ssl` (TLS from the first byte, port 465), `none` (a local relay on a trusted machine) |
| `Email__Smtp__UserName`, `Email__Smtp__Password` | the relay account - both or neither; the password is secret, environment only |

The links land on SPA routes (`/verify-email`, `/reset-password`), so `Email__WebBaseUrl` must be
the origin the frontend is served from — behind the proxy above, the `https://` host, not the
loopback bind. With `Email__Provider` left at `none`, `Email__RequireVerified` is inert: nothing is
enforced and accounts start confirmed, exactly as before.

## Payments

Payments ship **off**: `appsettings.Production.json` holds `Payments:Provider = none`, which
makes every checkout answer "no provider" instead of taking money. Turning eSewa on is the
variables below plus the site's URL registered with eSewa's merchant panel.

| Variable | What it does |
| --- | --- |
| `Payments__Provider` | `esewa` to take money; the default `none` refuses every checkout |
| `Payments__ProductCode` | the merchant id eSewa issued - not a secret, it is posted to the buyer's browser |
| `Payments__SecretKey` | the HMAC key that signs requests and verifies callbacks - secret, environment only |
| `Payments__ReturnBaseUrl` | the public origin (`https://nepalhub.example.com`): buyers are sent to `{ReturnBaseUrl}/payments/return/{reference}` and `{ReturnBaseUrl}/payments/failed/{reference}`, so it must be exactly how the site is reached |
| `Payments__Environment` | `sandbox` against eSewa's test hosts (the default), `production` against the real ones |
| `Payments__ReconcileIntervalSeconds` | how often the background sweep checks on abandoned checkouts (default `300`, `0` disables the sweep) |

Provider, product code, secret key and return URL are all required before anything is offered -
a half-configured install refuses checkout rather than half-taking money. Buy the cheapest thing
in sandbox end to end first, then flip `Payments__Environment` and buy once more in production.

### How a payment settles

**No redirect settles anything.** The browser returns to `/payments/return/{reference}` carrying
eSewa's signed `data` and `signature`; the API verifies the signature and only then moves the
row. There is no IPN endpoint - eSewa's asynchronous callback is not wired in - so what actually
settles money is one of these:

1. the verified signed return (above)
2. the buyer's own `GET /api/payments/{reference}`, which reconciles a row quiet for more than
   five minutes before answering
3. the buyer retrying checkout - the open reference resumes instead of a second payment
4. a ticket checkout that meets an event looking full - abandoned holds there are reconciled
   first, because they may be what is filling it
5. the background sweep (below)
6. an administrator's **check with the provider** on `/admin/payments`

Whichever arrives first wins: `Pending` moves to `Succeeded` exactly once, so the entitlement is
granted once even when two answers race.

### The background sweep

Every `ReconcileIntervalSeconds` the API takes the five oldest checkouts that have sat `Pending`
for more than five minutes, asks eSewa about them oldest first, and settles what was paid - a
buyer who closed the tab never has to come back. It is a hosted worker inside the API process
(nothing to schedule), one status call per row per pass, bounded so a backlog cannot become a
burst of calls; a pass that fails is logged and retried at the next interval. With no provider
configured the sweep finds nothing to ask about, and `Payments__ReconcileIntervalSeconds=0`
stops the worker at boot (that is what the test suite runs with). A pass that examined anything
logs one line: `Reconciled N stale pending payment(s) against the provider.`

## Backups

There are no user uploads to back up - the database is the whole site. `scripts/backup-db.sh`
takes a consistent logical dump (single transaction, routines, triggers, events), gzips it,
verifies the archive, and prunes anything older than `KEEP_DAYS` (default 14):

```bash
DB_USER=nepalhub_app MYSQL_DEFAULTS_FILE=/root/.my.cnf \
DB_NAME=nepalhub BACKUP_DIR=/var/backups/nepalhub \
bash scripts/backup-db.sh
```

`.my.cnf` keeps the password out of the crontab and out of `ps`:

```ini
[client]
user=nepalhub_app
password=change-me
```

Nightly, for example:

```cron
15 2 * * * DB_USER=nepalhub_app MYSQL_DEFAULTS_FILE=/root/.my.cnf DB_NAME=nepalhub BACKUP_DIR=/var/backups/nepalhub bash /srv/nepalhub/scripts/backup-db.sh >> /var/log/nepalhub-backup.log 2>&1
```

One host means the backup is only as safe as the machine it sits on: copy the directory off the
host (`rclone`, `rsync` - anything scheduled), and confirm the copy now and then.

### Restoring

`scripts/restore-db.sh` overwrites tables on purpose, so it refuses to run without
`RESTORE_CONFIRM=yes` and the API should be stopped first:

```bash
# stop the service, then:
RESTORE_CONFIRM=yes DB_USER=nepalhub_app MYSQL_DEFAULTS_FILE=/root/.my.cnf \
DB_NAME=nepalhub bash scripts/restore-db.sh /var/backups/nepalhub/nepalhub-20261007T021500Z.sql.gz

# start it again, then check both probes:
curl -fsS http://127.0.0.1:5080/health
curl -fsS http://127.0.0.1:5080/api/health
```

**A backup you have never restored is a hope, not a backup.** Before going live, drill it
against a scratch database the app user owns: create one, restore the latest dump into it,
compare the table count against the real database, drop it again. The suite's own database
(`nepalhub_test`) is a convenient target - it is recreated by the next test run either way.

## Free hosting: Vercel (container) with Aiven MySQL

For a personal, non-commercial deployment the whole app - API and the SPA it serves - fits on
Vercel's free Hobby plan as one container, with the database on Aiven's free MySQL plan. The
container runtime keeps the single-origin shape this runbook describes above: `Dockerfile.vercel`
at the repository root builds the Vite frontend, publishes the API with `dist/` dropped into
`wwwroot`, and runs nothing but Kestrel on port 8080 (`ASPNETCORE_HTTP_PORTS`, which the .NET 8
base image already sets); `vercel.json` declares that image as the one service every path
rewrites to. The Vercel project's `PORT` must be `8080` to match.

Three things differ from a VM deploy, and each is covered:

- the filesystem is wiped on every start, so the data-protection key ring cannot live in a
  folder: `DataProtection__Provider=database` keeps it in the `data_protection_keys` table the
  `AddDataProtectionKeys` migration creates, through the same `AppDbContext`. Without this every
  cold start would log every visitor out and invalidate any confirmation or reset link in flight.
- the database sits on Aiven, reached over the public internet with TLS (`SslMode=Required` in
  the connection string). It is the only state the site has; there are no uploads.
- migrations and seeding still run at startup, in the API process, exactly as above.

### Aiven

Create a free MySQL service, run `CREATE DATABASE nepalhub;` in its console, and note the host,
port, `avnadmin` password and the server version it reports (the free tier is a single node of
about 1 GB RAM and storage; it powers itself off after a long period of inactivity and emails
about it - one click in the console brings it back).

### Vercel

Import the repository with the repository root as the project root (where `vercel.json` sits)
and set:

| Variable | Value |
| --- | --- |
| `ASPNETCORE_ENVIRONMENT` | `Production` |
| `PORT` | `8080` |
| `ConnectionStrings__Default` | the Aiven connection string — either the `Server=...;Port=...;...` form or Aiven's `mysql://user:pass@host:port/db` URL; both are accepted, `SslMode=Required` is assumed for the URL form |
| `Database__ServerVersion` | Aiven's actual version (for example `8.0.36-mysql`), not the local default |
| `DataProtection__Provider` | `database` |
| `Seeding__DemoContent` | `true` to seed the demo businesses, jobs, offers and events; leave it out for an empty catalogue with only the reference data |
| `ForwardedHeaders__TrustAnyProxy` | `true` - Vercel's edge replaces `X-Forwarded-For` outright, so a visitor cannot inject their own, but it arrives from a pool of router addresses too dynamic for `KnownProxies`; with the flag the app believes the platform, and rate limits and `Secure` cookies work per real visitor |
| `BootstrapAdmin__Email` | one-time - the first-administrator procedure above: the address of the admin account, removed after the first sign-in |
| `BootstrapAdmin__Password` | one-time, optional - only with the address above, only when the account does not exist yet |

Deploy with `vercel deploy --prod` or a push to the connected branch. The first boot applies
every migration to the empty database and seeds the reference data, so `GET /api/health` must
answer `database: connected` and `/` must serve the SPA.

### The proxy, on Vercel

Forwarded headers are believed only from loopback or a named proxy by default, and on Vercel the
direct peer is Vercel's edge - a pool of router addresses, not one host to name. That is what
`ForwardedHeaders__TrustAnyProxy` is for: Vercel overwrites `X-Forwarded-For` wholesale (a client
cannot smuggle its own address through), so believing the address the platform hands over is
believing the visitor. Without the flag every visitor shares the proxy's address as far as the
rate limiter is concerned and both auth cookies lose their `Secure` flag. The flag is for this
hosting shape alone; a VM behind your own nginx keeps the loopback default and never sets it.

### What the free tier does and does not do

- the Hobby plan is personal and non-commercial, with monthly caps (invocations, active CPU,
  memory-hours, transfer); passing one pauses that feature until the window resets
- the container scales to zero between bursts, so the first request after an idle period pays a
  few seconds of cold start - the keys in MySQL are what keep that from logging everyone out
- `PaymentReconciliationWorker` only runs while an instance is warm; with `Payments:Provider`
  still `none` there is nothing for it to reconcile, and turning eSewa on later means moving the
  sweep to Vercel Cron
- the 4.5 MB request-body limit of Vercel Functions is above anything this API accepts today,
  because nothing is uploaded
