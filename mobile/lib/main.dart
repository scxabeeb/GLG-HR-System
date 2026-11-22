import 'dart:convert';

import 'package:file_picker/file_picker.dart';
import 'package:flutter/foundation.dart';
import 'package:flutter/material.dart';
import 'package:permission_handler/permission_handler.dart';
import 'package:url_launcher/url_launcher.dart';
import 'package:webview_flutter/webview_flutter.dart';
import 'package:webview_flutter_android/webview_flutter_android.dart';

const _blue = Color(0xff1745c3);
const _ink = Color(0xff14234b);
const _paper = Color(0xfff3f6fc);
const _sessionPath = '/api/mobile/v1/session';

void main() {
  const server = String.fromEnvironment('GAROWE_HR_URL');
  runApp(GaroweHrApp(server: parseServer(server, allowLoopback: kDebugMode)));
}

Uri? parseServer(String value, {bool allowLoopback = false}) {
  final uri = Uri.tryParse(value.trim());
  if (uri == null ||
      (uri.scheme != 'https' &&
          !(allowLoopback &&
              uri.scheme == 'http' &&
              uri.host == '127.0.0.1')) ||
      uri.host.isEmpty ||
      uri.userInfo.isNotEmpty ||
      (uri.path.isNotEmpty && uri.path != '/') ||
      uri.hasQuery ||
      uri.hasFragment) {
    return null;
  }
  return uri;
}

enum EmployeePage {
  overview("Overview", "/"),
  logout('Sign out', '/Identity/Account/Logout?mobile=true'),
  login('Sign in', '/Identity/Account/Login?ReturnUrl=$_sessionPath'),
  notifications('Notifications', '/Operations/Notifications'),
  profile('My profile', '/Me/Profile'),
  attendance('Attendance', '/Me/Profile#attendance'),
  payroll('Payroll', '/Me/Profile#payroll'),
  tasks('My tasks', '/Operations/Tasks?view=mine'),
  meetings('My meetings', '/Operations/Meetings');

  const EmployeePage(this.title, this.path);
  final String title;
  final String path;

  Uri url(Uri server) => server.resolve(path);
  Uri dataUrl(Uri server) => this == login || this == logout
      ? url(server)
      : server.resolve('/api/mobile/v1/$name');
}

class GaroweHrApp extends StatelessWidget {
  const GaroweHrApp({super.key, required this.server});

  final Uri? server;

  @override
  Widget build(BuildContext context) {
    final scheme = ColorScheme.fromSeed(seedColor: _blue).copyWith(
      primary: _blue,
      onSurface: _ink,
      secondaryContainer: const Color(0xffe2eaff),
    );
    return MaterialApp(
      title: 'Garowe City Management System',
      debugShowCheckedModeBanner: false,
      theme: ThemeData(
        colorScheme: scheme,
        scaffoldBackgroundColor: _paper,
        appBarTheme: const AppBarTheme(backgroundColor: _paper),
      ),
      home: _EmployeeShell(server: server),
    );
  }
}

class _EmployeeShell extends StatefulWidget {
  const _EmployeeShell({required this.server});

  final Uri? server;

  @override
  State<_EmployeeShell> createState() => _EmployeeShellState();
}

class _EmployeeShellState extends State<_EmployeeShell> {
  EmployeePage _selected = EmployeePage.overview;
  EmployeePage? _openPage;
  MemoryImage? _avatar;

  void _updateAvatar(Uint8List? bytes) {
    setState(() => _avatar = bytes == null ? null : MemoryImage(bytes));
  }

  void _signedOut() => setState(() {
    _avatar = null;
    _openPage = null;
    _selected = EmployeePage.overview;
    _signingIn = widget.server != null;
  });
  late bool _signingIn = widget.server != null;

  void _select(EmployeePage page) => setState(() {
    _selected = page;
    _openPage = null;
  });

  @override
  Widget build(BuildContext context) {
    if (_signingIn) {
      return Scaffold(
        appBar: AppBar(title: const Text('Garowe City Management System', maxLines: 2, style: TextStyle(fontSize: 17))),
        body: _PortalPage(
          server: widget.server!,
          page: EmployeePage.login,
          onSignedIn: () => setState(() => _signingIn = false),
        ),
      );
    }
    final page =
        _openPage ??
        (_selected == EmployeePage.overview && widget.server == null
            ? null
            : _selected);
    return Scaffold(
      appBar: AppBar(
        leading: _openPage != null
            ? BackButton(onPressed: () => setState(() => _openPage = null))
            : null,
        title: Text(page?.title ?? 'Garowe City Management System', maxLines: 2, style: const TextStyle(fontSize: 17)),
        actions: [
          IconButton(
            tooltip: 'Notifications',
            icon: const Icon(Icons.notifications_outlined),
            onPressed: () =>
                setState(() => _openPage = EmployeePage.notifications),
          ),
          PopupMenuButton<EmployeePage>(
            tooltip: 'Account',
            icon: CircleAvatar(
              radius: 16,
              foregroundImage: _avatar,
              backgroundColor: const Color(0xffe2eaff),
              child: const Icon(Icons.person_outline, size: 20, color: _blue),
            ),
            onSelected: (value) => setState(() => _openPage = value),
            itemBuilder: (context) => const [
              PopupMenuItem(
                value: EmployeePage.profile,
                child: ListTile(
                  leading: Icon(Icons.person_outline),
                  title: Text('My profile'),
                ),
              ),
              PopupMenuItem(
                value: EmployeePage.logout,
                child: ListTile(
                  leading: Icon(Icons.logout),
                  title: Text('Sign out'),
                ),
              ),
            ],
          ),
        ],
      ),
      body: page == null
          ? const _Overview()
          : widget.server == null
          ? const _Message(
              icon: Icons.link_off_rounded,
              title: 'Server setup required',
              detail: 'This app needs the Garowe City Management System HTTPS address.',
            )
          : _PortalPage(
              key: ValueKey(page),
              server: widget.server!,
              page: page,
              onSignedOut: _signedOut,
              onAvatar: _updateAvatar,
            ),
      bottomNavigationBar: _EmployeeNavigation(
        selected: _selected,
        onSelect: _select,
      ),
    );
  }
}

class _EmployeeNavigation extends StatelessWidget {
  const _EmployeeNavigation({required this.selected, required this.onSelect});

  final EmployeePage selected;
  final ValueChanged<EmployeePage> onSelect;

  static const _tabs = [
    (
      EmployeePage.overview,
      'Overview',
      Icons.dashboard_outlined,
      Icons.dashboard_rounded,
    ),
    (
      EmployeePage.tasks,
      'Tasks',
      Icons.assignment_outlined,
      Icons.assignment_rounded,
    ),
    (
      EmployeePage.meetings,
      'Meetings',
      Icons.groups_outlined,
      Icons.groups_rounded,
    ),
    (
      EmployeePage.attendance,
      'Attendance',
      Icons.event_available_outlined,
      Icons.event_available_rounded,
    ),
    (
      EmployeePage.payroll,
      'Payroll',
      Icons.receipt_long_outlined,
      Icons.receipt_long_rounded,
    ),
  ];

  @override
  Widget build(BuildContext context) {
    final theme = Theme.of(context);
    return Material(
      color: Colors.white,
      child: DecoratedBox(
        decoration: const BoxDecoration(
          border: Border(top: BorderSide(color: Color(0xffdce4f0))),
        ),
        child: SafeArea(
          top: false,
          minimum: const EdgeInsets.symmetric(horizontal: 2, vertical: 4),
          child: Center(
            heightFactor: 1,
            child: ConstrainedBox(
              constraints: const BoxConstraints(maxWidth: 680),
              child: Row(
                crossAxisAlignment: CrossAxisAlignment.start,
                children: [
                  for (final (page, label, icon, selectedIcon) in _tabs)
                    Expanded(
                      child: Semantics(
                        label: label,
                        selected: page == selected,
                        onTap: () => onSelect(page),
                        button: true,
                        child: ExcludeSemantics(
                          child: InkWell(
                            key: ValueKey('nav-${page.name}'),
                            onTap: () => onSelect(page),
                            borderRadius: BorderRadius.circular(14),
                            child: ConstrainedBox(
                              constraints: const BoxConstraints(minHeight: 60),
                              child: Padding(
                                padding: const EdgeInsets.symmetric(
                                  horizontal: 1,
                                  vertical: 5,
                                ),
                                child: Column(
                                  mainAxisSize: MainAxisSize.min,
                                  children: [
                                    AnimatedContainer(
                                      duration: const Duration(
                                        milliseconds: 160,
                                      ),
                                      width: 42,
                                      height: 27,
                                      decoration: BoxDecoration(
                                        color: page == selected
                                            ? theme
                                                  .colorScheme
                                                  .secondaryContainer
                                            : Colors.transparent,
                                        borderRadius: BorderRadius.circular(16),
                                      ),
                                      child: Icon(
                                        page == selected ? selectedIcon : icon,
                                        size: 21,
                                        color: page == selected ? _blue : _ink,
                                      ),
                                    ),
                                    const SizedBox(height: 4),
                                    Text(
                                      label,
                                      textAlign: TextAlign.center,
                                      style: TextStyle(
                                        fontSize: 10,
                                        height: 1.1,
                                        fontWeight: page == selected
                                            ? FontWeight.w800
                                            : FontWeight.w600,
                                        color: page == selected ? _blue : _ink,
                                      ),
                                    ),
                                  ],
                                ),
                              ),
                            ),
                          ),
                        ),
                      ),
                    ),
                ],
              ),
            ),
          ),
        ),
      ),
    );
  }
}

class _Overview extends StatelessWidget {
  const _Overview({this.data, this.onRefresh});

  final _OverviewData? data;
  final VoidCallback? onRefresh;

  @override
  Widget build(BuildContext context) {
    return ListView(
      padding: const EdgeInsets.fromLTRB(20, 8, 20, 24),
      children: [
        Center(
          child: ConstrainedBox(
            constraints: const BoxConstraints(maxWidth: 620),
            child: Column(
              crossAxisAlignment: CrossAxisAlignment.stretch,
              children: [
                Container(
                  padding: const EdgeInsets.all(24),
                  decoration: BoxDecoration(
                    gradient: const LinearGradient(
                      colors: [_ink, _blue],
                      begin: Alignment.topLeft,
                      end: Alignment.bottomRight,
                    ),
                    borderRadius: BorderRadius.circular(24),
                  ),
                  child: Column(
                    crossAxisAlignment: CrossAxisAlignment.start,
                    children: [
                      ClipOval(
                        child: Image.asset(
                          'assets/garowe-local-government.jpg',
                          width: 60,
                          height: 60,
                          fit: BoxFit.cover,
                          excludeFromSemantics: true,
                        ),
                      ),
                      const SizedBox(height: 16),
                      const Text(
                        'Employee dashboard',
                        style: TextStyle(
                          color: Colors.white,
                          fontSize: 26,
                          fontWeight: FontWeight.w700,
                        ),
                      ),
                      const Text(
                        'Garowe Local Government',
                        style: TextStyle(color: Color(0xffe2eaff)),
                      ),
                    ],
                  ),
                ),
                const SizedBox(height: 20),
                if (data case final overview?) ...[
                  Row(
                    mainAxisAlignment: MainAxisAlignment.spaceBetween,
                    children: [
                      Flexible(
                        child: Text(
                          'My progress',
                          style: Theme.of(context).textTheme.titleLarge,
                        ),
                      ),
                      IconButton(
                        onPressed: onRefresh,
                        icon: const Icon(Icons.refresh),
                        tooltip: 'Refresh progress',
                      ),
                    ],
                  ),
                  if (overview.tasks case final tasks?)
                    _TaskProgressGraphic(tasks: tasks),
                  if (overview.hours != null || overview.meetings != null)
                    Wrap(
                      spacing: 8,
                      runSpacing: 8,
                      children: [
                        if (overview.hours case final hours?)
                          Chip(
                            avatar: const Icon(Icons.schedule),
                            label: Text(
                              '${hours.toStringAsFixed(1)} work hours this month',
                            ),
                          ),
                        if (overview.meetings case final meetings?)
                          Chip(
                            avatar: const Icon(Icons.event_outlined),
                            label: Text(
                              '$meetings active or upcoming meetings',
                            ),
                          ),
                      ],
                    ),
                  const SizedBox(height: 16),
                ],
              ],
            ),
          ),
        ),
      ],
    );
  }
}

class _OverviewData {
  const _OverviewData(this.tasks, this.hours, this.meetings);

  factory _OverviewData.fromJson(Map<String, dynamic> data) {
    final tasks = data['tasks'] as Map<String, dynamic>?;
    return _OverviewData(
      tasks == null
          ? null
          : (
              open: tasks['open'] as int,
              inProgress: tasks['inProgress'] as int,
              done: tasks['done'] as int,
            ),
      data['hoursThisMonth'] as num?,
      data['upcomingMeetings'] as int?,
    );
  }

  final ({int open, int inProgress, int done})? tasks;
  final num? hours;
  final int? meetings;
}

class _TaskProgressGraphic extends StatelessWidget {
  const _TaskProgressGraphic({required this.tasks});

  final ({int open, int inProgress, int done}) tasks;

  @override
  Widget build(BuildContext context) {
    final total = tasks.open + tasks.inProgress + tasks.done;
    final progress = total == 0 ? 0.0 : tasks.done / total;
    final percent = '${total == 0 ? 0 : tasks.done * 100 ~/ total}%';
    return Card(
      margin: const EdgeInsets.only(bottom: 10),
      color: Colors.white,
      elevation: 0,
      child: Padding(
        padding: const EdgeInsets.all(16),
        child: Row(
          children: [
            SizedBox.square(
              dimension: 80,
              child: Stack(
                fit: StackFit.expand,
                children: [
                  CircularProgressIndicator(
                    value: progress,
                    strokeWidth: 7,
                    backgroundColor: const Color(0xffe2eaff),
                    semanticsLabel: 'Task completion',
                    semanticsValue: percent,
                  ),
                  Center(
                    child: Text(
                      percent,
                      style: const TextStyle(fontWeight: FontWeight.w800),
                    ),
                  ),
                ],
              ),
            ),
            const SizedBox(width: 16),
            Expanded(
              child: Column(
                crossAxisAlignment: CrossAxisAlignment.start,
                children: [
                  Text(
                    'My tasks',
                    style: Theme.of(context).textTheme.titleMedium,
                  ),
                  Text(
                    total == 0
                        ? 'No assigned tasks yet'
                        : '${tasks.done} of $total completed',
                  ),
                  Wrap(
                    spacing: 10,
                    children: [
                      Text('${tasks.open} open'),
                      Text('${tasks.inProgress} in progress'),
                    ],
                  ),
                ],
              ),
            ),
          ],
        ),
      ),
    );
  }
}

class _PortalPage extends StatefulWidget {
  const _PortalPage({
    super.key,
    required this.server,
    required this.page,
    this.onSignedIn,
    this.onSignedOut,
    this.onAvatar,
  });

  final Uri server;
  final EmployeePage page;
  final VoidCallback? onSignedIn;
  final VoidCallback? onSignedOut;
  final ValueChanged<Uint8List?>? onAvatar;

  @override
  State<_PortalPage> createState() => _PortalPageState();
}

class _PortalPageState extends State<_PortalPage> {
  late final WebViewController _controller;
  bool _loading = true;
  bool _failed = false;
  bool _scrollToSection = false;
  bool _usePortal = false;
  List<_EmployeeRow>? _items;
  _OverviewData? _overview;
  String? _message;
  String? _portalPath;
  String _filter = 'All';
  bool get _showsAvatar =>
      widget.page == EmployeePage.overview ||
      widget.page == EmployeePage.profile;
  bool get _isWorkList =>
      widget.page == EmployeePage.tasks || widget.page == EmployeePage.meetings;

  Uri get _target => _usePortal
      ? (_portalPath == null
            ? widget.page.url(widget.server)
            : widget.server.resolve(_portalPath!))
      : widget.page.dataUrl(widget.server);

  @override
  void initState() {
    super.initState();
    _controller = WebViewController(
      onPermissionRequest: defaultTargetPlatform == TargetPlatform.android
          ? _requestMeetingMedia
          : null,
    );
    _open();
  }

  Future<void> _requestMeetingMedia(WebViewPermissionRequest request) async {
    try {
      final current = Uri.tryParse(await _controller.currentUrl() ?? '');
      if (current?.httpOrigin != widget.server.origin ||
          current?.path.toLowerCase() != '/operations/meetingroom' ||
          !mounted) {
        await request.deny();
        return;
      }
      final permissions = [
        for (final type in request.types)
          if (type == WebViewPermissionResourceType.camera)
            Permission.camera
          else if (type == WebViewPermissionResourceType.microphone)
            Permission.microphone,
      ];
      if (permissions.isEmpty || permissions.length != request.types.length) {
        await request.deny();
        return;
      }
      final results = await permissions.request();
      if (results.values.every((status) => status.isGranted)) {
        await request.grant();
      } else {
        await request.deny();
      }
    } catch (_) {
      await request.deny();
    }
  }

  Future<void> _open() async {
    try {
      final platform = _controller.platform;
      if (platform is AndroidWebViewController &&
          widget.page == EmployeePage.profile) {
        await platform.setOnShowFileSelector((_) async {
          final result = await FilePicker.pickFiles(
            type: FileType.custom,
            allowedExtensions: ['jpg', 'jpeg', 'png'],
          );
          final path = result?.files.single.path;
          return path == null ? [] : [Uri.file(path).toString()];
        });
      }
      await _controller.setJavaScriptMode(JavaScriptMode.unrestricted);
      if (_showsAvatar) {
        await _controller.removeJavaScriptChannel('GaroweAvatar');
        await _controller.addJavaScriptChannel(
          'GaroweAvatar',
          onMessageReceived: _receiveAvatar,
        );
      }
      await _controller.setNavigationDelegate(
        NavigationDelegate(
          onNavigationRequest: (request) async {
            final uri = Uri.tryParse(request.url);
            if (uri?.httpOrigin == widget.server.origin) {
              return NavigationDecision.navigate;
            }
            if (request.isMainFrame &&
                uri?.scheme == 'https' &&
                uri?.httpOrigin != null &&
                mounted) {
              await _offerExternalLink(uri!);
            }
            return NavigationDecision.prevent;
          },
          onPageStarted: (_) {
            if (mounted) {
              setState(() {
                _loading = true;
                _failed = false;
              });
            }
          },
          onPageFinished: _pageFinished,
          onHttpError: (error) {
            if (mounted && error.request?.uri == _target) _showFailure();
          },
          onWebResourceError: (error) {
            if (mounted && error.isForMainFrame == true) _showFailure();
          },
        ),
      );
      await _controller.loadRequest(_target);
    } catch (_) {
      if (mounted) _showFailure();
    }
  }

  Future<void> _pageFinished(String url) async {
    if (!mounted) return;
    final current = Uri.tryParse(url);
    if (current?.httpOrigin != widget.server.origin) return;
    try {
      if (widget.page == EmployeePage.logout &&
          current?.path.toLowerCase() == '/identity/account/login') {
        widget.onSignedOut?.call();
        return;
      }
      if (_showsAvatar &&
          (current?.path == widget.page.dataUrl(widget.server).path ||
              current?.path.toLowerCase() == '/me/profile')) {
        await _loadAvatar();
      }
      if (widget.page == EmployeePage.login && current?.path == _sessionPath) {
        final session = await _readJson();
        if (mounted && session['signedIn'] == true) widget.onSignedIn?.call();
        return;
      }
      if (!_usePortal &&
          widget.page != EmployeePage.login &&
          widget.page != EmployeePage.logout &&
          current?.path == _target.path) {
        final feed = await _readJson();
        if (widget.page == EmployeePage.overview) {
          if (!mounted) return;
          setState(() {
            _overview = _OverviewData.fromJson(feed);
            _loading = false;
          });
          return;
        }
        final items = (feed['items'] as List<dynamic>)
            .map((item) => _EmployeeRow.fromJson(item as Map<String, dynamic>))
            .toList();
        if (!mounted || _usePortal) return;
        setState(() {
          _items = items;
          _message = feed['message'] as String?;
        });
      } else if (_scrollToSection && current?.path == _target.path) {
        _scrollToSection = false;
        await _controller.runJavaScript(
          '(() => { const el = document.getElementById(${jsonEncode(_target.fragment)}); '
          'const group = el?.closest("details"); if (group) group.open = true; '
          'el?.scrollIntoView(); })()',
        );
      }
      if (mounted) setState(() => _loading = false);
    } catch (_) {
      if (mounted) _showFailure();
    }
  }

  Future<void> _loadAvatar() => _controller.runJavaScript("""
    (() => {
      const image = new Image();
      image.onload = () => {
        const canvas = document.createElement('canvas');
        canvas.width = canvas.height = 96;
        const side = Math.min(image.naturalWidth, image.naturalHeight);
        if (!side) { GaroweAvatar.postMessage(''); return; }
        canvas.getContext('2d').drawImage(image,
          (image.naturalWidth - side) / 2, (image.naturalHeight - side) / 2,
          side, side, 0, 0, 96, 96);
        GaroweAvatar.postMessage(canvas.toDataURL('image/jpeg', 0.85).split(',')[1]);
      };
      image.onerror = () => GaroweAvatar.postMessage('');
      image.src = '/api/mobile/v1/avatar';
    })();
  """);

  Future<void> _receiveAvatar(JavaScriptMessage message) async {
    final current = Uri.tryParse(await _controller.currentUrl() ?? '');
    if (!mounted ||
        current?.httpOrigin != widget.server.origin ||
        message.message.length > 65536) {
      return;
    }
    try {
      widget.onAvatar?.call(
        message.message.isEmpty ? null : base64Decode(message.message),
      );
    } on FormatException {
      // An invalid image leaves the existing avatar in place.
    }
  }

  Future<Map<String, dynamic>> _readJson() async {
    final raw = await _controller.runJavaScriptReturningResult(
      'document.body.innerText',
    );
    var data = jsonDecode(raw.toString());
    if (data is String) data = jsonDecode(data);
    return data as Map<String, dynamic>;
  }

  Future<void> _switchPage({required bool portal, String? path}) async {
    setState(() {
      _usePortal = portal;
      _portalPath = path;
      _scrollToSection = portal && _target.hasFragment;
      _items = null;
      _overview = null;
      _message = null;
      _loading = true;
      _failed = false;
    });
    await _open();
  }

  Future<void> _openAction(String path) async {
    final url = widget.server.resolve(path);
    if (url.httpOrigin == widget.server.origin &&
        path.startsWith('/') &&
        !path.startsWith('//')) {
      await _switchPage(portal: true, path: path);
    } else if (url.scheme == 'https') {
      await _offerExternalLink(url);
    }
  }

  Future<void> _offerExternalLink(Uri uri) async {
    final open = await showDialog<bool>(
      context: context,
      builder: (context) => AlertDialog(
        title: const Text('Open in browser?'),
        content: Text(uri.host),
        actions: [
          TextButton(
            onPressed: () => Navigator.pop(context, false),
            child: const Text('Cancel'),
          ),
          FilledButton(
            onPressed: () => Navigator.pop(context, true),
            child: const Text('Open'),
          ),
        ],
      ),
    );
    if (open != true || !mounted) return;
    bool launched;
    try {
      launched = await launchUrl(uri, mode: LaunchMode.externalApplication);
    } catch (_) {
      launched = false;
    }
    if (!launched && mounted) {
      ScaffoldMessenger.of(
        context,
      ).showSnackBar(const SnackBar(content: Text('Could not open the link.')));
    }
  }

  void _showFailure() => setState(() {
    _loading = false;
    _failed = true;
  });

  @override
  Widget build(BuildContext context) {
    final profile = widget.page == EmployeePage.profile;
    final statusOptions = widget.page == EmployeePage.tasks
        ? const ['All', 'Open', 'In progress', 'Done', 'Cancelled']
        : const ['All', 'Upcoming', 'In progress', 'Ended', 'Cancelled'];
    final visibleItems = (_items ?? []).where(
      (row) => !_isWorkList || _filter == 'All' || row.status == _filter,
    );
    return Stack(
      children: [
        Padding(
          padding: EdgeInsets.only(bottom: _usePortal ? 60 : 0),
          child: WebViewWidget(controller: _controller),
        ),
        if (_overview case final data?)
          Positioned.fill(
            child: ColoredBox(
              color: _paper,
              child: _Overview(
                data: data,
                onRefresh: () => _switchPage(portal: false),
              ),
            ),
          ),
        if (_items case final items?)
          Positioned.fill(
            child: ColoredBox(
              color: _paper,
              child: ListView(
                padding: EdgeInsets.symmetric(
                  horizontal: ((MediaQuery.sizeOf(context).width - 680) / 2)
                      .clamp(16, double.infinity)
                      .toDouble(),
                  vertical: 16,
                ),
                children: [
                  Wrap(
                    spacing: 12,
                    children: [
                      OutlinedButton.icon(
                        onPressed: () => _switchPage(portal: false),
                        icon: const Icon(Icons.refresh),
                        label: const Text('Refresh'),
                      ),
                      TextButton(
                        onPressed: () => _switchPage(portal: true),
                        child: Text(
                          _isWorkList
                              ? (widget.page == EmployeePage.tasks
                                    ? 'Manage tasks'
                                    : 'Meeting actions')
                              : 'Open full page',
                        ),
                      ),
                    ],
                  ),
                  if (profile) ...[
                    const SizedBox(height: 8),
                    OutlinedButton.icon(
                      onPressed: () => _switchPage(
                        portal: true,
                        path: '/Me/Profile#profile-settings',
                      ),
                      icon: const Icon(Icons.photo_camera_outlined),
                      label: const Text('Change profile photo'),
                    ),
                    OutlinedButton.icon(
                      onPressed: () => _switchPage(
                        portal: true,
                        path:
                            '/Identity/Account/ChangePassword?ReturnUrl=%2FMe%2FProfile%23profile-settings',
                      ),
                      icon: const Icon(Icons.lock_outline),
                      label: const Text('Change password'),
                    ),
                  ],
                  if (_isWorkList)
                    Padding(
                      padding: const EdgeInsets.symmetric(vertical: 8),
                      child: Wrap(
                        spacing: 8,
                        children: [
                          for (final status in statusOptions)
                            ChoiceChip(
                              label: Text(status),
                              selected: _filter == status,
                              onSelected: (_) =>
                                  setState(() => _filter = status),
                            ),
                        ],
                      ),
                    ),
                  if (visibleItems.isEmpty)
                    Padding(
                      padding: const EdgeInsets.all(16),
                      child: Text(
                        items.isEmpty
                            ? _message ?? 'No records to show.'
                            : 'No records match this status.',
                      ),
                    ),
                  for (final row in visibleItems)
                    if (_isWorkList)
                      _WorkRow(
                        row: row,
                        isMeeting: widget.page == EmployeePage.meetings,
                        onAction: _openAction,
                      )
                    else
                      Card(
                        child: ListTile(
                          leading: profile
                              ? const CircleAvatar(
                                  child: Icon(Icons.person_outline),
                                )
                              : null,
                          title: Text(row.title),
                          subtitle: Text(
                            row.details.isEmpty
                                ? row.subtitle
                                : '${row.subtitle}\n${row.details}',
                          ),
                        ),
                      ),
                ],
              ),
            ),
          ),
        if (_usePortal && !_loading && !_failed)
          Positioned(
            right: 12,
            bottom: 12,
            child: FilledButton(
              onPressed: () => _switchPage(portal: false),
              child: const Text('Back to list'),
            ),
          ),
        if (_loading)
          const Align(
            alignment: Alignment.topCenter,
            child: LinearProgressIndicator(),
          ),
        if (_failed)
          Positioned.fill(
            child: ColoredBox(
              color: _paper,
              child: _Message(
                icon: Icons.cloud_off_rounded,
                title: 'Unable to load Garowe City Management System',
                detail: 'Check your connection and try again.',
                onRetry: () {
                  setState(() {
                    _failed = false;
                    _loading = true;
                  });
                  _open();
                },
              ),
            ),
          ),
      ],
    );
  }
}

extension on Uri {
  String? get httpOrigin =>
      (scheme == 'https' || scheme == 'http') && host.isNotEmpty
      ? origin
      : null;
}

class _EmployeeRow {
  const _EmployeeRow(
    this.title,
    this.subtitle,
    this.details,
    this.status,
    this.actions,
  );

  factory _EmployeeRow.fromJson(Map<String, dynamic> json) => _EmployeeRow(
    json['title'] as String,
    json['subtitle'] as String,
    json['details'] as String,
    json['status'] as String?,
    (json['actions'] as List<dynamic>? ?? [])
        .map((item) => Map<String, String>.from(item as Map))
        .toList(),
  );

  final String title;
  final String subtitle;
  final String details;
  final String? status;
  final List<Map<String, String>> actions;
}

class _WorkRow extends StatelessWidget {
  const _WorkRow({
    required this.row,
    required this.isMeeting,
    required this.onAction,
  });

  final _EmployeeRow row;
  final bool isMeeting;
  final ValueChanged<String> onAction;

  @override
  Widget build(BuildContext context) {
    final (foreground, background) = switch (row.status) {
      'Done' => (const Color(0xff166344), const Color(0xffe2f4ea)),
      'In progress' => (const Color(0xff744700), const Color(0xffffefcb)),
      'Ended' ||
      'Cancelled' => (const Color(0xff424c61), const Color(0xffedf0f6)),
      _ => (_blue, const Color(0xffe2eaff)),
    };
    return Card(
      color: Colors.white,
      elevation: 0,
      margin: const EdgeInsets.only(bottom: 8),
      child: ListTile(
        contentPadding: const EdgeInsets.symmetric(horizontal: 14, vertical: 8),
        leading: CircleAvatar(
          backgroundColor: background,
          foregroundColor: foreground,
          child: Icon(
            isMeeting
                ? Icons.event_available_outlined
                : Icons.assignment_outlined,
            size: 20,
          ),
        ),
        title: Text(
          row.title,
          style: const TextStyle(fontWeight: FontWeight.w700),
        ),
        subtitle: Column(
          crossAxisAlignment: CrossAxisAlignment.start,
          children: [
            const SizedBox(height: 4),
            Wrap(
              spacing: 8,
              runSpacing: 4,
              crossAxisAlignment: WrapCrossAlignment.center,
              children: [
                if (row.status case final status?)
                  DecoratedBox(
                    decoration: BoxDecoration(
                      color: background,
                      borderRadius: BorderRadius.circular(8),
                    ),
                    child: Padding(
                      padding: const EdgeInsets.symmetric(
                        horizontal: 8,
                        vertical: 3,
                      ),
                      child: Text(
                        status,
                        style: TextStyle(
                          color: foreground,
                          fontWeight: FontWeight.w600,
                        ),
                      ),
                    ),
                  ),
                Text(row.subtitle),
              ],
            ),
            if (row.details.isNotEmpty) ...[
              const SizedBox(height: 6),
              Text(row.details),
            ],
            if (row.actions.isNotEmpty)
              Wrap(
                spacing: 8,
                children: [
                  for (final action in row.actions)
                    TextButton(
                      onPressed: () => onAction(action['url']!),
                      child: Text(action['label']!),
                    ),
                ],
              ),
          ],
        ),
      ),
    );
  }
}

class _Message extends StatelessWidget {
  const _Message({
    required this.icon,
    required this.title,
    required this.detail,
    this.onRetry,
  });

  final IconData icon;
  final String title;
  final String detail;
  final VoidCallback? onRetry;

  @override
  Widget build(BuildContext context) {
    return Center(
      child: Padding(
        padding: const EdgeInsets.all(24),
        child: Column(
          mainAxisSize: MainAxisSize.min,
          children: [
            Icon(icon, size: 42, color: _blue),
            const SizedBox(height: 12),
            Text(
              title,
              style: Theme.of(context).textTheme.titleLarge,
              textAlign: TextAlign.center,
            ),
            Text(detail, textAlign: TextAlign.center),
            if (onRetry != null) ...[
              const SizedBox(height: 12),
              FilledButton(onPressed: onRetry, child: const Text('Try again')),
            ],
          ],
        ),
      ),
    );
  }
}
