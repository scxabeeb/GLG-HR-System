import 'ui_preferences.dart';
import 'package:file_picker/file_picker.dart';
import 'package:flutter/foundation.dart';
import 'package:flutter/material.dart';
import 'package:url_launcher/url_launcher.dart';

import 'api.dart';
import 'meeting_room.dart';
import 'management.dart';
import 'admin_workspace.dart';
import 'reports.dart';
export 'api.dart' show parseServer;

const blue = Color(0xff1745c3);
const ink = Color(0xff15264b);
const paper = Color(0xfff3f6fc);

void main() {
  const url = String.fromEnvironment(
    'GAROWE_HR_URL',
    defaultValue: 'https://erp.garowecity.pl.so',
  );
  runApp(GaroweHrApp(server: parseServer(url, allowLoopback: kDebugMode)));
}

ThemeData civicTheme() => ThemeData(
  fontFamily: 'Roboto',
  useMaterial3: true,
  colorScheme: ColorScheme.fromSeed(
    seedColor: blue,
    surface: Colors.white,
  ).copyWith(primary: blue, secondary: const Color(0xff9e8145)),
  scaffoldBackgroundColor: paper,
  appBarTheme: const AppBarTheme(
    backgroundColor: Colors.white,
    foregroundColor: ink,
    surfaceTintColor: Colors.transparent,
    scrolledUnderElevation: 0,
    toolbarHeight: 64,
    titleTextStyle: TextStyle(
      fontFamily: 'Roboto',
      color: ink,
      fontSize: 18,
      fontWeight: FontWeight.w700,
    ),
    shape: Border(bottom: BorderSide(color: Color(0xffe7ecf4))),
  ),
  inputDecorationTheme: InputDecorationTheme(
    filled: true,
    fillColor: const Color(0xfffafbfe),
    contentPadding: const EdgeInsets.symmetric(horizontal: 16, vertical: 17),
    labelStyle: const TextStyle(
      fontFamily: 'Roboto',
      color: Color(0xff586983),
      fontSize: 14,
    ),
    border: OutlineInputBorder(
      borderRadius: BorderRadius.circular(12),
      borderSide: const BorderSide(color: Color(0xffdce4ef)),
    ),
    enabledBorder: OutlineInputBorder(
      borderRadius: BorderRadius.circular(12),
      borderSide: const BorderSide(color: Color(0xffdce4ef)),
    ),
    focusedBorder: OutlineInputBorder(
      borderRadius: BorderRadius.circular(12),
      borderSide: const BorderSide(color: blue, width: 1.6),
    ),
  ),
  cardTheme: CardThemeData(
    elevation: 0,
    color: Colors.white,
    margin: EdgeInsets.zero,
    shape: RoundedRectangleBorder(
      borderRadius: BorderRadius.circular(18),
      side: const BorderSide(color: Color(0xffe4eaf3)),
    ),
  ),
  filledButtonTheme: FilledButtonThemeData(
    style: FilledButton.styleFrom(
      minimumSize: const Size(64, 48),
      shape: RoundedRectangleBorder(borderRadius: BorderRadius.circular(12)),
      textStyle: const TextStyle(
        fontFamily: 'Roboto',
        fontSize: 14,
        fontWeight: FontWeight.w700,
      ),
    ),
  ),
  outlinedButtonTheme: OutlinedButtonThemeData(
    style: OutlinedButton.styleFrom(
      minimumSize: const Size(64, 46),
      side: const BorderSide(color: Color(0xffcfdaec)),
      shape: RoundedRectangleBorder(borderRadius: BorderRadius.circular(12)),
      textStyle: const TextStyle(
        fontFamily: 'Roboto',
        fontSize: 14,
        fontWeight: FontWeight.w600,
      ),
    ),
  ),
  textButtonTheme: TextButtonThemeData(
    style: TextButton.styleFrom(
      minimumSize: const Size(48, 44),
      textStyle: const TextStyle(
        fontFamily: 'Roboto',
        fontWeight: FontWeight.w600,
      ),
    ),
  ),
  chipTheme: ChipThemeData(
    shape: RoundedRectangleBorder(borderRadius: BorderRadius.circular(9)),
    side: const BorderSide(color: Color(0xffdfe6f2)),
    selectedColor: const Color(0xffe7efff),
    labelStyle: const TextStyle(fontFamily: 'Roboto', fontSize: 12, color: ink),
  ),
  popupMenuTheme: PopupMenuThemeData(
    shape: RoundedRectangleBorder(borderRadius: BorderRadius.circular(16)),
    color: Colors.white,
    surfaceTintColor: Colors.transparent,
  ),
  dividerTheme: const DividerThemeData(color: Color(0xffe7ecf4), space: 28),
  listTileTheme: const ListTileThemeData(
    iconColor: blue,
    titleTextStyle: TextStyle(
      fontFamily: 'Roboto',
      color: ink,
      fontSize: 14,
      fontWeight: FontWeight.w600,
    ),
    subtitleTextStyle: TextStyle(
      fontFamily: 'Roboto',
      color: Color(0xff66748c),
      fontSize: 12,
      height: 1.5,
    ),
  ),
);

class GaroweHrApp extends StatefulWidget {
  const GaroweHrApp({super.key, required this.server});
  final Uri? server;
  @override
  State<GaroweHrApp> createState() => _GaroweHrAppState();
}

class _GaroweHrAppState extends State<GaroweHrApp> {
  final preferences = UiPreferences();
  late final Api? api = widget.server == null ? null : Api(widget.server!);
  @override
  void initState() {
    super.initState();
    preferences.load();
  }

  @override
  void dispose() {
    preferences.dispose();
    super.dispose();
  }

  @override
  Widget build(BuildContext context) => UiPreferencesScope(
    preferences: preferences,
    child: ListenableBuilder(
      listenable: preferences,
      builder: (context, _) => MaterialApp(
        title: 'Garowe City',
        debugShowCheckedModeBanner: false,
        theme: civicTheme(),
        darkTheme: civicDarkTheme(civicTheme()),
        themeMode: preferences.mode,
        home: api == null
            ? const Scaffold(
                body: Center(
                  child: UiText('Configure the Garowe City server address.'),
                ),
              )
            : SessionGate(api: api!),
      ),
    ),
  );
}

void message(BuildContext context, Object text) {
  ScaffoldMessenger.of(
    context,
  ).showSnackBar(SnackBar(content: UiText(text.toString())));
}

class SessionGate extends StatefulWidget {
  const SessionGate({super.key, required this.api});
  final Api api;
  @override
  State<SessionGate> createState() => _SessionGateState();
}

class _SessionGateState extends State<SessionGate> {
  bool loading = true;
  bool signedIn = false;
  String? error;

  @override
  void initState() {
    super.initState();
    widget.api.onUnauthorized = () {
      if (!mounted) return;
      Navigator.of(context).popUntil((route) => route.isFirst);
      setState(() => signedIn = false);
    };
    _restore();
  }

  Future<void> _restore() async {
    try {
      signedIn = await widget.api.restore();
    } catch (e) {
      error = e.toString();
    }
    if (mounted) setState(() => loading = false);
  }

  void _refresh() => setState(() => signedIn = widget.api.token != null);

  @override
  Widget build(BuildContext context) {
    if (loading) {
      return const Scaffold(body: Center(child: CircularProgressIndicator()));
    }
    if (error != null) {
      return Scaffold(
        body: Center(
          child: Padding(
            padding: const EdgeInsets.all(24),
            child: Column(
              mainAxisSize: MainAxisSize.min,
              children: [
                UiText.raw(error!),
                FilledButton(
                  onPressed: () {
                    setState(() {
                      error = null;
                      loading = true;
                    });
                    _restore();
                  },
                  child: const UiText('Try again'),
                ),
                TextButton(
                  onPressed: () async {
                    await widget.api.clear();
                    if (mounted) {
                      setState(() {
                        error = null;
                        signedIn = false;
                      });
                    }
                  },
                  child: const UiText('Sign in'),
                ),
              ],
            ),
          ),
        ),
      );
    }
    if (!signedIn) return LoginScreen(api: widget.api, onSignedIn: _refresh);
    if (widget.api.mustChange) {
      return PasswordScreen(
        api: widget.api,
        requiredChange: true,
        onChanged: _refresh,
      );
    }
    return EmployeeShell(
      api: widget.api,
      onSignedOut: () {
        setState(() => signedIn = false);
      },
    );
  }
}

class LoginScreen extends StatefulWidget {
  const LoginScreen({super.key, required this.api, required this.onSignedIn});
  final Api api;
  final VoidCallback onSignedIn;
  @override
  State<LoginScreen> createState() => _LoginScreenState();
}

class _LoginScreenState extends State<LoginScreen> {
  final username = TextEditingController();
  final password = TextEditingController();
  bool busy = false;
  bool show = false;
  String? error;
  final form = GlobalKey<FormState>();

  @override
  void dispose() {
    username.dispose();
    password.dispose();
    super.dispose();
  }

  Future<void> login() async {
    if (!form.currentState!.validate()) return;
    setState(() {
      busy = true;
      error = null;
    });
    try {
      await widget.api.login(username.text, password.text);
      widget.onSignedIn();
    } catch (e) {
      if (mounted) setState(() => error = e.toString());
    } finally {
      if (mounted) setState(() => busy = false);
    }
  }

  @override
  Widget build(BuildContext context) => Scaffold(
    body: SafeArea(
      child: Center(
        child: SingleChildScrollView(
          padding: const EdgeInsets.all(20),
          child: ConstrainedBox(
            constraints: const BoxConstraints(maxWidth: 440),
            child: Container(
              padding: const EdgeInsets.all(22),
              decoration: BoxDecoration(
                color: Theme.of(context).colorScheme.surface,
                borderRadius: BorderRadius.circular(24),
                border: Border.all(color: const Color(0xffe1e8f3)),
                boxShadow: const [
                  BoxShadow(
                    color: Color(0x091a3672),
                    blurRadius: 30,
                    offset: Offset(0, 12),
                  ),
                ],
              ),
              child: Form(
                key: form,
                child: Column(
                  crossAxisAlignment: CrossAxisAlignment.stretch,
                  children: [
                    const UiDisplayPreferences(),
                    const SizedBox(height: 16),
                    Center(
                      child: Image.asset(
                        'assets/garowe-local-government.jpg',
                        width: 64,
                        height: 64,
                      ),
                    ),
                    const SizedBox(height: 12),
                    UiText(
                      'Garowe City',
                      textAlign: TextAlign.center,
                      style: Theme.of(context).textTheme.headlineMedium
                          ?.copyWith(fontWeight: FontWeight.w800, color: ink),
                    ),
                    const UiText(
                      'MANAGEMENT SYSTEM',
                      textAlign: TextAlign.center,
                      style: TextStyle(
                        fontFamily: 'Roboto',
                        fontSize: 11,
                        letterSpacing: 2.2,
                        color: Color(0xff65748b),
                        fontWeight: FontWeight.w600,
                      ),
                    ),
                    const SizedBox(height: 20),
                    UiText(
                      'Welcome back',
                      style: Theme.of(context).textTheme.headlineSmall
                          ?.copyWith(color: ink, fontWeight: FontWeight.w800),
                    ),
                    const SizedBox(height: 6),
                    const UiText(
                      'Sign in to manage your work and stay connected.',
                      style: TextStyle(
                        fontFamily: 'Roboto',
                        color: Color(0xff66748c),
                        height: 1.5,
                      ),
                    ),
                    const SizedBox(height: 16),
                    TextFormField(
                      controller: username,
                      decoration: localizeDecoration(
                        context,
                        const InputDecoration(
                          labelText: 'Employee ID or email',
                          hintText: 'EMP0001',
                          prefixIcon: Icon(Icons.person_outline),
                        ),
                      ),
                      autofillHints: const [AutofillHints.username],
                      textInputAction: TextInputAction.next,
                      validator: (value) =>
                          value == null || value.trim().isEmpty
                          ? tr(context, 'Enter your employee ID or email.')
                          : null,
                    ),
                    const SizedBox(height: 16),
                    TextFormField(
                      controller: password,
                      obscureText: !show,
                      decoration: localizeDecoration(
                        context,
                        InputDecoration(
                          labelText: 'Password',
                          prefixIcon: const Icon(Icons.lock_outline),
                          suffixIcon: IconButton(
                            tooltip: tr(
                              context,
                              show ? 'Hide password' : 'Show password',
                            ),
                            icon: Icon(
                              show ? Icons.visibility_off : Icons.visibility,
                            ),
                            onPressed: () => setState(() => show = !show),
                          ),
                        ),
                      ),
                      autofillHints: const [AutofillHints.password],
                      onFieldSubmitted: (_) {
                        if (!busy) login();
                      },
                      validator: (value) => value == null || value.isEmpty
                          ? tr(context, 'Enter your password.')
                          : null,
                    ),
                    if (error != null)
                      Padding(
                        padding: const EdgeInsets.symmetric(vertical: 12),
                        child: UiText.raw(
                          error!,
                          style: const TextStyle(
                            fontFamily: 'Roboto',
                            color: Colors.red,
                          ),
                        ),
                      ),
                    const SizedBox(height: 24),
                    FilledButton(
                      onPressed: busy ? null : login,
                      child: UiText(busy ? 'Signing in…' : 'Sign in'),
                    ),
                    TextButton(
                      onPressed: busy
                          ? null
                          : () => Navigator.push(
                              context,
                              MaterialPageRoute(
                                builder: (_) => RecoveryScreen(api: widget.api),
                              ),
                            ),
                      child: const UiText('Forgot password?'),
                    ),
                  ],
                ),
              ),
            ),
          ),
        ),
      ),
    ),
  );
}

class RecoveryScreen extends StatefulWidget {
  const RecoveryScreen({super.key, required this.api});
  final Api api;
  @override
  State<RecoveryScreen> createState() => _RecoveryScreenState();
}

class _RecoveryScreenState extends State<RecoveryScreen> {
  final email = TextEditingController();
  final code = TextEditingController();
  final password = TextEditingController();
  bool sent = false, busy = false, show = false;
  String? notice;
  @override
  void dispose() {
    email.dispose();
    code.dispose();
    password.dispose();
    super.dispose();
  }

  Future<void> submit() async {
    if (!email.text.contains('@')) {
      message(context, 'Enter your registered email.');
      return;
    }
    if (sent && (code.text.trim().isEmpty || password.text.length < 4)) {
      message(
        context,
        'Enter the reset code and a password of at least 4 characters.',
      );
      return;
    }
    setState(() => busy = true);
    try {
      final result =
          await widget.api
                  .post('/api/mobile/v2/auth/${sent ? 'reset' : 'forgot'}', {
                    'email': email.text.trim(),
                    if (sent) 'code': code.text.trim(),
                    if (sent) 'password': password.text,
                  })
              as Map;
      if (!mounted) return;
      if (sent) {
        message(context, result['message']);
        Navigator.pop(context);
      } else {
        setState(() {
          sent = true;
          notice = result['message'].toString();
        });
      }
    } catch (e) {
      if (mounted) message(context, e);
    } finally {
      if (mounted) setState(() => busy = false);
    }
  }

  @override
  Widget build(BuildContext context) => FormPage(
    title: 'Reset password',
    children: [
      const UiText('Use the email registered with your account.'),
      TextField(
        controller: email,
        keyboardType: TextInputType.emailAddress,
        decoration: localizeDecoration(
          context,
          const InputDecoration(labelText: 'Registered email'),
        ),
      ),
      if (notice != null) UiText.raw(notice!),
      if (sent) ...[
        const UiText(
          'Paste the code from your reset email, or paste the complete reset link.',
        ),
        TextField(
          controller: code,
          decoration: localizeDecoration(
            context,
            const InputDecoration(labelText: 'Reset code or link'),
          ),
          onChanged: (value) {
            final uri = Uri.tryParse(value);
            if (uri != null && uri.queryParameters['code'] != null) {
              code.text = uri.queryParameters['code']!;
              email.text = uri.queryParameters['email'] ?? email.text;
            }
          },
        ),
        TextField(
          controller: password,
          obscureText: !show,
          decoration: localizeDecoration(
            context,
            InputDecoration(
              labelText: 'New password',
              suffixIcon: IconButton(
                onPressed: () => setState(() => show = !show),
                icon: Icon(show ? Icons.visibility_off : Icons.visibility),
              ),
            ),
          ),
        ),
      ],
      FilledButton(
        onPressed: busy ? null : submit,
        child: UiText(
          busy
              ? 'Please wait…'
              : sent
              ? 'Reset password'
              : 'Send reset instructions',
        ),
      ),
      if (sent)
        TextButton(
          onPressed: busy ? null : () => setState(() => sent = false),
          child: const UiText('Send another email'),
        ),
    ],
  );
}

class PasswordScreen extends StatefulWidget {
  const PasswordScreen({
    super.key,
    required this.api,
    required this.onChanged,
    this.requiredChange = false,
  });
  final Api api;
  final VoidCallback onChanged;
  final bool requiredChange;
  @override
  State<PasswordScreen> createState() => _PasswordScreenState();
}

class _PasswordScreenState extends State<PasswordScreen> {
  final current = TextEditingController(),
      password = TextEditingController(),
      confirm = TextEditingController();
  bool busy = false, show = false;
  @override
  void dispose() {
    current.dispose();
    password.dispose();
    confirm.dispose();
    super.dispose();
  }

  Future<void> save() async {
    if (password.text.length < 4 || password.text != confirm.text) {
      message(context, 'Use at least 4 characters and matching passwords.');
      return;
    }
    setState(() => busy = true);
    try {
      await widget.api.changePassword(current.text, password.text);
      if (!mounted) return;
      widget.onChanged();
      if (!widget.requiredChange) Navigator.pop(context);
    } catch (e) {
      if (mounted) message(context, e);
    } finally {
      if (mounted) setState(() => busy = false);
    }
  }

  @override
  Widget build(BuildContext context) => FormPage(
    title: 'Change password',
    canBack: !widget.requiredChange,
    children: [
      if (widget.requiredChange)
        const UiText(
          'Choose your own password before entering your workspace. Your initial password is your employee ID followed by 123.',
        ),
      for (final entry in [
        (current, 'Current password'),
        (password, 'New password'),
        (confirm, 'Confirm new password'),
      ])
        TextField(
          controller: entry.$1,
          obscureText: !show,
          decoration: localizeDecoration(
            context,
            InputDecoration(labelText: entry.$2),
          ),
        ),
      SwitchListTile(
        title: const UiText('Show passwords'),
        value: show,
        onChanged: (value) => setState(() => show = value),
      ),
      const UiText('Use at least 4 characters.'),
      FilledButton(
        onPressed: busy ? null : save,
        child: UiText(busy ? 'Saving…' : 'Save password'),
      ),
    ],
  );
}

class FormPage extends StatelessWidget {
  const FormPage({
    super.key,
    required this.title,
    required this.children,
    this.canBack = true,
    this.actions,
  });
  final String title;
  final List<Widget> children;
  final bool canBack;
  final List<Widget>? actions;
  @override
  Widget build(BuildContext context) => Scaffold(
    appBar: AppBar(
      title: UiText(title),
      automaticallyImplyLeading: canBack,
      actions: actions,
    ),
    body: SafeArea(
      top: false,
      child: Center(
        child: ConstrainedBox(
          constraints: const BoxConstraints(maxWidth: 720),
          child: ListView(
            padding: const EdgeInsets.all(20),
            children: [
              for (final child in children)
                Padding(
                  padding: const EdgeInsets.only(bottom: 16),
                  child: child,
                ),
            ],
          ),
        ),
      ),
    ),
  );
}

class EmployeeShell extends StatefulWidget {
  const EmployeeShell({
    super.key,
    required this.api,
    required this.onSignedOut,
  });
  final Api api;
  final VoidCallback onSignedOut;
  @override
  State<EmployeeShell> createState() => _EmployeeShellState();
}

class _EmployeeShellState extends State<EmployeeShell> {
  int index = 0;
  int revision = 0;
  Uint8List? avatar;
  static const labels = [
    'Overview',
    'Tasks',
    'Meetings',
    'Attendance',
    'Payroll',
  ];
  static const icons = [
    Icons.dashboard_outlined,
    Icons.assignment_outlined,
    Icons.groups_outlined,
    Icons.event_available_outlined,
    Icons.receipt_long_outlined,
  ];
  @override
  void initState() {
    super.initState();
    _avatar();
  }

  Future<void> _avatar() async {
    try {
      final bytes = await widget.api.avatar();
      if (mounted) setState(() => avatar = bytes);
    } catch (_) {}
  }

  Future<void> logout() async {
    final yes = await showDialog<bool>(
      context: context,
      builder: (context) => AlertDialog(
        title: const UiText('Sign out?'),
        content: const UiText('This ends your signed-in mobile session.'),
        actions: [
          TextButton(
            onPressed: () => Navigator.pop(context, false),
            child: const UiText('Cancel'),
          ),
          FilledButton(
            onPressed: () => Navigator.pop(context, true),
            child: const UiText('Sign out'),
          ),
        ],
      ),
    );
    if (yes != true) return;
    try {
      await widget.api.logout();
      widget.onSignedOut();
    } on ApiError catch (e) {
      if (e.unauthorized) {
        await widget.api.clear();
        widget.onSignedOut();
      } else if (mounted) {
        message(context, e);
      }
    }
  }

  Future<void> account(String value) async {
    if (value == 'logout') {
      await logout();
      return;
    }
    if (!mounted) return;
    await Navigator.push(
      context,
      MaterialPageRoute(
        builder: (_) => ProfileScreen(api: widget.api, onChanged: _avatar),
      ),
    );
    if (mounted) _avatar();
  }

  @override
  Widget build(BuildContext context) => Scaffold(
    drawer: Drawer(
      backgroundColor: Theme.of(context).colorScheme.surface,
      child: SafeArea(
        child: ListView(
          padding: const EdgeInsets.fromLTRB(16, 20, 16, 20),
          children: [
            Row(
              children: [
                ClipRRect(
                  borderRadius: BorderRadius.circular(12),
                  child: Image.asset(
                    'assets/garowe-local-government.jpg',
                    width: 52,
                    height: 52,
                  ),
                ),
                const SizedBox(width: 12),
                const Expanded(
                  child: Column(
                    crossAxisAlignment: CrossAxisAlignment.start,
                    children: [
                      UiText(
                        'Garowe City',
                        style: TextStyle(
                          fontFamily: 'Roboto',
                          color: ink,
                          fontSize: 21,
                          fontWeight: FontWeight.w800,
                        ),
                      ),
                      UiText(
                        'Management System',
                        style: TextStyle(
                          fontFamily: 'Roboto',
                          fontSize: 12,
                          color: Color(0xff66748c),
                        ),
                      ),
                    ],
                  ),
                ),
              ],
            ),
            const SizedBox(height: 16),
            UiText.raw(
              widget.api.email ?? 'My workspace',
              style: const TextStyle(
                fontFamily: 'Roboto',
                fontSize: 13,
                color: Color(0xff66748c),
              ),
            ),
            const Divider(),
            const UiText(
              'WORKSPACE',
              style: TextStyle(
                fontFamily: 'Roboto',
                fontSize: 11,
                letterSpacing: 1.4,
                fontWeight: FontWeight.w700,
                color: Color(0xff66748c),
              ),
            ),
            const SizedBox(height: 12),
            for (var i = 0; i < labels.length; i++)
              Padding(
                padding: const EdgeInsets.only(bottom: 4),
                child: ListTile(
                  shape: RoundedRectangleBorder(
                    borderRadius: BorderRadius.circular(12),
                  ),
                  selected: i == index,
                  selectedTileColor:
                      Theme.of(context).brightness == Brightness.dark
                      ? const Color(0xff223e69)
                      : const Color(0xffe9f0ff),
                  leading: Icon(icons[i]),
                  title: UiText(labels[i]),
                  onTap: () {
                    Navigator.pop(context);
                    setState(() {
                      index = i;
                      revision++;
                    });
                  },
                ),
              ),
            const Divider(),
            ListTile(
              leading: const Icon(Icons.notifications_outlined),
              title: const UiText('Notifications'),
              onTap: () {
                Navigator.pop(context);
                Navigator.push(
                  context,
                  MaterialPageRoute(
                    builder: (_) => NotificationsScreen(api: widget.api),
                  ),
                );
              },
            ),
            ListTile(
              leading: const Icon(Icons.person_outline),
              title: const UiText('My profile'),
              onTap: () {
                Navigator.pop(context);
                account('profile');
              },
            ),
            if (widget.api.isAdmin)
              ListTile(
                leading: const Icon(Icons.admin_panel_settings_outlined),
                title: const UiText('Administration'),
                onTap: () {
                  Navigator.pop(context);
                  Navigator.push(
                    context,
                    MaterialPageRoute(
                      builder: (_) => AdminWorkspace(api: widget.api),
                    ),
                  );
                },
              ),
            const UiDisplayPreferences(),
            ListTile(
              leading: const Icon(Icons.logout, color: Color(0xffb64141)),
              title: const UiText(
                'Sign out',
                style: TextStyle(
                  fontFamily: 'Roboto',
                  color: Color(0xffb64141),
                ),
              ),
              onTap: () {
                Navigator.pop(context);
                logout();
              },
            ),
            const Divider(),
            const UiText(
              'Garowe City · 2.5.0',
              style: TextStyle(
                fontFamily: 'Roboto',
                color: Color(0xff8390a4),
                fontSize: 11,
              ),
            ),
          ],
        ),
      ),
    ),
    appBar: AppBar(
      title: Row(
        children: [
          ClipRRect(
            borderRadius: BorderRadius.circular(8),
            child: Image.asset(
              'assets/garowe-local-government.jpg',
              width: 30,
              height: 30,
            ),
          ),
          const SizedBox(width: 10),
          Expanded(
            child: Column(
              crossAxisAlignment: CrossAxisAlignment.start,
              children: [
                const UiText(
                  'GAROWE CITY',
                  style: TextStyle(
                    fontFamily: 'Roboto',
                    color: Color(0xff77859c),
                    fontSize: 9,
                    letterSpacing: 1.5,
                    fontWeight: FontWeight.w700,
                  ),
                ),
                UiText(
                  labels[index],
                  maxLines: 1,
                  overflow: TextOverflow.ellipsis,
                  style: const TextStyle(
                    fontFamily: 'Roboto',
                    color: ink,
                    fontSize: 17,
                    fontWeight: FontWeight.w800,
                  ),
                ),
              ],
            ),
          ),
        ],
      ),
      actions: [
        IconButton(
          tooltip: tr(context, 'Notifications'),
          icon: const Icon(Icons.notifications_outlined),
          onPressed: () => Navigator.push(
            context,
            MaterialPageRoute(
              builder: (_) => NotificationsScreen(api: widget.api),
            ),
          ),
        ),
        PopupMenuButton<String>(
          tooltip: tr(context, 'Account'),
          onSelected: account,
          icon: CircleAvatar(
            radius: 16,
            foregroundImage: avatar == null ? null : MemoryImage(avatar!),
            child: const Icon(Icons.person_outline, size: 20),
          ),
          itemBuilder: (_) => const [
            PopupMenuItem(value: 'profile', child: UiText('My profile')),
            PopupMenuItem(value: 'logout', child: UiText('Sign out')),
          ],
        ),
      ],
    ),
    body: SafeArea(
      top: false,
      child: KeyedSubtree(
        key: ValueKey('$index-$revision'),
        child: switch (index) {
          1 => TasksScreen(api: widget.api),
          2 => MeetingsScreen(api: widget.api),
          3 => AttendanceScreen(api: widget.api),
          4 => PayrollScreen(api: widget.api),
          _ => OverviewScreen(
            api: widget.api,
            onOpen: (value) => setState(() => index = value),
          ),
        },
      ),
    ),
    bottomNavigationBar: Material(
      elevation: 8,
      shadowColor: const Color(0x16213968),
      shape: const RoundedRectangleBorder(
        borderRadius: BorderRadius.vertical(top: Radius.circular(20)),
      ),
      color: Theme.of(context).colorScheme.surface,
      child: SafeArea(
        top: false,
        child: Row(
          children: [
            for (var i = 0; i < labels.length; i++)
              Expanded(
                child: Semantics(
                  selected: i == index,
                  button: true,
                  label: tr(context, labels[i]),
                  child: InkWell(
                    key: ValueKey('nav-$i'),
                    onTap: () => setState(() {
                      index = i;
                      revision++;
                    }),
                    child: Padding(
                      padding: const EdgeInsets.symmetric(
                        vertical: 10,
                        horizontal: 2,
                      ),
                      child: Column(
                        mainAxisSize: MainAxisSize.min,
                        children: [
                          Container(
                            padding: const EdgeInsets.symmetric(
                              horizontal: 12,
                              vertical: 4,
                            ),
                            decoration: BoxDecoration(
                              color: i == index ? blue : Colors.transparent,
                              borderRadius: BorderRadius.circular(18),
                            ),
                            child: Icon(
                              icons[i],
                              color: i == index
                                  ? Colors.white
                                  : const Color(0xff7c8ba2),
                              size: 22,
                            ),
                          ),
                          const SizedBox(height: 3),
                          UiText(
                            labels[i],
                            textAlign: TextAlign.center,
                            style: TextStyle(
                              fontFamily: 'Roboto',
                              fontSize: 11,
                              fontWeight: i == index
                                  ? FontWeight.w800
                                  : FontWeight.w500,
                              color: i == index ? blue : ink,
                            ),
                          ),
                        ],
                      ),
                    ),
                  ),
                ),
              ),
          ],
        ),
      ),
    ),
  );
}

class DataView extends StatefulWidget {
  const DataView({super.key, required this.load, required this.builder});
  final Future<dynamic> Function() load;
  final Widget Function(dynamic data, VoidCallback reload) builder;
  @override
  State<DataView> createState() => _DataViewState();
}

class _DataViewState extends State<DataView> {
  late Future<dynamic> future = widget.load();
  void reload() {
    if (mounted) setState(() => future = widget.load());
  }

  @override
  Widget build(BuildContext context) => FutureBuilder<dynamic>(
    future: future,
    builder: (context, snapshot) {
      if (snapshot.connectionState != ConnectionState.done) {
        return const Center(child: CircularProgressIndicator());
      }
      if (snapshot.hasError) {
        return Center(
          child: Padding(
            padding: const EdgeInsets.all(24),
            child: Column(
              mainAxisSize: MainAxisSize.min,
              children: [
                const Icon(Icons.cloud_off_outlined, size: 40, color: blue),
                const SizedBox(height: 12),
                UiText.raw(
                  snapshot.error.toString(),
                  textAlign: TextAlign.center,
                ),
                FilledButton(
                  onPressed: reload,
                  child: const UiText('Try again'),
                ),
              ],
            ),
          ),
        );
      }
      return widget.builder(snapshot.data, reload);
    },
  );
}

Widget contentList(List<Widget> children, {Future<void> Function()? refresh}) {
  final list = LayoutBuilder(
    builder: (context, constraints) => ListView(
      physics: const AlwaysScrollableScrollPhysics(),
      padding: EdgeInsets.symmetric(
        horizontal: ((constraints.maxWidth - 680) / 2)
            .clamp(16, double.infinity)
            .toDouble(),
        vertical: 16,
      ),
      children: [
        for (final child in children)
          Padding(padding: const EdgeInsets.only(bottom: 12), child: child),
      ],
    ),
  );
  return refresh == null
      ? list
      : RefreshIndicator(onRefresh: refresh, child: list);
}

String date(Object? value, {bool time = false}) {
  final parsed = DateTime.tryParse(value?.toString() ?? '');
  if (parsed == null) return '—';
  final d = value.toString().endsWith('Z')
      ? parsed.toUtc().add(const Duration(hours: 3))
      : parsed;
  const months = [
    'Jan',
    'Feb',
    'Mar',
    'Apr',
    'May',
    'Jun',
    'Jul',
    'Aug',
    'Sep',
    'Oct',
    'Nov',
    'Dec',
  ];
  return '${d.day} ${months[d.month - 1]} ${d.year}${time ? ', ${d.hour.toString().padLeft(2, '0')}:${d.minute.toString().padLeft(2, '0')} EAT' : ''}';
}

String statusLabel(String value) =>
    value == 'InProgress' ? 'In progress' : value;
String money(dynamic value) => (value as num? ?? 0).toStringAsFixed(2);
Widget badge(String text) => Chip(
  label: UiText(
    statusLabel(text),
    style: const TextStyle(fontFamily: 'Roboto', fontSize: 12),
  ),
  padding: EdgeInsets.zero,
  visualDensity: VisualDensity.compact,
);

class DashboardMetric extends StatelessWidget {
  const DashboardMetric({
    super.key,
    required this.label,
    required this.value,
    required this.icon,
    required this.onTap,
  });
  final String label, value;
  final IconData icon;
  final VoidCallback onTap;
  @override
  Widget build(BuildContext context) => Card(
    child: InkWell(
      borderRadius: BorderRadius.circular(18),
      onTap: onTap,
      child: Padding(
        padding: const EdgeInsets.all(16),
        child: Column(
          crossAxisAlignment: CrossAxisAlignment.start,
          children: [
            Icon(icon, color: Theme.of(context).colorScheme.primary, size: 22),
            const SizedBox(height: 12),
            UiText.raw(
              value,
              maxLines: 1,
              overflow: TextOverflow.ellipsis,
              style: const TextStyle(
                fontFamily: 'Roboto',
                color: ink,
                fontSize: 25,
                fontWeight: FontWeight.w800,
              ),
            ),
            const SizedBox(height: 4),
            UiText(
              label,
              style: const TextStyle(
                fontFamily: 'Roboto',
                color: Color(0xff66748c),
                fontSize: 12,
                height: 1.4,
              ),
            ),
          ],
        ),
      ),
    ),
  );
}

class OverviewScreen extends StatelessWidget {
  const OverviewScreen({super.key, required this.api, required this.onOpen});
  final Api api;
  final ValueChanged<int> onOpen;
  @override
  Widget build(BuildContext context) => DataView(
    load: () => api.get('/api/mobile/v1/overview'),
    builder: (data, reload) {
      final tasks = data['tasks'] as Map?;
      final done = tasks?['done'] as int? ?? 0,
          open = tasks?['open'] as int? ?? 0,
          working = tasks?['inProgress'] as int? ?? 0;
      final total = done + open + working;
      return contentList([
        Container(
          padding: const EdgeInsets.all(22),
          decoration: BoxDecoration(
            gradient: const LinearGradient(
              colors: [Color(0xff142b5a), Color(0xff2354ba)],
              begin: Alignment.topLeft,
              end: Alignment.bottomRight,
            ),
            borderRadius: BorderRadius.circular(22),
          ),
          child: Row(
            children: [
              const Expanded(
                child: Column(
                  crossAxisAlignment: CrossAxisAlignment.start,
                  children: [
                    UiText(
                      'YOUR WORKSPACE',
                      style: TextStyle(
                        fontFamily: 'Roboto',
                        color: Color(0xffa9c4fb),
                        fontSize: 10,
                        letterSpacing: 1.7,
                        fontWeight: FontWeight.w700,
                      ),
                    ),
                    SizedBox(height: 8),
                    UiText(
                      'My dashboard',
                      style: TextStyle(
                        fontFamily: 'Roboto',
                        color: Colors.white,
                        fontSize: 25,
                        fontWeight: FontWeight.w800,
                      ),
                    ),
                    SizedBox(height: 6),
                    UiText(
                      'Your work, at a glance.',
                      style: TextStyle(
                        fontFamily: 'Roboto',
                        color: Color(0xffc8d8f7),
                        fontSize: 13,
                        height: 1.5,
                      ),
                    ),
                  ],
                ),
              ),
              const SizedBox(width: 12),
              Container(
                padding: const EdgeInsets.all(6),
                decoration: BoxDecoration(
                  color: Colors.white,
                  borderRadius: BorderRadius.circular(18),
                ),
                child: Image.asset(
                  'assets/garowe-local-government.jpg',
                  width: 48,
                  height: 48,
                ),
              ),
            ],
          ),
        ),
        LayoutBuilder(
          builder: (context, bounds) {
            final width = (bounds.maxWidth - 12) / 2;
            return Wrap(
              spacing: 12,
              runSpacing: 12,
              children: [
                if (tasks != null) ...[
                  SizedBox(
                    width: width,
                    child: DashboardMetric(
                      label: 'Active tasks',
                      value: '${open + working}',
                      icon: Icons.assignment_outlined,
                      onTap: () => onOpen(1),
                    ),
                  ),
                  SizedBox(
                    width: width,
                    child: DashboardMetric(
                      label: 'Completed',
                      value: '$done',
                      icon: Icons.task_alt,
                      onTap: () => onOpen(1),
                    ),
                  ),
                ],
                if (data['upcomingMeetings'] != null)
                  SizedBox(
                    width: width,
                    child: DashboardMetric(
                      label: 'Upcoming meetings',
                      value: '${data['upcomingMeetings']}',
                      icon: Icons.event_outlined,
                      onTap: () => onOpen(2),
                    ),
                  ),
                if (data['hoursThisMonth'] != null)
                  SizedBox(
                    width: width,
                    child: DashboardMetric(
                      label: 'Work hours this month',
                      value: money(data['hoursThisMonth']),
                      icon: Icons.schedule,
                      onTap: () => onOpen(3),
                    ),
                  ),
              ],
            );
          },
        ),
        if (tasks != null)
          Card(
            child: Padding(
              padding: const EdgeInsets.all(20),
              child: Row(
                children: [
                  SizedBox.square(
                    dimension: 78,
                    child: Stack(
                      fit: StackFit.expand,
                      children: [
                        CircularProgressIndicator(
                          value: total == 0 ? 0 : done / total,
                          backgroundColor: const Color(0xffe2eaff),
                          strokeWidth: 7,
                        ),
                        Center(
                          child: UiText(
                            '${total == 0 ? 0 : done * 100 ~/ total}%',
                            style: const TextStyle(
                              fontFamily: 'Roboto',
                              fontWeight: FontWeight.w800,
                            ),
                          ),
                        ),
                      ],
                    ),
                  ),
                  const SizedBox(width: 20),
                  Expanded(
                    child: Column(
                      crossAxisAlignment: CrossAxisAlignment.start,
                      children: [
                        UiText(
                          'My task progress',
                          style: Theme.of(context).textTheme.titleMedium,
                        ),
                        UiText('$done of $total completed'),
                        UiText('$open open · $working in progress'),
                        TextButton(
                          onPressed: () => onOpen(1),
                          child: const UiText('View my tasks'),
                        ),
                      ],
                    ),
                  ),
                ],
              ),
            ),
          ),
        AdminReportEntry(api: api, module: 'tasks', dashboard: true),
        OutlinedButton.icon(
          onPressed: reload,
          icon: const Icon(Icons.refresh),
          label: const UiText('Refresh progress'),
        ),
      ], refresh: () async => reload());
    },
  );
}

class TasksScreen extends StatelessWidget {
  const TasksScreen({super.key, required this.api});
  final Api api;
  @override
  Widget build(BuildContext context) => DataView(
    load: () => api.get('/api/mobile/v2/tasks'),
    builder: (data, reload) => Column(
      children: [
        ManagementActions(api: api, module: 'tasks', refresh: reload),
        Expanded(
          child: WorkList(
            items: List<Map<String, dynamic>>.from(data as List),
            isMeeting: false,
            onOpen: (item) async {
              await Navigator.push(
                context,
                MaterialPageRoute(
                  builder: (_) => TaskScreen(api: api, task: item),
                ),
              );
              reload();
            },
            reload: reload,
          ),
        ),
      ],
    ),
  );
}

Widget statusPill(String value) {
  final done = value == 'Done' || value == 'Present';
  final closed = value == 'Cancelled' || value == 'Ended';
  return Builder(
    builder: (context) {
      final dark = Theme.of(context).brightness == Brightness.dark;
      return Container(
        margin: const EdgeInsets.symmetric(vertical: 6),
        padding: const EdgeInsets.symmetric(horizontal: 8, vertical: 4),
        decoration: BoxDecoration(
          color: done
              ? (dark ? const Color(0xff163e31) : const Color(0xffe8f6ee))
              : closed
              ? (dark ? const Color(0xff2b384b) : const Color(0xffedf0f5))
              : (dark ? const Color(0xff223e69) : const Color(0xffeaf0ff)),
          borderRadius: BorderRadius.circular(7),
        ),
        child: UiText(
          statusLabel(value),
          style: TextStyle(
            fontFamily: 'Roboto',
            color: done
                ? (dark ? const Color(0xffa6e7c8) : const Color(0xff26734b))
                : closed
                ? (dark ? const Color(0xffc4d2e5) : const Color(0xff64748b))
                : (dark ? const Color(0xffc1d8ff) : blue),
            fontSize: 11,
            fontWeight: FontWeight.w700,
          ),
        ),
      );
    },
  );
}

class WorkList extends StatefulWidget {
  const WorkList({
    super.key,
    required this.items,
    required this.isMeeting,
    required this.onOpen,
    required this.reload,
  });
  final List<Map<String, dynamic>> items;
  final bool isMeeting;
  final ValueChanged<Map<String, dynamic>> onOpen;
  final VoidCallback reload;
  @override
  State<WorkList> createState() => _WorkListState();
}

String meetingStatus(Map m) {
  if (m['cancelled'] == true) return 'Cancelled';
  final now = DateTime.now().toUtc();
  if (m['endedUtc'] != null ||
      DateTime.parse(m['endsUtc']).toUtc().isBefore(now)) {
    return 'Ended';
  }
  return DateTime.parse(m['startsUtc']).toUtc().isAfter(now)
      ? 'Upcoming'
      : 'In progress';
}

class _WorkListState extends State<WorkList> {
  String filter = 'All', search = '';
  @override
  Widget build(BuildContext context) {
    final options = widget.isMeeting
        ? ['All', 'Upcoming', 'In progress', 'Ended', 'Cancelled']
        : ['All', 'Open', 'In progress', 'Done', 'Cancelled'];
    final rows = widget.items.where((item) {
      final status = widget.isMeeting
          ? meetingStatus(item)
          : statusLabel(item['status']);
      return (filter == 'All' || status == filter) &&
          '${item['title']} ${item['responsibility'] ?? ''} ${item['description'] ?? item['agenda'] ?? ''} ${item['assigneeName'] ?? ''}'
              .toLowerCase()
              .contains(search.toLowerCase());
    }).toList();
    return contentList([
      TextField(
        decoration: localizeDecoration(
          context,
          const InputDecoration(
            hintText: 'Search',
            prefixIcon: Icon(Icons.search),
          ),
        ),
        onChanged: (value) => setState(() => search = value),
      ),
      Wrap(
        spacing: 6,
        children: [
          for (final option in options)
            ChoiceChip(
              label: UiText(option),
              selected: filter == option,
              onSelected: (_) => setState(() => filter = option),
            ),
        ],
      ),
      if (rows.isEmpty)
        const Padding(
          padding: EdgeInsets.all(24),
          child: UiText('No records match this selection.'),
        ),
      for (final item in rows)
        Card(
          child: ListTile(
            contentPadding: const EdgeInsets.symmetric(
              horizontal: 16,
              vertical: 8,
            ),
            leading: Icon(
              widget.isMeeting
                  ? Icons.event_outlined
                  : Icons.assignment_outlined,
              color: blue,
            ),
            title: UiText.raw(
              item['responsibility'] ?? item['title'],
              maxLines: 2,
              overflow: TextOverflow.ellipsis,
              style: const TextStyle(
                fontFamily: 'Roboto',
                fontWeight: FontWeight.w700,
              ),
            ),
            subtitle: Column(
              crossAxisAlignment: CrossAxisAlignment.start,
              children: [
                statusPill(
                  widget.isMeeting
                      ? meetingStatus(item)
                      : statusLabel(item['status']),
                ),
                UiText.raw(
                  widget.isMeeting
                      ? date(item['startsUtc'], time: true)
                      : item['dueDate'] == null
                      ? 'No due date'
                      : 'Due ${date(item['dueDate'])}',
                ),
                if (widget.isMeeting)
                  UiText(
                    '${item['attending']} / ${item['invited']} checked in',
                  ),
                if (!widget.isMeeting && item['assigneeName'] != null)
                  UiText('Assigned to ${item['assigneeName']}'),
                if ((item['description'] ?? item['agenda'] ?? '')
                    .toString()
                    .isNotEmpty)
                  UiText.raw(
                    item['description'] ?? item['agenda'],
                    maxLines: 2,
                    overflow: TextOverflow.ellipsis,
                  ),
              ],
            ),
            trailing: const Icon(Icons.chevron_right),
            onTap: () => widget.onOpen(item),
          ),
        ),
    ], refresh: () async => widget.reload());
  }
}

class TaskScreen extends StatefulWidget {
  const TaskScreen({super.key, required this.api, required this.task});
  final Api api;
  final Map<String, dynamic> task;
  @override
  State<TaskScreen> createState() => _TaskScreenState();
}

class _TaskScreenState extends State<TaskScreen> {
  late String status = widget.task['status'];
  bool busy = false;
  Future<void> save() async {
    setState(() => busy = true);
    try {
      await widget.api.post(
        '/api/mobile/v2/tasks/${widget.task['id']}/status',
        {'status': status},
      );
      if (mounted) {
        message(context, 'Task status saved.');
        Navigator.pop(context);
      }
    } catch (e) {
      if (mounted) message(context, e);
    } finally {
      if (mounted) setState(() => busy = false);
    }
  }

  @override
  Widget build(BuildContext context) => FormPage(
    title: 'Task details',
    children: [
      UiText.raw(
        widget.task['responsibility'] ?? widget.task['title'],
        style: Theme.of(context).textTheme.headlineSmall,
      ),
      if (widget.task['responsibility'] != null)
        UiText('Team task · ${widget.task['title']}'),
      UiText('Due ${date(widget.task['dueDate'])}'),
      if (widget.task['assigneeName'] != null)
        UiText('Assigned to ${widget.task['assigneeName']}'),
      if (widget.task['canManage'] == true && status != 'Cancelled')
        Wrap(
          spacing: 8,
          children: [
            OutlinedButton.icon(
              icon: const Icon(Icons.edit_outlined),
              label: const UiText('Edit / reassign'),
              onPressed: () => openManager(
                context,
                TaskEditor(api: widget.api, taskId: widget.task['id']),
                () {
                  if (mounted) Navigator.pop(context);
                },
              ),
            ),
            TextButton.icon(
              icon: const Icon(Icons.cancel_outlined),
              label: const UiText('Cancel task'),
              onPressed: () => confirmManage(
                context,
                widget.api,
                '/api/mobile/v2/manage/tasks/${widget.task['id']}/cancel',
                'Cancel this task?',
                () {
                  if (mounted) Navigator.pop(context);
                },
              ),
            ),
          ],
        ),
      OutlinedButton.icon(
        icon: const Icon(Icons.history),
        label: const UiText('Activity & progress'),
        onPressed: () => Navigator.push(
          context,
          MaterialPageRoute(
            builder: (_) => TaskHistory(api: widget.api, id: widget.task['id']),
          ),
        ),
      ),
      UiText.raw(widget.task['description'] ?? ''),
      if (status != 'Cancelled') ...[
        DropdownButtonFormField<String>(
          initialValue: status,
          decoration: localizeDecoration(
            context,
            const InputDecoration(labelText: 'My status'),
          ),
          items: [
            for (final value in ['Open', 'InProgress', 'Done'])
              DropdownMenuItem(value: value, child: UiText(statusLabel(value))),
          ],
          onChanged: busy ? null : (value) => setState(() => status = value!),
        ),
        FilledButton(
          onPressed: busy ? null : save,
          child: UiText(busy ? 'Saving…' : 'Save status'),
        ),
      ] else
        const UiText('This task was cancelled.'),
    ],
  );
}

class MeetingsScreen extends StatelessWidget {
  const MeetingsScreen({super.key, required this.api});
  final Api api;
  @override
  Widget build(BuildContext context) => DataView(
    load: () => api.get('/api/mobile/v2/meetings'),
    builder: (data, reload) => Column(
      children: [
        ManagementActions(api: api, module: 'meetings', refresh: reload),
        Expanded(
          child: WorkList(
            items: List<Map<String, dynamic>>.from(data as List),
            isMeeting: true,
            reload: reload,
            onOpen: (meeting) async {
              await Navigator.push(
                context,
                MaterialPageRoute(
                  builder: (_) => MeetingScreen(api: api, id: meeting['id']),
                ),
              );
              reload();
            },
          ),
        ),
      ],
    ),
  );
}

class MeetingScreen extends StatelessWidget {
  const MeetingScreen({super.key, required this.api, required this.id});
  final Api api;
  final int id;
  Future<void> action(
    BuildContext context,
    String action,
    VoidCallback reload,
  ) async {
    try {
      await api.post('/api/mobile/v2/meetings/$id/$action', {});
      reload();
    } catch (e) {
      if (context.mounted) message(context, e);
    }
  }

  @override
  Widget build(BuildContext context) => Scaffold(
    appBar: AppBar(title: const UiText('Meeting details')),
    body: DataView(
      load: () => api.get('/api/mobile/v2/meetings/$id'),
      builder: (data, reload) {
        final m = data as Map<String, dynamic>;
        final closed = ['Ended', 'Cancelled'].contains(meetingStatus(m));
        return contentList([
          UiText.raw(
            m['title'],
            style: Theme.of(
              context,
            ).textTheme.headlineSmall?.copyWith(fontWeight: FontWeight.w700),
          ),
          badge(meetingStatus(m)),
          if (m['isOwner'] == true)
            Wrap(
              spacing: 8,
              runSpacing: 4,
              children: [
                if (!closed)
                  OutlinedButton.icon(
                    icon: const Icon(Icons.edit_outlined),
                    label: const UiText('Edit meeting'),
                    onPressed: () => openManager(
                      context,
                      MeetingEditor(api: api, meeting: m),
                      reload,
                    ),
                  ),
                OutlinedButton.icon(
                  icon: const Icon(Icons.notes_outlined),
                  label: const UiText('Manage minutes'),
                  onPressed: () => openManager(
                    context,
                    MinutesEditor(api: api, meeting: m),
                    reload,
                  ),
                ),
                if (!closed)
                  TextButton.icon(
                    icon: const Icon(Icons.cancel_outlined),
                    label: const UiText('Cancel meeting'),
                    onPressed: () => confirmManage(
                      context,
                      api,
                      '/api/mobile/v2/manage/meetings/$id/cancel',
                      'Cancel this meeting?',
                      reload,
                    ),
                  ),
              ],
            ),
          UiText.raw(date(m['startsUtc'], time: true)),
          UiText.raw(m['location'] ?? 'Online meeting'),
          Card(
            child: ListTile(
              leading: const Icon(Icons.how_to_reg_outlined),
              title: UiText('${m['attending']} of ${m['invited']} checked in'),
            ),
          ),
          if (!closed && m['isInvited'] == true)
            FilledButton.icon(
              onPressed: m['checkedIn'] == true
                  ? null
                  : () => action(context, 'attend', reload),
              icon: const Icon(Icons.check_circle_outline),
              label: UiText.raw(
                m['checkedIn'] == true
                    ? 'Attendance recorded'
                    : "I'm attending",
              ),
            ),
          if (!closed && m['location'] == null)
            FilledButton.icon(
              icon: const Icon(Icons.videocam_outlined),
              label: const UiText('Join meeting'),
              onPressed: () async {
                if ((m['joinUrl'] as String).isNotEmpty) {
                  final url = Uri.tryParse(m['joinUrl']);
                  if (url?.scheme == 'https') {
                    await launchUrl(url!, mode: LaunchMode.externalApplication);
                  }
                } else {
                  if (!context.mounted) return;
                  await Navigator.push(
                    context,
                    MaterialPageRoute(
                      builder: (_) =>
                          NativeMeetingRoom(api: api, meetingId: id),
                    ),
                  );
                  reload();
                }
              },
            ),
          if (m['participants'] != null)
            ExpansionTile(
              title: const UiText('Participants & attendance'),
              children: [
                for (final p in m['participants'] as List)
                  ListTile(
                    dense: true,
                    leading: Icon(
                      p['attendedUtc'] == null
                          ? Icons.person_outline
                          : Icons.check_circle_outline,
                    ),
                    title: UiText.raw(p['name'] ?? 'Participant'),
                    subtitle: UiText.raw(
                      p['attendedUtc'] == null
                          ? 'Not checked in'
                          : 'Checked in',
                    ),
                  ),
              ],
            ),
          UiText(
            'Shared agenda',
            style: Theme.of(context).textTheme.titleLarge,
          ),
          UiText.raw(
            (m['agenda'] as String).isEmpty
                ? 'No main agenda yet.'
                : m['agenda'],
          ),
          for (final item in m['agendaItems'] as List)
            Card(
              child: ListTile(
                title: UiText.raw(item['title']),
                subtitle: UiText.raw(item['notes'] ?? ''),
              ),
            ),
          if (!closed)
            OutlinedButton.icon(
              icon: const Icon(Icons.add),
              label: const UiText('Add agenda item'),
              onPressed: () async {
                await Navigator.push(
                  context,
                  MaterialPageRoute(
                    builder: (_) => AgendaScreen(api: api, id: id),
                  ),
                );
                reload();
              },
            ),
          UiText(
            'Published minutes',
            style: Theme.of(context).textTheme.titleLarge,
          ),
          UiText.raw(
            m['publishedMinutes'] ?? 'Minutes have not been published.',
          ),
          if (meetingStatus(m) == 'In progress' && m['isOwner'] == true)
            OutlinedButton.icon(
              icon: const Icon(Icons.stop_circle_outlined),
              label: const UiText('End meeting for everyone'),
              onPressed: () async {
                final yes = await showDialog<bool>(
                  context: context,
                  builder: (context) => AlertDialog(
                    title: const UiText('End meeting for everyone?'),
                    actions: [
                      TextButton(
                        onPressed: () => Navigator.pop(context, false),
                        child: const UiText('Cancel'),
                      ),
                      FilledButton(
                        onPressed: () => Navigator.pop(context, true),
                        child: const UiText('End meeting'),
                      ),
                    ],
                  ),
                );
                if (yes == true && context.mounted) {
                  action(context, 'end', reload);
                }
              },
            ),
        ]);
      },
    ),
  );
}

class AgendaScreen extends StatefulWidget {
  const AgendaScreen({super.key, required this.api, required this.id});
  final Api api;
  final int id;
  @override
  State<AgendaScreen> createState() => _AgendaScreenState();
}

class _AgendaScreenState extends State<AgendaScreen> {
  final title = TextEditingController(), notes = TextEditingController();
  bool busy = false;
  @override
  void dispose() {
    title.dispose();
    notes.dispose();
    super.dispose();
  }

  Future<void> save() async {
    if (title.text.trim().isEmpty) {
      message(context, 'Enter an agenda title.');
      return;
    }
    setState(() => busy = true);
    try {
      await widget.api.post('/api/mobile/v2/meetings/${widget.id}/agenda', {
        'title': title.text,
        'notes': notes.text,
      });
      if (mounted) Navigator.pop(context);
    } catch (e) {
      if (mounted) message(context, e);
    } finally {
      if (mounted) setState(() => busy = false);
    }
  }

  @override
  Widget build(BuildContext context) => FormPage(
    title: 'Add agenda item',
    children: [
      TextField(
        controller: title,
        maxLength: 160,
        decoration: localizeDecoration(
          context,
          const InputDecoration(labelText: 'Title'),
        ),
      ),
      TextField(
        controller: notes,
        maxLength: 2000,
        minLines: 4,
        maxLines: 8,
        decoration: localizeDecoration(
          context,
          const InputDecoration(labelText: 'Notes'),
        ),
      ),
      FilledButton(
        onPressed: busy ? null : save,
        child: UiText(busy ? 'Saving…' : 'Add to shared agenda'),
      ),
    ],
  );
}

class AttendanceScreen extends StatelessWidget {
  const AttendanceScreen({super.key, required this.api});
  final Api api;
  @override
  Widget build(BuildContext context) => DataView(
    load: () => api.get('/api/mobile/v1/attendance'),
    builder: (data, reload) => contentList([
      UiText('My attendance', style: Theme.of(context).textTheme.titleLarge),
      if ((data['items'] as List).isEmpty)
        UiText.raw(data['message'] ?? 'No attendance records yet.'),
      for (final item in data['items'])
        Card(
          child: ListTile(
            leading: const Icon(Icons.event_available_outlined, color: blue),
            title: UiText.raw(item['title']),
            subtitle: UiText('${item['subtitle']}\n${item['details']}'),
          ),
        ),
    ], refresh: () async => reload()),
  );
}

class PayrollScreen extends StatelessWidget {
  const PayrollScreen({super.key, required this.api});
  final Api api;
  @override
  Widget build(BuildContext context) => DataView(
    load: () => api.get('/api/mobile/v2/payroll'),
    builder: (data, reload) => contentList([
      UiText(
        'My published payslips',
        style: Theme.of(context).textTheme.titleLarge,
      ),
      if ((data as List).isEmpty) const UiText('No published payslips yet.'),
      for (final pay in data)
        Card(
          child: ListTile(
            leading: const Icon(Icons.receipt_long_outlined, color: blue),
            title: UiText('${date(pay['from'])} – ${date(pay['to'])}'),
            subtitle: UiText(
              'Net pay · ${pay['currency']} ${money(pay['net'])}',
            ),
            trailing: const Icon(Icons.chevron_right),
            onTap: () => Navigator.push(
              context,
              MaterialPageRoute(
                builder: (_) => PayslipScreen(api: api, id: pay['id']),
              ),
            ),
          ),
        ),
    ], refresh: () async => reload()),
  );
}

class PayslipScreen extends StatelessWidget {
  const PayslipScreen({super.key, required this.api, required this.id});
  final Api api;
  final int id;
  @override
  Widget build(BuildContext context) => Scaffold(
    appBar: AppBar(title: const UiText('Payslip')),
    body: DataView(
      load: () => api.get('/api/mobile/v2/payroll/$id'),
      builder: (p, reload) => contentList([
        UiText(
          '${date(p['from'])} – ${date(p['to'])}',
          style: Theme.of(context).textTheme.titleLarge,
        ),
        for (final (label, key) in [
          ('Basic salary', 'basic'),
          ('Allowances', 'allowanceTotal'),
          ('Overtime', 'overtime'),
          ('Gross pay', 'gross'),
          ('Attendance deducted', 'attendanceApplied'),
          ('Unpaid leave', 'unpaidLeave'),
          ('Net pay', 'net'),
        ])
          Card(
            child: ListTile(
              title: UiText(label),
              trailing: UiText('${p['currency']} ${money(p[key])}'),
            ),
          ),
        UiText(
          'Attendance waived · ${money((p['attendanceCalculated'] as num) - (p['attendanceApplied'] as num))}',
        ),
        UiText('Revision ${p['revision']}'),
      ]),
    ),
  );
}

class NotificationsScreen extends StatelessWidget {
  const NotificationsScreen({super.key, required this.api});
  final Api api;
  @override
  Widget build(BuildContext context) => Scaffold(
    appBar: AppBar(title: const UiText('Notifications')),
    body: DataView(
      load: () => api.get('/api/mobile/v2/notifications'),
      builder: (data, reload) => contentList([
        TextButton(
          onPressed: () async {
            try {
              await api.post('/api/mobile/v2/notifications/read-all', {});
              reload();
            } catch (e) {
              if (context.mounted) message(context, e);
            }
          },
          child: const UiText('Mark all read'),
        ),
        if ((data as List).isEmpty) const UiText('You have no notifications.'),
        for (final n in data)
          Card(
            child: ListTile(
              leading: Icon(
                n['readUtc'] == null
                    ? Icons.mark_email_unread_outlined
                    : Icons.drafts_outlined,
                color: blue,
              ),
              title: UiText.raw(n['title']),
              subtitle: UiText.raw(date(n['createdUtc'], time: true)),
              onTap: () async {
                try {
                  await api.post(
                    '/api/mobile/v2/notifications/${n['id']}/read',
                    {},
                  );
                  if (!context.mounted) return;
                  if (n['meetingId'] != null) {
                    await Navigator.push(
                      context,
                      MaterialPageRoute(
                        builder: (_) =>
                            MeetingScreen(api: api, id: n['meetingId']),
                      ),
                    );
                  } else if (n['taskId'] != null) {
                    final tasks = await api.get('/api/mobile/v2/tasks') as List;
                    final matches = tasks.where((t) => t['id'] == n['taskId']);
                    if (matches.isNotEmpty && context.mounted) {
                      await Navigator.push(
                        context,
                        MaterialPageRoute(
                          builder: (_) => TaskScreen(
                            api: api,
                            task: Map<String, dynamic>.from(matches.first),
                          ),
                        ),
                      );
                    }
                  }
                  reload();
                } catch (e) {
                  if (context.mounted) message(context, e);
                }
              },
            ),
          ),
      ], refresh: () async => reload()),
    ),
  );
}

class ProfileScreen extends StatelessWidget {
  const ProfileScreen({super.key, required this.api, required this.onChanged});
  final Api api;
  final VoidCallback onChanged;
  @override
  Widget build(BuildContext context) => Scaffold(
    appBar: AppBar(title: const UiText('My profile')),
    body: DataView(
      load: () async => [
        await api.get('/api/mobile/v2/profile'),
        await api.avatar(),
      ],
      builder: (data, reload) {
        final profile = data[0] as Map;
        final employee = profile['employee'] as Map?;
        final photo = data[1] as Uint8List?;
        return contentList([
          const UiDisplayPreferences(),
          Center(
            child: CircleAvatar(
              radius: 48,
              foregroundImage: photo == null ? null : MemoryImage(photo),
              child: const Icon(Icons.person_outline, size: 42),
            ),
          ),
          UiText.raw(
            employee?['fullName'] ?? api.email ?? 'My account',
            textAlign: TextAlign.center,
            style: Theme.of(context).textTheme.headlineSmall,
          ),
          if (employee != null) ...[
            Card(
              child: ListTile(
                title: UiText.raw(employee['employeeNumber']),
                subtitle: UiText.raw(employee['status']),
              ),
            ),
            Card(
              child: ListTile(
                title: const UiText('Employment start'),
                subtitle: UiText.raw(date(employee['hireDate'])),
              ),
            ),
          ] else
            const UiText(
              'Ask HR to link your employee record to view personal attendance and payroll.',
            ),
          Card(
            child: ListTile(
              leading: const Icon(Icons.email_outlined),
              title: UiText.raw(profile['email'] ?? ''),
            ),
          ),
          if (employee != null)
            FilledButton.icon(
              icon: const Icon(Icons.photo_camera_outlined),
              label: const UiText('Change profile photo'),
              onPressed: () async {
                try {
                  final selected = await FilePicker.pickFiles(
                    type: FileType.custom,
                    allowedExtensions: ['jpg', 'jpeg', 'png'],
                  );
                  final path = selected?.files.single.path;
                  if (path == null) return;
                  await api.uploadPhoto(path);
                  onChanged();
                  reload();
                } catch (e) {
                  if (context.mounted) message(context, e);
                }
              },
            ),
          OutlinedButton.icon(
            icon: const Icon(Icons.lock_outline),
            label: const UiText('Change password'),
            onPressed: () => Navigator.push(
              context,
              MaterialPageRoute(
                builder: (_) => PasswordScreen(api: api, onChanged: onChanged),
              ),
            ),
          ),
        ]);
      },
    ),
  );
}
