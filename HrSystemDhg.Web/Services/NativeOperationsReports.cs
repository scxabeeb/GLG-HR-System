using HrSystemDhg.Web.Data;
using HrSystemDhg.Web.Models;
using Microsoft.EntityFrameworkCore;

namespace HrSystemDhg.Web.Services;

public static class NativeOperationsReports
{
    public static void Map(WebApplication app)
    {
        var api = app.MapGroup("/api/mobile/v2/manage/reports").RequireAuthorization(NativeMobileApi.Policy);
        api.MapGet("/{module}", Report);
    }

    static async Task<IResult> Report(string module, DateOnly? from, DateOnly? to, string? search, string? status,
        int? page, HrDbContext db, ModuleAccess access)
    {
        var blocked = await access.BlockedAsync();
        if (!db.IsAdmin || !ModuleAccess.Allows(blocked, "reports") || !ModuleAccess.Allows(blocked, module)) return Results.Forbid();
        if (module is not ("tasks" or "meetings")) return Results.NotFound();
        if (from?.Year >= 9999 || to?.Year >= 9999 || from > to || search?.Length > 120 || page is < 1 or > 100000) return Results.BadRequest(new { message = "Enter valid report filters." });
        const int size = 100;
        var number = page ?? 1;
        var query = search?.Trim();
        var people = await db.Users.AsNoTracking().Select(u => new { u.Id,
            Name = db.Employees.Where(e => e.Id == u.EmployeeId).Select(e => e.FullName).FirstOrDefault() ?? u.Email ?? u.UserName ?? "Unknown user" }).ToDictionaryAsync(u => u.Id, u => u.Name);
        if (module == "tasks")
        {
            var tasks = db.WorkTasks.AsNoTracking();
            if (from != null) tasks = tasks.Where(t => t.DueDate >= from.Value.ToDateTime(TimeOnly.MinValue));
            if (to != null) tasks = tasks.Where(t => t.DueDate < to.Value.AddDays(1).ToDateTime(TimeOnly.MinValue));
            if (!string.IsNullOrEmpty(query))
            {
                var ids = people.Where(p => p.Value.Contains(query, StringComparison.OrdinalIgnoreCase)).Select(p => p.Key).ToArray();
                tasks = tasks.Where(t => t.Title.Contains(query) || t.Description.Contains(query) || ids.Contains(t.AssigneeId));
            }
            if (!string.IsNullOrEmpty(status))
            {
                if (!Enum.TryParse<TaskProgress>(status, out var parsed) || !Enum.IsDefined(parsed)) return Results.BadRequest(new { message = "Select a valid task status." });
                tasks = tasks.Where(t => t.Status == parsed);
            }
            var all = await tasks.OrderByDescending(t => t.Id).ToListAsync();
            var today = BusinessClock.Today;
            var employees = all.GroupBy(t => t.AssigneeId).Select(g => new {
                UserId = g.Key, Name = people.GetValueOrDefault(g.Key, "Unknown user"), Total = g.Count(),
                Open = g.Count(t => t.Status == TaskProgress.Open), InProgress = g.Count(t => t.Status == TaskProgress.InProgress),
                Done = g.Count(t => t.Status == TaskProgress.Done), Cancelled = g.Count(t => t.Status == TaskProgress.Cancelled),
                Overdue = g.Count(t => t.DueDate < today && t.Status is TaskProgress.Open or TaskProgress.InProgress),
                Progress = g.Count(t => t.Status != TaskProgress.Cancelled) == 0 ? 0 : g.Count(t => t.Status == TaskProgress.Done) * 100 / g.Count(t => t.Status != TaskProgress.Cancelled)
            }).OrderByDescending(e => e.Overdue).ThenBy(e => e.Name).ToArray();
            return Results.Ok(new { Total = all.Count, Page = number, PageSize = size,
                Summary = new { Done = all.Count(t => t.Status == TaskProgress.Done), Active = all.Count(t => t.Status is TaskProgress.Open or TaskProgress.InProgress), Overdue = employees.Sum(e => e.Overdue) },
                Employees = employees,
                Items = all.Skip((number - 1) * size).Take(size).Select(t => new { t.Id, t.Title, t.Description, t.Responsibility, t.TeamId, t.DueDate, t.UpdatedUtc,
                    AssigneeName = people.GetValueOrDefault(t.AssigneeId, "Unknown user"), Status = t.Status.ToString() }) });
        }
        var meetings = db.WorkMeetings.AsNoTracking();
        if (from != null) { var start = from.Value.ToDateTime(TimeOnly.MinValue).AddHours(-3); meetings = meetings.Where(m => m.StartsUtc >= start); }
        if (to != null) { var end = to.Value.AddDays(1).ToDateTime(TimeOnly.MinValue).AddHours(-3); meetings = meetings.Where(m => m.StartsUtc < end); }
        if (!string.IsNullOrEmpty(query)) meetings = meetings.Where(m => m.Title.Contains(query) || m.Agenda.Contains(query));
        var now = DateTime.UtcNow;
        if (!string.IsNullOrEmpty(status))
        {
            meetings = status switch {
                "Upcoming" => meetings.Where(m => !m.Cancelled && m.EndedUtc == null && m.StartsUtc > now),
                "InProgress" => meetings.Where(m => !m.Cancelled && m.EndedUtc == null && m.StartsUtc <= now && m.EndsUtc > now),
                "Completed" => meetings.Where(m => !m.Cancelled && (m.EndedUtc != null || m.EndsUtc <= now)),
                "Cancelled" => meetings.Where(m => m.Cancelled),
                _ => null! };
            if (meetings == null) return Results.BadRequest(new { message = "Select a valid meeting status." });
        }
        var total = await meetings.CountAsync();
        var completed = await meetings.CountAsync(m => !m.Cancelled && (m.EndedUtc != null || m.EndsUtc <= now));
        var rows = await meetings.OrderByDescending(m => m.StartsUtc).Skip((number - 1) * size).Take(size)
            .Select(m => new { m.Id, m.Title, m.Agenda, m.OwnerId, StartsUtc = DateTime.SpecifyKind(m.StartsUtc, DateTimeKind.Utc), m.Cancelled, m.EndedUtc, m.EndsUtc,
                HasMinutes = m.PublishedMinutes != null, Invited = m.Participants.Count(), Attended = m.Participants.Count(p => p.AttendedUtc != null) }).ToListAsync();
        return Results.Ok(new { Total = total, Page = number, PageSize = size,
            Summary = new { Completed = completed, WithMinutes = await meetings.CountAsync(m => m.PublishedMinutes != null), Cancelled = await meetings.CountAsync(m => m.Cancelled) },
            Items = rows.Select(m => new { m.Id, m.Title, m.Agenda, m.StartsUtc, m.HasMinutes, m.Invited, m.Attended,
                Organizer = people.GetValueOrDefault(m.OwnerId, "Unknown user"),
                Status = m.Cancelled ? "Cancelled" : m.EndedUtc != null || m.EndsUtc <= now ? "Completed" : m.StartsUtc <= now ? "InProgress" : "Upcoming" }) });
    }
}
