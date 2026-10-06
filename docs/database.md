# Database

**Status: MySQL 8.4.9 is installed and ten modules are live.** The schema below is the as-built
result of migrations `20260930092534_InitialCreate`, `20260930105251_AddCalculators`,
`20260930122740_AddQuiz`, `20260930154447_AddStudentResources`, `20261001041409_AddJobs`,
`20261001101158_AddBusinesses`, `20261001142733_AddDealsAndEvents`, `20261001155854_AddDealsAndEventsIndexes`,
`20261002162619_AddBadges`, `20261005042009_AddModeration` and `20261005042034_AddQuizAttemptCompletedAtIndex`,
not a proposal. Everything from "Planned modules" onwards is still design
only.

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
first), `ix_quiz_attempts_completed_at` (added by `20261005042034` — the leaderboard's today/week
boards filter on the timestamp alone), `ix_quiz_attempts_daily_quiz_id`,
`ix_quiz_attempt_answers_question_id` (answers "has this question ever been played?" with one probe)
and `ix_quiz_attempt_answers_selected_option_id`.

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

## Module 6 — Jobs (live)

One table, added by `20261001041409_AddJobs`, for a job posting. A posting is written by staff in
this phase rather than by an employer, so the author is a staff account rather than a business.

| Column | Type | Notes |
| --- | --- | --- |
| `id` | `char(36)` PK | GUID |
| `slug` | `varchar(160)` unique | `^[a-z0-9]+(-[a-z0-9]+)*$`; the public URL, editable only while the row is a draft |
| `title` | `varchar(200)` | the posting title |
| `organization` | `varchar(160)` | the employer, named in the posting rather than linked |
| `description` | `longtext` | plain text, rendered as text; blank lines are paragraph breaks; may be empty in a draft |
| `category` | `int` | `JobCategory` (sector); 0 = InformationTechnology, 1 = Teaching, … 14 = Other |
| `employment_type` | `int` | `EmploymentType`; 0 = FullTime, 1 = PartTime, 2 = Contract, 3 = Internship |
| `work_mode` | `int` | `WorkMode`; 0 = OnSite, 1 = Remote, 2 = Hybrid |
| `district_id` | `char(36)` nullable → `districts` (RESTRICT) | null for a remote posting; the province is read through it |
| `application_url` | `varchar(500)` | absolute `https`, shape-checked and never fetched |
| `application_deadline` | `date` | compared against the **Nepal** date (UTC+05:45) |
| `is_published` | `tinyint(1)` default 0 | only published rows appear on the public endpoints |
| `created_at`, `updated_at` | `datetime(6)` | |
| `created_by_user_id` | `char(36)` nullable → `users` (SET NULL) | the author, kept if the account is removed |

Indexes: `ix_jobs_slug` (unique), one composite per public filter so the board never scans the
whole table — `ix_jobs_is_published_category`, `ix_jobs_is_published_employment_type`,
`ix_jobs_is_published_work_mode`, `ix_jobs_is_published_application_deadline` and
`ix_jobs_is_published_created_at` (the default `Newest` order), plus `ix_jobs_district_id_is_published`
for the location filter and `ix_jobs_created_by_user_id` for the staff list.

Four decisions are worth stating because they look odd in the schema and are not:

- **There is no `status` column, and no expiry flag.** A posting's public status is derived on every
  read from `is_published` and `application_deadline` against the Nepal date: published and not yet
  closed is `Open`, published and within three days is `ClosingSoon`. Both of those stop being true
  on their own as the deadline approaches, so a stored value would be wrong the following morning,
  and a `pending` state has nothing to review in a phase where only staff write.
- **Location is `district_id` alone, and nullable.** Storing a province as well would let the two
  disagree. A remote posting has no fixed place, so it stores null rather than a placeholder district
  that would then appear in every district filter for that province.
- **`application_deadline` is `NOT NULL`.** There is no such thing as a job posting with no closing
  date: an employer who has not said when it closes has not finished writing the posting. A draft may
  still be saved without one, because a draft is allowed to be unfinished.
- **There is no `salary_min`, `salary_max` or `business_id`.** A salary nobody verifies is worse than
  none, and an employer account belongs to the business module; both would be columns added with the
  module that can fill them properly.

`20261001041409_AddJobs` is applied to both `nepalhub` and `nepalhub_test`. 16 rows ship with it,
one in each of the seven provinces, two of which are already past their deadline so the closing path
is visible in a fresh install rather than only in the tests. They are inserted by an idempotent
startup seeder with deterministic GUIDs from `ReferenceDataIds`, for the reason the other two
modules use one: staff own the copy from the moment it exists. Deadlines are computed relative to the
Nepal calendar date rather than hard-coded, so a seed written today does not ship two closed rows a
month later.

## Module 7 — Businesses (live)

Two tables, added by `20261001101158_AddBusinesses`. This is the first slice of the business
directory, and it landed **staff-managed listings only**: no business account, no owner column and no
claim flow, because a portal has nothing to attach to until someone can register as a business. Phase
7e kept that rule — staff still create, publish and verify every row — and added the missing
attachment points: `owner_user_id` and the claim queue (Module 11), the plan tables (Module 12), all
below rather than pre-created empty at the time.

`businesses` is one row per listing. A table per category would mean a copy of every query and a
client that has to guess which one to ask.

| Column | Type | Notes |
| --- | --- | --- |
| `id` | `char(36)` PK | GUID |
| `slug` | `varchar(160)` unique | `^[a-z0-9]+(-[a-z0-9]+)*$`; the public URL, editable only while the row is a draft |
| `name` | `varchar(200)` | the business name |
| `description` | `longtext` | plain text, rendered as text; blank lines are paragraph breaks; may be empty in a draft |
| `category` | `int` | `BusinessCategory`; 0 = Restaurants, 1 = Cafes, … 11 = Other |
| `district_id` | `char(36)` → `districts` (RESTRICT) | **not null**; the province is read through it |
| `address` | `varchar(300)` nullable | free text, not geocoded |
| `phone` | `varchar(20)` nullable | digits only after normalisation; required before publishing; not unique |
| `email` | `varchar(256)` nullable | unverified — nothing in this product sends to it |
| `website` | `varchar(500)` nullable | absolute `https` with a real host, shape-checked and never fetched |
| `is_published` | `tinyint(1)` default 0 | only published rows appear on the public endpoints |
| `is_verified` | `tinyint(1)` default 0 | whether staff have checked the listing |
| `verified_at` | `datetime(6)` nullable | when it was last checked; null while unchecked |
| `created_at`, `updated_at` | `datetime(6)` | |
| `created_by_user_id` | `char(36)` nullable → `users` (SET NULL) | the staff author, kept if the account is removed |

`business_hours` is a child table, at most one row per weekday, with a composite primary key of
`(business_id, day_of_week)` so the service is not asked to enforce it.

| Column | Type | Notes |
| --- | --- | --- |
| `business_id` | `char(36)` PK/FK → `businesses` (CASCADE) | |
| `day_of_week` | `tinyint` unsigned PK | `DayOfWeek`, 0 = Sunday … 6 = Saturday |
| `is_closed` | `tinyint(1)` default 0 | the business does not open on this day at all |
| `opens_at`, `closes_at` | `time(6)` nullable | null on a closed day |

Indexes on `businesses`: `ix_businesses_slug` (unique) and one composite per public filter so the
directory never scans the whole table — `ix_businesses_is_published_name` (the default `Name` order),
`ix_businesses_is_published_category`, `ix_businesses_is_published_is_verified`,
`ix_businesses_is_published_created_at` (the `Newest` order) and
`ix_businesses_district_id_is_published` for the location filter, plus
`ix_businesses_created_by_user_id` for the staff list.

Five decisions are worth stating because they look odd in the schema and are not:

- **The province is not a column.** It is read through `district_id`, because two columns naming the
  same place can disagree and a filter that contradicts its own row is worse than one fewer column.
- **`district_id` is `NOT NULL`, which is where this departs from the job board.** A remote job
  legitimately has no location; a directory of local businesses is useless without one, and a listing
  with no district cannot be found by anyone browsing their own area.
- **`is_published` and `is_verified` are two independent flags, not one status column.** Publishing
  requires verification, but unverifying does not retract, so a listing can stay live without its
  badge when a detail turns out to be wrong and someone needs to take responsibility for the
  correction. Merging them would force an unverified business off the directory entirely, which is a
  worse outcome than showing it honestly unchecked.
- **Hours are a child table rather than a JSON column or seven nullable columns on the parent.** All
  three eventually get a partial update that leaves one day out of step with the other six, and the
  child table buys "is it open now" for free, which a single string cannot answer. Empty is
  meaningful: a lot of businesses publish without stating hours, and "not listed" is honest where a
  wrong guess is not. `is_closed` exists because "closed" is not the absence of hours — a
  Sunday-only closure is a real schedule.
- **No `latitude`/`longitude`, `logo_url` or `cover_url`.** No map pin, because a district is the
  location a reader filters by and a hand-placed pin the server cannot verify is worse than none.
  Image storage is a later phase with its own decision about disk versus object storage.

`20261001101158_AddBusinesses` is applied to both `nepalhub` and `nepalhub_test`. 25 rows ship with it,
inserted by an idempotent startup seeder with deterministic GUIDs, for the same reason the other
modules use one: staff own the copy from the moment it exists. The seeds cover all twelve categories
and all seven provinces, and are weighted towards Bagmati on purpose — fifteen are in Kathmandu
valley, which is where most readers are and where a fresh install needs the province filter to have
something to do, while every other province still has at least one so no filter returns an empty
page. Three rows exist in states the staff screen has to handle: a draft waiting to be checked, a
listing checked before it went live, and one that is published but no longer carries its badge.

## Module 8 — Deals and events (live)

Added by `20261001142733_AddDealsAndEvents`, applied to both `nepalhub` and `nepalhub_test`. Two
tables, fifteen seeded rows each, both written by staff through the same seeder pattern as every other
module. A second migration, `20261001155854_AddDealsAndEventsIndexes`, adds the two indexes behind the
`Newest` sort — written as its own migration rather than folded into the create, because the first one
was already applied to a live development database and rewriting history to look tidier is not worth a
hand-edited schema.

### `deals`

| Column | Type | Notes |
| --- | --- | --- |
| `id` | `char(36)` PK | |
| `slug` | `varchar(160)` unique | `^[a-z0-9]+(?:-[a-z0-9]+)*$`, frozen once published |
| `title` | `varchar(200)` | |
| `summary` | `varchar(400)` nullable | the card's one-liner |
| `discount_text` | `varchar(120)` nullable | the offer in the business's own words |
| `description` | `longtext` | the full terms; length is enforced by the service, not the column |
| `business_id` | `char(36)` nullable → `businesses` (SET NULL) | optional; null on delete |
| `district_id` | `char(36)` nullable → `districts` (RESTRICT) | null means nationwide |
| `redemption_url` | `varchar(500)` nullable | absolute `https`, shape-checked, never fetched |
| `starts_on`, `ends_on` | `date` | inclusive end date |
| `is_published` | `tinyint(1)` default 0 | |
| `created_at`, `updated_at` | `datetime(6)` | |
| `created_by_user_id` | `char(36)` nullable → `users` (SET NULL) | |

### `events`

| Column | Type | Notes |
| --- | --- | --- |
| `id` | `char(36)` PK | |
| `slug` | `varchar(160)` unique | same pattern as `deals` |
| `title` | `varchar(200)` | |
| `summary` | `varchar(400)` nullable | |
| `description` | `longtext` | the full copy; length is enforced by the service, not the column |
| `category` | `tinyint` unsigned | `EventCategory`, serialised by name |
| `organiser` | `varchar(160)` nullable | the group running it |
| `business_id` | `char(36)` nullable → `businesses` (SET NULL) | optional; null on delete |
| `district_id` | `char(36)` → `districts` (RESTRICT) | **NOT NULL**, unlike `deals` |
| `venue` | `varchar(240)` nullable | free text, not geocoded |
| `starts_at`, `ends_at` | `datetime(6)` | Nepal wall-clock, no zone conversion |
| `registration_url` | `varchar(500)` nullable | absolute `https`, shape-checked, never fetched |
| `is_published` | `tinyint(1)` default 0 | |
| `created_at`, `updated_at` | `datetime(6)` | |
| `created_by_user_id` | `char(36)` nullable → `users` (SET NULL) | |

Six decisions are worth stating because they look odd in the schema and are not:

- **`deals.starts_on`/`ends_on` are `date`, not `datetime`.** An offer is redeemed on a day. A clock
  time on an offer invents a precision the business does not have and makes "ends today" mean
  something different at 09:00 than at 21:00 for no reason a reader can act on.
- **Neither table stores a countdown.** `days_remaining` and `is_ongoing` are computed against the
  current Nepal time on every read. A stored flag is wrong by the evening, and the daily quiz already
  established that a Nepal-facing date must come from a clock that knows about UTC+05:45 rather than
  from the machine or from UTC.
- **`events.starts_at`/`ends_at` are bare Nepal local times, not UTC.** They are written from what a
  staff member typed and read back the same way. Converting to UTC would move every event by five
  hours and three quarters, and the round trip through a JavaScript `Date` would move it again for
  every reader outside Nepal.
- **`deals.district_id` is nullable and `events.district_id` is not.** The two are different facts. A
  free-delivery offer that applies everywhere is the case that separates an offer from a business
  listing; an event with no location is an event nobody can attend or browse by.
- **The business foreign key is `SET NULL` on both.** An offer or event stays readable after the
  listing it was attached to is retracted, because the alternative is a live page about a real offer
  that 500s because an unrelated row was deleted. The business *name* survives with it; only the
  public `businessSlug` is withheld for an unlisted business, and that is a read-model decision rather
  than a column.
- **No `cover_url`, `price` or `coupon_code`; capacity and now `ticket_price`.** There is no image
  storage decision yet and an offer's headline is text in the business's own words rather than a
  price pair. An event used to collect nothing at all; Module 13 gave it `capacity` and
  `registration_count` — seats a reader can hold *on this site* — and 7f gave it `ticket_price`
  (Module 14), the price of one of those seats in whole rupees. It is an integer on purpose: the
  plan catalogue prices whole rupees too, and a payment that arrives as a `decimal` never matches
  one that left as an `int`.

Indexes: `ix_deals_slug` (unique) and one composite per public filter, each leading with
`is_published` because every public read tests that first —
`ix_deals_is_published_ends_on` (the `EndingSoon` order), `ix_deals_is_published_starts_on`,
`ix_deals_is_published_created_at` (the `Newest` order),
`ix_deals_district_id_is_published` (the district leads here, since a location filter tests both
together), `ix_deals_business_id`, plus `ix_deals_created_by_user_id`. `events` mirrors it with
`ix_events_is_published_starts_at` (the `Soonest` order), `ix_events_is_published_ends_at`,
`ix_events_is_published_category`, `ix_events_is_published_created_at` (the `Newest` order),
`ix_events_district_id_is_published`, `ix_events_business_id` and `ix_events_created_by_user_id`.

The seeds cover every category, every province and both sort orders, and include states a fresh
install would otherwise never show: a draft, an offer whose end date has already passed, and an event
that has already finished.

## Module 9 — Badges (live)

Added by `20261002162619_AddBadges`. Two tables: what a badge *is*, and the fact that this person has it.
Neither stores anything about a user that the quiz has not already written.

### `badges`

| Column | Type | Notes |
| --- | --- | --- |
| `id` | `char(36)` | PK, no default — ids are derived from `ReferenceDataIds.Badge(code)` |
| `code` | `varchar(50)` | Unique. The stable handle a client can key on |
| `name` | `varchar(100)` | Display name |
| `description` | `varchar(250)` | What earns it, in the player's words |
| `icon` | `varchar(40)` | A key such as `trophy`, not a filename. Resolved by the client |
| `criteria` | `varchar(40)` | `BadgeCriteria` by name: `QuizzesCompleted` or `ConsecutiveDayStreak` |
| `sort_order` | `int` | Display order |

The criterion is a closed enum stored as a string plus an integer `threshold`, and the rule is a
`>=` comparison against a number the quiz already persists.

**Not a JSON `criteria` blob.** An earlier draft of this document proposed one. A JSON criterion would
let the seed declare a rule that nothing evaluates, so a badge could be listed on the account page as
unearnable with no way to find out short of a player waiting forever for it. An enum cannot hold a rule
the code does not implement, and adding a badge to the seed needs no change to `BadgeRules`.

**No `xp_reward` column**, also contrary to the earlier draft. The quiz decides XP once per attempt. A
badge that quietly paid out again would make the total on the account page disagree with the sum of the
quiz results behind it, and "why did my XP go up twice" has no good answer.

Seeded through `HasData` with ids derived from the code, so a fresh environment is reproducible and the
three rows are identical on every machine. They are not staff-editable copy: `HasData` would overwrite a
real edit on the next migration, which is the trade `database.md`'s migration discipline accepts for rows
that are meant to match the code. Three ship — first quiz (1), seven-day streak (7), ten quizzes (10).

Indexes: `ix_badges_code` (unique) and `ix_badges_sort_order`, which is the order every read asks for.

### `user_badges`

| Column | Type | Notes |
| --- | --- | --- |
| `id` | `char(36)` | PK |
| `user_id` | `char(36)` | FK → `users`, `ON DELETE CASCADE` |
| `badge_definition_id` | `char(36)` | FK → `badges`, `ON DELETE CASCADE` |
| `awarded_at` | `datetime(6)` | The instant of the quiz that earned it, not the backfill instant |

The unique index on `(user_id, badge_definition_id)` is what makes granting idempotent, not a check
inside the service: the service skips a badge already held, and the index makes a second row for one
impossible even if two requests race. `awarded_at` records when the qualifying quiz happened rather than
when the row was written, so a backfilled account dates its badges to what it actually did.

Indexes: `ix_user_badges_user_id_badge_id` (unique, the idempotency guarantee),
`ix_user_badges_user_id_awarded_at` and `ix_user_badges_badge_id`.

**The total comes from the attempts, not from a counter.** `quizzesCompleted` is a `COUNT` over
`quiz_attempts` and the streak is `users.streak_count`, both read at award time. A tally kept on the user
row would be a second place for the quiz count to live, and it would be the one that drifts.

## Module 10 — Moderation and audit (live)

Added by `20261005042009_AddModeration`. Two tables: what a member complained about, and the
append-only record of what staff did about it and about other accounts. Both existed before the
actions they carry started happening, because a trail reconstructed after the fact is a
reconstruction, not a record.

### `reports`

| Column | Type | Notes |
| --- | --- | --- |
| `id` | `char(36)` | PK, no default |
| `reporter_user_id` | `char(36)` | FK → `users`, `ON DELETE CASCADE` — a deleted account takes its reports with it |
| `target_type` | `varchar(40)` | `ReportTargetType` by name: `Job`, `Business`, `Deal`, `Event`, `StudentResource` |
| `target_id` | `char(36)` | The reported row's id. Deliberately **not** a foreign key: one column cannot hold five of them |
| `reason` | `varchar(40)` | `ReportReason` by name: `Spam`, `Misleading`, `Inappropriate`, `Broken`, `Other` |
| `note` | `varchar(500)` | Optional, in the reporter's own words |
| `status` | `varchar(20)` | `ReportStatus` by name: `Open` (default), `Resolved`, `Dismissed` |
| `created_at` | `datetime(6)` | Defaults to `CURRENT_TIMESTAMP(6)` |
| `resolved_by_user_id` | `char(36)` nullable | FK → `users`, `ON DELETE SET NULL` — closing a report is a fact about the moderator, not about the reporter |
| `resolved_at` | `datetime(6)` nullable | The instant it left the queue |
| `resolution_note` | `varchar(500)` | What staff recorded when closing it |

The target id is validated at filing time through `PublicContentScope` — the same visibility rules
the public detail page uses — so a report can only point at something a reader could actually see.
A target that later disappears keeps its row: the report is the record of what was reported, not a
copy of the thing itself, so nothing cascades with it.

Indexes: `ix_reports_reporter_target` (**unique** — one report per person per target, so a
double-click, a retried request or a second tab is a `409 already_reported` rather than a duplicate
row, and a repeat after staff closed the first one is refused too) and
`ix_reports_status_created_at` — the queue's only order, open work first and newest first.

### `admin_audit_logs`

| Column | Type | Notes |
| --- | --- | --- |
| `id` | `char(36)` | PK, no default |
| `admin_user_id` | `char(36)` | FK → `users`, `ON DELETE CASCADE`. A plain id rather than a name, so a renamed account stays truthful |
| `action` | `varchar(50)` | Dotted name: `report.resolved`, `report.dismissed`, `user.activated`, `user.deactivated`, `user.role_granted`, `user.role_revoked` |
| `target_type` | `varchar(40)` | `report` or `user` |
| `target_id` | `varchar(64)` | Text, so the table needs no polymorphic key |
| `before` | `LONGTEXT` nullable | JSON snapshot of the subject as it stood |
| `after` | `LONGTEXT` nullable | JSON snapshot of what it became. Unbounded on purpose — a truncated audit trail reads as a complete one |
| `ip` | `varchar(45)` nullable | The address the action came from |
| `created_at` | `datetime(6)` | Defaults to `CURRENT_TIMESTAMP(6)` |

`IAuditLog.Record` appends to the caller's own `AppDbContext` and does not save: the log row and the
change it describes land in the same `SaveChanges`, so a rolled-back action leaves no trace and a
committed one always has its entry. Moderation and user administration write here; the seven content
modules do not, because their subject is the site's own catalogue rather than somebody else's
account. Rows are appended and never updated — this table is written once and read, never edited.

Indexes: `ix_admin_audit_logs_created_at` (newest-first reads),
`ix_admin_audit_logs_admin_user_id` ("what has this administrator ever done?") and
`ix_admin_audit_logs_target` ("who has ever touched this subject?").

The leaderboard is a read model over Module 1 and adds no table of its own; the reasoning lives with
the phase that built it rather than here.

## Module 11 — Ownership and claims (live)

Added by `20261005103010_AddBusinessOwnership`: one nullable column on the live `businesses` table,
plus the queue table that fills it. The directory starts as staff-written copy, so ownership is
asserted and decided rather than handed out — nothing about a filed claim is taken on trust.

### `businesses.owner_user_id`

| Column | Type | Notes |
| --- | --- | --- |
| `owner_user_id` | `char(36)` nullable | FK → `users`, `ON DELETE SET NULL`, `ix_businesses_owner_user_id`. The account that manages the listing; NULL for staff-written rows nobody has claimed. Deleting an account leaves the listing standing and unowned rather than pointing at nothing |

### `business_claims`

| Column | Type | Notes |
| --- | --- | --- |
| `id` | `char(36)` | PK, no default |
| `business_id` | `char(36)` | FK → `businesses`, `ON DELETE CASCADE` |
| `claimant_user_id` | `char(36)` | FK → `users`, `ON DELETE CASCADE` — a deleted account takes its claims with it |
| `evidence` | `varchar(500)` nullable | How the claimant is connected to the listing, in their own words |
| `status` | `varchar(20)` | `BusinessClaimStatus` by name: `Pending` (default), `Approved`, `Rejected` |
| `created_at` | `datetime(6)` | Defaults to `CURRENT_TIMESTAMP(6)` |
| `reviewed_by_user_id` | `char(36)` nullable | FK → `users`, `ON DELETE SET NULL` — deleting a moderator must not re-open who owns a listing |
| `reviewed_at` | `datetime(6)` nullable | The instant staff decided |
| `review_note` | `varchar(500)` | What staff recorded when deciding. Lives on the claim row, not in the audit `after` |

**No unique index on (business, claimant).** A rejected claimant who corrects their evidence
should be able to try again, and a database that refuses the second row would give no reason at
all. The one-pending-claim rule is checked in the service, where the refusal can be named
(`already_claimed`) alongside `already_owner` and `already_owned`.

Approval is the only thing that moves `owner_user_id`: it settles the rival claims, grants the
`business` role and writes `business.claim_approved` to the audit trail in the same `SaveChanges`.
A filed claim grants no access of its own.

Indexes: `ix_business_claims_status_created_at` (the queue: pending first, newest first) and
`ix_business_claims_business_claimant_status` (has this person already asked about this listing).

## Module 12 — Plans and subscriptions (live)

Added by `20261005104909_AddBusinessSubscriptions`. Three tables: what a plan *is*, one granted
window over a listing, and the queue an owner puts a request in. No money moves against any of
them in this phase — the request queue is the checkout, and a future provider's webhook resolves
into the same queue rather than a second approval path.

### `plans`

| Column | Type | Notes |
| --- | --- | --- |
| `id` | `char(36)` | PK, no default — seeded, not client-supplied |
| `tier` | `varchar(20)` | `PlanTier` by name: `Free`, `Featured`. **Unique** (`ix_plans_tier`) — one row per tier, so "the featured plan" is a lookup |
| `name` | `varchar(80)` | Display name, e.g. `Featured` |
| `description` | `varchar(500)` nullable | What the plan buys, on the catalogue |
| `period_days` | `int` | How long one grant lasts |
| `price_npr` | `int` | Whole rupees, default 0. A **catalogue** figure: it exists so a future checkout can read the same row |
| `is_active` | `bool` | Default 1. Inactive plans are hidden rather than labelled |
| `created_at` | `datetime(6)` | Defaults to `CURRENT_TIMESTAMP(6)` |
| `updated_at` | `datetime(6)` | `CURRENT_TIMESTAMP(6)` on insert and update |

Plans are never deleted — past subscriptions point at them — and are edited in place rather than
replaced. Seeded: Free (30 days, NPR 0) and Featured (30 days, NPR 3,000).

### `business_subscriptions`

| Column | Type | Notes |
| --- | --- | --- |
| `id` | `char(36)` | PK, no default |
| `business_id` | `char(36)` | FK → `businesses`, `ON DELETE CASCADE` |
| `plan_id` | `char(36)` | FK → `plans`, `ON DELETE RESTRICT` — a subscription must never be able to take its plan out of the table |
| `starts_on` | `date` | First day the subscription counts, in Nepal |
| `ends_on` | `date` | Last day, inclusive. Extending moves this forward |
| `status` | `varchar(20)` | `BusinessSubscriptionStatus` by name: `Active` (default), `Cancelled` |
| `created_at` | `datetime(6)` | Defaults to `CURRENT_TIMESTAMP(6)` |
| `granted_by_user_id` | `char(36)` nullable | FK → `users`, `ON DELETE SET NULL` — deleting an admin must not cancel what it granted |
| `subscription_request_id` | `char(36)` nullable | FK → `subscription_requests`, `ON DELETE SET NULL`. Null for a direct grant |
| `cancelled_at` | `datetime(6)` nullable | When the window was stopped; the row is kept as the record |

**Entitlement is derived, never stored as a liveness flag:** whether a listing is featured is
`ends_on >= today` against `INepalDateProvider`'s Nepal day, so an expired row reports itself
without a job. At most one row per business can be live — enforced in the service, because two
overlapping rows would make "is this listing featured" depend on which one the query happened to
find — and re-granting extends by `max(current_end, today) + period_days`.

Indexes: `ix_business_subscriptions_business_id_status` (is anything live for this listing — the
directory asks on every read) and `ix_business_subscriptions_status_ends_on` (expiry sweeps and
"ending soon").

### `subscription_requests`

| Column | Type | Notes |
| --- | --- | --- |
| `id` | `char(36)` | PK, no default |
| `business_id` | `char(36)` | FK → `businesses`, `ON DELETE CASCADE` |
| `plan_id` | `char(36)` | FK → `plans`, `ON DELETE RESTRICT` — approval writes a subscription pointing here forever after |
| `requester_user_id` | `char(36)` | FK → `users`, `ON DELETE CASCADE` |
| `note` | `varchar(500)` nullable | What the owner wants, in their own words |
| `status` | `varchar(20)` | `SubscriptionRequestStatus` by name: `Pending` (default), `Approved`, `Rejected` |
| `created_at` | `datetime(6)` | Defaults to `CURRENT_TIMESTAMP(6)` |
| `reviewed_by_user_id` | `char(36)` | FK → `users`, `ON DELETE SET NULL` — deleting an admin must not silently drop the requests it decided |
| `reviewed_at` | `datetime(6)` nullable | The instant it left the queue |
| `review_note` | `varchar(500)` | What the admin recorded — the closest thing to a receipt this phase has |

Approving settles the queue row **and** writes the subscription in the same `SaveChanges`, with
one `subscription.approved` audit entry naming both; a plan deactivated since the request answers
409 rather than being granted anyway.

Indexes: `ix_subscription_requests_status_created_at` (the admin queue) and
`ix_subscription_requests_business_id_status` (is there already a request waiting for this
listing — the `request_pending` refusal).

## Module 13 — Event bookings (live)

Added by `20261005104931_AddEventBookings`, which also gave the live `events` table the two
columns the counter needs. Taking a place happens on this site; paying for one does not until
`ticket_price` is set — and that decision lives in 7f (Module 14), not here.

### `events.capacity` and `events.registration_count`

| Column | Type | Notes |
| --- | --- | --- |
| `capacity` | `int` nullable | Places the event can hold. NULL means no cap — the nullable column is the distinction between "uncapped" and "zero seats", and a "0" would be a wrong answer to a question nobody asked |
| `registration_count` | `int` | Default 0. Places currently held — **a stored counter, deliberately not a row count**, so deleting an account or a booking never has to find and adjust every event it touched |

Availability is decided by an atomic conditional `UPDATE` against `registration_count`, released
if it fails: the counter is what both the capacity check and the reader's "12 spots left" read,
and two queries that disagree about availability is how an event sells out twice. The reader's
`remainingSpots` is derived at read time and clamped at zero, never negative.

### `event_bookings`

| Column | Type | Notes |
| --- | --- | --- |
| `id` | `char(36)` | PK, no default |
| `event_id` | `char(36)` | FK → `events`, `ON DELETE CASCADE` |
| `attendee_user_id` | `char(36)` | FK → `users`, `ON DELETE CASCADE`. The event's stored counter is what survives a deletion, precisely so nothing has to chase rows |
| `quantity` | `int` | Default 1. One to four in this phase; the column is a number for later |
| `status` | `varchar(20)` | `EventBookingStatus` by name: `Confirmed` (default), `Pending` (a priced seat taken before eSewa answered), `Cancelled` |
| `created_at` | `datetime(6)` | Defaults to `CURRENT_TIMESTAMP(6)` |
| `cancelled_at` | `datetime(6)` nullable | Cancelling toggles the row rather than deleting it, so the record of who had a place survives |

**Unique `ix_event_bookings_event_attendee` (event_id, attendee_user_id):** one row per person
per event for all time, cancelled or not. Booking again reactivates the same row, so a place can
never be held twice by one account even from two tabs racing — and the rebooking keeps its
original id, which is what "your place is back" means on the page.

Bookings write **no audit rows**: the trail is for admin and staff actions, and a reader booking
a seat is not one of those.

Indexes: `ix_event_bookings_event_attendee` (unique) and `ix_event_bookings_event_id_status`
(the staff attendee list and the event's own "who is coming").

## Module 14 — Payments (live)

Added by `20261006035723_AddPayments`, which also gave `events` its `ticket_price` column.
Money is asked for through eSewa ePay v2 and recorded here as a reference, an amount and a
status — the provider's own transaction id and nothing a card could be replayed from.

### `events.ticket_price`

| Column | Type | Notes |
| --- | --- | --- |
| `ticket_price` | `int` default 0 | Whole rupees for one place. **0 is the free case**, which is why the column is not nullable: every existing event became a free event the moment the migration ran, and NULL would have had to mean the same thing twice |

The price is read by the booking panel (which offers a `Pay NPR …` button instead of `Book` when it
is above zero) and by the staff event editor, so editing an event cannot silently zero it: the price
round-trips through the save contract like any other field.

### `payment_transactions`

| Column | Type | Notes |
| --- | --- | --- |
| `id` | `char(36)` PK | |
| `provider` | `varchar(20)` | e.g. `esewa`; the seam allows more than one |
| `merchant_txn_id` | `varchar(64)` unique | what we sent as `product_code` — the id eSewa answers about |
| `provider_txn_id` | `varchar(64)` nullable | whatever the status check reports; unknown until it does |
| `kind` | `varchar(20)` | `Subscription` or `EventTicket` |
| `reference` | `varchar(120)` | the request id, the booking id or the slug — what this payment is *for* |
| `user_id` | `char(36)` → `users` | who is paying |
| `business_id` | `char(36)` nullable → `businesses` | set for a subscription, null for a ticket |
| `event_id` | `char(36)` nullable → `events` | set for a ticket, null for a subscription |
| `amount` | `int` | whole rupees; compared against the plan or `ticket_price × places` before anything is granted |
| `status` | `varchar(20)` | `PaymentStatus` by name: `Pending`, `Succeeded`, `Failed`, `RefundDue`, `Refunded` |
| `failure_code` | `varchar(64)` nullable | `amount_mismatch`, `signature_invalid`, `status_unreachable`, `provider_failed` — why we did not believe it |
| `provider_notice` | `varchar(64)` nullable | what the last status check literally said, for an admin who needs to know the difference between "nothing happened" and "we could not ask" |
| `created_at`, `updated_at` | `datetime(6)` | |

Indexes: `ix_payment_transactions_reference_status` (settlement and the account history both lead
with the reference), `ix_payment_transactions_user_id_created_at`, and the unique index on
`merchant_txn_id` — **one row per attempt**, so two tabs clicking `Pay` resume one checkout instead
of inventing a second.

**Nothing here is settled by a redirect.** The browser returns to `/payments/return/{reference}`
carrying eSewa's signed `data` + `signature`, and `PaymentService` settles only after the
server-to-server status check agrees about the amount. The conditional `UPDATE` that moves
`Pending → Succeeded` is the latch: two answers grant once, audit once (`payment.succeeded`), and
leave everything else untouched. A `FULL_REFUND` seen on a later check moves a `Succeeded` row to
`Refunded` and writes `payment.refunded`; a cancelled paid booking parks the row at `RefundDue`,
because eSewa has no refund API and *requesting* one is an administrator's decision.

No card number, no token that stands for one, and no provider secret is ever written to this table:
the signing key lives in configuration, and a settlement that cannot be verified stays `Pending`
forever rather than being believed from a URL.

## Planned modules

Everything below this line is still absent from the database. Each will be added as its own
migration when its module is built. (Ownership, plans, subscriptions, bookings and payments were
listed here once — they are live as Modules 11–14 above.)

### Businesses, monetisation and claims

The live `businesses` and `business_hours` tables above are the first slice, now extended with
`owner_user_id` (Module 11). What a later phase adds is listed here as a record of the intent
rather than a plan:

- `business_categories` — a tree via `parent_id`. The live schema uses a `BusinessCategory` enum
  instead, because a fixed set of twelve labels is searchable, translatable and impossible to orphan;
  a tree is worth it only when staff need to define categories at runtime.
- `business_images` — `business_id`, `url`, `sort_order`, with the disk-versus-object-storage decision
  made first
- `businesses.latitude`/`longitude` (`decimal(10,7)`) with a composite index, added with the maps
  integration and only once there is a way to place a pin that is not invented
- ratings: `rating_average`, `rating_count`, `view_count`, and the `business_reviews` table behind them

Payments are live as Module 14: the adapter sits behind `Features/Payments/IPaymentGateway`, only
eSewa's opaque ids and the resulting entitlement are stored, and no card data ever touches this
database. There is still no webhook handling to design — ePay v2 has none — so the settlement path
is a status check that resolves into the live `subscription_requests` queue rather than a second
approval path.

`jobs.business_id` stays nullable and null on staff-written rows. It is reserved for employer-owned
postings, which needs the `owner_user_id` column that now exists — the claim approval path shows
the shape.

### Content modules

`jobs` is live (Module 6), `businesses` is live (Module 7), and `deals`/`events` are live (Module 8).
Everything else below is still design.

| Table | Key columns |
| --- | --- |
| `job_applications` | `job_id`, `user_id`, `cv_url`, `cover_letter`, `status`, `applied_at` (unique per job+user) |
| `jobs.business_id` | nullable → `businesses`, for employer-owned postings; null on staff-written rows |
| `jobs.salary_min`, `jobs.salary_max`, `jobs.currency` | nullable, added only when there is a rule for what a verified range means |
| `deals.redemption_codes`, `deals.redemption_uses` | only if offers ever become redeemable on this site rather than at the counter; a code table needs a rule about who mints codes |
| `events.ticket_price` | only with a payment provider (7f). `events.capacity` and `events.registration_count` are live as Module 13 — seats are held here, payments are not taken here |
| `businesses.owner_user_id` → `deals`/`events` | ownership of an offer or event, reusing the `owner_user_id` column and claim path of Module 11 |

Offers deliberately collect nothing on this site. A deal is redeemed at a counter and an event's
seat is confirmed without a price, so the honest value of both is a link out — which is why
`redemption_url` and `registration_url` exist and why no payment or ticket column does.

### Progression

`users.xp`, `users.level`, `users.streak_count` and `users.last_active_on` are live (Module 1) and are
written by the quiz. `badges` and `user_badges` are live too (Module 9), and the leaderboard reads
these same columns at request time with a table of its own. This section previously proposed
`leaderboard_entries` — cached ranking rows per period, refreshed by a background service — and the
plan was dropped when the board was built: the all-time order is a seek over `ix_users_level_xp`, the
period boards group `quiz_attempts` over `ix_quiz_attempts_completed_at`, and a refresh job would only
buy staleness for a figure that is already correct on demand. The reversal is recorded in
`docs/roadmap.md` rather than quietly deleted.

Levels stay derived from total XP and cached on the user row, so ranking reads one indexed column and
never recomputes the level.

### User data and operations

- `saved_items` — `user_id`, `item_type` (job/event/deal/business), `item_id`, `created_at`
  (unique per user+type+item)
- `notifications` — `user_id`, `type`, `title`, `body`, `is_read`, `created_at`

`reports` and `admin_audit_logs` were listed here once as prerequisites for moderation; they are live
as Module 10 now, above.

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
