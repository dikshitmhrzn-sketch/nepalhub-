# Database

**Status: MySQL 8.4.9 is installed and the first two modules are live.** The schema below is the
as-built result of migration `20260930092534_InitialCreate`, not a proposal. Everything from
"Planned modules" onwards is still design only.

## Local instance

| Item | Value |
| --- | --- |
| Service | `MySQLNepalHub84` (Windows service) |
| Port | `3307`, bound to `127.0.0.1` |
| Config | `C:\ProgramData\MySQL\NepalHub\my.ini` |
| Schemas | `nepalhub` (development), `nepalhub_test` (integration tests) |
| App account | `nepalhub_app`, granted only on those two schemas |
| Root account | random password, passwordless local access disabled |

MySQL is installed alongside the existing XAMPP MariaDB rather than on top of it, so XAMPP keeps
using port `3306` and is left running or stopped exactly as before. The `root` and `nepalhub_app`
passwords are never written to this repository; they are held in ASP.NET Core user-secrets and in the
operator's own notes.

The connection string lives in user-secrets, not in `appsettings.json`:

```powershell
cd backend/NepalHub.Api
dotnet user-secrets set "ConnectionStrings:Default" "Server=127.0.0.1;Port=3307;Database=nepalhub;User Id=nepalhub_app;Password=<password>"
```

Use `127.0.0.1` rather than `localhost`: on this machine `localhost` resolves to IPv6 first and
Pomelo's connection attempts stall long enough for the health check to time out.

## Conventions

| Decision | Value | Reason |
| --- | --- | --- |
| Engine | MySQL 8.4 (InnoDB) | Target of the platform |
| Charset / collation | `utf8mb4` / `utf8mb4_0900_ai_ci` | Full Unicode, incl. Nepali text |
| Table names | `snake_case`, plural | Readable in raw SQL; Pomelo maps to PascalCase C# |
| Column names | `snake_case` | Same |
| Primary key | `char(36)` holding a GUID | Predictable for a Flutter client; no sequential-ID enumeration |
| Timestamps | `created_at`, `updated_at` (UTC) | On the tables that own mutable state |
| Deletes | `is_active` soft flag for user accounts | Moderation and abuse handling |
| Money | `decimal(12,2)` | No floating point for currency |
| Enums | Small `int` code columns with a lookup table or validated C# enum | Never a MySQL `ENUM` — changing it is a table rewrite |
| Indexes | Explicit on every foreign key and every filter/sort column | Avoids silent full scans |
| Naming | FK: `<table>_fk_<column>`; index: `ix_<table>_<columns>` | Consistent, greppable |
| Deletes | `ON DELETE RESTRICT` for content, `CASCADE` only for owned child rows | Prevents accidental data loss |

Reference data (`provinces`, `districts`) and roles are seeded rows, so they deliberately have no
`created_at`/`updated_at` columns.

## Module 1 — Identity and gamification (live)

`ApplicationUser` extends `IdentityUser<Guid>` and maps to `users`. Roles are **not** an integer
column: they use ASP.NET Core Identity's own tables so that role checks are enforced by the
framework rather than by string comparisons in application code.

| Column | Type | Notes |
| --- | --- | --- |
| `id` | `char(36)` PK | GUID |
| `email` | `varchar(254)` | unique, stored lower-cased |
| `email_normalized` | `varchar(254)` | Identity's lookup column |
| `email_confirmed` | `tinyint(1)` | `1` from registration; no mail provider yet |
| `password_hash` | `varchar(255)` | ASP.NET Core Identity format |
| `security_stamp` | `varchar(128)` | invalidates sessions on password change |
| `concurrency_stamp` | `varchar(128)` | optimistic concurrency |
| `full_name` | `varchar(120)` | |
| `avatar_url` | `varchar(500)` nullable | |
| `phone_number` | `varchar(20)` nullable | unique when present |
| `is_active` | `tinyint(1)` default 1 | disabled accounts cannot sign in |
| `xp` | `int` default 0 | |
| `level` | `int` default 1 | derived from XP, cached for fast reads |
| `streak_count` | `int` default 0 | |
| `last_active_on` | `date` nullable | one row per day, enables streak logic |
| `created_at`, `updated_at` | `datetime(6)` | |

`ApplicationRole` extends `IdentityRole<Guid>` and maps to `roles` (`id`, `name`, `normalized_name`,
`concurrency_stamp`, `description`). Four roles are inserted at application start-up by
`RoleSeeder`, with deterministic GUIDs, so a fresh database is immediately usable:

| Role | Purpose |
| --- | --- |
| `user` | every registered account |
| `business` | reserved for business owners |
| `moderator` | moderation tools |
| `admin` | full access; guarded by the `admin-only` policy |

Identity's own child tables complete the store: `user_roles`, `user_claims`, `user_logins`,
`user_tokens` and `role_claims`. Token storage is deliberately **not** hand-rolled.

## Module 2 — Nepal reference data (live)

`provinces` (7 rows) and `districts` (77 rows) are seeded through EF Core `HasData`, with
deterministic GUIDs derived in `ReferenceDataIds`, so every environment gets identical primary keys
and foreign keys. Municipalities are not part of this phase; every address form added later will
select a district from these tables instead of accepting free text.

| Table | Key columns |
| --- | --- |
| `provinces` | `id` PK, `code` unique, `name` unique, `sort_order` |
| `districts` | `id` PK, `province_id` → `provinces` (RESTRICT), `code` unique, `name`, `sort_order` |

Codes are lowercase slugs prefixed with the province (`koshi-bhojpur`), so a code identifies its own
parent and stays readable in URLs and analytics. Display names follow the current official spellings,
including `Nawalparasi West`, `Rukum East` and `Rukum West`.

| Province | Districts |
| --- | --- |
| Koshi | 14 |
| Madhesh | 8 |
| Bagmati | 13 |
| Gandaki | 11 |
| Lumbini | 12 |
| Karnali | 10 |
| Sudurpashchim | 9 |
| **Total** | **77** |

Rukum East is recorded under Lumbini and Rukum West under Karnali, which is what the district counts
above assume.

## Planned modules

Nothing below exists in the database yet. Each will be added as its own migration when its module is
built.

### Businesses and monetisation

- `business_categories` — tree via `parent_id`, seeded tree (shops, services, restaurants, …)
- `businesses` — `id`, `owner_user_id` → `users`, `category_id`, `name`, `slug` (unique),
  `description`, `phone`, `email`, `website`, address columns + `district_id`/`municipality_id`,
  `latitude`/`longitude`, `logo_url`, `cover_url`, `status` (draft/pending/active/suspended),
  `is_verified`, `rating_average`, `rating_count`, `view_count`
- `business_images` — `business_id`, `url`, `sort_order`
- `business_hours` — `business_id`, `day_of_week`, `opens_at`, `closes_at`, `is_closed`
- `business_subscriptions` — `business_id`, `plan` (free/featured/premium), `status`,
  `price`, currency, `starts_at`, `ends_at`, `cancelled_at`, provider reference

`businesses` and `jobs` store `latitude`/`longitude` (`decimal(10,7)`) for map and distance-sorted
search, with a composite index on `(latitude, longitude)`.

Payments are handled by a gateway adapter behind an interface; only the provider's opaque transaction
id and the resulting entitlement are stored. No card data ever touches this database.

### Content modules

| Table | Key columns |
| --- | --- |
| `jobs` | `id`, `business_id` nullable, `title`, `slug`, `description`, `employment_type`, `work_mode` (onsite/remote/hybrid), `salary_min`, `salary_max`, `currency`, `category_id`, `district_id`, `status`, `expires_at`, `is_featured` |
| `job_applications` | `job_id`, `user_id`, `cv_url`, `cover_letter`, `status`, `applied_at` (unique per job+user) |
| `events` | `id`, `title`, `slug`, `description`, `organiser`, `venue`, `district_id`, `starts_at`, `ends_at`, `cover_url`, `status` |
| `deals` | `id`, `business_id`, `title`, `description`, `original_price`, `discounted_price`, `coupon_code`, `starts_at`, `ends_at`, `terms`, `is_active` |

### Quiz

- `quiz_questions` — `text`, `explanation`, `category`, `difficulty`, `is_active`
- `quiz_options` — `question_id`, `label`, `is_correct` (exactly one correct option per question,
  enforced in the service layer, never exposed in a response DTO)
- `quiz_attempts` — `user_id`, `question_id`, `selected_option_id`, `is_correct`, `answered_at`
- `daily_quiz_results` — `user_id`, `quiz_date` (unique per user+date), `score`, `max_score`,
  `xp_awarded`, `completed_at`

The unique constraint on `(user_id, quiz_date)` is what makes the daily quiz genuinely daily and
keeps XP from being farmed by reloading.

### Progression

- `badges` — `code`, `name`, `description`, `icon`, `criteria` (JSON), `xp_reward`
- `user_badges` — `user_id`, `badge_id`, `awarded_at` (unique per user+badge)
- `leaderboard_entries` — cached ranking rows per period, refreshed by a background service rather
  than recomputed on every request

### User data and operations

- `saved_items` — `user_id`, `item_type` (job/event/deal/business), `item_id`, `created_at`
  (unique per user+type+item)
- `notifications` — `user_id`, `type`, `title`, `body`, `is_read`, `created_at`
- `reports` — reporter, target type/id, reason, status — feeds moderation
- `admin_audit_logs` — `admin_user_id`, `action`, `target_type`, `target_id`, `before`, `after`,
  `ip`, `created_at`

`reports` and `admin_audit_logs` are required before moderation is built, not after, because both
grow irreversibly once they are in use.

## Migration discipline

1. One migration per module, named for the feature (`AddBusinesses`).
2. `dotnet ef migrations add`, review the generated SQL by reading the model, then
   `dotnet ef database update`.
3. Seed reference data (provinces, districts, badge definitions) through `HasData` with
   deterministic ids, so a fresh environment is reproducible.
4. Never edit an applied migration; add a new one.
5. Before going live: backup strategy, `innodb_file_per_table`, and a tested restore.

```powershell
dotnet ef migrations add <Name> --project backend/NepalHub.Api --startup-project backend/NepalHub.Api --output-dir Data/Migrations
dotnet ef database update --project backend/NepalHub.Api --startup-project backend/NepalHub.Api
```

To inspect the schema without guessing:

```sql
SELECT table_name, table_rows FROM information_schema.tables
WHERE table_schema = 'nepalhub' ORDER BY table_name;
```
