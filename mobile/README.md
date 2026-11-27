# Garowe City native employee app

Flutter screens for Android, iPhone and iPad. The app uses authenticated ASP.NET APIs and the server's MySQL database. There are no embedded WebViews.

- Branded responsive design with a civic login, metric dashboard, task progress chart, workspace drawer, and persistent five-tab navigation. Forms, lists, filters and status labels use one design system.
- Native sign-in, password visibility, email reset, mandatory password change, secure token storage and sign-out.
- Overview, task status updates, searchable task and meeting lists, shared agenda submissions, meeting check-in, attendance, published payslips and notifications.
- Native management: admins can manage all tasks and meetings; creators/organizers can manage their own. Create, assign, edit and cancel tasks, create team tasks and subtasks, review activity and progress, schedule/edit/cancel meetings, publish minutes, and end live sessions. Employees retain access to their assignments. Staff roles can assign other users and organize meetings, subject to module and branch permissions.
- Profile image upload and native password changes.
- Native WebRTC room: participant tiles, tap to focus, microphone/camera controls, camera switching, screen sharing, shared whiteboard, organizer mute/end and leave/rejoin. Rooms retain the server's six-participant limit. Screen capture support depends on the OS; camera/microphone permissions are requested by WebRTC. Recording and meeting-document management remain available in the web administration interface.
- Teams and Google Meet links launch the provider's app or browser.

## Server and authentication

The default server is `https://garowe-hr-production.up.railway.app`. Override with `--dart-define=GAROWE_HR_URL=https://erp.garowecity.pl.so`. Deploy the updated server before using this build. Native actions use `/api/mobile/v2`, while overview, avatar and attendance use the compatible `/api/mobile/v1` reads. V2 accepts bearer tokens only. Tokens expire after eight hours; sign in again when expired. Password change/reset, disabling an account and sign-out invalidate existing tokens. Personal attendance/payroll require an employee-linked account and a published payslip. Permissions apply server-side.

Password reset email requires configured SMTP and a public reset URL. Paste the code or entire reset link from the email into the native reset screen. No password or MySQL credentials are stored in the APK.

Video connections between separate networks need a configured TURN service via `MeetingRooms:IceUrl`, `IceUsername` and `IceCredential`. Test calls between the actual office/mobile networks before relying on them.

## Build and check

Use Flutter 3.38 or newer with the Android SDK, or Xcode and an Apple signing team for iOS.

```sh
flutter pub get
flutter analyze
flutter test
flutter build apk --release --split-per-abi
```

The arm64 APK supports the Infinix. Release builds currently use the project's existing debug signing key for compatibility with previously installed builds; configure a protected production signing key before store distribution.

For the iOS simulator and a local server, debug builds allow `--dart-define=GAROWE_HR_URL=http://127.0.0.1:5189`. Physical devices require reachable HTTPS.

Version 2.2 adds native administrator task and meeting reports with search, status and date filters, report pagination, and per-employee task completion/overdue summaries on the dashboard. Reports require administrator and module/report permissions; employees keep their personal dashboard.

Version 2.3 supports automatic employee ID sign-in (for example EMP0001). New registrations receive EMPID123 as their initial password and must change it before using the workspace. Email sign-in and email password recovery remain supported.

Version 2.3.1 accepts passwords with a minimum of 4 characters, without required uppercase letters, numbers or symbols. Password-change and recovery screens match the deployed server policy.
