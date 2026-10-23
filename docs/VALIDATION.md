# Validation — 22 October 2026

- Solution build: **passed, zero warnings and zero errors**.
- xUnit: **19 passed**, zero failed/skipped.
- Live MySQL integration runner: **36 passed**.
- Excel export downloaded through the browser and verified as a valid Open XML ZIP package with 37 rows and no formula cells.

## Browser workflows checked

- Admin, Management and HR sign-ins land on their role-specific dashboards. Employees land on their own workspace.
- Employee access to Admin rates is denied. HR access to payroll generation is denied. Management access to Admin rates is denied.
- HR employee directory, record details and the six-step onboarding form render.
- Demo payroll: create period, calculate, recalculate, approve review, lock, publish. The linked employee can open their own published payslip.
- Employee profile and balances render without unrelated form-validation errors.
- Leave-balance report displays annual/sick balances and unlimited unpaid leave correctly.
- Narrow-screen navigation opens/closes and the employee page uses the full content width. Desktop dashboards and payroll were also inspected.

## Regression cases covered

Approved paid leave, unpaid leave deducted once, partial-day unpaid leave versus absence, effective salary selection, holidays, overnight punches and overnight partial leave, overlap validation, late grace, duplicate imports, source changes blocking lock, fingerprints surviving fresh requests/decimal database normalization, waived deductions, reasoned net overrides, locked net overrides rejected, locked payroll remaining unchanged after new punches, preserved payroll revisions, branch scope with a future transfer, own-record filtering, CSV formula escaping, safe XLSX text cells, and support-session expiry independent of cookie renewal.

## Environment

The live integration and synthetic preview databases run in an isolated local **MySQL 9.4** instance with TCP networking disabled. The application provider/configuration and Docker Compose target **MySQL 8** as specified. The supplied MySQL 8 Docker deployment has not been executed because Docker Desktop's daemon was unavailable.

No real employee records were used. The local demo uses 12 synthetic employees. Production bootstrap/registered accounts require a password change; demo-only accounts skip that step to support UI testing.
