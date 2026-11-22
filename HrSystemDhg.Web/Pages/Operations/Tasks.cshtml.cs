using System.ComponentModel.DataAnnotations;
using HrSystemDhg.Web.Data;
using HrSystemDhg.Web.Models;
using HrSystemDhg.Web.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace HrSystemDhg.Web.Pages.Operations;
public class TasksModel(HrDbContext db, OperationsNotices notices, ModuleAccess access) : PageModel
{
    public string Actor => db.ActorId!;
    public bool CanAssign => db.IsStaff;
    public bool IsAdmin => db.IsAdmin;
    [BindProperty(SupportsGet = true)] public int? Focus { get; set; }
    public WorkTask? FocusedTask { get; private set; }
    public bool SharedSubtask { get; private set; }
    public bool EditingTeamAssignment { get; private set; }
    [BindProperty(SupportsGet = true)] public string? View { get; set; }
    public List<WorkTaskTeam> EmptyTeams { get; private set; } = [];
    public List<WorkTask> Items { get; private set; } = [];
    public HashSet<int> VisibleSourceMeetings { get; private set; } = [];
    public List<DepartmentOption> Departments { get; private set; } = [];
    public record DepartmentOption(int Id, string Label);
    public Dictionary<string,string> Assignable { get; private set; } = [];
    public Dictionary<string,string> AssignmentOptions { get; private set; } = [];
    public Dictionary<string,string> People { get; private set; } = [];
    public Dictionary<int, string[]> TeamRosters { get; private set; } = [];

    public Dictionary<int, (int Done, int Total)> TeamProgress { get; private set; } = [];

    public string PersonName(string id) => People.GetValueOrDefault(id, "Account unavailable");

    [BindProperty(SupportsGet = true)] public string? Kind { get; set; }
    [BindProperty(SupportsGet = true)] public string? Search { get; set; }
    [BindProperty(SupportsGet = true)] public TaskPriority? PriorityFilter { get; set; }
    [BindProperty(SupportsGet = true)] public TaskProgress? Filter { get; set; }
    [BindProperty] public TaskInput Input { get; set; } = new();
    public class TaskInput
    {
        public int Id { get; set; }
        [Required, StringLength(160)] public string Title { get; set; } = "";
        [StringLength(2500)] public string? Description { get; set; }
        [StringLength(255)] public string? AssigneeId { get; set; }
        public string[] AssigneeIds { get; set; } = [];
        public int? DepartmentId { get; set; }
        public bool AssignToAll { get; set; }
        public bool DivideWork { get; set; }
        [StringLength(1000)] public string? Responsibility { get; set; }
        [DataType(DataType.Date)] public DateTime? DueDate { get; set; }
        [EnumDataType(typeof(TaskPriority))] public TaskPriority Priority { get; set; } = TaskPriority.Normal;
    }

    private async Task Load(WorkTask? editing = null)
    {
        View = View switch { "created" => "created", "mine" => "mine", "all" when IsAdmin => "all", _ => "related" };
        var query = View switch {
            "created" => db.WorkTasks.Where(t => t.OwnerId == Actor),
            "all" => db.WorkTasks,
            "related" => OperationsPolicy.Tasks(db, Actor),
            _ => db.WorkTasks.Where(t => t.AssigneeId == Actor)
        };
        Kind = Kind is "individual" or "team" ? Kind : null;
        Search = Search?.Trim();
        if (Search?.Length > 120) Search = Search[..120];
        if (Kind == "individual") query = query.Where(t => t.TeamId == null);
        if (Kind == "team") query = query.Where(t => t.TeamId != null);
        if (PriorityFilter.HasValue) query = query.Where(t => t.Priority == PriorityFilter.Value);
        if (!string.IsNullOrEmpty(Search))
        {
            var term = Search;
            query = query.Where(t => t.Title.Contains(term) || t.Description.Contains(term)
                || (t.Responsibility != null && t.Responsibility.Contains(term))
                || (t.Team != null && t.Team.Brief != null && t.Team.Brief.Contains(term))
                || db.Users.Any(u => u.Id == t.AssigneeId && ((u.Email != null && u.Email.Contains(term))
                    || db.Employees.Any(e => e.Id == u.EmployeeId && e.FullName.Contains(term)))));
        }
        if (Filter.HasValue) query = query.Where(t => t.Status == Filter.Value);
        Items = await query.AsNoTracking().Include(t => t.Team).OrderBy(t => t.Status >= TaskProgress.Done).ThenByDescending(t => t.Priority)
            .ThenBy(t => t.DueDate == null).ThenBy(t => t.DueDate).ThenByDescending(t => t.Id).ToListAsync();
        var sourceIds = Items.Select(t => t.SourceMeetingId).OfType<int>().Distinct().ToArray();
        if (sourceIds.Length > 0 && ModuleAccess.Allows(await access.BlockedAsync(), "meetings"))
        {
            var meetings = await OperationsPolicy.Meetings(db, Actor)
                .Where(m => sourceIds.Contains(m.Id)).Select(m => m.Id).ToListAsync();
            VisibleSourceMeetings = meetings.ToHashSet();
        }
        var shownTeams = Items.Where(t => t.TeamId.HasValue).Select(t => t.TeamId!.Value).Distinct().ToArray();
        if (!Filter.HasValue && !PriorityFilter.HasValue && Kind != "individual")
        {
            var teams = db.WorkTaskTeams.AsNoTracking().Where(t => !shownTeams.Contains(t.Id));
            teams = View switch
            {
                "all" => teams,
                "created" => teams.Where(t => t.OwnerId == Actor),
                "related" => teams.Where(t => t.OwnerId == Actor || t.Members.Any(m => m.UserId == Actor)),
                _ => teams.Where(t => t.Members.Any(m => m.UserId == Actor))
            };
            if (!string.IsNullOrEmpty(Search))
            {
                var term = Search;
                teams = teams.Where(t => t.Title.Contains(term) || (t.Brief != null && t.Brief.Contains(term)));
                teams = teams.Where(t => !db.WorkTasks.Any(a => a.TeamId == t.Id &&
                    (View == "all" || (View != "mine" && a.OwnerId == Actor) || (View != "created" && a.AssigneeId == Actor))));
            }
            EmptyTeams = await teams.OrderByDescending(t => t.Id).ToListAsync();
        }
        var visibleTeamIds = Items.Select(t => t.Team).OfType<WorkTaskTeam>().Concat(EmptyTeams)
            .Where(t => IsAdmin || t.OwnerId == Actor).Select(t => t.Id).Distinct().ToArray();
        var progressTeamIds = shownTeams.Concat(EmptyTeams.Select(t => t.Id)).ToArray();
        var progress = await db.WorkTasks.AsNoTracking()
            .Where(t => t.TeamId.HasValue && progressTeamIds.Contains(t.TeamId.Value)
                && (visibleTeamIds.Contains(t.TeamId.Value) || t.AssigneeId == Actor)
                && t.Status != TaskProgress.Cancelled)
            .GroupBy(t => t.TeamId!.Value)
            .Select(g => new { TeamId = g.Key, Done = g.Count(t => t.Status == TaskProgress.Done), Total = g.Count() })
            .ToListAsync();
        TeamProgress = progress.ToDictionary(x => x.TeamId, x => (x.Done, x.Total));
        var roster = await db.WorkTaskTeamMembers.AsNoTracking()
            .Where(m => visibleTeamIds.Contains(m.TeamId)).ToListAsync();
        var participants = Items.SelectMany(t => new[] { t.OwnerId, t.AssigneeId })
            .Concat(roster.Select(m => m.UserId)).Distinct().ToArray();
        People = await db.Users.Where(u => participants.Contains(u.Id))
            .Select(u => new
            {
                u.Id,
                Name = db.Employees.Where(e => e.Id == u.EmployeeId)
                    .Select(e => e.FullName).FirstOrDefault() ?? u.Email ?? u.UserName ?? "Employee"
            }).ToDictionaryAsync(u => u.Id, u => u.Name);
        TeamRosters = roster.GroupBy(m => m.TeamId)
            .ToDictionary(g => g.Key, g => g.Select(m => PersonName(m.UserId)).Order().ToArray());
        if (CanAssign)
        {
            Assignable = await OperationsPolicy.EligiblePeople(db, "tasks").OrderBy(u => u.Email)
                .ToDictionaryAsync(u => u.Id, u => u.Email ?? u.UserName ?? "Employee");
            AssignmentOptions = Assignable;
            if (editing?.TeamId is int teamId)
            {
                var allowed = await db.WorkTaskTeamMembers.Where(m => m.TeamId == teamId)
                    .Where(m => editing.SubtaskId == null || !db.WorkTasks.Any(t =>
                        t.SubtaskId == editing.SubtaskId && t.Id != editing.Id && t.AssigneeId == m.UserId))
                    .Select(m => m.UserId).ToListAsync();
                AssignmentOptions = Assignable.Where(p => allowed.Contains(p.Key)).ToDictionary();
                if (allowed.Contains(Actor)) AssignmentOptions[Actor] = "Myself";

                // Keep the current assignee visible even if their account is no longer eligible.
                if (!AssignmentOptions.ContainsKey(editing.AssigneeId))
                    AssignmentOptions[editing.AssigneeId] = await db.Users.Where(u => u.Id == editing.AssigneeId)
                        .Select(u => u.Email ?? u.UserName).SingleOrDefaultAsync() ?? "Current assignee";
            }
            Departments = await (from department in db.Departments
                                 join branch in db.Branchs on department.BranchId equals branch.Id
                                 where department.IsActive && branch.IsActive
                                 orderby branch.Name, department.Name
                                 select new DepartmentOption(department.Id, branch.Name + " / " + department.Name)).ToListAsync();
        }
    }

    public async Task<IActionResult> OnGetAsync(int? edit)
    {
        if (Focus is int id)
        {
            Response.Headers.CacheControl = "no-store";
            FocusedTask = await OperationsPolicy.Tasks(db, Actor).AsNoTracking()
                .SingleOrDefaultAsync(t => t.Id == id && t.AssigneeId == Actor);
            return FocusedTask == null ? NotFound() : Page();
        }
        WorkTask? task = null;
        if (edit.HasValue)
        {
            task = await db.WorkTasks.AsNoTracking().SingleOrDefaultAsync(t => t.Id == edit && t.OwnerId == Actor);
            if (task == null) return NotFound();
            EditingTeamAssignment = task.TeamId != null;
            SharedSubtask = task.SubtaskId != null;
            Input = new() { Responsibility = task.Responsibility, Id = task.Id, Title = task.Title, Description = task.Description,
                AssigneeId = task.AssigneeId, DueDate = task.DueDate, Priority = task.Priority };
        }
        await Load(task);
        return Page();
    }

    public async Task<IActionResult> OnPostSaveAsync()
    {
        var selected = Input.AssigneeIds.Where(id => !string.IsNullOrWhiteSpace(id)).Distinct(StringComparer.Ordinal).ToArray();
        var bulkAssignment = Input.AssignToAll || Input.DepartmentId.HasValue;
        if ((bulkAssignment || Input.DivideWork) && (!CanAssign || Input.Id != 0)) return Forbid();
        if (Input.AssignToAll && Input.DepartmentId.HasValue)
            ModelState.AddModelError("Input.DepartmentId", "Choose either a department or all eligible employees.");
        if (bulkAssignment && ModelState.IsValid)
        {
            var includeSelf = selected.Contains(Actor, StringComparer.Ordinal);
            var eligible = OperationsPolicy.EligiblePeople(db, "tasks").Where(u => u.Id != Actor && u.EmployeeId != null);
            if (Input.DepartmentId is int departmentId)
            {
                if (!await db.Departments.AnyAsync(d => d.Id == departmentId && d.IsActive &&
                    db.Branchs.Any(b => b.Id == d.BranchId && b.IsActive)))
                    ModelState.AddModelError("Input.DepartmentId", "Select an active department in your branch scope.");

                var today = BusinessClock.Today;
                eligible = eligible.Where(u => db.Employees.Any(e => e.Id == u.EmployeeId && e.HireDate <= today &&
                        (e.TerminationDate == null || e.TerminationDate >= today) && (e.Status == "Active" || e.Status == "Probation")) &&
                    db.EmployeeAssignments.Where(a => a.EmployeeId == u.EmployeeId && a.EffectiveFrom <= today &&
                        (a.EffectiveTo == null || a.EffectiveTo >= today))
                        .OrderByDescending(a => a.EffectiveFrom).ThenByDescending(a => a.Id)
                        .Select(a => (int?)a.DepartmentId).FirstOrDefault() == departmentId);
            }
            var recipients = await eligible.Select(u => u.Id).ToListAsync();
            if (recipients.Count == 0)
                ModelState.AddModelError("Input.DepartmentId", "No other eligible employees are available for this selection.");
            selected = includeSelf ? recipients.Append(Actor).ToArray() : recipients.ToArray();
        }
        if (Input.DivideWork && selected.Length == 0)
            ModelState.AddModelError("Input.DivideWork", "Select at least one team member first.");
        if (Input.Id != 0 && selected.Length != 0)
            ModelState.AddModelError("Input.AssigneeIds", "Edit one existing assignment at a time.");
        if (selected.Length == 0)
            selected = [string.IsNullOrWhiteSpace(Input.AssigneeId) ? Actor : Input.AssigneeId];
        var others = selected.Where(id => id != Actor).ToArray();
        if (!bulkAssignment && (selected.Length > 100 || selected.Any(id => id.Length > 255)))
            ModelState.AddModelError("Input.AssigneeIds", "Choose up to 100 employees.");
        else if (!bulkAssignment && others.Length != 0)
        {
            var eligible = CanAssign
                ? await OperationsPolicy.EligiblePeople(db, "tasks").Where(u => others.Contains(u.Id)).Select(u => u.Id).ToListAsync()
                : [];
            if (others.Except(eligible, StringComparer.Ordinal).Any())
                ModelState.AddModelError("Input.AssigneeIds", "Select only active accounts with task access in your branch scope.");
        }
        if (Input.DueDate?.Year < 1900) ModelState.AddModelError("Input.DueDate", "Select a valid due date.");
        WorkTask? existing = null;
        if (Input.Id != 0)
        {
            existing = await db.WorkTasks.SingleOrDefaultAsync(t => t.Id == Input.Id && t.OwnerId == Actor);
            if (existing == null) return NotFound();
            EditingTeamAssignment = existing.TeamId != null;
            SharedSubtask = existing.SubtaskId != null;
            if (existing.SubtaskId is int subtaskId && selected.Length == 1 && selected[0] != existing.AssigneeId &&
                await db.WorkTasks.AnyAsync(t => t.SubtaskId == subtaskId && t.AssigneeId == selected[0] && t.Id != existing.Id))
                ModelState.AddModelError("Input.AssigneeId", "This employee already has this subtask.");
            if (EditingTeamAssignment && string.IsNullOrWhiteSpace(Input.Responsibility))
                ModelState.AddModelError("Input.Responsibility", "A team assignment needs a responsibility.");

        }
        if (existing?.TeamId is int teamId && selected.Length == 1 &&
            !await db.WorkTaskTeamMembers.AnyAsync(m => m.TeamId == teamId && m.UserId == selected[0]))
            ModelState.AddModelError("Input.AssigneeId", "Select an employee assigned to this team.");
        if (!ModelState.IsValid) { await Load(existing); return Page(); }
        if (Input.DivideWork)
        {
            var team = new WorkTaskTeam
            {
                Title = Input.Title.Trim(), Brief = Input.Description?.Trim(), OwnerId = Actor,
                Members = selected.Select(id => new WorkTaskTeamMember { UserId = id }).ToList()
            };
            db.WorkTaskTeams.Add(team);
            foreach (var member in selected.Where(id => id != Actor))
                OperationsNotices.AddUpdate(db, member, OperationsNoticeKind.TeamAssigned, team.Title);
            db.AuditLogs.Add(new() { Actor = Actor, Action = $"Created team task with {selected.Length} member(s)" });
            await db.SaveChangesAsync();
            TempData["OperationsNotice"] = "Team task created. Add its first subtask below.";
            return RedirectToPage("./TaskReport", null, new { teamId = team.Id }, "subtask-form");
        }

        var now = DateTime.UtcNow;
        var assignmentsToNotify = new List<WorkTask>();
        foreach (var assignee in selected)
        {
            var task = existing ?? new WorkTask { OwnerId = Actor };
            var responsibility = SharedSubtask ? task.Responsibility : EditingTeamAssignment ? Input.Responsibility!.Trim() : null;
            var title = EditingTeamAssignment ? task.Title : Input.Title.Trim();
            var previousAssignee = task.AssigneeId;
            var changes = new List<string>();
            if (task.Responsibility != responsibility) changes.Add("Responsibility");
            if (task.Title != title) changes.Add("Title");
            if (task.Description != (Input.Description?.Trim() ?? "")) changes.Add("Details");
            if (task.Priority != Input.Priority) changes.Add("Priority");
            if (task.DueDate != Input.DueDate?.Date) changes.Add("Due date");
            if (task.AssigneeId != assignee) changes.Add("Assignee");
            task.Title = title;
            task.Responsibility = responsibility;
            task.Description = Input.Description?.Trim() ?? "";
            task.Priority = Input.Priority;
            task.DueDate = Input.DueDate?.Date;
            task.AssigneeId = assignee;
            task.UpdatedUtc = now;
            if (existing == null) db.WorkTasks.Add(task);
            if (existing == null || changes.Count > 0)
                db.TaskActivities.Add(new()
                {
                    Task = task, ActorId = Actor, Action = existing == null ? "Created" : "Edited",
                    ChangedFields = existing == null ? null : string.Join(", ", changes),
                    NewStatus = existing == null ? task.Status : null, RecordedUtc = now
                });
            if (assignee != Actor && assignee != previousAssignee)
                assignmentsToNotify.Add(task);
        }

        await notices.AddTaskAssignmentsAsync(assignmentsToNotify);
        db.AuditLogs.Add(new() { Actor = Actor, Action = existing == null ? $"Created {selected.Length} work task assignment(s)" : $"Updated work task {existing.Id}" });
        await db.SaveChangesAsync();
        TempData["OperationsNotice"] = selected.Length > 1 ? $"Task assigned to {selected.Length} people. Each has their own status." : "Task saved.";
        return RedirectToPage(new { View = others.Length == 0 ? "mine" : "created" });
    }

    public async Task<IActionResult> OnPostProgressAsync(int id, TaskProgress? status)
    {
        if ((Focus.HasValue && Focus != id) || id <= 0 || status is not { } progress || !Enum.IsDefined(progress)) return BadRequest();
        var task = await OperationsPolicy.Tasks(db, Actor).SingleOrDefaultAsync(t => t.Id == id);
        if (task == null) return NotFound();
        if (task.OwnerId != Actor && (status == TaskProgress.Cancelled || task.Status == TaskProgress.Cancelled)) return Forbid();
        if (task.Status == progress) return RedirectToPage(new { View, Filter, Kind, Search, PriorityFilter, Focus });
        db.TaskActivities.Add(new()
        {
            TaskId = task.Id, ActorId = Actor, Action = "Status changed",
            PreviousStatus = task.Status, NewStatus = progress, RecordedUtc = DateTime.UtcNow
        });
        task.Status = progress;
        task.UpdatedUtc = DateTime.UtcNow;
        var other = task.OwnerId == Actor ? task.AssigneeId : task.OwnerId;
        if (other != Actor) await notices.AddAsync([other], OperationsNoticeKind.TaskProgress, task: task);
        db.AuditLogs.Add(new() { Actor = Actor, Action = $"Task {id}: {status}" });
        await db.SaveChangesAsync();
        if (Focus.HasValue) TempData["OperationsNotice"] = "Task status updated.";
        return RedirectToPage(new { View, Filter, Kind, Search, PriorityFilter, Focus });
    }

    private const string CsvColumns = "Title,Description,DueDate,Priority,AssigneeIds,DivideWork";

    public IActionResult OnGetSample()
    {
        return RegistrationCsv.Download("tasks-sample", CsvColumns, [["Review inventory", "Check the asset register", BusinessClock.Today.AddDays(1), TaskPriority.Normal, null, false]]);
    }

    public async Task<IActionResult> OnGetExportAsync()
    {
        var records = await OperationsPolicy.Tasks(db, Actor).AsNoTracking().Where(x => x.TeamId == null).OrderBy(x => x.Id).ToListAsync();
        Response.Headers.CacheControl = "no-store";
        return RegistrationCsv.Download("tasks", CsvColumns, records.Select(x => new object?[] { x.Title, x.Description, x.DueDate, x.Priority, x.AssigneeId == Actor ? "" : x.AssigneeId, false }));
    }

    public Task<IActionResult> OnPostImportAsync(IFormFile? upload)
    {
        return RegistrationCsv.ImportAsync(this, db, upload, CsvColumns, async row =>
        {
            Input = new TaskInput();
            Input.Title = row.Get<string>("Title");
            Input.Description = row.Optional("Description");
            Input.DueDate = row.Get<DateTime?>("DueDate");
            Input.Priority = row.Get<TaskPriority>("Priority");
            Input.DivideWork = row.Get<bool>("DivideWork");
            Input.AssigneeIds = row.List("AssigneeIds");
            TryValidateModel(Input, "Input");
            return await OnPostSaveAsync();
        }, () => RedirectToPage(new { view = CanAssign ? "created" : "mine" }));
    }
}
