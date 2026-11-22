# Garowe City native employee app

Flutter screens for Android, iPhone and iPad. The app uses authenticated ASP.NET APIs and the server's MySQL database. There are no embedded WebViews.

- Native sign-in, password visibility, email reset, mandatory password change, secure token storage and sign-out.
- Overview, task status updates, searchable task and meeting lists, shared agenda submissions, meeting check-in, attendance, published payslips and notifications.
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
