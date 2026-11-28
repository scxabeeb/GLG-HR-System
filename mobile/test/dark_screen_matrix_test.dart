import 'dart:typed_data';
import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:garowe_hr_mobile/api.dart';
import 'package:garowe_hr_mobile/main.dart';
import 'package:garowe_hr_mobile/management.dart';
import 'package:garowe_hr_mobile/ui_preferences.dart';

class ScreenApi extends Api {
  ScreenApi() : super(Uri.parse('https://example.test'));
  @override
  Future<Uint8List?> avatar() async => null;
  @override
  Future<dynamic> get(String path) async {
    if (path.contains('/manage/options')) {
      return {
        'people': [],
        'canAssign': false,
        'canOrganize': false,
        'actorId': 'self',
      };
    }
    if (path.endsWith('/overview')) {
      return {
        'tasks': {'done': 1, 'open': 2, 'inProgress': 0},
        'upcomingMeetings': 0,
        'hoursThisMonth': 8,
      };
    }
    if (path.endsWith('/attendance')) {
      return {'items': [], 'message': 'No attendance records yet.'};
    }
    if (path.endsWith('/profile')) return {'employee': null};
    if (path.contains('/payroll/')) {
      return {
        'from': '2026-11-01',
        'to': '2026-11-30',
        'currency': 'USD',
        'attendanceCalculated': 0,
        'attendanceApplied': 0,
        'revision': 1,
      };
    }
    return [];
  }
}

void main() {
  for (final lang in ['en', 'so']) {
    testWidgets('Core native screens fit 320px in dark $lang', (tester) async {
      tester.view.physicalSize = const Size(320, 800);
      tester.view.devicePixelRatio = 1;
      addTearDown(tester.view.resetPhysicalSize);
      addTearDown(tester.view.resetDevicePixelRatio);
      final prefs = UiPreferences(write: (k, v) async {})
        ..setDark(true)
        ..setLanguage(lang);
      addTearDown(prefs.dispose);
      final api = ScreenApi();
      final screens = <Widget>[
        OverviewScreen(api: api, onOpen: (_) {}),
        TasksScreen(api: api),
        MeetingsScreen(api: api),
        AttendanceScreen(api: api),
        PayrollScreen(api: api),
        NotificationsScreen(api: api),
        ProfileScreen(api: api, onChanged: () {}),
        PayslipScreen(api: api, id: 1),
        TaskEditor(api: api),
        MeetingEditor(api: api),
      ];
      for (final screen in screens) {
        await tester.pumpWidget(
          UiPreferencesScope(
            preferences: prefs,
            child: MaterialApp(
              theme: civicDarkTheme(civicTheme()),
              home: Scaffold(body: screen),
            ),
          ),
        );
        await tester.pumpAndSettle();
        expect(
          tester.takeException(),
          isNull,
          reason: '${screen.runtimeType} $lang',
        );
        await tester.pumpWidget(const SizedBox.shrink());
        await tester.pump();
      }
    });
  }
}
