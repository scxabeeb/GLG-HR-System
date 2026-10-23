# HR Management System — Requirements Specification

**Project:** HR System DHG  
**Stack:** ASP.NET Core **Razor Pages** (C#), **Entity Framework Core**, **MySQL**  
**Purpose:** Blueprint for building a multi-role HR platform covering employee records, organization structure, attendance (ZKTeco), leave, salary, and payroll with automatic deductions.

---

## 1. Product overview

The system manages the full employee lifecycle and monthly payroll. Registration is tied to **organization structure** (department, sub-department, and branch). Attendance is imported from **ZKTeco** devices. Unexcused absence or late time can reduce pay using **rates and hour rules** that Admin owns; Management may apply those deductions when generating payroll. **Approved leave** is not treated as absence and must not trigger deduction.

Four distinct logins:

| Role | Primary job |
|------|-------------|
| **Admin** | Configure the system: users, branches, departments, pay rates, deduction rules, ZKTeco connection, and audit. |
| **Management** | Oversee operations, approve exceptions, and **generate payroll** (including applying deductions). |
| **HR** | Register and maintain employees, education, work history, salary records, and leave. |
| **Employee** | View own profile, attendance, leave balance/requests, payslips. |

---

## 2. Technology stack (locked)

Build this as an **ASP.NET Core Razor Pages** web app. Persist all data with **EF Core** against **MySQL**. Do not use MVC controllers, Blazor, or SQL Server unless a later decision changes this file.

### 2.1 Runtime and packages

| Piece | Choice |
|-------|--------|
| Language | C# |
| UI | Razor Pages (`*.cshtml` + `PageModel`) |
| Auth | ASP.NET Core Identity (cookie), four roles: `Admin`, `Management`, `HR`, `Employee` |
| ORM | Entity Framework Core |
| Database | MySQL 8.x |
| MySQL provider | **Pomelo.EntityFrameworkCore.MySql** |
| Identity store | `Microsoft.AspNetCore.Identity.EntityFrameworkCore` on the same MySQL database |
| Background jobs | `IHostedService` / hosted service for ZKTeco punch sync |
| Files | `wwwroot/uploads` (or local `App_Data`) for photos and certificates in v1 |

Suggested NuGet packages:

- `Pomelo.EntityFrameworkCore.MySql`
- `Microsoft.EntityFrameworkCore.Design`
- `Microsoft.EntityFrameworkCore.Tools`
- `Microsoft.AspNetCore.Identity.EntityFrameworkCore`
- `Microsoft.AspNetCore.Identity.UI` (scaffold login/register, then customize)

Target: **.NET 8** (LTS) unless the machine already uses .NET 9 — pick one and keep it for the whole solution.

### 2.2 Solution layout

```
HrSystemDhg/
  HrSystemDhg.Web/                 // Razor Pages host
    Areas/
      Admin/Pages/
      Management/Pages/
      Hr/Pages/
      Identity/Pages/              // login, logout, change password
    Pages/
      Index.cshtml                 // role redirect after login
      Me/                          // employee self-service
    Program.cs
    appsettings.json
  HrSystemDhg.Infrastructure/      // optional; can live in Web for v1
    Data/HrDbContext.cs
    Data/Configurations/           // IEntityTypeConfiguration<T>
    Identity/ApplicationUser.cs
    Services/                      // payroll, attendance, ZkTeco client
  HrSystemDhg.sln
```

If keeping a **single Web project** for speed, put `Data/`, `Models/`, `Services/` inside `HrSystemDhg.Web`. Areas still separate the four modules.

### 2.3 Razor Pages and areas

| Area / folder | Authorize role | Examples |
|---------------|----------------|----------|
| `/Identity/Account/Login` | Anonymous | Cookie login |
| `/Admin/...` | `Admin` | Users, branches, departments, rates, ZKTeco, leave types |
| `/Management/...` | `Management` | Dashboard, payroll generate, exceptions |
| `/Hr/...` | `HR` | Employee register, salary, leave inbox |
| `/Me/...` | `Employee` | Profile, attendance, leave, payslips |

Use `[Authorize(Roles = "...")]` on `PageModel` (and area `_ViewStart` conventions). After login, `Pages/Index` redirects by role.

Page conventions:

- List + Create + Edit + Details per entity (e.g. `Hr/Employees/Index`, `Create`, `Edit/{id}`).
- Employee registration as a **wizard** of Razor Pages sharing `EmployeeId` in the route (or TempData until the first save).
- Cascading dropdowns: Branch → Department → Sub-department via a Razor Page **handler** (`OnGetDepartmentsAsync?branchId=`) returning JSON, or a small page handler — not a separate MVC API project.
- Tag Helpers for forms, validation (`asp-for`, `asp-validation-for`), and anti-forgery.

### 2.4 MySQL + EF Core

**Connection** (`appsettings.json` / User Secrets, never commit passwords):

```json
"ConnectionStrings": {
  "DefaultConnection": "Server=localhost;Port=3306;Database=hr_system_dhg;User=hr_app;Password=***;TreatTinyAsBoolean=true"
}
```

**Registration** (`Program.cs`):

```csharp
var cs = builder.Configuration.GetConnectionString("DefaultConnection")!;
var serverVersion = ServerVersion.AutoDetect(cs);

builder.Services.AddDbContext<HrDbContext>(options =>
    options.UseMySql(cs, serverVersion));

builder.Services.AddDefaultIdentity<ApplicationUser>(options =>
{
    options.SignIn.RequireConfirmedAccount = false;
    options.Password.RequiredLength = 8;
})
.AddRoles<IdentityRole>()
.AddEntityFrameworkStores<HrDbContext>();

builder.Services.AddRazorPages();
```

**`ApplicationUser`:** extend `IdentityUser` with `EmployeeId` (nullable FK). Pure Admin accounts have `EmployeeId = null`. Employees, HR, and Management users link to an `Employee` row.

**`HrDbContext`:** `IdentityDbContext<ApplicationUser>` plus `DbSet<>` for every domain entity in section 11.

Rules:

- Fluent API in `IEntityTypeConfiguration<T>` (indexes, unique employee number, unique work email, unique ZK PIN).
- Money as `decimal(18,2)`; dates as `date` / `datetime(6)`.
- Soft-delete `IsActive` on org units; do not hard-delete payroll or punches.
- **Migrations** only: `dotnet ef migrations add InitialCreate --project ...` then `dotnet ef database update`. No hand-edited production schema.
- Seed four roles in `Program.cs` / `IHostedService` on startup if missing.
- MySQL charset `utf8mb4` (Pomelo default) for Somali/Arabic later.

### 2.5 How modules map to pages and EF

| Module | Razor Pages (examples) | EF entities |
|--------|------------------------|-------------|
| Org | `Admin/Branches`, `Departments`, `SubDepartments` | `Branch`, `Department`, `SubDepartment` |
| Employee | `Hr/Employees/Create` wizard | `Employee`, `EmployeeAssignment`, `Education`, `WorkExperience`, `EmployeeDocument` |
| Salary | `Hr/Employees/Salary` | `SalaryVersion` |
| Attendance | `Hr/Attendance`, `Admin/ZkTeco` | `ZkDevice`, `ZkPunch`, `AttendanceDay`, `WorkRule`, `DeductionRule` |
| Leave | `Me/Leave/Create`, `Hr/Leave/Approve` | `LeaveType`, `LeaveBalance`, `LeaveRequest` |
| Payroll | `Management/Payroll/Generate` | `PayrollPeriod`, `PayrollLine`, `Payslip` |

ZKTeco sync is a **service** (`IZkTecoSyncService`) called from a hosted background worker and from an Admin page **Sync now** handler (`OnPostSyncAsync`). It writes `ZkPunch` rows, then rebuilds `AttendanceDay`. Payroll `PageModel` calls `IPayrollService.GenerateAsync(periodId, applyDeductions, overrides)`.

### 2.6 Create the project (when implementation starts)

```bash
dotnet new sln -n HrSystemDhg
dotnet new webapp -n HrSystemDhg.Web --auth Individual
dotnet sln add HrSystemDhg.Web
dotnet add HrSystemDhg.Web package Pomelo.EntityFrameworkCore.MySql
```

Replace the default SQLite/SQL Server Identity store with Pomelo + `HrDbContext` on MySQL. Create database `hr_system_dhg` and a dedicated MySQL user with DML rights.

---

## 3. Organization structure

Every employee is assigned a location in the org tree. Registration **cannot complete** without a valid assignment.

```
Company
 └── Branch (office / site)
      └── Department
           └── Sub-department (optional)
                └── Employee
```

### 3.1 Entities

- **Branch:** name, code, city, address, timezone (for punch times), active flag.
- **Department:** name, code, parent branch, head (manager employee, optional), active flag.
- **Sub-department:** name, code, parent department, active flag. Optional; some departments have none.
- **Cost center / GL code (optional):** for payroll export.

### 3.2 Rules

- An employee belongs to **one branch**, **one department**, and **zero or one sub-department**.
- Sub-department must belong to the selected department; department must belong to the selected branch.
- Transfers change branch/department/sub-department with an **effective date** and keep history.
- Inactive org units cannot receive **new** registrations; existing employees remain until transferred.

---

## 4. Module 1 — Employee registration

HR (and Admin) create the employee record. The employee login is created after a unique employee number and work email (or username) exist.

### 4.1 Personal details

- Employee number (unique, system-generated or HR-entered)
- Full name, gender, date of birth, nationality
- National ID / passport
- Phone, personal email, work email
- Address, emergency contact (name, relation, phone)
- Photo
- Marital status, dependents (optional)
- Hire date, employment type (permanent / contract / intern), status (active / probation / suspended / terminated)
- Reporting manager (employee in same or parent org unit)

### 4.2 Education

Multiple rows:

- Institution, qualification, field of study
- Start / end year (or “in progress”)
- Grade / GPA (optional)
- Certificate attachment (optional)

### 4.3 Work and experience

**Internal work (current role):**

- Job title, grade/level
- Branch, department, sub-department
- Contract start / end (if contract)
- Work schedule / shift group (links to attendance hours)

**Prior experience (multiple rows):**

- Employer, job title, country
- From / to dates
- Responsibilities (short text)
- Reference (optional)

### 4.4 Documents (optional but recommended)

- Contract, ID copy, certificates
- Stored per employee with type and expiry date (for ID/visa)

### 4.5 Registration workflow

1. HR selects **branch → department → sub-department (if any)**.
2. HR fills personal, education, experience, and current job.
3. HR (or Admin) attaches **salary registration** (section 4) before the employee is “payroll-ready”.
4. System creates **Employee** login with default password (must change on first login) or invite link.
5. Status becomes **Active** (or **Probation** if configured).

### 4.6 Validation

- Duplicate employee number, national ID, or work email is rejected.
- Hire date cannot be after today unless future-dated hire is allowed by Admin.
- At least one current job assignment with branch + department is required.

---

## 5. Module 2 — Salary registration

Salary is a **versioned** record, not a single field on the employee. Changes keep history (effective date).

### 5.1 Fields

- Basic salary
- Allowances (housing, transport, other — each named, amount or formula)
- Overtime eligible (yes/no)
- Bank name, account number, account name (for payment file)
- Currency
- Pay frequency (monthly)
- Effective from / to

### 5.2 Rules

- Only **HR** and **Admin** create or change salary; Management can **view** (or view summary, if Admin hides bank details).
- Employees see only their **own** current salary and payslips, not history of unpublished changes if HR marks a draft.
- A payroll period uses the salary version **effective on the period end date**.

---

## 6. Module 3 — Attendance (ZKTeco)

### 6.1 Integration

- Connect to **ZKTeco** (ZKTeco / ZKTeco-compatible devices: push SDK, ADMS, or BioTime / ZKBioTime API — implement against the actual device/software in use).
- Map device **user PIN / badge ID** ↔ **employee number**.
- Sync punch **in/out** (and break if the device provides it) into the HR system on a schedule (e.g. every 15 minutes) and on demand (“Sync now”).
- Store raw punches and a **daily attendance summary** per employee.

### 6.2 Daily summary (computed)

For each employee and calendar day:

- First in, last out
- Worked hours
- Expected hours (from shift / Admin rules)
- Late minutes, early leave minutes, missing punch
- Status: Present | Late | Absent | Weekend | Holiday | **On leave** | Incomplete punch

### 6.3 Admin-managed rates and hours

Admin configures:

- **Standard work hours** per day / week (global default)
- Per **branch**, **department**, or **shift**: start time, end time, grace minutes, break duration
- **Late deduction:** e.g. after grace, deduct X amount or Y% of daily rate per Z minutes (or per hour)
- **Absence deduction:** full daily rate (basic ÷ working days in month) or custom rate
- **Incomplete punch** policy: treat as absent, half day, or flag for HR
- **Overtime:** extra hours beyond expected; rate multiplier (optional)

Admin can **change** these rules at any time. New rules apply from an **effective date**; already **finalized** payroll periods are not recalculated unless Management reopens them.

### 6.4 Leave vs deduction (critical)

If the employee has **approved leave** covering that day (full day or hours):

- Attendance status is **On leave** (not Absent).
- **No absence/late deduction** for the leave portion.
- Leave hours do not count as worked hours for overtime.

Unapproved absence still deducts.

---

## 7. Module 4 — Leave management

### 7.1 Leave types (Admin configurable)

Examples: Annual, Sick, Unpaid, Maternity/Paternity, Emergency, Compensatory.

Each type:

- Paid / unpaid
- Accrual (days per year) or fixed balance
- Requires document (e.g. sick note after N days)
- Min/max consecutive days
- Gender or employment-type restriction (optional)

### 7.2 Employee flow

- See balance, history, and upcoming leave.
- Submit request: type, from/to (or hours), reason, attachment.
- Cannot overlap another approved leave (warning or block).

### 7.3 Approval

- **Reporting manager** and/or **HR** approve/reject (Admin sets chain).
- Management can override.
- On approval, attendance engine marks those days **On leave**.

### 7.4 Unpaid leave

- Unpaid leave **does** reduce payroll (pro-rata), but through the **leave/pay** rule, not the **absence** rule — so it is not double-counted with ZKTeco absence.

---

## 8. Module 5 — Payroll generation

### 8.1 Period

- Monthly (default): from date–to date, working days, public holidays.
- Status: Draft → Calculated → Reviewed → **Generated / Locked** → Paid (optional).

### 8.2 Who does what

| Step | Admin | Management | HR | Employee |
|------|-------|------------|----|----------|
| Configure rates/hours | Yes | View | View | No |
| Fix attendance / missing punches | — | Yes | Yes | Request correction |
| Approve leave before cutoff | — | Yes | Yes | Request |
| **Generate payroll** | Can | **Primary** | Assist | No |
| **Apply deductions** on generate | Can | **Yes** | No (unless granted) | No |
| Publish payslips | Can | Yes | Yes | View own |

### 8.3 Calculation (per employee)

```
Gross = Basic + Allowances + Approved overtime
− Attendance deductions (late / absence)   [only if Management applies them]
− Unpaid leave (pro-rata)
− Statutory / other deductions (if configured later)
= Net
```

**Apply deductions when generating:** Management (or Admin) chooses:

- Apply **all** auto-calculated ZKTeco deductions, or
- Apply **selected** employees / days, or
- Override a line (reason required, audited).

If they generate **without** applying attendance deductions, net pay ignores late/absence amounts for that run (still stored as “waived”).

### 8.4 Outputs

- Payroll register (by branch / department / sub-department)
- Payslip per employee (PDF or in-app)
- Bank payment list
- Deduction report (what was applied vs waived)

### 8.5 Locking

After generate + lock, punches and leave in that period cannot silently change net pay. Corrections require **reopen period** (Admin/Management) and a new generation.

---

## 9. Roles and access

### 9.1 Admin

- Create/disable users and assign roles.
- Branches, departments, sub-departments, holidays, shifts.
- Deduction **rates**, expected **hours**, grace, overtime multipliers.
- ZKTeco connection, device list, PIN mapping.
- Leave types and approval policy.
- Full audit log.
- All data; can impersonate support actions without using an employee password.

### 9.2 Management

- Dashboards: headcount, attendance %, payroll cost by branch/department.
- Approve leave (per policy).
- Review attendance exceptions.
- **Generate payroll** and **apply or waive deductions**.
- Cannot change global rates (Admin), unless also given Admin.
- Scope option: all company **or** assigned branches only.

### 9.3 HR

- Employee registration and updates (personal, education, work, experience).
- Salary registration and versions.
- Leave processing, document checks.
- Attendance cleanup (map unmapped ZK users, fix missing punches with reason).
- Prepare payroll data; typically does **not** lock payroll or change deduction **rates**.
- Sees employees in authorized branches/departments.

### 9.4 Employee

- Own profile (request correction for personal data; HR applies).
- Own attendance calendar and punches.
- Leave request and balance.
- Own payslips.
- No access to other employees’ salary or attendance.

### 9.5 Authentication (Identity)

- One site, cookie auth: `/Identity/Account/Login`.
- `ApplicationUser` uses **email or employee number** as username.
- Roles seeded: `Admin`, `Management`, `HR`, `Employee`.
- `[Authorize(Roles = "HR,Admin")]` on HR pages; Management cannot open Admin rate pages.
- First login: `MustChangePassword` flag on `ApplicationUser`; redirect to change-password page.
- Cookie sliding expiration; lockout via Identity (`MaxFailedAccessAttempts`).
- Anti-forgery on all POSTs (Razor Pages default).

---

## 10. Suggested information architecture (Razor routes)

```
/Identity/Account/Login
/Admin/Users, /Admin/Branches, /Admin/Departments, /Admin/WorkRules, /Admin/ZkTeco
/Management/Dashboard, /Management/Attendance/Exceptions, /Management/Payroll/Generate
/Hr/Employees, /Hr/Employees/Create, /Hr/Leave, /Hr/Attendance/Mapping
/Me/Profile, /Me/Attendance, /Me/Leave, /Me/Payslips
```

Reusable screens:

- Employee form (wizard: Assignment → Personal → Education → Experience → Salary → Documents)
- Org picker (branch → department → sub-department)
- Attendance calendar
- Leave request / approval
- Payroll run wizard (period → preview deductions → apply/waive → generate → lock)

---

## 11. Core data model (EF Core / MySQL)

Identity tables (AspNetUsers, AspNetRoles, …) plus:

| Table (MySQL) | Entity |
|---------------|--------|
| `branches` | `Branch` |
| `departments` | `Department` (FK Branch) |
| `sub_departments` | `SubDepartment` (FK Department) |
| `employees` | `Employee` |
| `employee_assignments` | `EmployeeAssignment` (dated org placement) |
| `educations` | `Education` |
| `work_experiences` | `WorkExperience` |
| `employee_documents` | `EmployeeDocument` |
| `salary_versions` | `SalaryVersion` |
| `shifts` / `work_rules` | `Shift`, `WorkRule` |
| `deduction_rules` | `DeductionRule` (effective-dated) |
| `zk_devices`, `zk_punches` | `ZkDevice`, `ZkPunch` |
| `attendance_days` | `AttendanceDay` |
| `leave_types`, `leave_balances`, `leave_requests` | `LeaveType`, `LeaveBalance`, `LeaveRequest` |
| `payroll_periods`, `payroll_lines`, `payslips` | `PayrollPeriod`, `PayrollLine`, `Payslip` |
| `audit_logs` | `AuditLog` |

`ApplicationUser.EmployeeId` → `employees.Id` (optional). Unique indexes: `Employee.EmployeeNumber`, `Employee.WorkEmail`, `ZkPunch` (`DeviceId`, `Pin`, `PunchTime`).

---

## 12. Business rules (summary)

1. No employee without **branch + department** (sub-department if the department requires it).
2. Payroll uses salary **effective** in the period.
3. ZKTeco punches drive attendance; unmapped PINs appear in an HR queue.
4. **Approved leave ⇒ no attendance deduction** for those days/hours.
5. Unpaid leave reduces pay once (leave rule), not again as absence.
6. Only **Admin** edits rates and expected hours (with effective dates).
7. **Management** decides whether auto-deductions are **applied** on payroll generate.
8. Locked payroll does not change when new punches arrive.
9. Employees see only their own confidential data.

---

## 13. Edge cases

- Night shift crossing midnight: punches belong to the shift’s work date.
- Multiple punches: use first in / last out unless Admin enables pair matching.
- Device clock vs branch timezone mismatch.
- Employee transferred mid-month: split attendance/payroll by assignment dates (or policy: pay on end-of-month assignment).
- Hired or terminated mid-month: pro-rate basic and expected days.
- Duplicate ZK PIN mapped to two employees: block sync until HR fixes.
- Leave pending at payroll cutoff: Management generates with “pending leave as absent” **or** wait; document the chosen default (recommend: **block generate** if pending leave exists for the period).
- Public holiday + leave: do not deduct; do not consume leave if Admin says holidays are not leave.
- Weekend work: overtime vs normal — Admin flag per branch.
- Salary change mid-month: use version at period end **or** split (recommend: period-end version unless HR creates a split rule).

---

## 14. Reports

- Employee list by branch / department / sub-department
- New hires / leavers
- Daily and monthly attendance, late, absence
- Leave register and balances
- Payroll register, deduction register, waived deductions
- ZKTeco unmapped users and failed syncs

---

## 15. Non-functional

- `[Authorize]` on every PageModel except login; authorize by role, not by hiding links.
- Parameter binding: always load `Employee` in the handler and check the current user may see that row (Employees: own `EmployeeId` only).
- Bank fields: restrict display; do not log account numbers.
- Audit: who changed salary, rates, applied/waived deduction, leave approval (`AuditLog` via EF `SaveChanges` interceptor or explicit writes).
- ZKTeco sync retries and last-success timestamp on the Admin page.
- `dotnet ef` migrations in source control; MySQL backup of punches and payroll.
- UI in English; `utf8mb4` for Somali/Arabic later.
- Bootstrap (default Razor Pages template) or a simple layout; desktop-first, usable on phone for `/Me`.
- Use `async` EF calls (`ToListAsync`, `SaveChangesAsync`) on all page handlers.

---

## 16. Implementation phases

| Phase | Scope |
|-------|--------|
| **P0** | `dotnet new webapp`, Pomelo + MySQL, `HrDbContext`, Identity roles, Areas, first migration |
| **P1** | Org CRUD pages, employee registration wizard, `/Me` profile |
| **P2** | Salary versions, leave types/requests/approvals, leave vs calendar |
| **P3** | ZKTeco sync hosted service, attendance days, Admin hours/rates |
| **P4** | Payroll generate PageModel, apply/waive deductions, payslips, lock period |
| **P5** | Dashboards, Excel/PDF export, audit polish, mid-month transfer/pro-rate |

Tests: `HrSystemDhg.Tests` with xUnit; in-memory or Testcontainers MySQL for EF queries (payroll deduction vs leave).

---

## 17. Acceptance criteria (high level)

- HR can register an employee under a chosen branch, department, and optional sub-department, with education and experience rows.
- Admin can set daily hours and deduction rates and change them with an effective date.
- Punches from ZKTeco appear on the employee attendance calendar after mapping.
- An employee on **approved leave** is not marked absent and is **not** deducted for those days.
- Management can generate payroll and choose to apply automatic deductions; waived items are listed.
- Each of the four roles lands on the correct module and cannot access another role’s restricted screens.
- Employee sees only their own data.

---

## 18. Open decisions (confirm during build)

1. Exact ZKTeco product: device-only (standalone), ZKBioTime, or ADMS cloud.
2. Payroll month: calendar month vs custom cutoff (e.g. 26th–25th).
3. Leave approval: manager then HR, or HR only.
4. Mid-month transfer: split pay vs end-of-month org unit.
5. Currency and statutory deductions (tax, pension) — in or out of v1.
6. .NET 8 vs .NET 9 (default **.NET 8 LTS**).
7. Single Web project vs Web + Infrastructure class library (default **single Web project** for v1).

---

*This file is the source of requirements for implementation. Stack is Razor Pages + EF Core + MySQL. Update it when a decision in section 18 is closed.*


## 19. Implementation decisions recorded on 22 October 2026

- Manual punch/CSV import replaces physical device/API synchronization, as explicitly requested by the user.
- .NET 8; one Web project with Razor Areas; EF Core/Pomelo targeting MySQL 8.
- Configurable date cutoffs and optional branch-specific payroll periods.
- HR/Management approval by default; optional reporting-manager-first review.
- Period-end salary version and organization snapshot; employment dates prorate pay.
- USD default with per-salary currency and separate currency totals; statutory tax/pension remains outside v1.
- Configurable working weekdays, holidays, incomplete-punch policy, overtime approval and effective rates.
- Payroll transitions: Draft → Calculated → Reviewed → Locked → Paid; publishing requires a locked or paid period. Reopening requires a reason and withdraws slips.
- Default leave entitlement is fixed annual credit; optional monthly accrual and annual employee credit overrides.
- In-app printable payslips/reports provide PDF through browser printing; CSV and .xlsx are direct downloads.
- Local validation used MySQL 9.4; the MySQL 8 Compose deployment is supplied separately.
