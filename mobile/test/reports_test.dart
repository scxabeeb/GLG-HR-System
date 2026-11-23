import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:garowe_hr_mobile/api.dart';
import 'package:garowe_hr_mobile/main.dart';
import 'package:garowe_hr_mobile/reports.dart';

class ReportApi extends Api {
  ReportApi() : super(Uri.parse('https://example.com'));
  String? lastPath;
  bool admin = true;
  @override
  Future<dynamic> get(String path) async {
    lastPath = path;
    if (path.contains('/options')) return {'canReport': admin};
    return {
      'total': 1,
      'pageSize': 100,
      'summary': {
        'done': 1,
        'active': 0,
        'overdue': 0,
        'completed': 1,
        'withMinutes': 1,
        'cancelled': 0,
      },
      'employees': [
        {
          'name': 'Employee with a long department name',
          'progress': 100,
          'done': 1,
          'open': 0,
          'inProgress': 0,
          'overdue': 0,
        },
      ],
      'items': [
        if (path.contains('/tasks'))
          {
            'id': 1,
            'title': 'Inspect city equipment',
            'assigneeName': 'Employee One',
            'status': 'Done',
          }
        else
          {
            'id': 1,
            'title': 'City planning meeting',
            'organizer': 'Administrator',
            'status': 'Completed',
            'startsUtc': '2026-11-23T06:00:00Z',
            'attended': 3,
            'invited': 4,
            'hasMinutes': true,
          },
      ],
    };
  }
}

void main() {
  testWidgets(
    'native task and meeting reports fit narrow screens and apply search',
    (tester) async {
      tester.view.physicalSize = const Size(320, 850);
      tester.view.devicePixelRatio = 1;
      addTearDown(tester.view.resetPhysicalSize);
      addTearDown(tester.view.resetDevicePixelRatio);
      final api = ReportApi();
      for (final module in ['tasks', 'meetings']) {
        await tester.pumpWidget(
          MaterialApp(
            theme: civicTheme(),
            home: OperationsReport(
              key: ValueKey(module),
              api: api,
              module: module,
            ),
          ),
        );
        await tester.pumpAndSettle();
        expect(
          find.text(module == 'tasks' ? 'Task report' : 'Meeting report'),
          findsOneWidget,
        );
        await tester.enterText(find.byType(TextField), 'City');
        await tester.tap(find.text('Apply'));
        await tester.pumpAndSettle();
        expect(api.lastPath, contains('search=City'));
        expect(tester.takeException(), isNull);
      }
    },
  );
  testWidgets(
    'admin dashboard shows employee progress and hides reports from employees',
    (tester) async {
      final api = ReportApi();
      await tester.pumpWidget(
        MaterialApp(
          theme: civicTheme(),
          home: Scaffold(
            body: AdminReportEntry(api: api, module: 'tasks', dashboard: true),
          ),
        ),
      );
      await tester.pumpAndSettle();
      expect(find.text('Employee task progress'), findsOneWidget);
      expect(find.text('100%'), findsOneWidget);
      api.admin = false;
      await tester.pumpWidget(
        MaterialApp(
          home: Scaffold(
            body: AdminReportEntry(key: UniqueKey(), api: api, module: 'tasks'),
          ),
        ),
      );
      await tester.pumpAndSettle();
      expect(find.text('Task report'), findsNothing);
    },
  );
}
