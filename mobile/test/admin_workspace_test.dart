import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:garowe_hr_mobile/admin_workspace.dart';
import 'package:garowe_hr_mobile/api.dart';
import 'package:garowe_hr_mobile/main.dart';
import 'package:garowe_hr_mobile/ui_preferences.dart';

class FakeAdmin extends Api {
  FakeAdmin() : super(Uri.parse('https://example.test'));
  String? savedPath;
  Map<String, dynamic>? saved;
  @override
  Future<dynamic> get(String path) async => [
    {'key': 'users', 'title': 'Users & permissions', 'group': 'Administration'},
  ];
  @override
  Future<dynamic> post(String path, Map<String, dynamic> body) async {
    if (path.endsWith('/load')) {
      return {
        'fields': [],
        'actions': [
          {
            'key': 'OnPostResetPasswordAsync',
            'label': 'Reset Password',
            'fields': [
              {
                'name': 'id',
                'label': 'User',
                'type': 'text',
                'value': 'test-user',
              },
              {'name': 'password', 'label': 'Password', 'type': 'password'},
              {
                'name': 'confirm',
                'label': 'Confirm password',
                'type': 'password',
              },
            ],
          },
        ],
        'lookups': {},
        'records': {
          'Items': [
            {'Id': 'test-user', 'UserName': 'Employee name'},
          ],
        },
      };
    }
    savedPath = path;
    saved = body;
    return {'message': 'Saved successfully'};
  }
}

void main() {
  testWidgets(
    'Admin native navigation and action work in Somali dark mode at 320px',
    (tester) async {
      tester.view.physicalSize = const Size(320, 800);
      tester.view.devicePixelRatio = 1;
      addTearDown(tester.view.resetPhysicalSize);
      addTearDown(tester.view.resetDevicePixelRatio);
      final prefs = UiPreferences(write: (k, v) async {})
        ..setDark(true)
        ..setLanguage('so');
      addTearDown(prefs.dispose);
      final api = FakeAdmin();
      await tester.pumpWidget(
        UiPreferencesScope(
          preferences: prefs,
          child: MaterialApp(
            theme: civicDarkTheme(civicTheme()),
            home: AdminWorkspace(api: api),
          ),
        ),
      );
      await tester.pumpAndSettle();
      await tester.tap(find.byType(ExpansionTile).first);
      await tester.pumpAndSettle();
      await tester.tap(find.text('Isticmaalayaasha iyo oggolaanshaha'));
      await tester.pumpAndSettle();
      expect(find.text('Employee name'), findsOneWidget);
      await tester.tap(find.text('Maamulka').last);
      await tester.pumpAndSettle();
      await tester.tap(find.text('Dib u deji furaha sirta'));
      await tester.pumpAndSettle();
      final fields = find.byType(TextFormField);
      await tester.enterText(fields.at(1), '1234');
      await tester.enterText(fields.at(2), '1234');
      FocusManager.instance.primaryFocus?.unfocus();
      await tester.pumpAndSettle();
      await tester.scrollUntilVisible(
        find.byWidgetPredicate((w) => w is FilledButton),
        150,
        scrollable: find.byType(Scrollable).last,
      );
      final save = find.byWidgetPredicate((w) => w is FilledButton).last;
      await tester.ensureVisible(save);
      await tester.tap(save);
      await tester.pumpAndSettle();
      await tester.tap(find.text('Xaqiiji'));
      await tester.pumpAndSettle();
      expect(
        api.savedPath,
        '/api/mobile/v2/admin/users/action/OnPostResetPasswordAsync',
      );
      expect((api.saved!['values'] as Map)['password'], '1234');
      expect(tester.takeException(), isNull);
    },
  );
  testWidgets('Dark dialogs have readable native surfaces', (tester) async {
    await tester.pumpWidget(
      MaterialApp(
        theme: civicDarkTheme(civicTheme()),
        home: Builder(
          builder: (context) => Scaffold(
            body: TextButton(
              onPressed: () => showDialog<void>(
                context: context,
                builder: (_) => const AlertDialog(title: Text('Details')),
              ),
              child: const Text('Open'),
            ),
          ),
        ),
      ),
    );
    await tester.tap(find.text('Open'));
    await tester.pumpAndSettle();
    final theme = Theme.of(tester.element(find.byType(AlertDialog)));
    expect(theme.brightness, Brightness.dark);
    expect(theme.dialogTheme.backgroundColor, theme.colorScheme.surface);
    expect(theme.colorScheme.onSurface.computeLuminance(), greaterThan(.5));
    expect(tester.takeException(), isNull);
  });
}
