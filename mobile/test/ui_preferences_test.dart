import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:garowe_hr_mobile/ui_preferences.dart';
import 'package:garowe_hr_mobile/main.dart';
import 'package:garowe_hr_mobile/api.dart';

void main() {
  testWidgets(
    'Somali and dark mode persist without translating records or entered values',
    (tester) async {
      final values = <String, String>{};
      final prefs = UiPreferences(
        write: (key, value) async {
          values[key] = value;
        },
        read: (key) async => values[key],
      );
      final controller = TextEditingController(text: 'EMP0001');
      addTearDown(controller.dispose);
      addTearDown(prefs.dispose);
      await tester.pumpWidget(
        UiPreferencesScope(
          preferences: prefs,
          child: ListenableBuilder(
            listenable: prefs,
            builder: (context, _) => MaterialApp(
              theme: civicTheme(),
              darkTheme: civicDarkTheme(civicTheme()),
              themeMode: prefs.mode,
              home: Scaffold(
                body: Column(
                  children: [
                    const UiDisplayPreferences(),
                    const UiText('Tasks'),
                    const UiText.raw('Employees'),
                    TextField(controller: controller),
                  ],
                ),
              ),
            ),
          ),
        ),
      );
      await tester.tap(find.byKey(const ValueKey('display-language')));
      await tester.pumpAndSettle();
      await tester.tap(find.text('Soomaali').last);
      await tester.pumpAndSettle();
      expect(find.text('Hawlaha'), findsOneWidget);
      expect(find.text('Employees'), findsOneWidget);
      expect(controller.text, 'EMP0001');
      await tester.tap(find.byKey(const ValueKey('display-theme')));
      await tester.pumpAndSettle();
      expect(
        Theme.of(tester.element(find.byType(Scaffold))).brightness,
        Brightness.dark,
      );
      expect(values['garowe.mobile.language'], 'so');
      expect(values['garowe.mobile.theme'], 'dark');
      final restored = UiPreferences(read: (key) async => values[key]);
      await restored.load();
      expect(restored.language, 'so');
      expect(restored.mode, ThemeMode.dark);
      restored.dispose();
      expect(tester.takeException(), isNull);
    },
  );
  testWidgets(
    'Native sign-in preferences fit 320 pixels and retain credentials',
    (tester) async {
      tester.view.physicalSize = const Size(320, 760);
      tester.view.devicePixelRatio = 1;
      addTearDown(tester.view.resetPhysicalSize);
      addTearDown(tester.view.resetDevicePixelRatio);
      final prefs = UiPreferences(write: (key, value) async {});
      addTearDown(prefs.dispose);
      await tester.pumpWidget(
        UiPreferencesScope(
          preferences: prefs,
          child: ListenableBuilder(
            listenable: prefs,
            builder: (context, _) => MaterialApp(
              theme: civicTheme(),
              darkTheme: civicDarkTheme(civicTheme()),
              themeMode: prefs.mode,
              home: LoginScreen(
                api: Api(Uri.parse('https://example.com')),
                onSignedIn: () {},
              ),
            ),
          ),
        ),
      );
      final fields = find.byType(TextFormField);
      await tester.enterText(fields.first, 'EMP0001');
      await tester.enterText(fields.last, '1234');
      prefs.setLanguage('so');
      prefs.setDark(true);
      await tester.pumpAndSettle();
      expect(find.text('Soo gal'), findsWidgets);
      expect(find.text('EMP0001'), findsWidgets);
      expect(tester.takeException(), isNull);
    },
  );
}
