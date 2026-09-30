# Database design (proposed — not yet created)

**Status: design only. MySQL 8 is not installed and no tables exist.** The application starts and
runs with an empty `AppDbContext`. Tables are introduced one module at a time, each with its own EF
Core migration, in the order below.

## Conventions

| Decision | Value | Reason |
| --- | --- | --- |
| Engine | MySQL 8.0 / 8.4 (InnoDB) | Target of the platform |
| Charset / collation | `utf8mb4` / `utf8mb4_0900_ai_ci` | Full Unicode, incl. Nepali text |
| Table names | `snake_case`, plural | Readable in raw SQL; Pomelo maps to PascalCase C# |
| Column names | `snake_case` | Same |
| Primary key | `char(36)` holding a GUID | Predictable for a Flutter client; no sequential-ID enumeration |
| Timestamps | `created_at`, `updated_at` (UTC) | Every table |
| Deletes | `is_deleted` + `is_active` soft flags for user content | Moderation, abuse reports, audit trails |
| Money | `decimal(12,2)` | No floating point for currency |
| Enums | Small `int` code columns with a lookup table or validated C# enum | Never a MySQL `ENUM` — changing it is a table rewrite |
| Indexes | Explicit on every foreign key and every filter/sort column | Avoids silent full scans |
| Naming | FK: `<table>_fk_<column>`; index: `ix_<table>_<columns>`; check: `ck_<table>_<rule>` | Consistent, greppable |
| Deletes | `ON DELETE RESTRICT` for content, `CASCADE` only for owned child rows | Prevents accidental data loss |

Every entity in C# carries `CreatedAt`/`UpdatedAt`, and user-facing records carry `IsDeleted`.

## Module 1 — Identity and gamification (first migration)

`users` — the base of everything else.

| Column | Type | Notes |
| --- | --- | --- |
| `id` | `char(36)` PK | GUID |
| `email` | `varchar(254)` | unique, stored lower-cased |
| `password_hash` | `varchar(255)` | ASP.NET Core Identity format |
| `full_name` | `varchar(120)` | |
| `avatar_url` | `varchar(500)` nullable | |
| `phone` | `varchar(20)` nullable | unique when present, E.164 |
| `role` | `int` | `user` / `business` / `admin` / `moderator` |
| `is_email_verified` | `tinyint(1)` | |
| `is_active` | `tinyint(1)` | |
| `xp` | `int` default 0 | |
| `level` | `int` default 1 | derived from XP, cached for fast reads |
| `streak_count` | `int` default 0 | |
| `last_active_on` | `date` nullable | one row per day, enables streak logic |
| `created_at`, `updated_at` | `datetime(6)` | |

Plus the standard ASP.NET Core Identity tables (`AspNetRoles`, `AspNetUserRoles`,
`AspNetUserClaims`, `AspNetUserLogins`, `AspNetUserTokens`) created automatically by Identity.

Token tables are deliberately **not** hand-rolled.

## Module 2 — Nepal reference data

`provinces` (7 rows), `districts` (77 rows), `municipalities` (local levels). Every address form
in the platform — jobs, businesses, events, deals — selects from these instead of free text.

| Table | Key columns |
| --- | --- |
| `provinces` | `id`, `code`, `name`, `sort_order` |
| `districts` | `id`, `province_id` → `provinces`, `code`, `name` |
| `municipalities` | `id`, `district_id` → `districts`, `name`, `type` (rural/urban) |

`businesses` and `jobs` also store `latitude`/`longitude` (`decimal(10,7)`) for map and
distance-sorted search, with a composite index on `(latitude, longitude)`.

## Module 3 — Businesses and monetisation

- `business_categories` — tree via `parent_id`, seeded tree (shops, services, restaurants, …)
- `businesses` — `id`, `owner_user_id` → `users`, `category_id`, `name`, `slug` (unique),
  `description`, `phone`, `email`, `website`, address columns + `district_id`/`municipality_id`,
  `latitude`/`longitude`, `logo_url`, `cover_url`, `status` (draft/pending/active/suspended),
  `is_verified`, `rating_average`, `rating_count`, `view_count`
- `business_images` — `business_id`, `url`, `sort_order`
- `business_hours` — `business_id`, `day_of_week`, `opens_at`, `closes_at`, `is_closed`
- `business_subscriptions` — `business_id`, `plan` (free/featured/premium), `status`,
  `price`, currency, `starts_at`, `ends_at`, `cancelled_at`, provider reference

Payments are handled by a gateway adapter behind an interface; only the provider's opaque
transaction id and the resulting entitlement are stored. No card data ever touches this database.

## Module 4 — Content modules

| Table | Key columns |
| --- | --- |
| `jobs` | `id`, `business_id` nullable, `title`, `slug`, `description`, `employment_type`, `work_mode` (onsite/remote/hybrid), `salary_min`, `salary_max`, `currency`, `category_id`, `district_id`, `status`, `expires_at`, `is_featured` |
| `job_applications` | `job_id`, `user_id`, `cv_url`, `cover_letter`, `status`, `applied_at` (unique per job+user) |
| `events` | `id`, `title`, `slug`, `description`, `organiser`, `venue`, `district_id`, `starts_at`, `ends_at`, `cover_url`, `status` |
| `deals` | `id`, `business_id`, `title`, `description`, `original_price`, `discounted_price`, `coupon_code`, `starts_at`, `ends_at`, `terms`, `is_active` |

## Module 5 — Quiz

- `quiz_questions` — `text`, `explanation`, `category`, `difficulty`, `is_active`
- `quiz_options` — `question_id`, `label`, `is_correct` (exactly one correct option per question,
  enforced in the service layer, never exposed in a response DTO)
- `quiz_attempts` — `user_id`, `question_id`, `selected_option_id`, `is_correct`, `answered_at`
- `daily_quiz_results` — `user_id`, `quiz_date` (unique per user+date), `score`, `max_score`,
  `xp_awarded`, `completed_at`

The unique constraint on `(user_id, quiz_date)` is what makes the daily quiz genuinely daily and
keeps XP from being farmed by reloading.

## Module 6 — Progression

- `badges` — `code`, `name`, `description`, `icon`, `criteria` (JSON), `xp_reward`
- `user_badges` — `user_id`, `badge_id`, `awarded_at` (unique per user+badge)
- `leaderboard_entries` — cached ranking rows per period, refreshed by a background service rather
  than recomputed on every request

## Module 7 — User data and operations

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
3. Seed reference data (provinces, districts, badge definitions) through
   `IEntitySeed`/`HasData` so a fresh environment is reproducible.
4. Never edit an applied migration; add a new one.
5. Before going live: backup strategy, `innodb_file_per_table`, and a tested restore.
