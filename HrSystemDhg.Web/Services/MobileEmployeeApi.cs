using HrSystemDhg.Web.Data;
using HrSystemDhg.Web.Models;
using Microsoft.EntityFrameworkCore;

namespace HrSystemDhg.Web.Services;

public static class MobileEmployeeApi
{
    private const int PageSize = 100;

    public static async Task<IResult> Read(string section, HrDbContext db, ModuleAccess access, DocumentService documents, HttpContext context, CancellationToken ct)
    {
        context.Response.Headers.CacheControl = "no-store";
        if (section == "session") return Results.Json(new { signedIn = true });
        var actor = db.ActorId!;
        var blocked = await access.BlockedAsync();
        if (section == "overview") return await Overview(db, actor, blocked, ct);
        var module = section switch
        {
            "profile" or "attendance" or "payroll" or "avatar" => "personal",
            "tasks" => "tasks",
            "meetings" => "meetings",
            _ => null
        };
        if (!ModuleAccess.Allows(blocked, module)) return Results.StatusCode(403);
        if (module == "personal" && !new[] { "Employee", "HR", "Management", "Admin" }.Any(context.User.IsInRole))
            return Results.StatusCode(403);

        if (section == "tasks")
        {
            var tasks = await db.WorkTasks.AsNoTracking().Where(t => t.AssigneeId == actor)
                .OrderByDescending(t => t.UpdatedUtc).ThenByDescending(t => t.Id).Take(PageSize)
                .Select(t => new { t.Id, t.Title, t.Responsibility, t.Description, t.Status, t.DueDate }).ToListAsync(ct);
            return Rows(tasks.Select(t => new Row(t.Responsibility ?? t.Title,
                t.DueDate.HasValue ? $"Due {t.DueDate:dd MMM yyyy}" : "No due date",
                t.Responsibility == null ? t.Description : $"{t.Title}\n{t.Description}".TrimEnd(),
                HrSystemDhg.Web.Pages.Operations.TaskReportModel.Label(t.Status),
                t.Status == TaskProgress.Cancelled ? [] : [new("Update status", $"/Operations/Tasks?focus={t.Id}")])));
        }

        if (section == "meetings")
        {
            var now = DateTime.UtcNow;
            var meetings = await OperationsPolicy.Meetings(db, actor).AsNoTracking()
                .OrderBy(m => m.Cancelled || m.EndedUtc != null || m.EndsUtc <= now)
                .ThenBy(m => m.StartsUtc >= now ? m.StartsUtc : (DateTime?)null)
                .ThenByDescending(m => m.StartsUtc).Take(PageSize)
                .Select(m => new { m.Id, m.Title, m.StartsUtc, m.EndsUtc, m.Cancelled, m.EndedUtc, m.Location, m.JoinUrl,
                    Attending = m.Participants.Count(p => p.AttendedUtc != null),
                    Invited = m.Participants.Count(),
                    IsInvited = m.Participants.Any(p => p.UserId == actor),
                    CheckedIn = m.Participants.Any(p => p.UserId == actor && p.AttendedUtc != null) }).ToListAsync(ct);
            return Rows(meetings.Select(m =>
            {
                var closed = m.Cancelled || m.EndedUtc != null || m.EndsUtc <= now;
                var details = $"/Operations/MeetingDetails?id={m.Id}";
                var actions = new List<RowAction>
                {
                    closed ? new("View agenda", details + "#meeting-agenda")
                        : new("Add agenda item", details + "#add-agenda-item")
                };
                if (!closed && m.IsInvited)
                    actions.Add(new(m.CheckedIn ? "Attendance recorded" : "I'm attending", details + "#meeting-attendance"));
                if (!closed && m.Location == null)
                {
                    if (m.JoinUrl.Length == 0 && m.StartsUtc <= now.AddMinutes(10))
                        actions.Add(new("Join meeting", $"/Operations/MeetingRoom?id={m.Id}"));
                    else if (OperationsPolicy.IsJoinUrl(m.JoinUrl))
                        actions.Add(new("Join video meeting", m.JoinUrl));
                }
                return new Row(m.Title, $"{OperationsPolicy.Local(m.StartsUtc):dd MMM yyyy, hh:mm tt} EAT",
                    $"{m.Location ?? "Online meeting"} · {m.Attending} / {m.Invited} employees checked in", m.Cancelled ? "Cancelled" : closed ? "Ended" : m.StartsUtc <= now ? "In progress" : "Upcoming", actions);
            }));
        }

        if (section == "notifications")
        {
            var notices = await OperationsNotices.Visible(db.OperationsNotifications.AsNoTracking()
                    .Where(n => n.RecipientId == actor), blocked)
                .OrderByDescending(n => n.Id).Take(PageSize)
                .Select(n => new { n.Title, n.Kind, n.CreatedUtc, n.ReadUtc }).ToListAsync(ct);
            return Rows(notices.Select(n => new Row(n.Title,
                $"{HrSystemDhg.Web.Pages.Operations.NotificationsModel.Label(n.Kind)} · {OperationsPolicy.Local(n.CreatedUtc):dd MMM yyyy, hh:mm tt} EAT",
                n.ReadUtc == null ? "Unread" : "")));
        }

        if (section == "avatar")
        {
            var photo = await (from user in db.Users
                               join document in db.EmployeeDocuments on user.EmployeeId equals document.EmployeeId
                               where user.Id == actor && document.Type == "Photo"
                                   && (document.ContentType == "image/jpeg" || document.ContentType == "image/png")
                               orderby document.Id descending
                               select new { document.StoragePath, document.ContentType }).FirstOrDefaultAsync(ct);
            if (photo == null) return Results.NotFound();
            var path = Path.Combine(documents.Root, Path.GetFileName(photo.StoragePath));
            if (!File.Exists(path)) return Results.NotFound();
            context.Response.Headers["X-Content-Type-Options"] = "nosniff";
            return Results.File(path, photo.ContentType);
        }

        if (section is not ("profile" or "attendance" or "payroll")) return Results.NotFound();
        var employeeId = await db.Users.Where(u => u.Id == actor).Select(u => u.EmployeeId).SingleOrDefaultAsync(ct);
        if (employeeId == null)
            return Results.Json(new Feed([], "Your account needs a linked employee profile."));

        if (section == "attendance")
        {
            var records = await db.AttendanceDays.AsNoTracking().Where(d => d.EmployeeId == employeeId)
                .OrderByDescending(d => d.Date).Take(PageSize)
                .Select(d => new { d.Date, d.Status, d.FirstIn, d.LastOut, d.WorkedHours, d.LateMinutes, d.MissingMinutes, d.StatusMessage }).ToListAsync(ct);
            return Rows(records.Select(d => new Row(d.Date.ToString("dd MMM yyyy"), d.Status,
                $"Time in {d.FirstIn?.ToString("hh:mm tt") ?? "—"} EAT · Time out {d.LastOut?.ToString("hh:mm tt") ?? "—"} · {d.WorkedHours:0.##} credited hours · {d.LateMinutes} min late · {d.MissingMinutes} min missing · {d.StatusMessage} · EAT (UTC+3)")));
        }

        if (section == "payroll")
        {
            var lines = await (from line in db.PayrollLines.AsNoTracking()
                               join period in db.PayrollPeriods.AsNoTracking() on line.PayrollPeriodId equals period.Id
                               where line.EmployeeId == employeeId && db.Payslips.Any(p => p.PayrollLineId == line.Id && p.IsPublished)
                               orderby period.From descending, line.Id descending
                               select new { period.From, line.Currency, line.Net, line.Gross, line.AttendanceApplied, line.UnpaidLeave })
                .Take(PageSize).ToListAsync(ct);
            return Rows(lines.Select(l => new Row(l.From.ToString("MMM yyyy"), $"Net pay · {l.Currency} {l.Net:N2}",
                $"Gross {l.Gross:N2} · Attendance deducted {l.AttendanceApplied:N2} · Unpaid leave {l.UnpaidLeave:N2}")));
        }

        var name = await db.Employees.AsNoTracking().Where(e => e.Id == employeeId)
            .Select(e => new { e.FullName, e.EmployeeNumber, e.Status }).SingleOrDefaultAsync(ct);
        return name == null ? Rows([]) : Rows([new(name.FullName, name.EmployeeNumber, name.Status)]);
    }

    private static async Task<IResult> Overview(HrDbContext db, string actor, HashSet<string> blocked, CancellationToken ct)
    {
        var today = BusinessClock.Today;
        var month = new DateTime(today.Year, today.Month, 1);
        var now = DateTime.UtcNow;
        var tasks = ModuleAccess.Allows(blocked, "tasks")
            ? await db.WorkTasks.Where(t => t.AssigneeId == actor && t.Status != TaskProgress.Cancelled)
                .GroupBy(t => t.Status).Select(g => new { Status = g.Key, Count = g.Count() })
                .ToDictionaryAsync(g => g.Status, g => g.Count, ct)
            : null;
        int? meetings = ModuleAccess.Allows(blocked, "meetings")
            ? await OperationsPolicy.Meetings(db, actor).CountAsync(m => !m.Cancelled && m.EndedUtc == null && m.EndsUtc > now, ct)
            : null;
        decimal? hours = null;
        var employeeId = await db.Users.Where(u => u.Id == actor).Select(u => u.EmployeeId).SingleOrDefaultAsync(ct);
        if (employeeId.HasValue && ModuleAccess.Allows(blocked, "personal"))
            hours = await db.AttendanceDays.Where(d => d.EmployeeId == employeeId && d.Date >= month && d.Date < today.AddDays(1))
                .SumAsync(d => (decimal?)d.WorkedHours, ct) ?? 0;
        return Results.Json(new
        {
            Tasks = tasks == null ? null : new
            {
                Open = tasks.GetValueOrDefault(TaskProgress.Open),
                InProgress = tasks.GetValueOrDefault(TaskProgress.InProgress),
                Done = tasks.GetValueOrDefault(TaskProgress.Done)
            },
            UpcomingMeetings = meetings,
            HoursThisMonth = hours
        });
    }

    private static IResult Rows(IEnumerable<Row> items) => Results.Json(new Feed(items));
    private sealed record Feed(IEnumerable<Row> Items, string? Message = null);
    private sealed record Row(string Title, string Subtitle, string Details, string? Status = null, IReadOnlyList<RowAction>? Actions = null);
    private sealed record RowAction(string Label, string Url);
}
