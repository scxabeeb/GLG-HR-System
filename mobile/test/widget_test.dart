import 'package:flutter/material.dart';
import 'dart:typed_data';
import 'package:flutter_test/flutter_test.dart';
import 'package:garowe_hr_mobile/main.dart';
import 'package:garowe_hr_mobile/api.dart';
import 'package:garowe_hr_mobile/management.dart';

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
    'native management forms submit task and meeting assignments at 320px',
    (tester) async {
      tester.view.physicalSize = const Size(320, 700);
      tester.view.devicePixelRatio = 1;
      addTearDown(tester.view.resetPhysicalSize);
      addTearDown(tester.view.resetDevicePixelRatio);
      final api = _ManagerApi();
      await tester.pumpWidget(
        MaterialApp(
          theme: civicTheme(),
          home: Builder(
            builder: (context) => Scaffold(
              body: Column(
                children: [
                  TextButton(
                    onPressed: () => Navigator.push(
                      context,
                      MaterialPageRoute(builder: (_) => TaskEditor(api: api)),
                    ),
                    child: const Text('Open task'),
                  ),
                  TextButton(
                    onPressed: () => Navigator.push(
                      context,
                      MaterialPageRoute(
                        builder: (_) => MeetingEditor(api: api),
                      ),
                    ),
                    child: const Text('Open meeting'),
                  ),
                ],
              ),
            ),
          ),
        ),
      );
      await tester.tap(find.text('Open task'));
      await tester.pumpAndSettle();
      await tester.enterText(find.byType(TextField).first, 'Service report');
      FocusManager.instance.primaryFocus?.unfocus();
      await tester.pumpAndSettle();
      final employeeChoice = find.widgetWithText(
        CheckboxListTile,
        'Employee One',
      );
      await Scrollable.ensureVisible(
        tester.element(employeeChoice),
        alignment: .5,
      );
      await tester.pumpAndSettle();
      await tester.tap(employeeChoice);
      FocusManager.instance.primaryFocus?.unfocus();
      await tester.pumpAndSettle();
      final saveTask = find.widgetWithText(FilledButton, 'Save assignment');
      await Scrollable.ensureVisible(tester.element(saveTask), alignment: .5);
      await tester.pumpAndSettle();
      await tester.tap(saveTask);
      await tester.pumpAndSettle();
      expect(api.path, '/api/mobile/v2/manage/tasks');
      expect(api.body!['assigneeIds'], ['employee']);
      expect(api.body!['title'], 'Service report');
      expect(tester.takeException(), isNull);
      await tester.tap(find.text('Open meeting'));
      await tester.pumpAndSettle();
      await tester.enterText(find.byType(TextField).first, 'Weekly review');
      FocusManager.instance.primaryFocus?.unfocus();
      await tester.pumpAndSettle();
      final meetingChoice = find.widgetWithText(
        CheckboxListTile,
        'Employee One',
      );
      await Scrollable.ensureVisible(
        tester.element(meetingChoice),
        alignment: .5,
      );
      await tester.pumpAndSettle();
      await tester.tap(meetingChoice);
      FocusManager.instance.primaryFocus?.unfocus();
      await tester.pumpAndSettle();
      final saveMeeting = find.widgetWithText(
        FilledButton,
        'Save meeting and notify participants',
      );
      await Scrollable.ensureVisible(
        tester.element(saveMeeting),
        alignment: .5,
      );
      await tester.pumpAndSettle();
      await tester.tap(saveMeeting);
      await tester.pumpAndSettle();
      expect(api.path, '/api/mobile/v2/manage/meetings');
      expect(api.body!['guests'], ['employee']);
      expect(api.body!['mode'], 'BuiltIn');
      expect(
        DateTime.parse(
          api.body!['ends'],
        ).isAfter(DateTime.parse(api.body!['starts'])),
        isTrue,
      );
      expect(tester.takeException(), isNull);
    },
  );
  testWidgets(
    'native bottom navigation, profile and inbox work at 320 pixels',
    (tester) async {
      tester.view.physicalSize = const Size(320, 700);
      tester.view.devicePixelRatio = 1;
      addTearDown(tester.view.resetPhysicalSize);
      addTearDown(tester.view.resetDevicePixelRatio);
      await tester.pumpWidget(
        MaterialApp(
          theme: civicTheme(),
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
          theme: civicTheme(),
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
    if (path.contains('/manage/options')) {
      return {
        'people': <dynamic>[],
        'canAssign': false,
        'canOrganize': false,
        'actorId': 'self',
      };
    }
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

class _ManagerApi extends Api {
  _ManagerApi() : super(Uri.parse('https://example.com'));
  String? path;
  Map<String, dynamic>? body;
  @override
  Future<dynamic> get(String path) async => {
    'people': [
      {'id': 'manager', 'name': 'Organizer'},
      {'id': 'employee', 'name': 'Employee One'},
    ],
    'canAssign': true,
    'canOrganize': true,
    'isAdmin': true,
    'actorId': 'manager',
  };
  @override
  Future<dynamic> post(String path, Map<String, dynamic> body) async {
    this.path = path;
    this.body = body;
    return {'message': 'Saved'};
  }
}
