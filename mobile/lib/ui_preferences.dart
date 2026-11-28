import 'package:flutter/material.dart';
import 'package:flutter_secure_storage/flutter_secure_storage.dart';
import 'ui_strings.dart';

class UiPreferences extends ChangeNotifier {
  UiPreferences({this.write, this.read});
  final Future<void> Function(String key, String value)? write;
  final Future<String?> Function(String key)? read;
  static const _storage = FlutterSecureStorage();
  ThemeMode mode = ThemeMode.system;
  String language = 'en';
  bool _alive = true;
  int _revision = 0;
  Future<void> load() async {
    final revision = _revision;
    try {
      final theme =
          await (read?.call('garowe.mobile.theme') ??
              _storage.read(key: 'garowe.mobile.theme'));
      final lang =
          await (read?.call('garowe.mobile.language') ??
              _storage.read(key: 'garowe.mobile.language'));
      if (!_alive || revision != _revision) return;
      mode = theme == 'dark'
          ? ThemeMode.dark
          : theme == 'light'
          ? ThemeMode.light
          : ThemeMode.system;
      language = lang == 'so' ? 'so' : 'en';
      notifyListeners();
    } catch (_) {}
  }

  Future<void> _save(String key, String value) async {
    try {
      await (write?.call(key, value) ?? _storage.write(key: key, value: value));
    } catch (_) {}
  }

  void setLanguage(String value) {
    _revision++;
    language = value == 'so' ? 'so' : 'en';
    notifyListeners();
    _save('garowe.mobile.language', language);
  }

  void setDark(bool dark) {
    _revision++;
    mode = dark ? ThemeMode.dark : ThemeMode.light;
    notifyListeners();
    _save('garowe.mobile.theme', dark ? 'dark' : 'light');
  }

  @override
  void dispose() {
    _alive = false;
    super.dispose();
  }
}

class UiPreferencesScope extends InheritedNotifier<UiPreferences> {
  const UiPreferencesScope({
    super.key,
    required UiPreferences preferences,
    required super.child,
  }) : super(notifier: preferences);
  static UiPreferences? maybeOf(BuildContext context) => context
      .dependOnInheritedWidgetOfExactType<UiPreferencesScope>()
      ?.notifier;
}

String tr(BuildContext context, String value) =>
    UiPreferencesScope.maybeOf(context)?.language == 'so'
    ? (somaliStrings[value] ?? value)
    : value;

// Record strings use .raw: language changes must not alter names or entered data.
class UiText extends StatelessWidget {
  const UiText(
    this.data, {
    super.key,
    this.style,
    this.textAlign,
    this.maxLines,
    this.overflow,
    this.softWrap,
    this.textDirection,
    this.semanticsLabel,
    this.textScaler,
  }) : translate = true;
  const UiText.raw(
    this.data, {
    super.key,
    this.style,
    this.textAlign,
    this.maxLines,
    this.overflow,
    this.softWrap,
    this.textDirection,
    this.semanticsLabel,
    this.textScaler,
  }) : translate = false;
  final String data;
  final bool translate;
  final TextStyle? style;
  final TextAlign? textAlign;
  final int? maxLines;
  final TextOverflow? overflow;
  final bool? softWrap;
  final TextDirection? textDirection;
  final String? semanticsLabel;
  final TextScaler? textScaler;
  @override
  Widget build(BuildContext context) {
    var resolved = style;
    if (Theme.of(context).brightness == Brightness.dark &&
        style?.color != null) {
      const muted = [
        Color(0xff586983),
        Color(0xff66748c),
        Color(0xff65748b),
        Color(0xff8390a4),
        Color(0xff77859c),
        Color(0xff7c8ba2),
      ];
      final color = style!.color!;
      if (color == const Color(0xff15264b)) {
        resolved = style!.copyWith(color: const Color(0xffe1eaf8));
      } else if (muted.contains(color)) {
        resolved = style!.copyWith(color: const Color(0xffb6c8e1));
      } else if (color == const Color(0xff1745c3)) {
        resolved = style!.copyWith(color: const Color(0xffa5c3ff));
      }
    }
    return Text(
      translate ? tr(context, data) : data,
      style: resolved,
      textAlign: textAlign,
      maxLines: maxLines,
      overflow: overflow,
      softWrap: softWrap,
      textDirection: textDirection,
      semanticsLabel: semanticsLabel,
      textScaler: textScaler,
    );
  }
}

InputDecoration localizeDecoration(
  BuildContext context,
  InputDecoration value,
) => value.copyWith(
  labelText: value.labelText == null ? null : tr(context, value.labelText!),
  hintText: value.hintText == null ? null : tr(context, value.hintText!),
  helperText: value.helperText == null ? null : tr(context, value.helperText!),
  errorText: value.errorText == null ? null : tr(context, value.errorText!),
);

class UiDisplayPreferences extends StatelessWidget {
  const UiDisplayPreferences({super.key});
  @override
  Widget build(BuildContext context) {
    final preferences = UiPreferencesScope.maybeOf(context);
    if (preferences == null) return const SizedBox.shrink();
    final dark = Theme.of(context).brightness == Brightness.dark;
    return Wrap(
      alignment: WrapAlignment.end,
      crossAxisAlignment: WrapCrossAlignment.center,
      spacing: 8,
      children: [
        Semantics(
          label: tr(context, 'Language'),
          child: DropdownButton<String>(
            key: const ValueKey('display-language'),
            value: preferences.language,
            underline: const SizedBox.shrink(),
            icon: const Icon(Icons.expand_more, size: 18),
            items: const [
              DropdownMenuItem(value: 'en', child: Text('English')),
              DropdownMenuItem(value: 'so', child: Text('Soomaali')),
            ],
            onChanged: (value) {
              if (value != null) preferences.setLanguage(value);
            },
          ),
        ),
        IconButton(
          key: const ValueKey('display-theme'),
          tooltip: tr(context, dark ? 'Light mode' : 'Dark mode'),
          icon: Icon(
            dark ? Icons.light_mode_outlined : Icons.dark_mode_outlined,
          ),
          onPressed: () => preferences.setDark(!dark),
        ),
      ],
    );
  }
}

ThemeData civicDarkTheme(ThemeData base) {
  final scheme =
      ColorScheme.fromSeed(
        seedColor: const Color(0xff759dff),
        brightness: Brightness.dark,
        surface: const Color(0xff18273e),
      ).copyWith(
        primary: const Color(0xffa5c3ff),
        onSurface: const Color(0xffe1eaf8),
      );
  return base.copyWith(
    brightness: Brightness.dark,
    colorScheme: scheme,
    scaffoldBackgroundColor: const Color(0xff101a2a),
    textTheme: base.textTheme.apply(
      bodyColor: scheme.onSurface,
      displayColor: scheme.onSurface,
    ),
    appBarTheme: base.appBarTheme.copyWith(
      backgroundColor: scheme.surface,
      foregroundColor: scheme.onSurface,
      titleTextStyle: base.appBarTheme.titleTextStyle?.copyWith(
        color: scheme.onSurface,
      ),
    ),
    cardTheme: base.cardTheme.copyWith(
      color: scheme.surface,
      shape: RoundedRectangleBorder(
        borderRadius: BorderRadius.circular(18),
        side: const BorderSide(color: Color(0xff30425b)),
      ),
    ),
    drawerTheme: DrawerThemeData(backgroundColor: scheme.surface),
    inputDecorationTheme: base.inputDecorationTheme.copyWith(
      fillColor: const Color(0xff101d31),
      labelStyle: const TextStyle(color: Color(0xffb6c8e1)),
      enabledBorder: OutlineInputBorder(
        borderRadius: BorderRadius.circular(12),
        borderSide: const BorderSide(color: Color(0xff435875)),
      ),
    ),
    listTileTheme: base.listTileTheme.copyWith(
      iconColor: scheme.primary,
      titleTextStyle: base.listTileTheme.titleTextStyle?.copyWith(
        color: scheme.onSurface,
      ),
      subtitleTextStyle: base.listTileTheme.subtitleTextStyle?.copyWith(
        color: const Color(0xffb6c8e1),
      ),
    ),
    chipTheme: base.chipTheme.copyWith(
      backgroundColor: const Color(0xff223e69),
      selectedColor: const Color(0xff2a4c7e),
      labelStyle: const TextStyle(color: Color(0xffe1eaf8)),
      side: const BorderSide(color: Color(0xff435875)),
    ),
    popupMenuTheme: base.popupMenuTheme.copyWith(color: scheme.surface),
    dividerTheme: const DividerThemeData(color: Color(0xff30425b), space: 28),
  );
}
