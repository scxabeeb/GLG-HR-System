using System.ComponentModel.DataAnnotations;
using HrSystemDhg.Web.Data;
using HrSystemDhg.Web.Models;
using HrSystemDhg.Web.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace HrSystemDhg.Web.Pages.Operations;

[RequestFormLimits(ValueCountLimit = 20000)]
public class TaskReportModel(HrDbContext db, OperationsNotices notices) : PageModel
{
    public bool IsAdmin => db.IsAdmin;
    [BindProperty(SupportsGet = true)] public string? Scope { get; set; }
    [BindProperty(SupportsGet = true)] public TaskProgress? Status { get; set; }
    [BindProperty(SupportsGet = true)] public int? TaskId { get; set; }
    [BindProperty(SupportsGet = true)] public int? TeamId { get; set; }
    public bool CanAddSubtask { get; private set; }
    public Dictionary<string,string> EligibleAssignees { get; private set; } = [];
    public List<SubtaskInput> Drafts { get; private set; } = [new()];

    public class SubtaskInput
    {
        public string[] Assignees { get; set; } = [];
        [Required, StringLength(1000)] public string Responsibility { get; set; } = "";
        [StringLength(2500)] public string? Details { get; set; }
        public DateTime? DueDate { get; set; }
        [EnumDataType(typeof(TaskPriority))] public TaskPriority Priority { get; set; } = TaskPriority.Normal;
    }

    public string? TeamBrief { get; private set; }
    public List<string> TeamMembers { get; private set; } = [];
    public string? TeamTitle { get; private set; }
    public string? TaskTitle { get; private set; }
    public Dictionary<TaskProgress,int> Totals { get; private set; } = [];
    public List<WorkTask> Tasks { get; private set; } = [];
    public List<TaskActivity> Activity { get; private set; } = [];
    public Dictionary<string,string> People { get; private set; } = [];
    public static string Label(TaskProgress value) => value == TaskProgress.InProgress ? "In progress" : value.ToString();

    private IQueryable<WorkTask> ScopedTasks(string actor)
    {
        Scope = Scope switch { "all" when IsAdmin => "all", "created" => "created", "mine" => "mine", _ => "related" };
        return Scope switch
        {
            "all" => db.WorkTasks.AsNoTracking(),
            "mine" => db.WorkTasks.AsNoTracking().Where(t => t.AssigneeId == actor),
            "created" => db.WorkTasks.AsNoTracking().Where(t => t.OwnerId == actor),
            _ => OperationsPolicy.Tasks(db, actor).AsNoTracking()
        };
    }

    public async Task<IActionResult> OnGetAsync()
    {
        Response.Headers.CacheControl = "no-store";
        var actor = db.ActorId!;
        var tasks = ScopedTasks(actor);
        if (TeamId is int teamId)
        {
            var team = await db.WorkTaskTeams.AsNoTracking().SingleOrDefaultAsync(t => t.Id == teamId && (IsAdmin || t.OwnerId == actor || t.Members.Any(m => m.UserId == actor)));
            if (team == null) return NotFound();
            TeamTitle = team.Title;
            TeamBrief = team.Brief;
            var canSeeTeam = IsAdmin || team.OwnerId == actor;
            CanAddSubtask = db.IsStaff && team.OwnerId == actor;
            if (CanAddSubtask)
                EligibleAssignees = await OperationsPolicy.EligiblePeople(db, "tasks")
                    .Where(u => db.WorkTaskTeamMembers.Any(m => m.TeamId == teamId && m.UserId == u.Id)).OrderBy(u => u.Email)
                    .ToDictionaryAsync(u => u.Id, u => u.Email ?? u.UserName ?? "Employee");
            if (canSeeTeam)
                TeamMembers = await db.Users.Where(u => db.WorkTaskTeamMembers.Any(m => m.TeamId == teamId && m.UserId == u.Id))
                    .OrderBy(u => u.Email).Select(u => u.Email ?? u.UserName ?? "Employee").ToListAsync();
            tasks = db.WorkTasks.AsNoTracking().Where(t => t.TeamId == teamId && (canSeeTeam || t.AssigneeId == actor));
        }
        else if (TaskId is int id)
        {
            if (IsAdmin) { Scope = "all"; tasks = db.WorkTasks.AsNoTracking(); }
            else { Scope = "related"; tasks = OperationsPolicy.Tasks(db, actor).AsNoTracking(); }
            tasks = tasks.Where(t => t.Id == id);
            TaskTitle = await tasks.Select(t => t.Title).SingleOrDefaultAsync();
            if (TaskTitle == null) return NotFound();
        }
        Totals = await tasks.GroupBy(t => t.Status).Select(g => new { Status = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.Status, x => x.Count);
        if (Status.HasValue) tasks = tasks.Where(t => t.Status == Status.Value);

        Tasks = await tasks.OrderByDescending(t => t.UpdatedUtc).ThenByDescending(t => t.Id).ToListAsync();
        Activity = await db.TaskActivities.AsNoTracking().Where(a => tasks.Any(t => t.Id == a.TaskId))
            .Include(a => a.Task).OrderByDescending(a => a.RecordedUtc).ThenByDescending(a => a.Id).Take(100).ToListAsync();
        var ids = Tasks.Select(t => t.AssigneeId).Concat(Activity.Select(a => a.ActorId)).Distinct().ToArray();
        People = await db.Users.AsNoTracking().Where(u => ids.Contains(u.Id))
            .ToDictionaryAsync(u => u.Id, u => u.Email ?? u.UserName ?? "User");
        return Page();
    }
    public async Task<IActionResult> OnGetCsvAsync(string format = "csv")
    {
        Response.Headers.CacheControl = "no-store";
        var loaded = await OnGetAsync();
        if (loaded is not PageResult) return loaded;
        var query = IsAdmin && (TeamId.HasValue || TaskId.HasValue) ? db.WorkTasks.AsNoTracking() : ScopedTasks(db.ActorId!);
        if (TeamId.HasValue) query = query.Where(t => t.TeamId == TeamId);
        if (TaskId.HasValue) query = query.Where(t => t.Id == TaskId);
        if (Status.HasValue) query = query.Where(t => t.Status == Status.Value);
        var tasks = await query.OrderByDescending(t => t.UpdatedUtc).ThenByDescending(t => t.Id).Take(10001).ToListAsync();
        if (tasks.Count > 10000) return BadRequest("Choose a narrower scope or status to export up to 10,000 rows.");
        var ids = tasks.Select(t => t.AssigneeId).Distinct().ToArray();
        var names = await db.Users.Where(u => ids.Contains(u.Id)).ToDictionaryAsync(u => u.Id, u => u.Email ?? u.UserName ?? "User");
        var rows = new List<string[]> { new[] { "Task", "Responsibility", "Assignee", "Due date", "Status", "Updated (EAT)" } };
        rows.AddRange(tasks.Select(t => new[] { t.Title, t.Responsibility ?? "", names.GetValueOrDefault(t.AssigneeId, ""),
            t.DueDate?.ToString("yyyy-MM-dd") ?? "", Label(t.Status),
            t.UpdatedUtc is DateTime date ? OperationsPolicy.Local(date).ToString("yyyy-MM-dd HH:mm") : "" }));
        return ExportService.Report(rows, "task-progress", format);
    }

    public async Task<IActionResult> OnPostAddSubtaskAsync([Bind(Prefix = "Drafts")] List<SubtaskInput>? drafts)
    {
        if (!db.IsStaff) return Forbid();
        var actor = db.ActorId!;
        var team = await db.WorkTaskTeams.SingleOrDefaultAsync(t => t.Id == TeamId && t.OwnerId == actor);
        if (team == null) return NotFound();
        if (drafts == null || drafts.Count is < 1 or > 50)
        {
            ModelState.AddModelError("", "Add between 1 and 50 subtasks per save.");
            return await OnGetAsync();
        }
        Drafts = drafts;
        var eligible = (await OperationsPolicy.EligiblePeople(db, "tasks")
            .Where(u => db.WorkTaskTeamMembers.Any(m => m.TeamId == team.Id && m.UserId == u.Id))
            .Select(u => u.Id).ToListAsync()).ToHashSet(StringComparer.Ordinal);
        for (var i = 0; i < drafts.Count; i++)
        {
            var draft = drafts[i];
            draft.Assignees = draft.Assignees.Distinct(StringComparer.Ordinal).ToArray();
            if (draft.Assignees.Length == 0 || draft.Assignees.Any(id => !eligible.Contains(id)))
                ModelState.AddModelError($"Drafts[{i}].Assignees", $"Subtask {i + 1}: select at least one eligible team member.");
            if (draft.DueDate?.Year < 1900)
                ModelState.AddModelError($"Drafts[{i}].DueDate", $"Subtask {i + 1}: select a valid due date.");
        }
        if (!ModelState.IsValid) return await OnGetAsync();

        var now = DateTime.UtcNow;
        var assignments = new List<WorkTask>();
        foreach (var draft in drafts)
        {
            var subtask = new WorkSubtask { TeamId = team.Id, Responsibility = draft.Responsibility.Trim() };
            db.WorkSubtasks.Add(subtask);
            foreach (var assignee in draft.Assignees)
            {
                var task = new WorkTask
                {
                    TeamId = team.Id, Subtask = subtask, OwnerId = actor, Title = team.Title,
                    AssigneeId = assignee, Responsibility = subtask.Responsibility, Description = draft.Details?.Trim() ?? "",
                    DueDate = draft.DueDate?.Date, Priority = draft.Priority, UpdatedUtc = now
                };
                db.WorkTasks.Add(task);
                db.TaskActivities.Add(new() { Task = task, ActorId = actor, Action = "Created subtask", NewStatus = task.Status, RecordedUtc = now });
                if (assignee != actor) assignments.Add(task);
            }
        }
        db.AuditLogs.Add(new() { Actor = actor, Action = $"Added {drafts.Count} subtask(s) to team task {team.Id}" });
        await notices.AddTaskAssignmentsAsync(assignments);
        await db.SaveChangesAsync();
        TempData["OperationsNotice"] = drafts.Count == 1 ? "Subtask added." : $"{drafts.Count} subtasks added.";
        return RedirectToPage(new { teamId = team.Id });
    }
}
