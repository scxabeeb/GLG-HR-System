import "package:flutter/material.dart";
import 'package:flutter_test/flutter_test.dart';
import 'package:garowe_hr_mobile/main.dart';

void main() {
  test('accepts a configured HTTPS origin and maps employee routes', () {
    final server = parseServer('https://hr.example.com:5189')!;
    expect(EmployeePage.attendance.url(server).fragment, 'attendance');
    expect(EmployeePage.payroll.url(server).fragment, 'payroll');
    expect(EmployeePage.tasks.url(server).queryParameters['view'], 'mine');
    expect(
      EmployeePage.login.url(server).queryParameters['ReturnUrl'],
      '/api/mobile/v1/session',
    );
    expect(
      EmployeePage.notifications.url(server).path,
      '/Operations/Notifications',
    );
    expect(EmployeePage.payroll.dataUrl(server).path, '/api/mobile/v1/payroll');
    expect(EmployeePage.login.dataUrl(server), EmployeePage.login.url(server));
    expect(
      parseServer('http://127.0.0.1:5189', allowLoopback: true),
      isNotNull,
    );
    expect(parseServer('http://192.168.1.2:5189', allowLoopback: true), isNull);
    expect(parseServer('http://localhost:5189'), isNull);
    expect(parseServer('https://hr.example.com/admin'), isNull);
  });

  testWidgets('dashboard and bottom navigation expose employee services', (
    tester,
  ) async {
    await tester.binding.setSurfaceSize(const Size(320, 640));
    addTearDown(() => tester.binding.setSurfaceSize(null));
    await tester.pumpWidget(const GaroweHrApp(server: null));
    expect(find.text('Employee dashboard'), findsOneWidget);
    expect(find.byKey(const ValueKey('nav-profile')), findsNothing);
    expect(find.text('Attendance'), findsOneWidget);
    expect(find.text('Payroll'), findsOneWidget);
    await tester.tap(find.byTooltip('Account'));
    await tester.pumpAndSettle();
    expect(find.text('Sign out'), findsOneWidget);
    await tester.tap(find.text('My profile'));
    await tester.pumpAndSettle();
    expect(find.widgetWithText(AppBar, 'My profile'), findsOneWidget);
    await tester.tap(find.byTooltip('Back'));
    await tester.pumpAndSettle();
    expect(find.text('Employee dashboard'), findsOneWidget);
    for (final (page, title) in [
      ('attendance', 'Attendance'),
      ('payroll', 'Payroll'),
      ('tasks', 'My tasks'),
      ('meetings', 'My meetings'),
    ]) {
      await tester.tap(find.byKey(ValueKey('nav-$page')));
      await tester.pumpAndSettle();
      expect(find.widgetWithText(AppBar, title), findsOneWidget);
      expect(find.text('Server setup required'), findsOneWidget);
      expect(tester.takeException(), isNull);
    }
    await tester.tap(find.byTooltip('Notifications'));
    await tester.pumpAndSettle();
    expect(find.text('Notifications'), findsOneWidget);
    await tester.tap(find.byTooltip('Back'));
    await tester.pumpAndSettle();
    expect(find.text('My meetings'), findsOneWidget);
    await tester.tap(find.byKey(const ValueKey('nav-overview')));
    await tester.pumpAndSettle();
    expect(find.text('Employee dashboard'), findsOneWidget);
  });
}
