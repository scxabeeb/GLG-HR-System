# Garowe Local Government — HR System DHG

A multi-role HR platform built with **ASP.NET Core 8 Razor Pages**, **Identity**, **EF Core 8**, **Pomelo**, and **MySQL 8**. The supplied Garowe emblem and its blue, red, white, and gold palette are used throughout the UI. The original specification is preserved in [REQUIREMENTS.md](REQUIREMENTS.md).

## Start with Docker / MySQL 8

```sh
cd /Users/saedhabeb/Documents/HrSystemDhg
cp .env.example .env
# Edit .env and replace all password placeholders with strong, unique passwords.
./scripts/start-local.sh
```

Open [the HR system](http://localhost:5189). Sign in with `BOOTSTRAP_EMAIL` and `BOOTSTRAP_PASSWORD` from your `.env` file. The administrator must change that password on first sign-in. MySQL, uploads, and cookie encryption keys have separate persistent volumes. The app is bound to localhost by default. The container runs migrations and creates missing roles on startup.

The startup script requires Docker with Compose v2 and `curl`. It checks Docker and the daemon, validates Compose configuration, runs `docker compose up --build -d`, and waits up to 180 seconds for an HTTP success or redirect at `http://localhost:5189`. You can run it from any directory. It suppresses Docker output to keep configuration secrets out of the terminal and prints recovery steps on failure. A failed startup/readiness check leaves any started containers and persistent data in place. To stop the stack without deleting data, run `docker compose down` from this folder (without `-v`).

## Run against your own MySQL server

Create an empty `hr_system_dhg` database with `utf8mb4`, then set a connection string outside source control:

```sh
export ConnectionStrings__DefaultConnection='Server=localhost;Port=3306;Database=hr_system_dhg;User=hr_app;Password=YOUR_PASSWORD;'
export Bootstrap__Email='admin@example.com'
export Bootstrap__Password='YOUR_STRONG_INITIAL_PASSWORD'
dotnet restore HrSystemDhg.sln
dotnet run --project HrSystemDhg.Web -- --migrate
dotnet run --project HrSystemDhg.Web -- --seed
unset Bootstrap__Password
dotnet run --project HrSystemDhg.Web --urls http://localhost:5189
```

`--migrate` uses checked-in EF migrations; it does not use `EnsureCreated`. Run migrations with a schema-capable account and use an application account with normal data permissions afterward. Roles and missing system settings are created at normal startup. Bootstrap configuration only creates a missing administrator; it never resets an existing account.

Set `Security__RequireHttps=true` behind your HTTPS deployment. Back up MySQL and `App_Data/documents`, and retain Data Protection keys across application restarts.

## First-use workflow

1. **Admin:** create branches, departments, optional sub-departments, devices, leave types, work calendars, and effective deduction rates. Set working weekdays explicitly for your organization. Set approval and bank-visibility policy in Settings.
2. **HR:** use the six-step employee wizard: Assignment → Personal → Education → Experience → Salary → Documents. An initial employee login is created in step one, with mandatory password change. Payroll excludes registrations until completion. Set grade, contract dates, shift and employment status from the employee record. Use dated transfers to retain organization history.
3. **Attendance:** import individual punches or UTF-8 CSV with the exact header `DeviceId,Pin,Timestamp`. Example: `1,D001,2026-10-22T08:00:00+03:00`. Times without offsets are device-local; ISO timestamps with offsets convert to the mapped employee's branch timezone. Duplicates are ignored. Unmapped local punches enter the PIN queue. Import a maximum 20,000 rows / 2 MB per CSV. Rebuild summaries after calendar or leave changes. Audited manual corrections preserve raw punches.
4. **Leave:** employees see their balances and request full or partial days, with supporting documents when required. Holidays and non-working weekdays do not consume leave. HR/Management decides requests. Optional manager-first approval adds a direct-report inbox in employee self-service. Annual credits can override type defaults per year.
5. **Payroll:** choose dates, branch, and the exact working-day count. Calculate a preview; pending leave and unresolved incomplete punches block it. Review per-day deductions, waive selected amounts, or override net lines with reasons. Approve review, lock, publish payslips, and optionally mark paid. Source changes between calculation and locking require recalculation. Reopening requires a reason and unpublishes previous slips. Earlier payroll revisions are retained.
6. **Reports:** filter employees, hires/leavers, attendance, leave, payroll, deductions, and bank lists by organization and dates. Export CSV or `.xlsx`. Payslips and reports support browser printing / Save as PDF. HR can publish already locked payslips from Reports; HR cannot calculate, review or lock payroll.

Admin can add/remove roles, disable accounts, and restrict staff to specific branches. No selected branches means company-wide access. Employees see only their linked employee record. Bank exports are limited to HR/Admin unless Admin explicitly enables Management access. Support sessions are audited, last 15 minutes, and are read-only.

## Tests

```sh
dotnet test HrSystemDhg.Tests --no-restore
# Optional real database integration suite. Use a dedicated database named dhg_integration.
export DHG_TEST_CONNECTION='Server=localhost;Database=dhg_integration;User=YOUR_TEST_USER;Password=YOUR_TEST_PASSWORD;'
dotnet run --project HrSystemDhg.IntegrationTests
```

The integration runner applies migrations and creates synthetic fixtures. It refuses databases whose connection string does not name `dhg_integration`. It never drops a database. The suite covers approved/unpaid/partial leave, holidays, effective salary, late deductions, idempotent imports, overnight shifts, review fingerprints, locked payroll, revision history, and row-level branch/employee access. See [docs/VALIDATION.md](docs/VALIDATION.md) for recorded results.

## Optional synthetic demo

Use a **separate database** containing `preview` or `demo` in its name, and set `ASPNETCORE_ENVIRONMENT=Development` and `Demo__Password` to a temporary password you choose. Migrate, then run:

```sh
dotnet run --project HrSystemDhg.Web -- --demo
```

This creates 12 fictional employees and four accounts: `admin@demo.garowe.local`, `hr@demo.garowe.local`, `management@demo.garowe.local`, and `employee@demo.garowe.local`, using `Demo__Password`. Demo accounts intentionally skip first-password-change for UI testing. Normal registered and bootstrap accounts always require it. Demo seeding refuses production environments and non-demo database names.

## Project layout

- `HrSystemDhg.Web/Areas/Admin`, `Hr`, `Management`, `Identity`: Razor Areas with server-side role authorization.
- `HrSystemDhg.Web/Pages/Me`: employee self-service, manager inbox and printable payslips.
- `Data`, `Models`, `Services`, `Migrations`: Identity/domain persistence and business services.
- `App_Data/documents`: protected uploads served only through an authorized download handler; never static web files.
- `HrSystemDhg.Tests`: xUnit tests. `HrSystemDhg.IntegrationTests`: real-MySQL checks. `HrSystemDhg.Checks`: original lightweight policy checks.

## Agreed policies

Manual attendance import replaces physical ZKTeco/API sync per the user's instruction. .NET 8 and a single Web project are retained. Payroll uses the salary at period end, prorates hires/leavers, and records organization at period end; daily attendance uses dated assignments and effective work rules. Calendar dates and custom cutoff periods are both supported. USD is the default salary currency, and totals are grouped by currency. Statutory tax/pension is outside v1, as allowed by the source specification. Net pay is floored at zero. No payroll or raw punch records are hard-deleted.

## Private local database configuration

In Development, the app reads `HrSystemDhg.Web/App_Data/local-settings.json` when present. This file is ignored by Git and excluded from source archives and Docker images. Environment variables take precedence. The HTTP launch profile uses `http://127.0.0.1:5189`.

A MySQL server connection must be verified before migrations are applied. MySQL Workbench is a client; the HR application connects directly to the same server and database.

## Password recovery

Password fields provide Show/Hide buttons. Use **Forgot password?** on the login page to request a reset. Reset links expire after one hour and can be used once. Responses do not disclose whether an account exists; recovery endpoints allow 10 requests per IP per 15 minutes. Successful resets invalidate previous sessions.

In Development without SMTP, reset messages are saved privately in `HrSystemDhg.Web/App_Data/password-reset-outbox`. Signed-in administrators can retrieve recent links from **Password reset requests**. This page is unavailable outside Development. Local files are excluded from source packages.

For email delivery configure `Smtp__Host`, `Smtp__Port` (default 587), `Smtp__From`, and optional `Smtp__Username` / `Smtp__Password` through environment variables. Set `PasswordReset__PublicOrigin` to the canonical HTTPS origin in production. No email service is configured by default.

## Administration workspace

Navigation groups workspace, people and attendance, finance and reporting, administration, and security/support. System settings links to organization structure, leave policies, working rules, devices, audit, and users. General preferences control the organization name, future hires, manager approvals, and Management bank exports.

Users & permissions supports email search, account creation for all four roles, role grants/revocations, revoking all roles, enabling/disabling accounts, and branch restrictions. Operational roles require a linked employee. Role permissions are documented on the page. Administrators cannot disable themselves, revoke their own Admin role, or remove all of their own roles. Role changes invalidate existing sessions within one minute; disabled accounts are blocked on the next request. Changes are audited. Organizational records can be deactivated through their edit pages to preserve HR history.

Validation: clean solution build; 26 unit tests and 51 isolated MySQL integration checks passed, including self-access safeguards and all-role revocation with security-stamp rotation. Desktop and mobile administration layouts inspected in the browser.

Every navigation group is expandable. Expand all / Collapse all controls all groups; group preferences persist in the browser. The current page group opens automatically. Native details controls support keyboard navigation. Navigation redesign verified with a clean build, expand/collapse interaction checks and reload persistence in the browser.

## Employee registration and optional onboarding

Registration validates required assignment details, field lengths, email, hire date, duplicate employee/account identifiers and password rules. Empty device PINs are normalized before duplicate checks. The initial save creates the employee, assignment and linked login in a transaction.

Personal details, education, experience and salary steps have explicit Skip controls. Nationality and marital status use selection lists, and dates use date pickers. Server validation rejects invalid selections, future birth dates, birth dates after hire, negative dependents, duplicate IDs, incomplete education/experience and reversed date ranges. Blank optional personal strings are normalized to avoid database NOT NULL failures.

Finish & save employee accepts missing documents and salary; the employee is retained with a success message. Without a published salary the employee remains in onboarding and is excluded from payroll. Add a salary and finish onboarding later. Existing saved sections can be revisited from the step links.

Validation: 26 unit tests plus 58 isolated MySQL checks passed, including creation of an employee/assignment/login, saving blank personal fields, skipping incomplete education, rejecting invalid marital status/future birth dates, and final save without salary. Personal selectors and Skip controls verified in the browser.
