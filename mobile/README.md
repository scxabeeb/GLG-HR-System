# Garowe HR for employees

Flutter app for Android, iPhone and iPad. The bottom bar has **Overview**, **Tasks**, **Meetings**, **Attendance**, and **Payroll**. Overview opens personal attendance, current salary and published payslips, and the employee profile. Tasks opens My Tasks. Meetings opens the employee meeting list; external HTTPS meeting links can open in the device browser.

The app starts at **Sign in**. Sign in with your employee account to open Overview, where the top account icon opens **My profile** and **Sign out**. Use **Forgot password? Reset by email** on the sign-in screen with the email saved on your account. Live delivery requires the web server’s SMTP and public reset URL configuration. The bell opens the Garowe HR inbox for task assignments, meeting invitations, payslips, and other updates. The app uses the existing Garowe HR sign-in and permission checks. Employee records stay in the HR server. The Flutter lists read live records from `GET /api/mobile/v1/{section}` using the ASP.NET Identity session in the system WebView. The server scopes results to the signed-in user and reads them from the configured MySQL database. Each section returns up to 100 records. Task and meeting rows link directly to status updates, shared agendas, and joining an available meeting. Built-in meeting rooms open in the app, and supported external meeting links open in the browser. Use **Open full page** for payslip details and notification actions.

## Run on a device

1. Use Flutter 3.38 or later. Set up Android Studio for Android or Xcode and a signing team for iPhone or iPad.
2. Use a Garowe HR HTTPS origin that the device can reach and whose certificate the device trusts. The Mac's current loopback HTTP address on port 5189 only serves the Mac. For office use, configure a trusted HTTPS endpoint reachable from the office network or VPN. The server must include the attendance and payroll anchors in the updated employee profile.
3. From this directory, run:

   ```sh
   flutter pub get
   flutter devices
   flutter run -d DEVICE_ID --dart-define=GAROWE_HR_URL=https://hr.example.org
   ```

   Replace the example host with your Garowe HR origin. Sign in with an existing employee account. Attendance and payroll need a linked employee profile, and payslips appear after publication.

## Local iOS Simulator

With Garowe HR running on the Mac on port 5189, use a debug build:

```sh
flutter run -d SIMULATOR_ID --dart-define=GAROWE_HR_URL=http://127.0.0.1:5189
```

This HTTP option is restricted to the debug build and the Mac loopback address. Physical-device deployments use the HTTPS address described above.

## Checks

```sh
flutter analyze
flutter test
```
