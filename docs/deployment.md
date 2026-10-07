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
| `ASPNETCORE_HTTPS_PORTS` | `443` when the app itself should redirect plain http to https |
| `Payments__Provider`, `Payments__ProductCode`, `Payments__SecretKey`, `Payments__ReturnBaseUrl`, `Payments__Environment` | eSewa; the payments section below covers when and how |

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
