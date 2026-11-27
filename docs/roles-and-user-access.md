# Roles, account access and permissions

Open **Main settings → Users & security**. Admin accounts can manage both **Users & permissions** and **Roles & permissions**.

## Employee accounts and roles

Employee registration already creates a login linked to that employee. Search the Users page using the employee ID, full name, username or email. Select several assigned roles and save; the employee profile and employment records remain linked. HR, Finance, Management, Employee, General Secretary and Chairman can be combined. General Secretary includes Management. Assign the built-in Admin role only for full system administration.

## Custom roles

Create a custom role with one or more operational capability templates, then configure its menu/submenu matrix. Capabilities provide the existing application duties and action authorization; menu choices narrow access. An Allow choice never bypasses branch scope, ownership, employee-profile requirements or payroll/leave approval stages.

Built-in role names and capabilities are protected. Their menu restrictions can be configured except for Admin, which retains full access. Custom roles cannot inherit Admin; assign Admin explicitly when full administrative authority is intended. Unused custom roles can be deleted after removing their assignments.

## Effective permissions

1. Operational capabilities come from built-in roles and the selected capabilities of assigned custom roles.
2. Any assigned role's menu/submenu Deny applies.
3. Individual Inherit follows the role result. Individual Allow removes an inherited denial for that exact key; individual Deny blocks it.
4. A denied parent menu blocks all of its submenus. Allow the parent separately when restoring access.
5. Existing shared-report and document safeguards still apply when an entire underlying module is denied; use submenu denials for finer control.
6. Built-in Admin retains full access. Users cannot remove their own Admin role or deactivate their own account.

The server enforces these rules for page URLs, actions and mobile APIs; navigation reflects the same result. Assignment pickers use the effective task/meeting access rules. Custom capability changes are re-evaluated on authenticated requests rather than trusting stale inherited claims in a session.

Role definitions and permissions use existing Identity role claims. User Allow overrides use Identity user claims; existing user restrictions remain explicit Deny entries. No database migration or rewriting of existing role memberships is required. Existing accounts inherit their previous behavior until an administrator saves changes.

## Passwords and account status

Administrators can set a temporary password for another account, including another administrator. Password validation and confirmation apply. Old passwords stop working, existing web/mobile sessions are revoked, lockout counters are cleared, and the user must change the temporary password after signing in. Passwords are never included in audit messages. Use **Change my password** for the current account.

Inactive accounts cannot sign in or continue authenticated requests. Reactivation requires a fresh sign-in and does not restore a revoked token. Employee, attendance and payroll history remain intact. Role assignment changes also invalidate existing sessions. Menu changes take effect on subsequent requests without requiring a new password.

Passwords require at least 4 characters. Uppercase letters, lowercase letters, numbers and symbols are optional. The same policy applies to account creation, administrator resets, email recovery and web/mobile password changes. Existing passwords remain valid.
