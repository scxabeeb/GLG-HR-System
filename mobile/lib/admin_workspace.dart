import 'dart:convert';
import 'package:flutter/material.dart';
import 'package:file_picker/file_picker.dart';
import 'api.dart';
import 'ui_preferences.dart';

class AdminWorkspace extends StatefulWidget {
  const AdminWorkspace({super.key, required this.api});
  final Api api;
  @override
  State<AdminWorkspace> createState() => _AdminWorkspaceState();
}

class _AdminWorkspaceState extends State<AdminWorkspace> {
  late Future<dynamic> future;
  @override
  void initState() {
    super.initState();
    reload();
  }

  void reload() {
    future = widget.api.get('/api/mobile/v2/admin/catalog');
  }

  @override
  Widget build(BuildContext context) => Scaffold(
    appBar: AppBar(title: const UiText('Administration')),
    body: FutureBuilder<dynamic>(
      future: future,
      builder: (context, snapshot) {
        if (snapshot.hasError) {
          return Center(
            child: Column(
              mainAxisSize: MainAxisSize.min,
              children: [
                UiText.raw('${snapshot.error}'),
                TextButton(
                  onPressed: () => setState(reload),
                  child: const UiText('Retry'),
                ),
              ],
            ),
          );
        }
        if (!snapshot.hasData) {
          return const Center(child: CircularProgressIndicator());
        }
        final rows = List<Map<String, dynamic>>.from(snapshot.data as List);
        return ListView(
          padding: const EdgeInsets.all(16),
          children: [
            const UiDisplayPreferences(),
            const UiText('Manage your organization'),
            for (final group in rows.map((r) => r['group']).toSet())
              Card(
                margin: const EdgeInsets.symmetric(vertical: 8),
                child: ExpansionTile(
                  initiallyExpanded: group == 'HR',
                  leading: const Icon(Icons.admin_panel_settings_outlined),
                  title: UiText('$group'),
                  children: [
                    for (final row in rows.where((r) => r['group'] == group))
                      ListTile(
                        title: UiText(row['title']),
                        trailing: const Icon(Icons.chevron_right),
                        onTap: () => Navigator.push(
                          context,
                          MaterialPageRoute(
                            builder: (_) =>
                                AdminModule(api: widget.api, module: row),
                          ),
                        ),
                      ),
                  ],
                ),
              ),
          ],
        );
      },
    ),
  );
}

class AdminModule extends StatefulWidget {
  const AdminModule({
    super.key,
    required this.api,
    required this.module,
    this.initial = const {},
  });
  final Api api;
  final Map<String, dynamic> module, initial;
  @override
  State<AdminModule> createState() => _AdminModuleState();
}

class _AdminModuleState extends State<AdminModule> {
  Map<String, dynamic>? data;
  late Map<String, dynamic> values;
  String? error;
  String search = '';
  bool busy = false;
  @override
  void initState() {
    super.initState();
    values = {...widget.initial};
    load();
  }

  Future<void> load() async {
    setState(() {
      busy = true;
      error = null;
    });
    try {
      final response = await widget.api.post(
        '/api/mobile/v2/admin/${widget.module['key']}/load',
        {'values': values},
      );
      if (mounted) setState(() => data = Map<String, dynamic>.from(response));
    } catch (e) {
      if (mounted) setState(() => error = '$e');
    } finally {
      if (mounted) setState(() => busy = false);
    }
  }

  List<Map<String, dynamic>> get actions =>
      List<Map<String, dynamic>>.from(data?['actions'] ?? []);
  Future<void> action(
    Map<String, dynamic> action, [
    Map<String, dynamic>? row,
  ]) async {
    final initial = <String, dynamic>{...values};
    var source = data;
    final recordId = row?['Id'] ?? row?['id'];
    if (recordId != null) {
      try {
        source = Map<String, dynamic>.from(
          await widget.api.post(
            '/api/mobile/v2/admin/${widget.module['key']}/load',
            {
              'values': {
                ...values,
                'Id': recordId,
                'employeeId': recordId,
                'edit': recordId,
                'PeriodId': recordId,
              },
            },
          ),
        );
      } catch (e) {
        if (mounted) {
          ScaffoldMessenger.of(
            context,
          ).showSnackBar(SnackBar(content: UiText.raw('$e')));
        }
        return;
      }
      final records = source['records'];
      if (records is Map) {
        if (records['UserAccess'] is Map) {
          initial['access'] = (records['UserAccess'] as Map)['$recordId'];
        }
        if (records['AssignedRoles'] is Map) {
          initial['roles'] = (records['AssignedRoles'] as Map)['$recordId'];
        }
        if (records['Capabilities'] is List) {
          initial['capabilities'] = records['Capabilities'];
        }
        if (widget.module['key'] == 'roles') {
          final denied = List<String>.from(records['Denied'] ?? []);
          initial['access'] = {
            for (final entry in (source['lookups']?['menus'] ?? []))
              entry['value']: denied.contains(entry['value'])
                  ? 'Deny'
                  : 'Allow',
          };
        }
      }
    }
    if (!mounted) return;
    if (row != null) {
      for (final e in row.entries) {
        if (e.value is String || e.value is num || e.value is bool) {
          initial[e.key] = e.value;
        }
      }
      final id = row['Id'] ?? row['id'];
      if (id != null) initial['Id'] = id;
    }
    final matching = List<Map<String, dynamic>>.from(
      source?['actions'] ?? [],
    ).where((candidate) => candidate['key'] == action['key']);
    final effectiveAction = matching.isEmpty ? action : matching.first;
    final response = await Navigator.push<Map<String, dynamic>>(
      context,
      MaterialPageRoute(
        builder: (_) => AdminAction(
          api: widget.api,
          module: widget.module,
          action: effectiveAction,
          fields: List<Map<String, dynamic>>.from(
            source?[(action['readOnly'] == true ||
                        action['load'] == true ||
                        '${action['key']}'.contains('Import') ||
                        '${action['key']}'.contains('Csv'))
                    ? 'filters'
                    : 'fields'] ??
                [],
          ),
          initial: initial,
          lookups: Map<String, dynamic>.from(source?["lookups"] ?? {}),
        ),
      ),
    );
    if (response != null && mounted) {
      if (response['redirect'] is Map) {
        values.addAll(Map<String, dynamic>.from(response['redirect']));
      }
      await load();
    }
  }

  static const creationRoutes = <String, (String, String, String)>{
    'employees': ('register', 'Register employee', 'OnPostAsync'),
    'register': ('register', 'Register employee', 'OnPostAsync'),
    'assets': ('asset-edit', 'Register asset', 'OnPostAsync'),
    'asset-edit': ('asset-edit', 'Register asset', 'OnPostAsync'),
    'branches': ('branches', 'Add branch', 'OnPostAsync'),
    'departments': ('departments', 'Add department', 'OnPostAsync'),
    'sub-departments': ('sub-departments', 'Add sub-department', 'OnPostAsync'),
    'users': ('users', 'Create user', 'OnPostCreateAsync'),
    'roles': ('roles', 'Create role', 'OnPostCreateAsync'),
    'devices': ('devices', 'Register device', 'OnPostAsync'),
    'fleet': ('fleet', 'Register vehicle', 'OnPostSaveAsync'),
    'fleet-trips': ('fleet-trips', 'Request trip', 'OnPostRequestAsync'),
    'fleet-reports': ('fleet-reports', 'Add expense', 'OnPostExpenseAsync'),
    'salary': ('salary', 'Register salary', 'OnPostAsync'),
    'leave': ('leave', 'Request leave', 'OnPostCreateAsync'),
    'leave-types': ('leave-types', 'Add leave type', 'OnPostAsync'),
    'leave-balances': ('leave-balances', 'Add leave balance', 'OnPostAsync'),
    'report-views': ('report-views', 'Create report', 'OnPostAsync'),
    'rules': ('rules', 'Add work rule', 'OnPostAsync'),
  };
  Future<void> createRecord() async {
    final route = creationRoutes[widget.module['key']];
    if (route == null) return;
    try {
      final fresh = Map<String, dynamic>.from(
        await widget.api.post('/api/mobile/v2/admin/${route.$1}/load', {
          'values': <String, dynamic>{},
        }),
      );
      final choices = List<Map<String, dynamic>>.from(
        fresh['actions'] ?? [],
      ).where((a) => a['key'] == route.$3);
      if (choices.isEmpty) {
        throw const ApiError('Registration is unavailable. Refresh and retry.');
      }
      if (!mounted) return;
      final response = await Navigator.push<Map<String, dynamic>>(
        context,
        MaterialPageRoute(
          builder: (_) => AdminAction(
            api: widget.api,
            module: {'key': route.$1, 'title': route.$2},
            action: {...choices.first, 'label': route.$2, 'creating': true},
            fields: List<Map<String, dynamic>>.from(fresh['fields'] ?? []),
            initial: const {},
            lookups: Map<String, dynamic>.from(fresh['lookups'] ?? {}),
          ),
        ),
      );
      if (response != null && mounted) {
        await load();
      }
    } catch (e) {
      if (mounted) {
        ScaffoldMessenger.of(
          context,
        ).showSnackBar(SnackBar(content: UiText.raw('$e')));
      }
    }
  }

  Widget creationButton() => Padding(
    padding: const EdgeInsets.only(bottom: 16),
    child: FilledButton.icon(
      key: const ValueKey('admin-create-record'),
      onPressed: busy ? null : createRecord,
      icon: const Icon(Icons.add_circle_outline),
      label: UiText(creationRoutes[widget.module['key']]!.$2),
    ),
  );

  Future<void> detail(Map<String, dynamic> row) => showModalBottomSheet<void>(
    context: context,
    isScrollControlled: true,
    builder: (context) => SafeArea(
      child: DraggableScrollableSheet(
        expand: false,
        initialChildSize: .8,
        builder: (context, scroll) => ListView(
          controller: scroll,
          padding: const EdgeInsets.all(20),
          children: [
            UiText.raw(
              recordTitle(row),
              style: Theme.of(context).textTheme.titleLarge,
            ),
            for (final e in row.entries)
              if (e.value != null)
                ListTile(
                  contentPadding: EdgeInsets.zero,
                  title: UiText(e.key),
                  subtitle: SelectableText(
                    e.value is Map || e.value is List
                        ? const JsonEncoder.withIndent('  ').convert(e.value)
                        : '${e.value}',
                  ),
                ),
            for (final a in actions.where((a) => a['readOnly'] != true))
              OutlinedButton(
                onPressed: () {
                  Navigator.pop(context);
                  action(a, row);
                },
                child: UiText(a['label'] ?? 'Save'),
              ),
          ],
        ),
      ),
    ),
  );
  @override
  Widget build(BuildContext context) => DefaultTabController(
    length: 2,
    child: Scaffold(
      appBar: AppBar(
        title: UiText(widget.module['title']),
        actions: [
          IconButton(
            onPressed: busy ? null : load,
            icon: const Icon(Icons.refresh),
          ),
        ],
        bottom: TabBar(
          tabs: [
            Tab(text: tr(context, 'Records')),
            Tab(text: tr(context, 'Management')),
          ],
        ),
      ),
      body: busy
          ? const Center(child: CircularProgressIndicator())
          : error != null
          ? Center(
              child: Padding(
                padding: const EdgeInsets.all(20),
                child: Column(
                  mainAxisSize: MainAxisSize.min,
                  children: [
                    UiText.raw(error!),
                    TextButton(onPressed: load, child: const UiText('Retry')),
                  ],
                ),
              ),
            )
          : TabBarView(
              children: [
                ListView(
                  padding: const EdgeInsets.all(16),
                  children: [
                    if (creationRoutes.containsKey(widget.module['key']))
                      creationButton(),
                    TextField(
                      decoration: InputDecoration(
                        labelText: tr(context, 'Search records'),
                        prefixIcon: const Icon(Icons.search),
                      ),
                      onChanged: (v) =>
                          setState(() => search = v.toLowerCase()),
                    ),
                    const SizedBox(height: 12),
                    if (data?['records'] is Map)
                      for (final e in (data!['records'] as Map).entries)
                        if (e.value is List && (e.value as List).isNotEmpty)
                          Card(
                            margin: const EdgeInsets.only(bottom: 12),
                            child: ExpansionTile(
                              title: UiText('${e.key}'),
                              initiallyExpanded: true,
                              children: [
                                for (final row in (e.value as List).where(
                                  (r) => jsonEncode(
                                    r,
                                  ).toLowerCase().contains(search),
                                ))
                                  if (row is Map)
                                    ListTile(
                                      title: UiText.raw(recordTitle(row)),
                                      subtitle: UiText.raw(
                                        recordSummary(row),
                                        maxLines: 2,
                                        overflow: TextOverflow.ellipsis,
                                      ),
                                      trailing: const Icon(Icons.chevron_right),
                                      onTap: () => detail(
                                        Map<String, dynamic>.from(row),
                                      ),
                                    )
                                  else
                                    ListTile(title: UiText.raw('$row')),
                              ],
                            ),
                          ),
                    const UiText(
                      'Lists show up to 200 records. Use page filters to narrow results.',
                    ),
                  ],
                ),
                ListView(
                  padding: const EdgeInsets.all(16),
                  children: [
                    if (creationRoutes.containsKey(widget.module['key']))
                      creationButton(),
                    FilledButton.tonalIcon(
                      onPressed: () => action({
                        'key': 'load',
                        'label': 'Apply filters',
                        'load': true,
                      }),
                      icon: const Icon(Icons.filter_list),
                      label: const UiText('Filters & record selection'),
                    ),
                    const SizedBox(height: 12),
                    for (final a in actions)
                      Card(
                        margin: const EdgeInsets.only(bottom: 8),
                        child: ListTile(
                          title: UiText(a['label'] ?? 'Save'),
                          leading: Icon(
                            a['readOnly'] == true
                                ? Icons.download_outlined
                                : Icons.edit_outlined,
                          ),
                          trailing: const Icon(Icons.chevron_right),
                          onTap: () => action(a),
                        ),
                      ),
                  ],
                ),
              ],
            ),
    ),
  );
}

String recordTitle(Map row) {
  for (final key in [
    'FullName',
    'Name',
    'Title',
    'EmployeeNumber',
    'Email',
    'UserName',
    'Id',
  ]) {
    if (row[key] != null) return '${row[key]}';
  }
  return row.values.take(2).join(' · ');
}

String recordSummary(Map row) => row.entries
    .where((e) => e.value != null && e.value is! Map && e.value is! List)
    .take(4)
    .map((e) => '${e.key}: ${e.value}')
    .join(' · ');

class AdminAction extends StatefulWidget {
  const AdminAction({
    super.key,
    required this.api,
    required this.module,
    required this.action,
    required this.fields,
    required this.initial,
    this.lookups = const {},
  });
  final Api api;
  final Map<String, dynamic> module, action, initial;
  final Map<String, dynamic> lookups;
  final List<Map<String, dynamic>> fields;
  @override
  State<AdminAction> createState() => _AdminActionState();
}

class _AdminActionState extends State<AdminAction> {
  final form = GlobalKey<FormState>();
  final controllers = <String, TextEditingController>{};
  final values = <String, dynamic>{};
  final files = <String, String>{};
  late final List<Map<String, dynamic>> fields;
  String? error;
  bool busy = false;
  @override
  void initState() {
    super.initState();
    final indexed = <String, Map<String, dynamic>>{};
    for (final f in [
      ...widget.fields,
      ...List<Map<String, dynamic>>.from(widget.action['fields'] ?? []),
    ]) {
      indexed[(f['name'] as String).toLowerCase()] = f;
    }
    fields = indexed.values
        .where(
          (f) =>
              widget.action['creating'] != true ||
              ![
                'id',
                'input.id',
                'input.version',
                'search',
                'pagenumber',
                'rowsperpage',
                'connection',
                'viewmode',
                'kind',
              ].contains('${f['name']}'.toLowerCase()),
        )
        .toList();
    values.addAll(widget.initial);
    for (final f in fields) {
      final name = f['name'] as String;
      final matches = widget.initial.entries.where(
        (e) => e.key.toLowerCase() == name.toLowerCase(),
      );
      final value = matches.isEmpty ? f['value'] : matches.first.value;
      values[name] = value;
      if (f['type'] != 'file' && f['type'] != 'bool') {
        controllers[name] = TextEditingController(
          text: value == null
              ? ''
              : value is List
              ? value.join(';')
              : value is Map
              ? jsonEncode(value)
              : '$value',
        );
      }
    }
  }

  @override
  void dispose() {
    for (final c in controllers.values) {
      c.dispose();
    }
    super.dispose();
  }

  Future<void> submit() async {
    if (!form.currentState!.validate()) return;
    for (final f in fields) {
      final name = f['name'] as String;
      if (f['type'] == 'bool' || f['type'] == 'file') continue;
      final text = controllers[name]?.text ?? '';
      if (f['type'] == 'list') {
        values[name] = text
            .split(';')
            .map((x) => x.trim())
            .where((x) => x.isNotEmpty)
            .toList();
      } else if (f['type'] == 'map') {
        try {
          final map = jsonDecode(text.isEmpty ? '{}' : text);
          if (map is! Map) throw const FormatException();
          for (final e in map.entries) {
            values['$name[${e.key}]'] = '${e.value}';
          }
          values[name] = text;
        } catch (_) {
          setState(() => error = 'Enter a valid permission map.');
          return;
        }
      } else {
        values[name] = text;
      }
    }
    final confirmed = await showDialog<bool>(
      context: context,
      builder: (context) => AlertDialog(
        title: UiText(widget.action['label'] ?? 'Save'),
        content: const UiText('Confirm this change to the live system?'),
        actions: [
          TextButton(
            onPressed: () => Navigator.pop(context, false),
            child: const UiText('Cancel'),
          ),
          FilledButton(
            onPressed: () => Navigator.pop(context, true),
            child: const UiText('Confirm'),
          ),
        ],
      ),
    );
    if (confirmed != true) return;
    setState(() {
      busy = true;
      error = null;
    });
    try {
      final path = widget.action['load'] == true
          ? 'load'
          : 'action/${widget.action['key']}';
      final result = Map<String, dynamic>.from(
        await widget.api.post(
          '/api/mobile/v2/admin/${widget.module['key']}/$path',
          {'values': values},
        ),
      );
      if (result['data'] != null) {
        await FilePicker.saveFile(
          dialogTitle: 'Save file',
          fileName: result['file'] ?? 'download',
          bytes: base64Decode(result['data']),
        );
      }
      if (mounted) {
        ScaffoldMessenger.of(context).showSnackBar(
          SnackBar(
            content: UiText(
              (result['message'] as String?)?.isNotEmpty == true
                  ? result['message']
                  : 'Saved successfully',
            ),
          ),
        );
        Navigator.pop(
          context,
          widget.action['load'] == true ? {'redirect': values} : result,
        );
      }
    } catch (e) {
      if (mounted) setState(() => error = '$e');
    } finally {
      if (mounted) setState(() => busy = false);
    }
  }

  @override
  Widget build(BuildContext context) => Scaffold(
    appBar: AppBar(title: UiText(widget.action['label'] ?? 'Save')),
    body: Form(
      key: form,
      child: ListView(
        padding: const EdgeInsets.all(20),
        children: [
          const UiText(
            'Use record identifiers from the records tab. Existing validation and approval rules apply.',
          ),
          const SizedBox(height: 16),
          for (final f in fields)
            Padding(
              padding: const EdgeInsets.only(bottom: 14),
              child: field(f),
            ),
          if (error != null)
            UiText.raw(
              error!,
              style: TextStyle(color: Theme.of(context).colorScheme.error),
            ),
          FilledButton.icon(
            onPressed: busy ? null : submit,
            icon: busy
                ? const SizedBox(
                    width: 18,
                    height: 18,
                    child: CircularProgressIndicator(strokeWidth: 2),
                  )
                : const Icon(Icons.check),
            label: UiText(
              widget.action['load'] == true
                  ? 'Apply'
                  : widget.action['label'] ?? 'Save',
            ),
          ),
        ],
      ),
    ),
  );
  List<Map<String, dynamic>> choicesFor(String name) {
    final key = name.split('.').last.toLowerCase();
    if (key == 'parentid') {
      return List<Map<String, dynamic>>.from(
        widget.lookups[widget.module['key'] == 'sub-departments'
                ? 'departments'
                : 'branches'] ??
            [],
      );
    }
    final lookup = key == 'typeid' || key == 'leavetypeid'
        ? 'leaveTypes'
        : key.contains('vehicle')
        ? 'vehicles'
        : key == 'driverid'
        ? 'employees'
        : key == 'id'
        ? 'records'
        : key.contains('shift')
        ? 'shifts'
        : key.contains('subdepartment')
        ? 'subDepartments'
        : key.contains('department')
        ? 'departments'
        : key.contains('branch')
        ? 'branches'
        : key.contains('employee') || key == 'managerid'
        ? 'employees'
        : key.contains('device') || key == 'terminalid'
        ? 'devices'
        : key == 'roles' || key == 'role'
        ? 'roles'
        : key == 'capabilities'
        ? 'capabilities'
        : key == 'blocked' || key == 'denied'
        ? 'menus'
        : key.contains('userid') || key.contains('assignee')
        ? 'users'
        : null;
    if (lookup == null) return [];
    return List<Map<String, dynamic>>.from(widget.lookups[lookup] ?? []);
  }

  Future<void> selectChoices(
    Map<String, dynamic> f,
    List<Map<String, dynamic>> options,
  ) async {
    final name = f['name'] as String, multiple = f['type'] == 'list';
    final selected = controllers[name]!.text
        .split(';')
        .where((v) => v.isNotEmpty)
        .toSet();
    var search = '';
    final result = await showDialog<Set<String>>(
      context: context,
      builder: (context) => StatefulBuilder(
        builder: (context, update) => AlertDialog(
          title: UiText(f['label'] ?? name),
          content: SizedBox(
            width: 450,
            height: 420,
            child: Column(
              children: [
                TextField(
                  decoration: InputDecoration(labelText: tr(context, 'Search')),
                  onChanged: (v) => update(() => search = v.toLowerCase()),
                ),
                Expanded(
                  child: ListView(
                    children: [
                      for (final row in options.where(
                        (r) => '${r['label']}'.toLowerCase().contains(search),
                      ))
                        CheckboxListTile(
                          title: UiText.raw('${row['label']}'),
                          value: selected.contains('${row['value']}'),
                          onChanged: (v) => update(() {
                            if (!multiple) selected.clear();
                            if (v == true) {
                              selected.add('${row['value']}');
                            } else {
                              selected.remove('${row['value']}');
                            }
                          }),
                        ),
                    ],
                  ),
                ),
              ],
            ),
          ),
          actions: [
            TextButton(
              onPressed: () => Navigator.pop(context),
              child: const UiText('Cancel'),
            ),
            FilledButton(
              onPressed: () => Navigator.pop(context, selected),
              child: const UiText('Apply'),
            ),
          ],
        ),
      ),
    );
    if (result != null && mounted) {
      setState(() => controllers[name]!.text = result.join(';'));
    }
  }

  Widget field(Map<String, dynamic> f) {
    final name = f['name'] as String, type = f['type'];
    final label = tr(context, f['label'] ?? name);
    final choices = choicesFor(name);
    if (choices.isNotEmpty && type != 'bool' && type != 'map') {
      final selected = controllers[name]!.text.split(';');
      final labels = choices
          .where((r) => selected.contains('${r['value']}'))
          .map((r) => '${r['label']}')
          .join(', ');
      return ListTile(
        contentPadding: EdgeInsets.zero,
        title: UiText(f['label'] ?? name),
        subtitle: UiText.raw(
          labels.isEmpty ? tr(context, 'Select') : labels,
          maxLines: 3,
          overflow: TextOverflow.ellipsis,
        ),
        trailing: const Icon(Icons.expand_more),
        onTap: () => selectChoices(f, choices),
      );
    }
    if (type == 'map' &&
        (name.toLowerCase() == 'access' ||
            name.toLowerCase().contains('permission'))) {
      Map<String, dynamic> current = {};
      try {
        current = Map<String, dynamic>.from(
          jsonDecode(
            controllers[name]!.text.isEmpty ? '{}' : controllers[name]!.text,
          ),
        );
      } catch (_) {}
      final keys = List<Map<String, dynamic>>.from(
        widget.lookups['menus'] ?? [],
      );
      for (final row in keys) {
        current.putIfAbsent(
          '${row['value']}',
          () => widget.module['key'] == 'roles' ? 'Allow' : 'Inherit',
        );
      }
      controllers[name]!.text = jsonEncode(current);
      return ExpansionTile(
        title: UiText(f['label'] ?? name),
        children: [
          for (final row in keys)
            DropdownButtonFormField<String>(
              initialValue: current['${row['value']}'],
              decoration: InputDecoration(
                labelText: tr(context, '${row['label']}'),
              ),
              items: [
                for (final value
                    in (widget.module['key'] == 'roles'
                        ? ['Allow', 'Deny']
                        : ['Inherit', 'Allow', 'Deny']))
                  DropdownMenuItem(value: value, child: UiText(value)),
              ],
              onChanged: (v) {
                current['${row['value']}'] = v;
                controllers[name]!.text = jsonEncode(current);
              },
            ),
        ],
      );
    }

    if (type == 'bool') {
      return SwitchListTile(
        contentPadding: EdgeInsets.zero,
        title: UiText(f['label'] ?? name),
        value:
            values[name] == true ||
            values[name] == 'True' ||
            values[name] == 'true',
        onChanged: busy ? null : (v) => setState(() => values[name] = v),
      );
    }
    if (type == 'file') {
      return OutlinedButton.icon(
        icon: const Icon(Icons.attach_file),
        label: UiText(files[name] ?? f['label'] ?? name),
        onPressed: busy
            ? null
            : () async {
                final picked = await FilePicker.pickFiles(withData: true);
                if (picked == null) return;
                final file = picked.files.single;
                if (file.size > 5 * 1024 * 1024) {
                  if (mounted) {
                    setState(() => error = 'Choose a file up to 5 MB.');
                  }
                  return;
                }
                final bytes = file.bytes;
                if (bytes == null || !mounted) return;
                setState(() {
                  values['$name.data'] = base64Encode(bytes);
                  values['$name.name'] = file.name;
                  values['$name.type'] =
                      file.extension == 'jpg' || file.extension == 'jpeg'
                      ? 'image/jpeg'
                      : file.extension == 'png'
                      ? 'image/png'
                      : file.extension == 'pdf'
                      ? 'application/pdf'
                      : 'application/octet-stream';
                  files[name] = file.name;
                });
              },
      );
    }
    if (type == 'enum' ||
        (f['options'] is List && (f['options'] as List).isNotEmpty)) {
      final options = List<String>.from(f['options'] ?? []);
      final current = controllers[name]!.text;
      return DropdownButtonFormField<String>(
        initialValue: options.contains(current) ? current : null,
        decoration: InputDecoration(labelText: label),
        items: [
          for (final option in options)
            DropdownMenuItem(value: option, child: UiText(option)),
        ],
        onChanged: (v) => controllers[name]!.text = v ?? '',
      );
    }
    if (type == 'date') {
      return TextFormField(
        controller: controllers[name],
        readOnly: true,
        decoration: InputDecoration(
          labelText: label,
          suffixIcon: const Icon(Icons.calendar_month_outlined),
        ),
        onTap: busy
            ? null
            : () async {
                var current =
                    DateTime.tryParse(controllers[name]!.text) ??
                    DateTime.now();
                if (current.year < 1900 || current.year > 2100) {
                  current = DateTime.now();
                }
                final picked = await showDatePicker(
                  context: context,
                  initialDate: current,
                  firstDate: DateTime(1900),
                  lastDate: DateTime(2100),
                );
                if (picked == null || !mounted) return;
                final time = await showTimePicker(
                  context: context,
                  initialTime: TimeOfDay.fromDateTime(current),
                );
                if (!mounted) return;
                setState(
                  () => controllers[name]!.text = DateTime(
                    picked.year,
                    picked.month,
                    picked.day,
                    time?.hour ?? 0,
                    time?.minute ?? 0,
                  ).toIso8601String(),
                );
              },
      );
    }
    return TextFormField(
      controller: controllers[name],
      enabled: !busy,
      obscureText: type == 'password',
      maxLines: type == 'map'
          ? 4
          : type == 'password'
          ? 1
          : null,
      keyboardType: type == 'number'
          ? const TextInputType.numberWithOptions(decimal: true, signed: true)
          : TextInputType.text,
      decoration: InputDecoration(
        labelText: label,
        helperText: type == 'date'
            ? tr(context, 'Date: YYYY-MM-DD; time: YYYY-MM-DD HH:MM')
            : type == 'list'
            ? tr(context, 'Separate selected identifiers with semicolons')
            : type == 'map'
            ? tr(context, 'Permission map: menu name to Allow, Deny or Inherit')
            : null,
      ),
      validator: (v) {
        if (type == 'map' && (v ?? '').isNotEmpty) {
          try {
            if (jsonDecode(v!) is! Map) {
              return tr(context, 'Enter a valid permission map.');
            }
          } catch (_) {
            return tr(context, 'Enter a valid permission map.');
          }
        }
        return null;
      },
    );
  }
}
