import 'package:file_picker/file_picker.dart';
import 'package:flutter/foundation.dart';
import 'package:flutter/material.dart';
import 'package:url_launcher/url_launcher.dart';

import 'api.dart';
import 'meeting_room.dart';
export 'api.dart' show parseServer;

const blue = Color(0xff1745c3);
const ink = Color(0xff15264b);
const paper = Color(0xfff3f6fc);

void main() {
  const url = String.fromEnvironment(
    'GAROWE_HR_URL',
    defaultValue: 'https://garowe-hr-production.up.railway.app',
  );
  runApp(GaroweHrApp(server: parseServer(url, allowLoopback: kDebugMode)));
}

class GaroweHrApp extends StatelessWidget {
  const GaroweHrApp({super.key, required this.server});
  final Uri? server;
  @override
  Widget build(BuildContext context) => MaterialApp(
    title: 'Garowe City',
    debugShowCheckedModeBanner: false,
    theme: ThemeData(
      colorScheme: ColorScheme.fromSeed(
        seedColor: blue,
      ).copyWith(primary: blue),
      scaffoldBackgroundColor: paper,
      appBarTheme: const AppBarTheme(
        backgroundColor: paper,
        titleTextStyle: TextStyle(
          color: ink,
          fontSize: 19,
          fontWeight: FontWeight.w700,
        ),
      ),
      inputDecorationTheme: InputDecorationTheme(
        filled: true,
        fillColor: Colors.white,
        border: OutlineInputBorder(borderRadius: BorderRadius.circular(14)),
      ),
      cardTheme: const CardThemeData(elevation: 0, color: Colors.white),
      useMaterial3: true,
    ),
    home: server == null
        ? const Scaffold(
            body: Center(
              child: Text('Configure the Garowe City server address.'),
            ),
          )
        : SessionGate(api: Api(server!)),
  );
}

void message(BuildContext context, Object text) {
  ScaffoldMessenger.of(
    context,
  ).showSnackBar(SnackBar(content: Text(text.toString())));
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
                Text(error!),
                FilledButton(
                  onPressed: () {
                    setState(() {
                      error = null;
                      loading = true;
                    });
                    _restore();
                  },
                  child: const Text('Try again'),
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
                  child: const Text('Sign in'),
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
          padding: const EdgeInsets.all(24),
          child: ConstrainedBox(
            constraints: const BoxConstraints(maxWidth: 440),
            child: Form(
              key: form,
              child: Column(
                crossAxisAlignment: CrossAxisAlignment.stretch,
                children: [
                  Center(
                    child: Image.asset(
                      'assets/garowe-local-government.jpg',
                      width: 86,
                      height: 86,
                    ),
                  ),
                  const SizedBox(height: 20),
                  Text(
                    'Garowe City',
                    textAlign: TextAlign.center,
                    style: Theme.of(context).textTheme.headlineMedium?.copyWith(
                      fontWeight: FontWeight.w800,
                      color: ink,
                    ),
                  ),
                  const Text('Management System', textAlign: TextAlign.center),
                  const SizedBox(height: 28),
                  Text(
                    'Sign in',
                    style: Theme.of(context).textTheme.titleLarge,
                  ),
                  const SizedBox(height: 16),
                  TextFormField(
                    controller: username,
                    decoration: const InputDecoration(
                      labelText: 'Email or employee number',
                      prefixIcon: Icon(Icons.person_outline),
                    ),
                    autofillHints: const [AutofillHints.username],
                    textInputAction: TextInputAction.next,
                    validator: (value) => value == null || value.trim().isEmpty
                        ? 'Enter your email or employee number.'
                        : null,
                  ),
                  const SizedBox(height: 16),
                  TextFormField(
                    controller: password,
                    obscureText: !show,
                    decoration: InputDecoration(
                      labelText: 'Password',
                      prefixIcon: const Icon(Icons.lock_outline),
                      suffixIcon: IconButton(
                        tooltip: show ? 'Hide password' : 'Show password',
                        icon: Icon(
                          show ? Icons.visibility_off : Icons.visibility,
                        ),
                        onPressed: () => setState(() => show = !show),
                      ),
                    ),
                    autofillHints: const [AutofillHints.password],
                    onFieldSubmitted: (_) {
                      if (!busy) login();
                    },
                    validator: (value) => value == null || value.isEmpty
                        ? 'Enter your password.'
                        : null,
                  ),
                  if (error != null)
                    Padding(
                      padding: const EdgeInsets.symmetric(vertical: 12),
                      child: Text(
                        error!,
                        style: const TextStyle(color: Colors.red),
                      ),
                    ),
                  const SizedBox(height: 20),
                  FilledButton(
                    onPressed: busy ? null : login,
                    child: Text(busy ? 'Signing in…' : 'Sign in'),
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
                    child: const Text('Forgot password?'),
                  ),
                ],
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
    if (sent && (code.text.trim().isEmpty || password.text.length < 10)) {
      message(
        context,
        'Enter the reset code and a password of at least 10 characters.',
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
      const Text('Use the email registered with your account.'),
      TextField(
        controller: email,
        keyboardType: TextInputType.emailAddress,
        decoration: const InputDecoration(labelText: 'Registered email'),
      ),
      if (notice != null) Text(notice!),
      if (sent) ...[
        const Text(
          'Paste the code from your reset email, or paste the complete reset link.',
        ),
        TextField(
          controller: code,
          decoration: const InputDecoration(labelText: 'Reset code or link'),
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
          decoration: InputDecoration(
            labelText: 'New password',
            suffixIcon: IconButton(
              onPressed: () => setState(() => show = !show),
              icon: Icon(show ? Icons.visibility_off : Icons.visibility),
            ),
          ),
        ),
      ],
      FilledButton(
        onPressed: busy ? null : submit,
        child: Text(
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
          child: const Text('Send another email'),
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
    if (password.text.length < 10 || password.text != confirm.text) {
      message(context, 'Use at least 10 characters and matching passwords.');
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
        const Text('Choose a new password before entering your workspace.'),
      for (final entry in [
        (current, 'Current password'),
        (password, 'New password'),
        (confirm, 'Confirm new password'),
      ])
        TextField(
          controller: entry.$1,
          obscureText: !show,
          decoration: InputDecoration(labelText: entry.$2),
        ),
      SwitchListTile(
        title: const Text('Show passwords'),
        value: show,
        onChanged: (value) => setState(() => show = value),
      ),
      const Text(
        'Use at least 10 characters with uppercase, lowercase, a number and a symbol.',
      ),
      FilledButton(
        onPressed: busy ? null : save,
        child: Text(busy ? 'Saving…' : 'Save password'),
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
      title: Text(title),
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
        title: const Text('Sign out?'),
        content: const Text('This ends your signed-in mobile session.'),
        actions: [
          TextButton(
            onPressed: () => Navigator.pop(context, false),
            child: const Text('Cancel'),
          ),
          FilledButton(
            onPressed: () => Navigator.pop(context, true),
            child: const Text('Sign out'),
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
    appBar: AppBar(
      title: Text(labels[index]),
      actions: [
        IconButton(
          tooltip: 'Notifications',
          icon: const Icon(Icons.notifications_outlined),
          onPressed: () => Navigator.push(
            context,
            MaterialPageRoute(
              builder: (_) => NotificationsScreen(api: widget.api),
            ),
          ),
        ),
        PopupMenuButton<String>(
          tooltip: 'Account',
          onSelected: account,
          icon: CircleAvatar(
            radius: 16,
            foregroundImage: avatar == null ? null : MemoryImage(avatar!),
            child: const Icon(Icons.person_outline, size: 20),
          ),
          itemBuilder: (_) => const [
            PopupMenuItem(value: 'profile', child: Text('My profile')),
            PopupMenuItem(value: 'logout', child: Text('Sign out')),
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
      color: Colors.white,
      child: SafeArea(
        top: false,
        child: Row(
          children: [
            for (var i = 0; i < labels.length; i++)
              Expanded(
                child: Semantics(
                  selected: i == index,
                  button: true,
                  label: labels[i],
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
                              color: i == index
                                  ? const Color(0xffe2eaff)
                                  : Colors.transparent,
                              borderRadius: BorderRadius.circular(18),
                            ),
                            child: Icon(
                              icons[i],
                              color: i == index ? blue : ink,
                              size: 22,
                            ),
                          ),
                          const SizedBox(height: 3),
                          Text(
                            labels[i],
                            textAlign: TextAlign.center,
                            style: TextStyle(
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
                Text(snapshot.error.toString(), textAlign: TextAlign.center),
                FilledButton(onPressed: reload, child: const Text('Try again')),
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
  label: Text(statusLabel(text), style: const TextStyle(fontSize: 12)),
  padding: EdgeInsets.zero,
  visualDensity: VisualDensity.compact,
);

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
          padding: const EdgeInsets.all(24),
          decoration: BoxDecoration(
            gradient: const LinearGradient(colors: [ink, blue]),
            borderRadius: BorderRadius.circular(22),
          ),
          child: Column(
            crossAxisAlignment: CrossAxisAlignment.start,
            children: [
              Image.asset(
                'assets/garowe-local-government.jpg',
                width: 55,
                height: 55,
              ),
              const SizedBox(height: 16),
              Text(
                'My dashboard',
                style: Theme.of(context).textTheme.headlineSmall?.copyWith(
                  color: Colors.white,
                  fontWeight: FontWeight.w800,
                ),
              ),
              const Text(
                'Garowe City Management System',
                style: TextStyle(color: Colors.white70),
              ),
            ],
          ),
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
                          child: Text(
                            '${total == 0 ? 0 : done * 100 ~/ total}%',
                            style: const TextStyle(fontWeight: FontWeight.w800),
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
                        Text(
                          'My task progress',
                          style: Theme.of(context).textTheme.titleMedium,
                        ),
                        Text('$done of $total completed'),
                        Text('$open open · $working in progress'),
                        TextButton(
                          onPressed: () => onOpen(1),
                          child: const Text('View my tasks'),
                        ),
                      ],
                    ),
                  ),
                ],
              ),
            ),
          ),
        Wrap(
          spacing: 8,
          runSpacing: 8,
          children: [
            if (data['hoursThisMonth'] != null)
              badge('${money(data['hoursThisMonth'])} work hours this month'),
            if (data['upcomingMeetings'] != null)
              badge('${data['upcomingMeetings']} active / upcoming meetings'),
          ],
        ),
        OutlinedButton.icon(
          onPressed: reload,
          icon: const Icon(Icons.refresh),
          label: const Text('Refresh progress'),
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
    builder: (data, reload) => WorkList(
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
          '${item['title']} ${item['responsibility'] ?? ''} ${item['description'] ?? item['agenda'] ?? ''}'
              .toLowerCase()
              .contains(search.toLowerCase());
    }).toList();
    return contentList([
      TextField(
        decoration: const InputDecoration(
          hintText: 'Search',
          prefixIcon: Icon(Icons.search),
        ),
        onChanged: (value) => setState(() => search = value),
      ),
      Wrap(
        spacing: 6,
        children: [
          for (final option in options)
            ChoiceChip(
              label: Text(option),
              selected: filter == option,
              onSelected: (_) => setState(() => filter = option),
            ),
        ],
      ),
      if (rows.isEmpty)
        const Padding(
          padding: EdgeInsets.all(24),
          child: Text('No records match this selection.'),
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
            title: Text(
              item['responsibility'] ?? item['title'],
              style: const TextStyle(fontWeight: FontWeight.w700),
            ),
            subtitle: Column(
              crossAxisAlignment: CrossAxisAlignment.start,
              children: [
                badge(widget.isMeeting ? meetingStatus(item) : item['status']),
                Text(
                  widget.isMeeting
                      ? date(item['startsUtc'], time: true)
                      : item['dueDate'] == null
                      ? 'No due date'
                      : 'Due ${date(item['dueDate'])}',
                ),
                if (widget.isMeeting)
                  Text('${item['attending']} / ${item['invited']} checked in'),
                if ((item['description'] ?? item['agenda'] ?? '')
                    .toString()
                    .isNotEmpty)
                  Text(
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
      Text(
        widget.task['responsibility'] ?? widget.task['title'],
        style: Theme.of(context).textTheme.headlineSmall,
      ),
      if (widget.task['responsibility'] != null)
        Text('Team task · ${widget.task['title']}'),
      Text('Due ${date(widget.task['dueDate'])}'),
      Text(widget.task['description'] ?? ''),
      if (status != 'Cancelled') ...[
        DropdownButtonFormField<String>(
          initialValue: status,
          decoration: const InputDecoration(labelText: 'My status'),
          items: [
            for (final value in ['Open', 'InProgress', 'Done'])
              DropdownMenuItem(value: value, child: Text(statusLabel(value))),
          ],
          onChanged: busy ? null : (value) => setState(() => status = value!),
        ),
        FilledButton(
          onPressed: busy ? null : save,
          child: Text(busy ? 'Saving…' : 'Save status'),
        ),
      ] else
        const Text('This task was cancelled.'),
    ],
  );
}

class MeetingsScreen extends StatelessWidget {
  const MeetingsScreen({super.key, required this.api});
  final Api api;
  @override
  Widget build(BuildContext context) => DataView(
    load: () => api.get('/api/mobile/v2/meetings'),
    builder: (data, reload) => WorkList(
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
    appBar: AppBar(title: const Text('Meeting details')),
    body: DataView(
      load: () => api.get('/api/mobile/v2/meetings/$id'),
      builder: (data, reload) {
        final m = data as Map<String, dynamic>;
        final closed = ['Ended', 'Cancelled'].contains(meetingStatus(m));
        return contentList([
          Text(
            m['title'],
            style: Theme.of(
              context,
            ).textTheme.headlineSmall?.copyWith(fontWeight: FontWeight.w700),
          ),
          badge(meetingStatus(m)),
          Text(date(m['startsUtc'], time: true)),
          Text(m['location'] ?? 'Online meeting'),
          Card(
            child: ListTile(
              leading: const Icon(Icons.how_to_reg_outlined),
              title: Text('${m['attending']} of ${m['invited']} checked in'),
            ),
          ),
          if (!closed && m['isInvited'] == true)
            FilledButton.icon(
              onPressed: m['checkedIn'] == true
                  ? null
                  : () => action(context, 'attend', reload),
              icon: const Icon(Icons.check_circle_outline),
              label: Text(
                m['checkedIn'] == true
                    ? 'Attendance recorded'
                    : "I'm attending",
              ),
            ),
          if (!closed && m['location'] == null)
            FilledButton.icon(
              icon: const Icon(Icons.videocam_outlined),
              label: const Text('Join meeting'),
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
          Text('Shared agenda', style: Theme.of(context).textTheme.titleLarge),
          Text(
            (m['agenda'] as String).isEmpty
                ? 'No main agenda yet.'
                : m['agenda'],
          ),
          for (final item in m['agendaItems'] as List)
            Card(
              child: ListTile(
                title: Text(item['title']),
                subtitle: Text(item['notes'] ?? ''),
              ),
            ),
          if (!closed)
            OutlinedButton.icon(
              icon: const Icon(Icons.add),
              label: const Text('Add agenda item'),
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
          Text(
            'Published minutes',
            style: Theme.of(context).textTheme.titleLarge,
          ),
          Text(m['publishedMinutes'] ?? 'Minutes have not been published.'),
          if (!closed && m['isOwner'] == true)
            OutlinedButton.icon(
              icon: const Icon(Icons.stop_circle_outlined),
              label: const Text('End meeting for everyone'),
              onPressed: () async {
                final yes = await showDialog<bool>(
                  context: context,
                  builder: (context) => AlertDialog(
                    title: const Text('End meeting for everyone?'),
                    actions: [
                      TextButton(
                        onPressed: () => Navigator.pop(context, false),
                        child: const Text('Cancel'),
                      ),
                      FilledButton(
                        onPressed: () => Navigator.pop(context, true),
                        child: const Text('End meeting'),
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
        decoration: const InputDecoration(labelText: 'Title'),
      ),
      TextField(
        controller: notes,
        maxLength: 2000,
        minLines: 4,
        maxLines: 8,
        decoration: const InputDecoration(labelText: 'Notes'),
      ),
      FilledButton(
        onPressed: busy ? null : save,
        child: Text(busy ? 'Saving…' : 'Add to shared agenda'),
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
      Text('My attendance', style: Theme.of(context).textTheme.titleLarge),
      if ((data['items'] as List).isEmpty)
        Text(data['message'] ?? 'No attendance records yet.'),
      for (final item in data['items'])
        Card(
          child: ListTile(
            leading: const Icon(Icons.event_available_outlined, color: blue),
            title: Text(item['title']),
            subtitle: Text('${item['subtitle']}\n${item['details']}'),
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
      Text(
        'My published payslips',
        style: Theme.of(context).textTheme.titleLarge,
      ),
      if ((data as List).isEmpty) const Text('No published payslips yet.'),
      for (final pay in data)
        Card(
          child: ListTile(
            leading: const Icon(Icons.receipt_long_outlined, color: blue),
            title: Text('${date(pay['from'])} – ${date(pay['to'])}'),
            subtitle: Text('Net pay · ${pay['currency']} ${money(pay['net'])}'),
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
    appBar: AppBar(title: const Text('Payslip')),
    body: DataView(
      load: () => api.get('/api/mobile/v2/payroll/$id'),
      builder: (p, reload) => contentList([
        Text(
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
              title: Text(label),
              trailing: Text('${p['currency']} ${money(p[key])}'),
            ),
          ),
        Text(
          'Attendance waived · ${money((p['attendanceCalculated'] as num) - (p['attendanceApplied'] as num))}',
        ),
        Text('Revision ${p['revision']}'),
      ]),
    ),
  );
}

class NotificationsScreen extends StatelessWidget {
  const NotificationsScreen({super.key, required this.api});
  final Api api;
  @override
  Widget build(BuildContext context) => Scaffold(
    appBar: AppBar(title: const Text('Notifications')),
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
          child: const Text('Mark all read'),
        ),
        if ((data as List).isEmpty) const Text('You have no notifications.'),
        for (final n in data)
          Card(
            child: ListTile(
              leading: Icon(
                n['readUtc'] == null
                    ? Icons.mark_email_unread_outlined
                    : Icons.drafts_outlined,
                color: blue,
              ),
              title: Text(n['title']),
              subtitle: Text(date(n['createdUtc'], time: true)),
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
    appBar: AppBar(title: const Text('My profile')),
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
          Center(
            child: CircleAvatar(
              radius: 48,
              foregroundImage: photo == null ? null : MemoryImage(photo),
              child: const Icon(Icons.person_outline, size: 42),
            ),
          ),
          Text(
            employee?['fullName'] ?? api.email ?? 'My account',
            textAlign: TextAlign.center,
            style: Theme.of(context).textTheme.headlineSmall,
          ),
          if (employee != null) ...[
            Card(
              child: ListTile(
                title: Text(employee['employeeNumber']),
                subtitle: Text(employee['status']),
              ),
            ),
            Card(
              child: ListTile(
                title: const Text('Employment start'),
                subtitle: Text(date(employee['hireDate'])),
              ),
            ),
          ] else
            const Text(
              'Ask HR to link your employee record to view personal attendance and payroll.',
            ),
          Card(
            child: ListTile(
              leading: const Icon(Icons.email_outlined),
              title: Text(profile['email'] ?? ''),
            ),
          ),
          if (employee != null)
            FilledButton.icon(
              icon: const Icon(Icons.photo_camera_outlined),
              label: const Text('Change profile photo'),
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
            label: const Text('Change password'),
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
