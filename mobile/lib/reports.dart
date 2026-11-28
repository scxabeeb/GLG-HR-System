import 'ui_preferences.dart';
import 'package:flutter/material.dart';
import 'api.dart';
import 'main.dart' show DataView, TaskScreen, MeetingScreen, contentList, date;

class AdminReportEntry extends StatelessWidget {
  const AdminReportEntry({
    super.key,
    required this.api,
    required this.module,
    this.dashboard = false,
  });
  final Api api;
  final String module;
  final bool dashboard;
  @override
  Widget build(BuildContext context) => FutureBuilder<dynamic>(
    future: api.get('/api/mobile/v2/manage/options?module=$module'),
    builder: (context, snapshot) {
      if (!snapshot.hasData || (snapshot.data as Map)['canReport'] != true) {
        return const SizedBox.shrink();
      }
      if (dashboard) return EmployeeProgress(api: api);
      return OutlinedButton.icon(
        icon: const Icon(Icons.assessment_outlined),
        label: UiText(module == 'tasks' ? 'Task report' : 'Meeting report'),
        onPressed: () => Navigator.push(
          context,
          MaterialPageRoute(
            builder: (_) => OperationsReport(api: api, module: module),
          ),
        ),
      );
    },
  );
}

class EmployeeProgress extends StatelessWidget {
  const EmployeeProgress({super.key, required this.api});
  final Api api;
  @override
  Widget build(BuildContext context) => Card(
    child: Padding(
      padding: const EdgeInsets.all(16),
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          UiText(
            'Employee task progress',
            style: Theme.of(context).textTheme.titleMedium,
          ),
          const UiText('Completion excludes cancelled assignments.'),
          SizedBox(
            height: 370,
            child: DataView(
              load: () => api.get('/api/mobile/v2/manage/reports/tasks'),
              builder: (data, reload) {
                final employees = List<Map<String, dynamic>>.from(
                  data['employees'] as List,
                );
                return employees.isEmpty
                    ? const Center(child: UiText('No assigned tasks yet.'))
                    : ListView.separated(
                        itemCount: employees.length,
                        separatorBuilder: (_, _) => const Divider(),
                        itemBuilder: (context, index) => ProgressRow(
                          employee: employees[index],
                          onTap: () => Navigator.push(
                            context,
                            MaterialPageRoute(
                              builder: (_) => OperationsReport(
                                api: api,
                                module: 'tasks',
                                initialSearch:
                                    employees[index]['name'] as String,
                              ),
                            ),
                          ),
                        ),
                      );
              },
            ),
          ),
          TextButton.icon(
            onPressed: () => Navigator.push(
              context,
              MaterialPageRoute(
                builder: (_) => OperationsReport(api: api, module: 'tasks'),
              ),
            ),
            icon: const Icon(Icons.assessment_outlined),
            label: const UiText('View task report'),
          ),
        ],
      ),
    ),
  );
}

class ProgressRow extends StatelessWidget {
  const ProgressRow({super.key, required this.employee, this.onTap});
  final Map<String, dynamic> employee;
  final VoidCallback? onTap;
  @override
  Widget build(BuildContext context) => InkWell(
    onTap: onTap,
    child: Padding(
      padding: const EdgeInsets.symmetric(vertical: 12),
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          Row(
            children: [
              Expanded(
                child: UiText(
                  '${employee['name']}',
                  style: const TextStyle(fontWeight: FontWeight.w600),
                ),
              ),
              UiText('${employee['progress']}%'),
            ],
          ),
          const SizedBox(height: 8),
          LinearProgressIndicator(
            value: (employee['progress'] as num).toDouble() / 100,
            minHeight: 6,
            borderRadius: BorderRadius.circular(6),
          ),
          const SizedBox(height: 8),
          UiText(
            '${employee['done']} done · ${employee['inProgress']} in progress · ${employee['open']} open · ${employee['overdue']} overdue',
            style: Theme.of(context).textTheme.bodySmall,
          ),
        ],
      ),
    ),
  );
}

class OperationsReport extends StatefulWidget {
  const OperationsReport({
    super.key,
    required this.api,
    required this.module,
    this.initialSearch = '',
  });
  final Api api;
  final String module;
  final String initialSearch;
  @override
  State<OperationsReport> createState() => _OperationsReportState();
}

class _OperationsReportState extends State<OperationsReport> {
  late final TextEditingController search = TextEditingController(
    text: widget.initialSearch,
  );
  String status = '';
  DateTimeRange? range;
  int page = 1;
  int revision = 0;
  bool get tasks => widget.module == 'tasks';
  @override
  void dispose() {
    search.dispose();
    super.dispose();
  }

  String get path {
    String day(DateTime value) =>
        '${value.year}-${value.month.toString().padLeft(2, '0')}-${value.day.toString().padLeft(2, '0')}';
    return '/api/mobile/v2/manage/reports/${widget.module}?${Uri(queryParameters: {'page': '$page', 'search': search.text.trim(), 'status': status, if (range != null) 'from': day(range!.start), if (range != null) 'to': day(range!.end)}).query}';
  }

  @override
  Widget build(BuildContext context) => Scaffold(
    appBar: AppBar(title: UiText(tasks ? 'Task report' : 'Meeting report')),
    body: Column(
      children: [
        Padding(
          padding: const EdgeInsets.all(16),
          child: Column(
            children: [
              TextField(
                controller: search,
                decoration: localizeDecoration(
                  context,
                  InputDecoration(
                    labelText: tasks ? 'Task or employee' : 'Meeting or agenda',
                    prefixIcon: const Icon(Icons.search),
                  ),
                ),
                onSubmitted: (_) => setState(() {
                  page = 1;
                  revision++;
                }),
              ),
              const SizedBox(height: 10),
              DropdownButtonFormField<String>(
                key: ValueKey(status),
                initialValue: status,
                isExpanded: true,
                decoration: localizeDecoration(
                  context,
                  const InputDecoration(labelText: 'Status'),
                ),
                items: [
                  const DropdownMenuItem(
                    value: '',
                    child: UiText('All statuses'),
                  ),
                  for (final value
                      in tasks
                          ? ['Open', 'InProgress', 'Done', 'Cancelled']
                          : [
                              'Upcoming',
                              'InProgress',
                              'Completed',
                              'Cancelled',
                            ])
                    DropdownMenuItem(
                      value: value,
                      child: UiText(
                        value == 'InProgress' ? 'In progress' : value,
                      ),
                    ),
                ],
                onChanged: (value) => setState(() {
                  status = value ?? '';
                  page = 1;
                  revision++;
                }),
              ),
              Wrap(
                spacing: 8,
                children: [
                  TextButton.icon(
                    icon: const Icon(Icons.date_range),
                    label: UiText.raw(
                      range == null
                          ? (tasks ? 'Due dates' : 'Meeting dates (EAT)')
                          : '${date(range!.start.toIso8601String())} – ${date(range!.end.toIso8601String())}',
                    ),
                    onPressed: () async {
                      final picked = await showDateRangePicker(
                        context: context,
                        firstDate: DateTime(2000),
                        lastDate: DateTime(2100),
                        initialDateRange: range,
                      );
                      if (picked != null && mounted) {
                        setState(() {
                          range = picked;
                          page = 1;
                          revision++;
                        });
                      }
                    },
                  ),
                  TextButton(
                    onPressed: () => setState(() {
                      range = null;
                      search.clear();
                      status = '';
                      page = 1;
                      revision++;
                    }),
                    child: const UiText('Clear'),
                  ),
                  FilledButton(
                    onPressed: () => setState(() {
                      page = 1;
                      revision++;
                    }),
                    child: const UiText('Apply'),
                  ),
                ],
              ),
            ],
          ),
        ),
        Expanded(
          child: DataView(
            key: ValueKey('$revision-$page'),
            load: () => widget.api.get(path),
            builder: (data, reload) {
              final items = List<Map<String, dynamic>>.from(
                data['items'] as List,
              );
              final summary = data['summary'] as Map;
              return contentList([
                UiText(
                  '${data['total']} ${tasks ? 'assignments' : 'meetings'} match',
                  style: Theme.of(context).textTheme.titleMedium,
                ),
                UiText(
                  tasks
                      ? '${summary['done']} done · ${summary['active']} active · ${summary['overdue']} overdue'
                      : '${summary['completed']} completed · ${summary['withMinutes']} with minutes · ${summary['cancelled']} cancelled',
                ),
                if (tasks)
                  ExpansionTile(
                    title: const UiText('Progress by employee'),
                    children: [
                      for (final employee in List<Map<String, dynamic>>.from(
                        data['employees'] as List,
                      ))
                        Padding(
                          padding: const EdgeInsets.symmetric(horizontal: 16),
                          child: ProgressRow(employee: employee),
                        ),
                    ],
                  ),
                if (items.isEmpty)
                  const Padding(
                    padding: EdgeInsets.all(24),
                    child: UiText('No records match these filters.'),
                  ),
                for (final item in items)
                  Card(
                    child: ListTile(
                      isThreeLine: true,
                      title: UiText(
                        '${item['responsibility'] ?? item['title']}',
                      ),
                      subtitle: UiText(
                        tasks
                            ? '${item['assigneeName']} · ${item['status']}\nDue ${date(item['dueDate'])}'
                            : '${item['organizer']} · ${item['status']}\n${date(item['startsUtc'])} · ${item['attended']}/${item['invited']} attended · ${item['hasMinutes'] == true ? 'Minutes available' : 'No minutes'}',
                      ),
                      trailing: const Icon(Icons.chevron_right),
                      onTap: () async {
                        await Navigator.push(
                          context,
                          MaterialPageRoute(
                            builder: (_) => tasks
                                ? TaskScreen(
                                    api: widget.api,
                                    task: {...item, 'canManage': true},
                                  )
                                : MeetingScreen(
                                    api: widget.api,
                                    id: item['id'],
                                  ),
                          ),
                        );
                        reload();
                      },
                    ),
                  ),
                Wrap(
                  alignment: WrapAlignment.spaceBetween,
                  crossAxisAlignment: WrapCrossAlignment.center,
                  children: [
                    TextButton(
                      onPressed: page > 1
                          ? () => setState(() {
                              page--;
                            })
                          : null,
                      child: const UiText('Previous'),
                    ),
                    UiText('Page $page'),
                    TextButton(
                      onPressed:
                          page * (data['pageSize'] as int) <
                              (data['total'] as int)
                          ? () => setState(() {
                              page++;
                            })
                          : null,
                      child: const UiText('Next'),
                    ),
                  ],
                ),
              ], refresh: () async => reload());
            },
          ),
        ),
      ],
    ),
  );
}
