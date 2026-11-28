import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:garowe_hr_mobile/api.dart';
import 'package:garowe_hr_mobile/admin_workspace.dart';
import 'package:garowe_hr_mobile/main.dart';
import 'package:garowe_hr_mobile/ui_preferences.dart';

class RegistrationApi extends Api {
  RegistrationApi() : super(Uri.parse('https://example.test'));
  String? savedPath;
  Map<String, dynamic>? saved;
  @override
  Future<dynamic> post(String path, Map<String, dynamic> body) async {
    if (path.endsWith('/load')) {
      return {
        'fields': path.contains('/asset-edit/')
            ? [
                {'name': 'Input.Id', 'type': 'number', 'value': 0},
                {
                  'name': 'Input.Tag',
                  'label': 'Tag',
                  'type': 'text',
                  'value': '',
                },
                {
                  'name': 'Input.Name',
                  'label': 'Name',
                  'type': 'text',
                  'value': '',
                },
                {
                  'name': 'Input.Category',
                  'label': 'Category',
                  'type': 'text',
                  'value': '',
                },
              ]
            : [],
        'filters': [],
        'actions': path.contains('/asset-edit/')
            ? [
                {'key': 'OnPostAsync', 'label': 'Save', 'fields': []},
              ]
            : [],
        'lookups': {},
        'records': {'Items': []},
      };
    }
    savedPath = path;
    saved = body;
    return {'message': 'Saved successfully'};
  }
}

void main() {
  testWidgets(
    'Asset directory offers native registration and submits new record at 320px',
    (tester) async {
      tester.view.physicalSize = const Size(320, 800);
      tester.view.devicePixelRatio = 1;
      addTearDown(tester.view.resetPhysicalSize);
      addTearDown(tester.view.resetDevicePixelRatio);
      final api = RegistrationApi();
      await tester.pumpWidget(
        MaterialApp(
          theme: civicDarkTheme(civicTheme()),
          home: AdminModule(
            api: api,
            module: const {'key': 'assets', 'title': 'Asset directory'},
          ),
        ),
      );
      await tester.pumpAndSettle();
      final create = find.byKey(const ValueKey('admin-create-record')).first;
      expect(create, findsOneWidget);
      await tester.tap(create);
      await tester.pumpAndSettle();
      final fields = find.byType(TextFormField);
      expect(fields, findsNWidgets(3));
      await tester.enterText(fields.at(0), 'ASSET-001');
      await tester.enterText(fields.at(1), 'Office laptop');
      await tester.enterText(fields.at(2), 'Computer');
      FocusManager.instance.primaryFocus?.unfocus();
      await tester.pumpAndSettle();
      final save = find.byWidgetPredicate((w) => w is FilledButton).last;
      await tester.ensureVisible(save);
      await tester.tap(save);
      await tester.pumpAndSettle();
      await tester.tap(find.text('Confirm'));
      await tester.pumpAndSettle();
      expect(
        api.savedPath,
        '/api/mobile/v2/admin/asset-edit/action/OnPostAsync',
      );
      expect((api.saved!['values'] as Map)['Input.Name'], 'Office laptop');
      expect((api.saved!['values'] as Map).containsKey('Input.Id'), false);
      expect(tester.takeException(), isNull);
    },
  );
}
