# Database

**Status: MySQL 8.4.9 is installed and the first three modules are live.** The schema below is the
as-built result of migrations `20260930092534_InitialCreate`, `20260930105251_AddCalculators`,
`20260930122740_AddQuiz` and `20260930154447_AddStudentResources`, not a proposal. Everything from
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

## Module 3 — Calculators (live)

`calculators` is one row per calculator, added by `20260930105251_AddCalculators`. The whole input
form is stored as JSON, so publishing another calculator is a data change rather than a schema
change. Rows are seeded by an idempotent startup seeder rather than `HasData`, because a seeded
calculator's copy is expected to be edited by staff afterwards and must survive a reseed.

| Column | Type | Notes |
| --- | --- | --- |
| `id` | `char(36)` PK | GUID |
| `slug` | `varchar(80)` unique | `^[a-z0-9]+(-[a-z0-9]+)*$`; the public URL, and immutable once created |
| `name` | `varchar(120)` | |
| `summary` | `varchar(280)` | the catalogue card line |
| `description` | `varchar(4000)` | the explanation on the calculator page |
| `category` | `int` | `CalculatorCategory`; 0 = Finance, 1 = Education, 2 = Health, 3 = Engineering, 4 = Everyday |
| `inputs_json` | `json` | array of `InputFieldDefinition`: `key`, `label`, `kind`, `unit`, `minimum`, `maximum`, `step`, `defaultValue`, `required`, `help` |
| `sort_order` | `int` | catalogue order, lowest first |
| `is_published` | `tinyint(1)` default 0 | only published rows appear on the public endpoints |
| `created_by_id` | `char(36)` nullable → `users` (SET NULL) | the author, kept if the account is removed |
| `created_at`, `updated_at` | `datetime(6)` | |

Indexes: `ix_calculators_slug` (unique), `ix_calculators_category`, `ix_calculators_sort_order`,
`ix_calculators_is_published`, and `ix_calculators_created_by_id` for the staff list. The public
catalogue query is `WHERE is_published = 1 ORDER BY sort_order, name`, which the sort index serves.

`kind` is one of `Amount`, `Percent`, `Years` or `Number`; it drives the unit suffix and the input
step on the rendered field. `category` and `kind` are serialised to the API by name, so a client
never depends on the stored ordinal.

Seeded calculators: `loan-emi` and `compound-interest` (Finance) and `percentage-calculator`
(Everyday). The seeder only inserts, so a calculator an administrator deletes stays deleted.

## Module 4 — Daily quiz (live)

Six tables, added by `20260930122740_AddQuiz`. The bank of questions is authored by staff; the
daily set is materialised from it on first use, which is what makes a single day's quiz identical
for everybody without a scheduled job ever having to run.

### Question bank

| Table | Key columns |
| --- | --- |
| `quiz_questions` | `id` PK, `text` varchar(600), `explanation` varchar(1500), `category` int (`QuizCategory`), `is_published` tinyint(1) default 0, `created_by_user_id` nullable → `users` (SET NULL), `created_at`, `updated_at` |
| `quiz_options` | `id` PK, `question_id` → `quiz_questions` (CASCADE), `position` int, `text` varchar(300), `is_correct` tinyint(1) default 0 |

Indexes: `ix_quiz_questions_is_published_id` (the draw only reads published rows),
`ix_quiz_questions_category` (the category-balanced draw), `ix_quiz_questions_created_by_user_id`,
and `ix_quiz_options_question_id_position`, which is **unique** so options cannot share a slot.

`category` is one of `Nepal`, `GeneralKnowledge`, `Science`, `Technology`, `History`, `Geography`
and is serialised to the API by name, so a client never depends on the stored ordinal.
`is_correct` lives only here and in the staff contracts: no pre-answer response DTO has a field
that would reveal it, which is the whole reason grading happens server-side.

Question text uniqueness across the bank is enforced by the service, not by a unique index, so
that two staff members saving similar wording at the same time get a readable conflict rather than
a duplicate-key error.

Validation is split in two, and the split is deliberate. The *shape* of a question — two to six
options, each with text, no two the same — is always required, because a draft with no options is
not worth storing. The *playability* of a question — exactly one correct answer and an explanation
of at least 10 characters — is required only when publishing, which is the difference between
"not finished yet" and "wrong". Requiring the answer on a draft would leave staff no way to save a
half-written question.

### Daily sets and attempts

| Table | Key columns |
| --- | --- |
| `daily_quizzes` | `id` PK, `quiz_date` date, `question_count` int, `created_at` |
| `daily_quiz_questions` | PK `(daily_quiz_id, question_id)`, `position` int |
| `quiz_attempts` | `id` PK, `user_id` → `users` (CASCADE), `daily_quiz_id` → `daily_quizzes` (CASCADE), `score`, `max_score`, `xp_awarded`, `completed_at` |
| `quiz_attempt_answers` | PK `(attempt_id, question_id)`, `selected_option_id` → `quiz_options` (RESTRICT), `is_correct` tinyint(1) |

Two unique indexes carry the integrity model:

- `ix_daily_quizzes_quiz_date` — one set per date, so a repeated materialisation is a no-op even
  when two visitors race on the first request of the day.
- `ix_quiz_attempts_user_id_daily_quiz_id` — one completed run per person per day, so a second
  submit is a `409` rather than a second XP payout. This is the only defence that actually
  matters; the service's pre-check is a courtesy that saves a user a confusing error.

Supporting indexes: `ix_daily_quiz_questions_daily_quiz_id_position` (reading a day in order),
`ix_daily_quiz_questions_question_id`, `ix_quiz_attempts_user_id_completed_at` (quiz history, newest
first), `ix_quiz_attempts_daily_quiz_id`, `ix_quiz_attempt_answers_question_id` (answers "has this
question ever been played?" with one probe) and `ix_quiz_attempt_answers_selected_option_id`.

`daily_quiz_questions` and `quiz_attempt_answers` point at questions and options with **RESTRICT**,
not cascade. A question a player has already seen is part of somebody's history, and the staff
delete rule refuses to remove it anyway; cascade would only be a way to lose it by accident.

Progression is not a new table. `users.xp`, `users.level`, `users.streak_count` and
`users.last_active_on` from Module 1 are written by the quiz service, and `xp_awarded` on the
attempt is the audit trail for the payout. An attempt stores the XP it granted so a later change to
the scoring formula cannot retroactively rewrite a player's history.

`20260930122740_AddQuiz` is applied to both `nepalhub` and `nepalhub_test`. Questions are seeded by
an idempotent startup seeder with deterministic GUIDs from `ReferenceDataIds`, for the same reason
calculators are: staff own the copy from the moment it exists, and a reseed must not overwrite
their corrections. The seeder only inserts, and only for the IDs it knows. 31 questions and 124
options ship this way, each question with four options and exactly one correct.

## Module 5 — Student Hub (live)

One table, added by `20260930154447_AddStudentResources`, for three kinds of resource: a study
note, a past question paper and a scholarship. They are one table because they are searched,
filtered, paged and expired identically; three tables would mean three of everything and a client
that has to guess which one to ask.

| Column | Type | Notes |
| --- | --- | --- |
| `id` | `char(36)` PK | GUID |
| `slug` | `varchar(160)` unique | `^[a-z0-9]+(-[a-z0-9]+)*$`; the public URL, and immutable once created |
| `title` | `varchar(200)` | |
| `summary` | `varchar(300)` | the catalogue card line; an empty string while a draft is half-written |
| `body` | `longtext` | plain text, rendered as text; blank lines are paragraph breaks |
| `type` | `int` | `ResourceType`; 0 = Note, 1 = QuestionPaper, 2 = Scholarship |
| `category` | `int` | `ResourceCategory` (subject); e.g. 1 = Mathematics, 8 = Nepali |
| `level` | `int` | `EducationLevel`; 0 = General, 1 = PlusTwo, 2 = Undergraduate, 3 = Postgraduate |
| `external_url` | `varchar(500)` nullable | question papers only; absolute `https`, shape-checked and never fetched |
| `academic_year` | `int` nullable | question papers only; Bikram Sambat year, validated to 2000–2100 |
| `provider` | `varchar(160)` nullable | scholarships only |
| `award_amount_npr` | `decimal(12,2)` nullable | scholarships only; money is never a float |
| `eligibility` | `varchar(500)` nullable | scholarships only |
| `expires_on` | `date` nullable | scholarships only; compared against the **Nepal** date (UTC+05:45) |
| `is_published` | `tinyint(1)` default 0 | only published rows appear on the public endpoints |
| `created_at`, `updated_at` | `datetime(6)` | |
| `created_by_user_id` | `char(36)` nullable → `users` (SET NULL) | the author, kept if the account is removed |

Indexes: `ix_resources_slug` (unique), and one composite per public filter so the catalogue never
scans the whole table — `ix_resources_is_published_resource_type`,
`ix_resources_is_published_category`, `ix_resources_is_published_education_level`,
`ix_resources_is_published_expires_on` and `ix_resources_is_published_created_at` (the default
`Newest` order), plus `ix_resources_created_by_user_id` for the staff list.

Three decisions are worth stating because they look odd in the schema and are not:

- **`expires_on` is a `date`, not a `datetime`, and there is no expiry flag column.** Nothing needs
  a boolean that says "expired", because expired is a fact about the row and the date it is compared
  to, not a state somebody has to remember to set. The comparison happens in C# against the Nepal
  date so a scholarship disappears at the same moment for every reader, whatever the server's own
  timezone is.
- **`summary` and `body` are `NOT NULL` rather than nullable.** A draft is allowed to be empty, so
  the column could have been nullable, but "no summary yet" and "a summary of nothing" are the same
  thing to every reader of this table, and one representation of empty is one fewer thing for a
  client to handle. The publishing rule is what distinguishes a finished row, not a null.
- **There is no `sort_order`.** The public order is created date, closing date or title, all three
  answerable from indexes, and a fourth hand-maintained integer would be a column staff would
  forget to renumber. It belongs to the calculators table, where the order is editorial rather than
  derived.

`20260930154447_AddStudentResources` is applied to both `nepalhub` and `nepalhub_test`. 24 rows ship
with it — 10 notes, 7 question papers and 7 scholarships, two of which are already past their
deadline so the expiry path is visible in a fresh install rather than only in the tests. They are
inserted by an idempotent startup seeder with deterministic GUIDs from `ReferenceDataIds`, for the
reason the other two modules use one: staff own the copy from the moment it exists. Scholarship
deadlines are computed relative to the Nepal calendar date rather than hard-coded, so a seed written
today does not ship two expired rows a month later.

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

### Progression

`users.xp`, `users.level`, `users.streak_count` and `users.last_active_on` are already live (Module
1) and are written by the quiz. What is still planned:

- `badges` — `code`, `name`, `description`, `icon`, `criteria` (JSON), `xp_reward`
- `user_badges` — `user_id`, `badge_id`, `awarded_at` (unique per user+badge)
- `leaderboard_entries` — cached ranking rows per period, refreshed by a background service rather
  than recomputed on every request

Levels are derived from total XP and cached on the user row, so a leaderboard can be built later
without a migration. Badges and leaderboards are deliberately not part of the quiz: both need
their own read models.

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

1. One migration per module, named for the feature (`AddCalculators`).
2. `dotnet ef migrations add`, review the generated SQL by reading the model, then
   `dotnet ef database update`.
3. Seed reference data (provinces, districts, badge definitions) through `HasData` with
   deterministic ids, so a fresh environment is reproducible. Seed rows that staff are expected to
   edit — calculator copy, for instance — go through an idempotent startup seeder instead, because
   `HasData` would overwrite a real edit on the next migration.
4. Apply to `nepalhub_test` as well as `nepalhub`, so the integration tests run against the same
   schema the app uses.
5. Never edit an applied migration; add a new one.
6. Before going live: backup strategy, `innodb_file_per_table`, and a tested restore.

```powershell
dotnet ef migrations add <Name> --project backend/NepalHub.Api --startup-project backend/NepalHub.Api --output-dir Data/Migrations
dotnet ef database update --project backend/NepalHub.Api --startup-project backend/NepalHub.Api
```

To inspect the schema without guessing:

```sql
SELECT table_name, table_rows FROM information_schema.tables
WHERE table_schema = 'nepalhub' ORDER BY table_name;
```
