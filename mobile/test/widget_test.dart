import 'package:flutter/material.dart';
import 'dart:typed_data';
import 'package:flutter_test/flutter_test.dart';
import 'package:garowe_hr_mobile/main.dart';
import 'package:garowe_hr_mobile/api.dart';

void main() {
  test('server addresses must be HTTPS origins', () {
    expect(parseServer('https://erp.garowecity.pl.so'), isNotNull);
    expect(parseServer('https://user:secret@example.com'), isNull);
    expect(parseServer('https://example.com/path'), isNull);
    expect(parseServer('http://example.com'), isNull);
    expect(
      parseServer('http://127.0.0.1:5189', allowLoopback: true),
      isNotNull,
    );
  });
  testWidgets(
    'native bottom navigation, profile and inbox work at 320 pixels',
    (tester) async {
      tester.view.physicalSize = const Size(320, 700);
      tester.view.devicePixelRatio = 1;
      addTearDown(tester.view.resetPhysicalSize);
      addTearDown(tester.view.resetDevicePixelRatio);
      await tester.pumpWidget(
        MaterialApp(
          home: EmployeeShell(api: _FakeApi(), onSignedOut: () {}),
        ),
      );
      await tester.pumpAndSettle();
      expect(find.text('My dashboard'), findsOneWidget);
      for (var i = 1; i < 5; i++) {
        await tester.tap(find.byKey(ValueKey('nav-$i')));
        await tester.pumpAndSettle();
        expect(tester.takeException(), isNull);
      }
      await tester.tap(find.byTooltip('Account'));
      await tester.pumpAndSettle();
      await tester.tap(find.text('My profile'));
      await tester.pumpAndSettle();
      expect(find.text('Change password'), findsOneWidget);
      expect(tester.takeException(), isNull);
      await tester.tap(find.byTooltip('Back'));
      await tester.pumpAndSettle();
      await tester.tap(find.byTooltip('Notifications'));
      await tester.pumpAndSettle();
      expect(find.widgetWithText(AppBar, 'Notifications'), findsOneWidget);
      expect(tester.takeException(), isNull);
    },
  );
  testWidgets(
    'native sign-in validates fields and opens reset on a narrow screen',
    (tester) async {
      tester.view.physicalSize = const Size(320, 640);
      tester.view.devicePixelRatio = 1;
      addTearDown(tester.view.resetPhysicalSize);
      addTearDown(tester.view.resetDevicePixelRatio);
      await tester.pumpWidget(
        MaterialApp(
          home: LoginScreen(
            api: Api(Uri.parse('https://example.com')),
            onSignedIn: () {},
          ),
        ),
      );
      await tester.ensureVisible(find.widgetWithText(FilledButton, 'Sign in'));
      await tester.tap(find.widgetWithText(FilledButton, 'Sign in'));
      await tester.pumpAndSettle();
      expect(find.text('Enter your email or employee number.'), findsOneWidget);
      expect(find.text('Enter your password.'), findsOneWidget);
      await tester.ensureVisible(find.byTooltip('Show password'));
      await tester.tap(find.byTooltip('Show password'));
      await tester.pump();
      expect(find.byTooltip('Hide password'), findsOneWidget);
      await tester.ensureVisible(find.text('Forgot password?'));
      await tester.tap(find.text('Forgot password?'));
      await tester.pumpAndSettle();
      expect(find.text('Reset password'), findsWidgets);
      expect(tester.takeException(), isNull);
    },
  );
}

class _FakeApi extends Api {
  _FakeApi() : super(Uri.parse('https://example.com'));
  @override
  Future<Uint8List?> avatar() async => null;
  @override
  Future<dynamic> get(String path) async {
    if (path.endsWith('/overview')) {
      return {
        'tasks': {'done': 1, 'open': 2, 'inProgress': 0},
        'upcomingMeetings': 1,
        'hoursThisMonth': 8,
      };
    }
    if (path.endsWith('/attendance')) {
      return {'items': [], 'message': 'No attendance records yet.'};
    }
    if (path.endsWith('/profile')) {
      return {'email': 'employee@example.com', 'employee': null};
    }
    return <dynamic>[];
  }
}
