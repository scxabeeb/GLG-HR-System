# Garowe City workspace design

The web application uses one shared shell for its modules. `workspace-polish.css` follows the existing civic and portal styles so page workflows, forms and print exports keep their behavior.

- Login uses the configured organization name and seal, a clear account field, password visibility, recovery, Caps Lock feedback and submission feedback.
- The Admin, HR and Management dashboards share the operations overview. Metric cards and module links respect both module and submenu access. Users with denied task-management access see only their permitted task workspace.
- Navigation keeps the existing HR, tasks, meetings, assets and fleet groups, plus main settings and main reports. Search opens matching nested submenus. The mobile drawer supports keyboard focus, Escape and returning to the menu button.
- Tables use consistent headings, row spacing and hover states. A sideways-scroll hint and keyboard focus appear only for overflowing tables. Existing record pagination, filters, actions and export handlers are retained.
- Forms share labels, input spacing, focus and validation styles. Phone inputs use 16-pixel text to avoid browser zoom. Import/export tools remain separate from registration forms.
- Main reports groups available reports by module. Report navigation respects individual submenu access. Filters and CSV, Excel and PDF actions remain in their existing workflow. Print styles suppress navigation and scrolling hints.

## Verification

The existing isolated native/browser fixture can additionally run `scripts/check-workspace-design.cjs` by setting `DHG_WORKSPACE_DESIGN_CHECK=1` alongside `DHG_SETTINGS_BROWSER_CHECK=1`. The visual checks use disposable local data and Chrome/Playwright, validate viewport widths from 320 to 1440 pixels, and visit the main employee, attendance, leave, salary, payroll, task, meeting, asset, fleet, settings and report pages. `DESIGN_SCREENSHOTS` optionally selects an output folder.

The browser suite also audits rendered navigation, registration, permissions, report exports, payroll review controls and connector forwarding. This is a regression check of those workflows, not a claim that every possible production action or terminal firmware has been exercised.

## Display preferences

The language selector and sun/moon button are available on sign-in and workspace pages. English/Somali and light/dark preferences persist in the current browser. Somali translates navigation, dashboard labels, common forms, report headings and shared actions. Employee names, entered values, stored records and exported data are preserved. Translation uses text nodes rather than injected HTML, and option values remain unchanged. The server continues to enforce the same permissions in either language.

## Administrator employee reset

`/Admin/DataReset` is restricted to Admin with antiforgery protection. It previews affected counts, saves a private server backup (plus uploaded employee documents), and requires an exact confirmation phrase. The backup expires for reset use after 15 minutes; a changed data fingerprint requires a fresh backup. Deletions run within a database transaction. Admin and General Secretary accounts, organization configuration, device pairing/stamps and other module records remain. Asset/vehicle employee links are cleared. All other login accounts are removed, and existing employee sessions stop authenticating.

Employee numbering starts again at EMP0001 when available, but terminal PINs stay above the previous known PIN range so retained terminal fingerprints cannot accidentally match new staff. Physical terminal users/fingerprints are not erased. Backups are outside the public web root; only Admin can download backup data. This is a permanent maintenance action, not a normal employee deletion screen.

The optional `DHG_RESET_CHECK=1` browser check exercises this workflow only on the disposable loopback fixture. Never point this destructive test at a real-data server.

### Departments across branches

A department retains its primary branch and can serve additional branches selected in Settings → Departments. Existing department IDs, assignments and history are preserved. Registration and transfers choose the employee's actual branch explicitly; department membership is validated for that branch. Department reports can span offices while branch reports, device assignments, payroll and branch-based access continue using each employee assignment's branch. Organization CSV files include semicolon-separated AdditionalBranchIds. A department branch with employee assignment history cannot be removed. No departments are merged automatically.

Employee directory headers, expanded employee headers and navigation hover states now use explicit dark surfaces and readable foreground colors in dark mode.
