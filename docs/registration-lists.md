# Registration list pagination

All registration lists use a default of 20 records, with 20, 50, 100, 200 and All choices, a matching-record count and page controls.

Employee directory and daily attendance paginate database queries through `ListPager`. Search, department/status or employee/date filters are applied before counting and paging. Employee details and terminal-command redirects preserve the selected page. Documents and command history load only for employees on that page.

Other existing tables and card registers use `registration-pagination.js`, enabled explicitly with `data-registration-list`. The layout loads it once. It preserves the server's query filters, retains checkbox selections and input values, supports independent lists on one page, remembers the selected size, and exposes the current page in the URL. Hash links reveal the page containing their target. Print output includes every authorized matching record rather than silently printing only one page.

Covered registers:

- Organization: branches, departments, sub-departments, branch trees and inline registration lists.
- Administration: users, both registered-device tables, device configurations, leave types, report definitions, work rules, shifts and holidays.
- HR: employees, attendance, salary versions, leave requests, employee leave balances, balance credits and corrections.
- Payroll: employee salary/payroll register and payroll periods; financial amounts and workflow logic are unchanged.
- Assets: asset register and assignment history.
- Fleet: vehicles, service registrations, trips and expense records.
- Operations: individual tasks, team tasks, subtask assignments, meetings and saved recordings.
- Personal workspace: agenda items, leave requests, published payslips and correction requests.

A team task remains one list item with all of its subtasks. Organization department rows stay grouped with their sub-departments. Reference guides, aggregate totals, dropdown options, biometric dialogs and registration forms are not paginated.

When adding another register, mark the table or collection with a descriptive `data-registration-list` value. Collections can specify `data-list-items`; grouped tables can specify `data-row-start`. Return the full authorized matching dataset for these collections so All is complete. Large new datasets should use database pagination as the employee and attendance pages do. Do not use client pagination as an authorization boundary.

Operational diagnostics retain their separate safety/history limits; export limits are unchanged. Regression coverage includes database page boundaries, 205 employees, 601 attendance summaries, table/card controls, grouped rows, target links, saved selections, empty views and complete print output.
