import 'ui_preferences.dart';
import 'package:flutter/material.dart';
import 'api.dart';
import 'reports.dart';
import 'main.dart'
    show DataView, FormPage, TaskScreen, contentList, date, message;

Future<void> openManager(
  BuildContext context,
  Widget page,
  VoidCallback refresh,
) async {
  await Navigator.push(context, MaterialPageRoute(builder: (_) => page));
  refresh();
}

Future<void> confirmManage(
  BuildContext context,
  Api api,
  String path,
  String title,
  VoidCallback done,
) async {
  final yes = await showDialog<bool>(
    context: context,
    builder: (c) => AlertDialog(
      title: UiText.raw(title),
      content: const UiText('Participants or assignees will see this change.'),
      actions: [
        TextButton(
          onPressed: () => Navigator.pop(c, false),
          child: const UiText('Keep'),
        ),
        FilledButton(
          onPressed: () => Navigator.pop(c, true),
          child: const UiText('Confirm'),
        ),
      ],
    ),
  );
  if (yes != true) return;
  try {
    await api.post(path, {});
    done();
  } catch (e) {
    if (context.mounted) message(context, e);
  }
}

class ManagementActions extends StatelessWidget {
  const ManagementActions({
    super.key,
    required this.api,
    required this.module,
    required this.refresh,
  });
  final Api api;
  final String module;
  final VoidCallback refresh;
  @override
  Widget build(BuildContext context) => FutureBuilder<dynamic>(
    future: api.get('/api/mobile/v2/manage/options?module=$module'),
    builder: (context, snapshot) {
      if (!snapshot.hasData) return const SizedBox.shrink();
      final options = snapshot.data as Map;
      return Padding(
        padding: const EdgeInsets.fromLTRB(16, 12, 16, 0),
        child: Wrap(
          spacing: 8,
          runSpacing: 4,
          children: [
            if (options['canReport'] == true)
              AdminReportEntry(api: api, module: module),
            if (module == 'tasks') ...[
              FilledButton.icon(
                icon: const Icon(Icons.add),
                label: UiText.raw(
                  options['canAssign'] == true
                      ? 'Create task'
                      : 'Personal task',
                ),
                onPressed: () =>
                    openManager(context, TaskEditor(api: api), refresh),
              ),
              OutlinedButton.icon(
                icon: const Icon(Icons.groups_outlined),
                label: const UiText('Team tasks'),
                onPressed: () =>
                    openManager(context, TeamTasks(api: api), refresh),
              ),
            ],
            if (module == 'meetings' && options['canOrganize'] == true)
              FilledButton.icon(
                icon: const Icon(Icons.add),
                label: const UiText('Schedule meeting'),
                onPressed: () =>
                    openManager(context, MeetingEditor(api: api), refresh),
              ),
          ],
        ),
      );
    },
  );
}

class PeoplePicker extends StatelessWidget {
  const PeoplePicker({
    super.key,
    required this.people,
    required this.selected,
    required this.changed,
    this.single = false,
  });
  final List<Map<String, dynamic>> people;
  final Set<String> selected;
  final VoidCallback changed;
  final bool single;
  @override
  Widget build(BuildContext context) => Column(
    crossAxisAlignment: CrossAxisAlignment.stretch,
    children: [
      UiText.raw(
        single ? 'Assigned employee' : 'Select employees',
        style: Theme.of(context).textTheme.titleMedium,
      ),
      if (people.isEmpty) const UiText('No eligible accounts available.'),
      if (!single && people.isNotEmpty)
        TextButton(
          onPressed: () {
            if (selected.length == people.length) {
              selected.clear();
            } else {
              selected.addAll(people.map((p) => p['id'] as String));
            }
            changed();
          },
          child: const UiText('Select / clear all'),
        ),
      Container(
        constraints: const BoxConstraints(maxHeight: 260),
        decoration: BoxDecoration(
          border: Border.all(
            color: Theme.of(context).colorScheme.outlineVariant,
          ),
          borderRadius: BorderRadius.circular(12),
        ),
        child: ListView(
          shrinkWrap: true,
          children: [
            for (final p in people)
              CheckboxListTile(
                dense: true,
                title: UiText.raw(p['name'] ?? 'Employee'),
                value: selected.contains(p['id']),
                onChanged: (value) {
                  if (single) selected.clear();
                  if (value == true) {
                    selected.add(p['id']);
                  } else {
                    selected.remove(p['id']);
                  }
                  changed();
                },
              ),
          ],
        ),
      ),
      UiText('${selected.length} selected'),
    ],
  );
}

class TaskEditor extends StatefulWidget {
  const TaskEditor({
    super.key,
    required this.api,
    this.taskId,
    this.team,
    this.createTeam = false,
  });
  final Api api;
  final int? taskId;
  final Map<String, dynamic>? team;
  final bool createTeam;
  @override
  State<TaskEditor> createState() => _TaskEditorState();
}

class _TaskEditorState extends State<TaskEditor> {
  final title = TextEditingController(),
      description = TextEditingController(),
      responsibility = TextEditingController();
  final selected = <String>{};
  List<Map<String, dynamic>> people = [];
  bool loading = true, busy = false, teamAssignment = false;
  String? error;
  DateTime? due;
  int priority = 1;
  @override
  void initState() {
    super.initState();
    load();
  }

  Future<void> load() async {
    try {
      final options =
          await widget.api.get('/api/mobile/v2/manage/options?module=tasks')
              as Map;
      people = List<Map<String, dynamic>>.from(options['people']);
      if (widget.team != null) {
        final roster = (widget.team!['members'] as List)
            .map((p) => p['userId'])
            .toSet();
        people = people.where((p) => roster.contains(p['id'])).toList();
      }
      if (widget.taskId != null) {
        final task =
            await widget.api.get('/api/mobile/v2/manage/tasks/${widget.taskId}')
                as Map;
        teamAssignment = task['teamId'] != null;
        title.text = task['title'];
        description.text = task['description'] ?? '';
        responsibility.text = task['responsibility'] ?? '';
        selected.add(task['assigneeId']);
        due = DateTime.tryParse(task['dueDate'] ?? '');
        priority = task['priority'];
        if (!people.any((p) => p['id'] == task['assigneeId'])) {
          people.add({
            'id': task['assigneeId'],
            'name': task['assigneeName'] ?? 'Current employee',
          });
        }
      } else if (options['canAssign'] != true) {
        selected.add(options['actorId']);
      }
    } catch (e) {
      error = e.toString();
    }
    if (mounted) setState(() => loading = false);
  }

  @override
  void dispose() {
    title.dispose();
    description.dispose();
    responsibility.dispose();
    super.dispose();
  }

  Future<void> save() async {
    if (title.text.trim().isEmpty || selected.isEmpty) {
      message(context, 'Enter a title and select employees.');
      return;
    }
    setState(() => busy = true);
    try {
      final body = <String, dynamic>{
        'title': title.text.trim(),
        'description': description.text,
        'assigneeIds': selected.toList(),
        'dueDate': due == null
            ? null
            : '${due!.year}-${due!.month.toString().padLeft(2, '0')}-${due!.day.toString().padLeft(2, '0')}',
        'priority': priority,
        'responsibility': responsibility.text,
        'id': widget.taskId ?? 0,
      };
      String path = '/api/mobile/v2/manage/tasks';
      if (widget.createTeam) {
        path = '/api/mobile/v2/manage/teams';
        body['members'] = selected.toList();
      }
      if (widget.team != null) {
        path = '/api/mobile/v2/manage/teams/${widget.team!['id']}/subtasks';
      }
      await widget.api.post(path, body);
      if (mounted) Navigator.pop(context);
    } catch (e) {
      if (mounted) message(context, e);
    } finally {
      if (mounted) setState(() => busy = false);
    }
  }

  @override
  Widget build(BuildContext context) => FormPage(
    title: widget.createTeam
        ? 'Create team task'
        : widget.team != null
        ? 'Add responsibility / subtask'
        : widget.taskId != null
        ? 'Edit task'
        : 'Create task',
    children: [
      if (loading)
        const Center(child: CircularProgressIndicator())
      else if (error != null)
        UiText.raw(error!)
      else ...[
        if (widget.team != null)
          UiText('Parent task · ${widget.team!['title']}'),
        TextField(
          controller: title,
          readOnly: teamAssignment,
          maxLength: 160,
          decoration: localizeDecoration(
            context,
            InputDecoration(
              labelText: widget.team == null
                  ? 'Task name'
                  : 'Subtask / responsibility',
            ),
          ),
        ),
        TextField(
          controller: description,
          maxLength: 2500,
          minLines: 3,
          maxLines: 6,
          decoration: localizeDecoration(
            context,
            const InputDecoration(labelText: 'Description / instructions'),
          ),
        ),
        if (teamAssignment)
          TextField(
            controller: responsibility,
            maxLength: 1000,
            decoration: localizeDecoration(
              context,
              const InputDecoration(
                labelText: 'Team responsibility (when applicable)',
              ),
            ),
          ),
        if (!widget.createTeam) ...[
          OutlinedButton.icon(
            icon: const Icon(Icons.calendar_today_outlined),
            label: UiText.raw(
              due == null ? 'Set due date' : date(due!.toIso8601String()),
            ),
            onPressed: busy
                ? null
                : () async {
                    final value = await showDatePicker(
                      context: context,
                      initialDate: due ?? DateTime.now(),
                      firstDate: DateTime(2000),
                      lastDate: DateTime(2100),
                    );
                    if (value != null) setState(() => due = value);
                  },
          ),
          if (due != null)
            TextButton(
              onPressed: () => setState(() => due = null),
              child: const UiText('Clear due date'),
            ),
          DropdownButtonFormField<int>(
            isExpanded: true,
            initialValue: priority,
            decoration: localizeDecoration(
              context,
              const InputDecoration(labelText: 'Priority'),
            ),
            items: [
              for (var i = 0; i < 4; i++)
                DropdownMenuItem(
                  value: i,
                  child: UiText(['Low', 'Normal', 'High', 'Urgent'][i]),
                ),
            ],
            onChanged: (v) => setState(() => priority = v!),
          ),
        ],
        PeoplePicker(
          people: people,
          selected: selected,
          single: widget.taskId != null,
          changed: () => setState(() {}),
        ),
        FilledButton(
          onPressed: busy ? null : save,
          child: UiText(
            busy
                ? 'Saving…'
                : widget.createTeam
                ? 'Create team task'
                : 'Save assignment',
          ),
        ),
      ],
    ],
  );
}

class TeamTasks extends StatelessWidget {
  const TeamTasks({super.key, required this.api});
  final Api api;
  @override
  Widget build(BuildContext context) => Scaffold(
    appBar: AppBar(title: const UiText('Team tasks')),
    body: DataView(
      load: () async => [
        await api.get('/api/mobile/v2/manage/teams'),
        await api.get('/api/mobile/v2/manage/options?module=tasks'),
      ],
      builder: (data, reload) => contentList([
        if (data[1]['canAssign'] == true)
          FilledButton.icon(
            icon: const Icon(Icons.add),
            label: const UiText('Create team task'),
            onPressed: () => openManager(
              context,
              TaskEditor(api: api, createTeam: true),
              reload,
            ),
          ),
        if ((data[0] as List).isEmpty) const UiText('No team tasks yet.'),
        for (final team in data[0] as List)
          Card(
            child: Padding(
              padding: const EdgeInsets.all(16),
              child: Column(
                crossAxisAlignment: CrossAxisAlignment.stretch,
                children: [
                  UiText.raw(
                    team['title'],
                    style: Theme.of(context).textTheme.titleMedium,
                  ),
                  UiText.raw(team['brief'] ?? ''),
                  const SizedBox(height: 8),
                  LinearProgressIndicator(
                    value: team['total'] == 0
                        ? 0
                        : (team['done'] as num) / (team['total'] as num),
                  ),
                  UiText(
                    '${team['done']} / ${team['total']} assignments complete',
                  ),
                  UiText(
                    'Team: ${(team['members'] as List).map((p) => p['name']).join(', ')}',
                  ),
                  for (final sub in team['subtasks'] as List)
                    ExpansionTile(
                      title: UiText.raw(sub['responsibility']),
                      children: [
                        for (final assignment
                            in sub['assignments'] as List? ?? [])
                          ListTile(
                            dense: true,
                            title: UiText.raw(
                              assignment['assigneeName'] ?? 'Employee',
                            ),
                            subtitle: UiText.raw(assignment['status']),
                            trailing: const Icon(Icons.chevron_right),
                            onTap:
                                team['canManage'] == true ||
                                    assignment['assigneeId'] ==
                                        data[1]['actorId']
                                ? () => openManager(
                                    context,
                                    TaskScreen(
                                      api: api,
                                      task: Map<String, dynamic>.from(
                                        assignment,
                                      ),
                                    ),
                                    reload,
                                  )
                                : null,
                          ),
                      ],
                    ),
                  if (team['canManage'] == true)
                    OutlinedButton.icon(
                      icon: const Icon(Icons.add),
                      label: const UiText('Add subtask'),
                      onPressed: () => openManager(
                        context,
                        TaskEditor(
                          api: api,
                          team: Map<String, dynamic>.from(team),
                        ),
                        reload,
                      ),
                    ),
                ],
              ),
            ),
          ),
      ]),
    ),
  );
}

class TaskHistory extends StatelessWidget {
  const TaskHistory({super.key, required this.api, required this.id});
  final Api api;
  final int id;
  @override
  Widget build(BuildContext context) => Scaffold(
    appBar: AppBar(title: const UiText('Task activity & progress')),
    body: DataView(
      load: () => api.get('/api/mobile/v2/manage/tasks/$id'),
      builder: (task, reload) => contentList([
        UiText.raw(
          task['title'],
          style: Theme.of(context).textTheme.titleLarge,
        ),
        UiText('Assigned to ${task['assigneeName'] ?? 'Employee'}'),
        UiText('Status · ${task['status']}'),
        for (final event in task['activity'])
          Card(
            child: ListTile(
              leading: const Icon(Icons.history),
              title: UiText.raw(event['action']),
              subtitle: UiText(
                '${event['actor'] ?? 'User'} · ${date(event['recordedUtc'], time: true)}\n${event['changedFields'] ?? ''}${event['newStatus'] == null ? '' : '\n${event['previousStatus'] ?? ''} → ${event['newStatus']}'}',
              ),
            ),
          ),
      ]),
    ),
  );
}

class MeetingEditor extends StatefulWidget {
  const MeetingEditor({super.key, required this.api, this.meeting});
  final Api api;
  final Map<String, dynamic>? meeting;
  @override
  State<MeetingEditor> createState() => _MeetingEditorState();
}

class _MeetingEditorState extends State<MeetingEditor> {
  final title = TextEditingController(),
      agenda = TextEditingController(),
      location = TextEditingController(),
      link = TextEditingController();
  final selected = <String>{};
  List<Map<String, dynamic>> people = [];
  bool loading = true, busy = false;
  String? error;
  String mode = 'BuiltIn';
  late DateTime starts, ends;
  DateTime eat(String raw) =>
      DateTime.parse(raw).toUtc().add(const Duration(hours: 3));
  @override
  void initState() {
    super.initState();
    final now = DateTime.now().toUtc().add(const Duration(hours: 3));
    starts = DateTime(now.year, now.month, now.day, now.hour + 1);
    ends = starts.add(const Duration(hours: 1));
    final m = widget.meeting;
    if (m != null) {
      title.text = m['title'];
      agenda.text = m['agenda'] ?? '';
      location.text = m['location'] ?? '';
      link.text = m['joinUrl'] ?? '';
      mode = location.text.isNotEmpty
          ? 'InPerson'
          : link.text.isNotEmpty
          ? 'External'
          : 'BuiltIn';
      final s = eat(m['startsUtc']), e = eat(m['endsUtc']);
      starts = DateTime(s.year, s.month, s.day, s.hour, s.minute);
      ends = DateTime(e.year, e.month, e.day, e.hour, e.minute);
      for (final p in m['participants'] as List? ?? []) {
        selected.add(p['userId']);
      }
    }
    load();
  }

  Future<void> load() async {
    try {
      final options =
          await widget.api.get('/api/mobile/v2/manage/options?module=meetings')
              as Map;
      people = List<Map<String, dynamic>>.from(options['people']);
      for (final id in selected) {
        if (!people.any((p) => p['id'] == id)) {
          people.add({'id': id, 'name': 'Existing participant'});
        }
      }
    } catch (e) {
      error = e.toString();
    }
    if (mounted) setState(() => loading = false);
  }

  @override
  void dispose() {
    title.dispose();
    agenda.dispose();
    location.dispose();
    link.dispose();
    super.dispose();
  }

  Future<void> selectTime(bool start) async {
    final value = start ? starts : ends;
    final day = await showDatePicker(
      context: context,
      initialDate: value,
      firstDate: DateTime(2000),
      lastDate: DateTime(2100),
    );
    if (day == null || !mounted) return;
    final time = await showTimePicker(
      context: context,
      initialTime: TimeOfDay.fromDateTime(value),
    );
    if (time != null) {
      setState(() {
        final chosen = DateTime(
          day.year,
          day.month,
          day.day,
          time.hour,
          time.minute,
        );
        if (start) {
          starts = chosen;
        } else {
          ends = chosen;
        }
      });
    }
  }

  String utc(DateTime d) => DateTime.utc(
    d.year,
    d.month,
    d.day,
    d.hour,
    d.minute,
  ).subtract(const Duration(hours: 3)).toIso8601String();
  Future<void> save() async {
    if (title.text.trim().isEmpty || !ends.isAfter(starts)) {
      message(context, 'Enter a title and an end time after the start.');
      return;
    }
    setState(() => busy = true);
    try {
      await widget.api.post('/api/mobile/v2/manage/meetings', {
        'id': widget.meeting?['id'] ?? 0,
        'title': title.text,
        'agenda': agenda.text,
        'starts': utc(starts),
        'ends': utc(ends),
        'mode': mode,
        'location': location.text,
        'joinUrl': link.text,
        'guests': selected.toList(),
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
    title: widget.meeting == null ? 'Schedule meeting' : 'Edit meeting',
    children: [
      if (loading)
        const Center(child: CircularProgressIndicator())
      else if (error != null)
        UiText.raw(error!)
      else ...[
        TextField(
          controller: title,
          maxLength: 160,
          decoration: localizeDecoration(
            context,
            const InputDecoration(labelText: 'Meeting title'),
          ),
        ),
        TextField(
          controller: agenda,
          maxLength: 2500,
          minLines: 3,
          maxLines: 6,
          decoration: localizeDecoration(
            context,
            const InputDecoration(labelText: 'Main agenda'),
          ),
        ),
        DropdownButtonFormField<String>(
          isExpanded: true,
          initialValue: mode,
          decoration: localizeDecoration(
            context,
            const InputDecoration(labelText: 'Meeting type'),
          ),
          items: const [
            DropdownMenuItem(
              value: 'BuiltIn',
              child: UiText('Garowe City video room'),
            ),
            DropdownMenuItem(
              value: 'InPerson',
              child: UiText('In-person / venue'),
            ),
            DropdownMenuItem(
              value: 'External',
              child: UiText('Teams / Google Meet'),
            ),
          ],
          onChanged: (v) => setState(() => mode = v!),
        ),
        if (mode == 'InPerson')
          TextField(
            controller: location,
            maxLength: 250,
            decoration: localizeDecoration(
              context,
              const InputDecoration(labelText: 'Venue / location'),
            ),
          ),
        if (mode == 'External')
          TextField(
            controller: link,
            decoration: localizeDecoration(
              context,
              const InputDecoration(
                labelText: 'Teams or Google Meet join link',
              ),
            ),
          ),
        OutlinedButton.icon(
          icon: const Icon(Icons.schedule),
          label: UiText(
            'Starts · ${date(starts.toIso8601String(), time: true)}',
          ),
          onPressed: busy ? null : () => selectTime(true),
        ),
        OutlinedButton.icon(
          icon: const Icon(Icons.schedule),
          label: UiText('Ends · ${date(ends.toIso8601String(), time: true)}'),
          onPressed: busy ? null : () => selectTime(false),
        ),
        const UiText('Meeting times are East Africa Time (UTC+3).'),
        PeoplePicker(
          people: people,
          selected: selected,
          changed: () => setState(() {}),
        ),
        FilledButton(
          onPressed: busy ? null : save,
          child: UiText(
            busy ? 'Saving…' : 'Save meeting and notify participants',
          ),
        ),
      ],
    ],
  );
}

class MinutesEditor extends StatefulWidget {
  const MinutesEditor({super.key, required this.api, required this.meeting});
  final Api api;
  final Map<String, dynamic> meeting;
  @override
  State<MinutesEditor> createState() => _MinutesEditorState();
}

class _MinutesEditorState extends State<MinutesEditor> {
  late final text = TextEditingController(
    text: widget.meeting['publishedMinutes'] ?? '',
  );
  bool busy = false;
  @override
  void dispose() {
    text.dispose();
    super.dispose();
  }

  @override
  Widget build(BuildContext context) => FormPage(
    title: 'Meeting minutes',
    children: [
      UiText.raw(widget.meeting['title']),
      TextField(
        controller: text,
        minLines: 8,
        maxLines: 18,
        maxLength: 12000,
        decoration: localizeDecoration(
          context,
          const InputDecoration(labelText: 'Minutes / decisions'),
        ),
      ),
      FilledButton(
        onPressed: busy
            ? null
            : () async {
                setState(() => busy = true);
                try {
                  await widget.api.post(
                    '/api/mobile/v2/manage/meetings/${widget.meeting['id']}/minutes',
                    {'minutes': text.text},
                  );
                  if (context.mounted) Navigator.pop(context);
                } catch (e) {
                  if (context.mounted) message(context, e);
                } finally {
                  if (mounted) setState(() => busy = false);
                }
              },
        child: UiText(busy ? 'Publishing…' : 'Publish minutes to participants'),
      ),
    ],
  );
}
