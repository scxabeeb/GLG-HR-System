# n8n automation

Open Settings → Communications → Automation & n8n as an administrator. Integration is disabled by default. Download inactive workflow templates from this page; import them into your own n8n instance.

## Connect events

1. Import the events workflow. Its four module branches are placeholders for your delivery actions. Configure your email/WhatsApp provider nodes separately; the template does not send external messages by itself.
2. In n8n create a Header Auth credential: header `X-Garowe-Webhook-Key`, with a random shared password of at least 16 characters. Select it on the webhook node.
3. Copy the webhook's **production** HTTPS URL into the ERP settings and enter the same shared password. Save with delivery disabled. Publish the n8n workflow and send a synthetic test from ERP; check both execution history and delivery status.
4. Enable only the modules you need after reviewing the data being sent. Event snapshots include IDs, titles, statuses, dates and attendance times; contact information and payroll totals are separate opt-ins. Passwords, bank details and biometric templates are not exported.

## Scheduled reminders

Download/rotate the separate reminder key in ERP and create another n8n Header Auth credential using `X-Garowe-Reminder-Key`. Select this credential on each HTTP request node in the reminders and daily templates. Enable preset reminders in ERP, then publish these workflows.

The reminders workflow runs every 15 minutes: task reminders for due/overdue assignments, and meeting reminders within 30 minutes of start. The daily digest runs at 4 PM East Africa Time Saturday–Thursday for administrators. These jobs create in-app notices only. Duplicate calls are deduplicated per user/task/day or meeting occurrence. Jobs cannot approve payroll, alter attendance or mark tasks complete.

## Optional report readers

The reports template uses a separate `X-Garowe-Automation-Key` credential downloaded from ERP. It reads bounded module summaries; it has no administrator access. Event recipient lookup requires explicit contact sharing. Payroll totals require their own option. Keep credential downloads private and delete local copies after configuring the credential vault. Rotating a key invalidates the previous one.

## Reliability and operations

Domain changes and outbox events save within the same database transaction. The worker sends asynchronously, retries failures up to eight attempts, and records status without storing response bodies. Delivery is at least once: downstream actions should deduplicate using `X-Garowe-Event-Id` / `eventId`. Disabled modules do not queue new events. Disabling the integration pauses delivery. Changing the destination cancels old-generation queued events, preventing delivery to a new destination accidentally.

HTTPS port 443 is required. Redirects and private/reserved network destinations are blocked. Secrets are encrypted with the application's data protection keys; persist those keys in hosting. Existing direct task/meeting notification features remain active: choose a single owner for external messages to avoid sending duplicates.

Test locally with `DHG_AUTOMATION_CHECK=1 python3 scripts/check-native-admin.py`. This uses a disposable database, disables the real worker and tests deliveries through a fake HTTP handler.
