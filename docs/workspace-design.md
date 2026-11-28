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
