# Operations workspace

Open **Operations** in the sidebar.

- **Task manager:** anyone signed in can create a personal task and update assigned work. HR, Management, and Admin can select multiple active accounts in scope when creating a task. Each person receives their own assignment and notification, with independent progress. The creator can track and edit the individual assignments under Created by me. The creator can edit details, reassign, or cancel a task. Priorities and overdue dates appear in the list; use the status filter to find completed work. Only the creator and assignee can see a task.
- **Meeting organizer:** HR, Management, and Admin can schedule, edit, or cancel meetings. Select employee accounts and enter times in Mogadishu time. Garowe HR creates its own browser video room. Invitees see it in their HR workspace; the organizer can copy a guest link for people without an HR account. Existing Teams and Google Meet meetings retain their previous links. Clearing an existing provider link switches that meeting to a Garowe room.
- **Vehicle fleet:** Admin and Management can register vehicles, assign a current driver from the vehicle’s branch, track mileage and status, and schedule or complete maintenance. Management branch restrictions also apply to fleet and GPS data. A retired vehicle can no longer submit GPS data.

## Run a Garowe meeting

1. Schedule the meeting in **Operations → Meeting organizer**. No provider URL is required.
2. Open **Garowe meeting room** up to ten minutes before the scheduled start. Enter a display name, then join with camera and microphone or join without them. Up to six people can join.
3. Use **Share with a guest → Copy join link** for people outside the HR system. Anyone with that link can enter the room during the meeting window. Treat it as private, share it only with intended guests, and cancel the meeting to invalidate it. Rescheduling also invalidates older guest links. The app does not email invitations. Organizers can record Garowe rooms from the meeting page.

The local HTTP address works for testing on this Mac. Outside guests need the app hosted at a reachable HTTPS address with WebSocket upgrades supported on `/operations/meeting-signal`. If using a reverse proxy, configure trusted forwarded scheme and host so the app sees the public HTTPS origin. Signaling runs on one app instance; use a single instance for these rooms. Keep the ASP.NET data-protection key ring persistent and private so invitations survive a restart.

Browser audio and video use peer-to-peer WebRTC. Different networks can block direct connections. For reliable remote use, configure a dedicated [TURN relay](https://webrtc.org/getting-started/turn-server) using `MeetingRooms__IceUrl` (such as a `turns:` URL), `MeetingRooms__IceUsername`, and `MeetingRooms__IceCredential`. Browsers in an authorized room receive the ICE credentials, so use credentials dedicated to meeting media and rotate them periodically. No third-party relay is configured by default. The current rooms are intended for small meetings; the app does not provide screen sharing or a webinar mode.

## Connect a tracker or gateway

The generic feed accepts HTTP JSON from compatible hardware or middleware. Trackers with a proprietary protocol need a gateway that translates their messages to the request below. Test with a vehicle you create for setup before connecting a real fleet vehicle.

1. Open the vehicle and select **Issue tracker key**. Copy it once and store it privately on the device or gateway. Only a hash is kept in the database.
2. Send a POST to `/api/fleet/<vehicle-id>/positions` on the deployed HR server. Use HTTPS, `Authorization: Bearer <tracker-key>`, and `Content-Type: application/json`. Use a timestamp with a UTC offset. Example body (replace the timestamp with the actual observation time):

```json
{
  "latitude": 8.4,
  "longitude": 48.5,
  "recordedAt": "2026-11-09T10:15:00+03:00",
  "speedKph": 32.5
}
```

3. Open the vehicle’s GPS section. The latest location has a timestamp and a map link, with the last 50 fixes in history. **Watch latest position** checks every 15 seconds while enabled. Map coordinates are shared with OpenStreetMap only when someone clicks the map link.
4. Rotate the key if it is exposed, or revoke it to disconnect the feed. Retiring the vehicle also revokes its key.

The endpoint rejects out-of-range coordinates and speeds, timestamps more than 30 days old or five minutes in the future, and invalid or revoked keys. A retry with the same vehicle and timestamp is accepted without storing a second point. It limits an IP to 180 requests per minute; use a sensible reporting cadence if multiple devices share a gateway. Plain HTTP is accepted only for local loopback testing in Development. Use a correctly configured HTTPS deployment before connecting equipment outside this Mac. Historical positions stay in MySQL; decide on a retention period before rolling the feature out fleet-wide.

The additive `AddOperationsWorkspace` migration must be applied before opening the new pages. Follow the migration command in the project README.

## Employee inbox and WhatsApp

- **My tasks** shows assigned work, including personal tasks. **Created by me** tracks work the user assigned. **All tasks** is an Admin-only monitoring view. An admin who is neither creator nor assignee cannot change that task from this view.
- **Inbox** is private to each account. It shows new assignments, progress changes, meeting invitations, reschedules and cancellations. Employees can open their meeting or join an active Garowe room from the inbox. Only the intended recipient can mark their notices as read.
- Staff enable WhatsApp from **Inbox → WhatsApp alerts**. HR must first record their mobile number in international format. If HR changes it, the employee must opt in again.

To enable outbound alerts, register the system’s number with the [Meta WhatsApp Business Platform](https://www.postman.com/meta/whatsapp-business-platform/documentation/wlk6lh4/whatsapp-cloud-api). In WhatsApp Manager, prepare two approved templates in the configured language, with **no dynamic variables**. Example generic bodies: “A new task was assigned to you. Open your Garowe HR inbox.” and “You have a new meeting invitation. Open your Garowe HR inbox.” Keep the sender token in private server configuration or environment variables:

```text
WhatsApp__Enabled=true
WhatsApp__GraphVersion=<supported version from Meta API setup, including v prefix>
WhatsApp__PhoneNumberId=<Meta phone number ID, not the mobile number>
WhatsApp__AccessToken=<private Meta access token>
WhatsApp__TaskTemplate=<approved task template name>
WhatsApp__MeetingTemplate=<approved meeting template name>
WhatsApp__Language=en_US
WhatsApp__SenderNumber=<optional display number in international format>
```

Default is disabled. Enabling the sender does not backfill old alerts. The sender checks consent, current mobile number and current assignment before sending. **System settings → WhatsApp notifications** shows queue totals; “Accepted” does not mean the recipient received it. Network outcomes that may have sent a message are not automatically retried. Keep private data and guest links out of WhatsApp templates.

**Task progress report** shows status totals, assignees and due dates, and recent changes with the actor and timestamp. Employees see their assignments and tasks they created; administrators can select all assignments. Use an assignment’s Activity history link to focus on one task. The report logs new activity from the date the feature is enabled.

Select **Create a team task**, give it a title and brief, and choose its employees. Save the team task first. Then open **Team progress & add subtasks** and add each responsibility. Use the checkboxes to assign a subtask to any number of eligible team members. Their progress is tracked separately under the same subtask. Use **+ Add another subtask** to enter multiple responsibilities before saving them together. Team members can open the team task and see their own assignments. The creator and admins can see the complete progress report. Existing team rosters are populated from their previous assignees.

## Fleet trips, expenses and reports

- **Fleet trips**: staff request a trip for their current branch. Management or Admin can approve or reject it, choosing a vehicle and an active driver from that branch. The app checks overlapping bookings for both the vehicle and the driver. A requester can cancel a pending or approved trip; the assigned driver or a fleet manager logs departure and return mileage. Requesters and assigned drivers receive in-app updates.
- **Vehicle fleet**: edit the vehicle to record insurance, registration, and inspection dates. GPS and maintenance continue to work from the vehicle detail page.
- **Fleet reports & expenses**: log fuel litres or other costs, with a currency and receipt reference. Use date and vehicle filters for trip activity, completed mileage, and cost totals by currency. Export trips or expenses as CSV. The renewal panel shows upcoming or overdue service, registration, insurance and inspection dates. Managers see data in their authorized branches; employees see only their own requests and driver assignments.

The sidebar groups Tasks, Meetings and Fleet separately. **Tasks → Manage tasks** opens Created by me; **Task progress report** exports the selected scope and status. **Meetings → Manage meetings** opens scheduling for staff organizers. **Meeting reports** filters by date, scope and status, counts invitations (not attendance), and exports CSV without join links. Only admins can report across all meetings.

## Meeting recordings

In an active Garowe meeting, the signed-in organizer can select **Start recording** and **Stop & save recording**. The indicator is shown to participants, including guests who join later. Keep the meeting tab open while saving. The organizer can play or download files under **Meetings → Saved recordings**. Invited participants and guests cannot access that library's files. External provider rooms are not recorded by Garowe HR.

A browser with MediaRecorder, Web Audio and canvas capture is required. Recording captures the audio and video available in the organizer's browser. The limit is 100 MB per file and 10 files per meeting; the browser stops near the file limit. If saving fails, download the file before closing the tab. Recordings are stored outside the public web root in `App_Data/meeting-recordings`, or the private directory configured by `MeetingRooms__RecordingDirectory`. Include that directory in your server backups.

## In-person meetings and minutes

In **Meetings → Manage meetings**, select the in-person format and enter a location, time, agenda and invited employees. Use **Agenda & minutes** to copy the internal agenda link. Only the organizer and invited employees can open it. The organizer can save private draft notes and publish minutes to the invitees' inboxes. Editing a draft does not change the published copy. Use **Upcoming & in progress** or **Past & cancelled** to find a meeting.
