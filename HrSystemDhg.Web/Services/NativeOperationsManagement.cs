using HrSystemDhg.Web.Data;
using HrSystemDhg.Web.Models;
using Microsoft.EntityFrameworkCore;

namespace HrSystemDhg.Web.Services;

public static class NativeOperationsManagement
{
    public sealed record TaskInput(int Id, string Title, string? Description, string[] AssigneeIds, DateTime? DueDate, int Priority, string? Responsibility);
    public sealed record MeetingInput(int Id, string Title, string? Agenda, DateTimeOffset Starts, DateTimeOffset Ends, string Mode, string? Location, string? JoinUrl, string[] Guests);
    public sealed record MinutesInput(string? Minutes);
    public sealed record TeamInput(string Title, string? Description, string[] Members);
    public sealed record SubtaskInput(string Title, string? Description, string[] AssigneeIds, DateTime? DueDate, int Priority);
    static IResult Error(string message) => Results.BadRequest(new { message });
    static async Task<bool> Allowed(ModuleAccess access, string module) => ModuleAccess.Allows(await access.BlockedAsync(), module);
    static bool Title(string? value) => !string.IsNullOrWhiteSpace(value) && value.Trim().Length <= 160;
    static IQueryable<WorkTask> Tasks(HrDbContext db) => db.IsAdmin ? db.WorkTasks : OperationsPolicy.Tasks(db, db.ActorId!);
    public static void Map(WebApplication app)
    {
        NativeOperationsReports.Map(app);
        var api = app.MapGroup("/api/mobile/v2/manage").RequireAuthorization(NativeMobileApi.Policy);
        api.MapGet("/options", Options);
        api.MapPost("/tasks", SaveTask);
        api.MapGet("/tasks/{id:int}", TaskDetail);
        api.MapPost("/tasks/{id:int}/cancel", CancelTask);
        api.MapPost("/meetings", SaveMeeting);
        api.MapPost("/meetings/{id:int}/cancel", CancelMeeting);
        api.MapPost("/meetings/{id:int}/minutes", PublishMinutes);
        api.MapGet("/teams", Teams);
        api.MapPost("/teams", CreateTeam);
        api.MapPost("/teams/{id:int}/subtasks", Subtask);
    }
    static async Task<IResult> Options(string module, HrDbContext db, ModuleAccess access)
    {
        if (module is not ("tasks" or "meetings") || !await Allowed(access, module)) return Results.Forbid();
        var people = OperationsPolicy.EligiblePeople(db, module);
        if (!db.IsStaff) people = people.Where(u => u.Id == db.ActorId);
        var rows = await people.OrderBy(u => u.Email).Select(u => new { u.Id, Name = db.Employees.Where(e => e.Id == u.EmployeeId).Select(e => e.FullName).FirstOrDefault() ?? u.Email ?? u.UserName }).Take(1000).ToListAsync();
        return Results.Ok(new { People = rows, CanAssign = db.IsStaff, CanOrganize = db.IsStaff, IsAdmin = db.IsAdmin, CanReport = db.IsAdmin && await Allowed(access, "reports"), ActorId = db.ActorId });
    }
    static async Task<bool> Assignees(HrDbContext db, string module, string[] ids)
    {
        if (ids.Length is < 1 or > 100 || ids.Any(id => string.IsNullOrEmpty(id) || id.Length > 255)) return false;
        if (!db.IsStaff && ids.Any(id => id != db.ActorId)) return false;
        return await OperationsPolicy.EligiblePeople(db, module).CountAsync(u => ids.Contains(u.Id)) == ids.Length;
    }
    static async Task<IResult> TaskDetail(int id, HrDbContext db, ModuleAccess access)
    {
        if (!await Allowed(access, "tasks")) return Results.Forbid();
        var task = await Tasks(db).AsNoTracking().SingleOrDefaultAsync(t => t.Id == id);
        if (task == null) return Results.NotFound();
        var activity = await db.TaskActivities.AsNoTracking().Where(a => a.TaskId == id).OrderByDescending(a => a.Id).Take(100)
            .Select(a => new { a.Action, a.ChangedFields, RecordedUtc = DateTime.SpecifyKind(a.RecordedUtc, DateTimeKind.Utc), PreviousStatus = a.PreviousStatus.ToString(), NewStatus = a.NewStatus.ToString(), Actor = db.Users.Where(u => u.Id == a.ActorId).Select(u => u.Email).FirstOrDefault() }).ToListAsync();
        var name = await db.Users.Where(u => u.Id == task.AssigneeId).Select(u => db.Employees.Where(e => e.Id == u.EmployeeId).Select(e => e.FullName).FirstOrDefault() ?? u.Email).SingleOrDefaultAsync();
        return Results.Ok(new { task.Id, task.Title, task.Description, task.Responsibility, task.DueDate, Priority = (int)task.Priority, Status = task.Status.ToString(), task.AssigneeId, AssigneeName = name, task.TeamId, task.SubtaskId, CanManage = db.IsAdmin || task.OwnerId == db.ActorId, Activity = activity });
    }
    static async Task<IResult> SaveTask(TaskInput input, HrDbContext db, ModuleAccess access, OperationsNotices notices)
    {
        if (!await Allowed(access, "tasks")) return Results.Forbid();
        if (!Title(input.Title) || input.Description?.Length > 2500 || input.Responsibility?.Length > 1000 || !Enum.IsDefined((TaskPriority)input.Priority) || input.DueDate?.Year < 1900) return Error("Enter a title, valid priority and date, and a description up to 2,500 characters.");
        var ids = input.AssigneeIds?.Distinct().ToArray() ?? [];
        if (!await Assignees(db, "tasks", ids)) return Error("Select up to 100 eligible employees within your permission scope.");
        var existing = input.Id == 0 ? null : await db.WorkTasks.SingleOrDefaultAsync(t => t.Id == input.Id && (db.IsAdmin || t.OwnerId == db.ActorId));
        if (input.Id != 0 && existing == null) return Results.NotFound();
        if (existing != null && (ids.Length != 1 || existing.Status == TaskProgress.Cancelled)) return Error("Edit one active assignment at a time.");
        if (existing?.TeamId is int team && (!await db.WorkTaskTeamMembers.AnyAsync(m => m.TeamId == team && m.UserId == ids[0]) || string.IsNullOrWhiteSpace(input.Responsibility))) return Error("Choose a team member and describe their responsibility.");
        if (existing?.SubtaskId is int sub && await db.WorkTasks.AnyAsync(t => t.SubtaskId == sub && t.AssigneeId == ids[0] && t.Id != existing.Id && t.Status != TaskProgress.Cancelled)) return Error("This employee already has the subtask.");
        var parentTitle = existing?.TeamId is int parent ? await db.WorkTaskTeams.Where(t => t.Id == parent).Select(t => t.Title).SingleAsync() : null;
        var notify = new List<WorkTask>();
        foreach (var id in ids)
        {
            var task = existing ?? new WorkTask { OwnerId = db.ActorId!, AssigneeId = id };
            var previous = task.AssigneeId;
            task.Title = parentTitle ?? input.Title.Trim(); task.Description = input.Description?.Trim() ?? ""; task.DueDate = input.DueDate?.Date;
            task.Priority = (TaskPriority)input.Priority; task.AssigneeId = id; task.UpdatedUtc = DateTime.UtcNow;
            if (task.TeamId != null) task.Responsibility = input.Responsibility!.Trim();
            if (existing == null) db.WorkTasks.Add(task);
            db.TaskActivities.Add(new() { Task = task, ActorId = db.ActorId!, Action = existing == null ? "Created" : "Edited", ChangedFields = existing == null ? null : "Title, description, due date, priority or assignee", RecordedUtc = DateTime.UtcNow });
            if (existing == null || previous != id) notify.Add(task);
        }
        await notices.AddTaskAssignmentsAsync(notify);
        db.AuditLogs.Add(new() { Actor = db.ActorId!, Action = input.Id == 0 ? $"Created {ids.Length} mobile task assignments" : $"Edited mobile task {input.Id}" });
        await db.SaveChangesAsync(); return Results.Ok(new { message = "Task saved." });
    }
    static async Task<IResult> CancelTask(int id, HrDbContext db, ModuleAccess access, OperationsNotices notices)
    {
        if (!await Allowed(access, "tasks")) return Results.Forbid();
        var task = await db.WorkTasks.SingleOrDefaultAsync(t => t.Id == id && (db.IsAdmin || t.OwnerId == db.ActorId));
        if (task == null) return Results.NotFound();
        if (task.Status == TaskProgress.Cancelled) return Results.Ok(new { message = "Already cancelled." });
        var previous = task.Status; task.Status = TaskProgress.Cancelled; task.UpdatedUtc = DateTime.UtcNow;
        db.TaskActivities.Add(new() { TaskId = id, ActorId = db.ActorId!, Action = "Cancelled", PreviousStatus = previous, NewStatus = task.Status, RecordedUtc = DateTime.UtcNow });
        await notices.AddAsync([task.AssigneeId], OperationsNoticeKind.TaskProgress, task: task);
        db.AuditLogs.Add(new() { Actor = db.ActorId!, Action = $"Cancelled mobile task {id}" }); await db.SaveChangesAsync();
        return Results.Ok(new { message = "Task cancelled." });
    }
    static async Task<IResult> SaveMeeting(MeetingInput input, HrDbContext db, ModuleAccess access, OperationsNotices notices)
    {
        if (!await Allowed(access, "meetings")) return Results.Forbid();
        var meeting = input.Id == 0 ? null : await db.WorkMeetings.Include(m => m.Participants).SingleOrDefaultAsync(m => m.Id == input.Id && (db.IsAdmin || m.OwnerId == db.ActorId));
        if (input.Id != 0 && meeting == null) return Results.NotFound();
        if (input.Id == 0 && !db.IsStaff) return Results.Forbid();
        if (meeting != null && (meeting.Cancelled || meeting.EndedUtc != null || meeting.EndsUtc <= DateTime.UtcNow)) return Error("This meeting is closed.");
        if (!Title(input.Title) || input.Agenda?.Length > 2500 || input.Ends <= input.Starts || input.Starts.Year < 1900 || input.Ends.Year >= 9999 || input.Mode is not ("BuiltIn" or "InPerson" or "External") || input.Location?.Length > 250 || input.JoinUrl?.Length > 2048) return Error("Enter a title, agenda and valid start/end times.");
        if (input.Mode == "InPerson" && string.IsNullOrWhiteSpace(input.Location)) return Error("Enter the meeting venue.");
        if (input.Mode == "External" && !OperationsPolicy.IsJoinUrl(input.JoinUrl)) return Error("Enter a valid Teams or Google Meet link.");
        var guests = input.Guests?.Distinct().Where(id => id != (meeting?.OwnerId ?? db.ActorId)).ToArray() ?? [];
        if (guests.Length > 100 || guests.Any(id => string.IsNullOrEmpty(id) || id.Length > 255) || await OperationsPolicy.EligiblePeople(db, "meetings").CountAsync(u => guests.Contains(u.Id)) != guests.Length) return Error("Choose up to 100 eligible participants.");
        meeting ??= new WorkMeeting { OwnerId = db.ActorId! };
        var previous = meeting.Participants.Select(p => p.UserId).ToArray();
        meeting.Title = input.Title.Trim(); meeting.Agenda = input.Agenda?.Trim() ?? ""; meeting.StartsUtc = input.Starts.UtcDateTime; meeting.EndsUtc = input.Ends.UtcDateTime;
        meeting.Location = input.Mode == "InPerson" ? input.Location!.Trim() : null; meeting.JoinUrl = input.Mode == "External" ? input.JoinUrl!.Trim() : "";
        meeting.Participants.RemoveAll(p => !guests.Contains(p.UserId));
        foreach (var id in guests.Except(meeting.Participants.Select(p => p.UserId))) meeting.Participants.Add(new() { UserId = id });
        if (input.Id == 0) db.WorkMeetings.Add(meeting);
        await notices.AddAsync(guests.Except(previous), OperationsNoticeKind.MeetingInvitation, meeting: meeting);
        if (input.Id != 0) await notices.AddAsync(guests.Intersect(previous), OperationsNoticeKind.MeetingUpdated, meeting: meeting);
        db.AuditLogs.Add(new() { Actor = db.ActorId!, Action = input.Id == 0 ? "Scheduled meeting from mobile" : $"Edited meeting {input.Id} from mobile" });
        await db.SaveChangesAsync(); return Results.Ok(new { message = "Meeting saved.", meeting.Id });
    }
    static async Task<IResult> CancelMeeting(int id, HrDbContext db, ModuleAccess access, OperationsNotices notices, MeetingRooms rooms)
    {
        if (!await Allowed(access, "meetings")) return Results.Forbid();
        var m = await db.WorkMeetings.SingleOrDefaultAsync(m => m.Id == id && (db.IsAdmin || m.OwnerId == db.ActorId));
        if (m == null) return Results.NotFound();
        if (m.EndedUtc != null || m.EndsUtc <= DateTime.UtcNow) return Error("This meeting has ended.");
        if (!m.Cancelled)
        {
            m.Cancelled = true; var ids = await db.Set<MeetingParticipant>().Where(p => p.MeetingId == id).Select(p => p.UserId).ToArrayAsync();
            await notices.AddAsync(ids, OperationsNoticeKind.MeetingCancelled, meeting: m);
            db.AuditLogs.Add(new() { Actor = db.ActorId!, Action = $"Cancelled meeting {id} from mobile" });
            await db.SaveChangesAsync(); await rooms.BroadcastEndedAsync(id);
        }
        return Results.Ok(new { message = "Meeting cancelled." });
    }
    static async Task<IResult> PublishMinutes(int id, MinutesInput input, HrDbContext db, ModuleAccess access, OperationsNotices notices)
    {
        if (!await Allowed(access, "meetings")) return Results.Forbid();
        if (input.Minutes?.Length > 12000) return Error("Minutes must be up to 12,000 characters.");
        var m = await db.WorkMeetings.SingleOrDefaultAsync(m => m.Id == id && (db.IsAdmin || m.OwnerId == db.ActorId));
        if (m == null) return Results.NotFound();
        m.MinutesDraft = input.Minutes?.Trim(); m.PublishedMinutes = m.MinutesDraft; m.MinutesPublishedUtc = DateTime.UtcNow;
        var recipients = await db.Set<MeetingParticipant>().Where(p => p.MeetingId == id).Select(p => p.UserId).ToArrayAsync();
        await notices.AddAsync(recipients, OperationsNoticeKind.MeetingMinutesPublished, meeting: m);
        db.AuditLogs.Add(new() { Actor = db.ActorId!, Action = $"Published meeting {id} minutes from mobile" }); await db.SaveChangesAsync();
        return Results.Ok(new { message = "Minutes published." });
    }
    static async Task<IResult> Teams(HrDbContext db, ModuleAccess access)
    {
        if (!await Allowed(access, "tasks")) return Results.Forbid();
        var teams = await db.WorkTaskTeams.AsNoTracking().Include(t => t.Members)
            .Where(t => db.IsAdmin || t.OwnerId == db.ActorId || t.Members.Any(m => m.UserId == db.ActorId)).OrderByDescending(t => t.Id).Take(500).ToListAsync();
        var ids = teams.Select(t => t.Id).ToArray();
        var subtasks = await db.WorkSubtasks.AsNoTracking().Where(s => ids.Contains(s.TeamId)).ToListAsync();
        var tasks = await db.WorkTasks.AsNoTracking().Where(t => t.TeamId.HasValue && ids.Contains(t.TeamId.Value) && t.Status != TaskProgress.Cancelled).ToListAsync();
        var users = teams.SelectMany(t => t.Members.Select(m => m.UserId)).Concat(tasks.Select(t => t.AssigneeId)).Distinct().ToArray();
        var names = await db.Users.Where(u => users.Contains(u.Id)).ToDictionaryAsync(u => u.Id, u => u.Email ?? u.UserName ?? "Employee");
        return Results.Ok(teams.Select(t => new {
            t.Id, t.Title, t.Brief, CanManage = db.IsAdmin || t.OwnerId == db.ActorId,
            Members = t.Members.Select(m => new { m.UserId, Name = names.GetValueOrDefault(m.UserId, "Employee") }),
            Subtasks = subtasks.Where(s => s.TeamId == t.Id).Select(s => new { s.Id, s.Responsibility,
                Assignments = tasks.Where(w => w.SubtaskId == s.Id).Select(w => new { w.Id, w.Title, w.Description, w.Responsibility, w.AssigneeId, w.DueDate, w.TeamId, Status = w.Status.ToString(), CanManage = db.IsAdmin || w.OwnerId == db.ActorId, AssigneeName = names.GetValueOrDefault(w.AssigneeId, "Employee") }) }),
            Total = tasks.Count(w => w.TeamId == t.Id), Done = tasks.Count(w => w.TeamId == t.Id && w.Status == TaskProgress.Done)
        }));
    }
    static async Task<IResult> CreateTeam(TeamInput input, HrDbContext db, ModuleAccess access)
    {
        if (!await Allowed(access, "tasks") || !db.IsStaff) return Results.Forbid();
        var members = input.Members?.Distinct().ToArray() ?? [];
        if (!Title(input.Title) || input.Description?.Length > 2500 || !await Assignees(db, "tasks", members)) return Error("Enter a title and select eligible team members.");
        var team = new WorkTaskTeam { Title = input.Title.Trim(), Brief = input.Description?.Trim(), OwnerId = db.ActorId!, Members = members.Select(id => new WorkTaskTeamMember { UserId = id }).ToList() };
        db.WorkTaskTeams.Add(team);
        foreach (var member in members) OperationsNotices.AddUpdate(db, member, OperationsNoticeKind.TeamAssigned, team.Title);
        db.AuditLogs.Add(new() { Actor = db.ActorId!, Action = "Created team task from mobile" }); await db.SaveChangesAsync();
        return Results.Ok(new { message = "Team task created.", team.Id });
    }
    static async Task<IResult> Subtask(int id, SubtaskInput input, HrDbContext db, ModuleAccess access, OperationsNotices notices)
    {
        if (!await Allowed(access, "tasks")) return Results.Forbid();
        var team = await db.WorkTaskTeams.Include(t => t.Members).SingleOrDefaultAsync(t => t.Id == id && (db.IsAdmin || t.OwnerId == db.ActorId));
        if (team == null) return Results.NotFound();
        var ids = input.AssigneeIds?.Distinct().ToArray() ?? [];
        if (!Title(input.Title) || input.Description?.Length > 2500 || !Enum.IsDefined((TaskPriority)input.Priority) || ids.Length == 0 || ids.Any(i => !team.Members.Any(m => m.UserId == i)) || !await Assignees(db, "tasks", ids)) return Error("Describe the responsibility and choose team members.");
        var sub = new WorkSubtask { TeamId = id, Responsibility = input.Title.Trim() }; db.WorkSubtasks.Add(sub);
        var tasks = ids.Select(person => new WorkTask { TeamId = id, Subtask = sub, Title = team.Title, Responsibility = sub.Responsibility, Description = input.Description?.Trim() ?? "", OwnerId = team.OwnerId, AssigneeId = person, DueDate = input.DueDate?.Date, Priority = (TaskPriority)input.Priority, UpdatedUtc = DateTime.UtcNow }).ToList();
        db.WorkTasks.AddRange(tasks);
        foreach (var task in tasks) db.TaskActivities.Add(new() { Task = task, ActorId = db.ActorId!, Action = "Created", RecordedUtc = DateTime.UtcNow });
        await notices.AddTaskAssignmentsAsync(tasks); db.AuditLogs.Add(new() { Actor = db.ActorId!, Action = $"Added team {id} subtask from mobile" });
        await db.SaveChangesAsync(); return Results.Ok(new { message = "Subtask assigned." });
    }
}
