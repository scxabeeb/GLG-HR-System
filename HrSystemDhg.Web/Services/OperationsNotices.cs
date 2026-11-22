using System.Text.RegularExpressions;
using HrSystemDhg.Web.Data;
using HrSystemDhg.Web.Models;
using Microsoft.EntityFrameworkCore;

namespace HrSystemDhg.Web.Services;

public sealed class OperationsNotices(HrDbContext db, IConfiguration config)
{
    public IQueryable<OperationsNotification> For(string userId) =>
        db.OperationsNotifications.Where(n => n.RecipientId == userId);

    public static IQueryable<OperationsNotification> Visible(IQueryable<OperationsNotification> query, HashSet<string> blocked) =>
        query.Where(n =>
            (!blocked.Contains("tasks") || (n.TaskId == null && n.Kind != OperationsNoticeKind.TeamAssigned)) &&
            (!blocked.Contains("meetings") || n.MeetingId == null) &&
            (!blocked.Contains("fleet") || n.FleetTripId == null) &&
            (!blocked.Contains("assets") || n.Kind != OperationsNoticeKind.AssetAssigned) &&
            (!blocked.Contains("leave") || n.Kind != OperationsNoticeKind.LeaveDecided) &&
            (!blocked.Contains("payroll") || n.Kind != OperationsNoticeKind.PayslipPublished) &&
            (!blocked.Contains("attendance") || n.Kind != OperationsNoticeKind.AttendanceCorrectionDecided));

    public static async Task AddEmployeeUpdatesAsync(HrDbContext db, IEnumerable<int> employees, OperationsNoticeKind kind, string title)
    {
        var ids = employees.Distinct().ToArray();
        if (ids.Length == 0) return;
        var recipients = await db.Users.Where(u => u.IsEnabled && u.EmployeeId.HasValue && ids.Contains(u.EmployeeId.Value))
            .Select(u => u.Id).ToListAsync();
        foreach (var id in recipients)
            AddUpdate(db, id, kind, title);
    }

    public static void AddUpdate(HrDbContext db, string recipient, OperationsNoticeKind kind, string title) =>
        db.OperationsNotifications.Add(new()
        {
            RecipientId = recipient, Kind = kind, Title = title[..Math.Min(title.Length, 160)],
            CreatedUtc = DateTime.UtcNow
        });

    public static string? NormalizePhone(string? input)
    {
        if (string.IsNullOrWhiteSpace(input)) return null;
        var cleaned = Regex.Replace(input.Trim(), @"[ ()-]", "");
        if (cleaned.StartsWith("00")) cleaned = "+" + cleaned[2..];
        return Regex.IsMatch(cleaned, @"\A\+[1-9][0-9]{7,14}\z") ? cleaned : null;
    }

    public async Task<string?> RegisteredNumberAsync(string userId, CancellationToken ct = default) =>
        NormalizePhone(await (from user in db.Users
                               join employee in db.Employees.IgnoreQueryFilters() on user.EmployeeId equals (int?)employee.Id
                               where user.Id == userId && user.IsEnabled
                               select employee.Phone).SingleOrDefaultAsync(ct));

    public async Task AddFleetTripAsync(FleetTrip trip, string actor)
    {
        var people = await db.Users.Where(u => u.IsEnabled && u.Id != actor &&
            (u.Id == trip.RequesterId || (trip.DriverId.HasValue && u.EmployeeId == trip.DriverId)))
            .Select(u => u.Id).ToListAsync();
        var title = $"{trip.Status}: {trip.Destination}";
        foreach (var userId in people)
            db.OperationsNotifications.Add(new()
            {
                RecipientId = userId, FleetTrip = trip, Kind = OperationsNoticeKind.FleetTripUpdated,
                Title = title[..Math.Min(160, title.Length)],
                CreatedUtc = DateTime.UtcNow
            });
    }

    public async Task AddTaskAssignmentsAsync(IEnumerable<WorkTask> assignments)
    {
        var maySend = WhatsAppSettings.From(config) != null;
        foreach (var batch in assignments.Chunk(100))
        {
            var ids = batch.Select(t => t.AssigneeId).Distinct().ToArray();
            var active = (await db.Users.Where(u => u.IsEnabled && ids.Contains(u.Id)).Select(u => u.Id).ToListAsync()).ToHashSet();
            var optedIn = maySend
                ? (await db.WhatsAppPreferences.Where(p => ids.Contains(p.UserId)).Select(p => p.UserId).ToListAsync()).ToHashSet()
                : new HashSet<string>();
            foreach (var task in batch.Where(t => active.Contains(t.AssigneeId)))
                db.OperationsNotifications.Add(new()
                {
                    RecipientId = task.AssigneeId, Kind = OperationsNoticeKind.TaskAssigned,
                    Title = task.Title, Task = task, CreatedUtc = DateTime.UtcNow,
                    WhatsApp = optedIn.Contains(task.AssigneeId) ? WhatsAppDelivery.Queued : WhatsAppDelivery.InAppOnly
                });
        }
    }

    public async Task AddAsync(IEnumerable<string> recipients, OperationsNoticeKind kind, WorkTask? task = null, WorkMeeting? meeting = null)
    {
        var ids = recipients.Distinct().ToArray();
        if (ids.Length == 0) return;
        var active = await db.Users.Where(u => u.IsEnabled && ids.Contains(u.Id)).Select(u => u.Id).ToListAsync();
        var maySend = WhatsAppSettings.From(config) != null &&
            kind is OperationsNoticeKind.TaskAssigned or OperationsNoticeKind.MeetingInvitation;
        var optedIn = maySend
            ? await db.WhatsAppPreferences.Where(p => ids.Contains(p.UserId)).Select(p => p.UserId).ToListAsync()
            : [];
        foreach (var id in active)
            db.OperationsNotifications.Add(new()
            {
                RecipientId = id, Kind = kind, Title = task?.Title ?? meeting!.Title, Task = task, Meeting = meeting,
                Progress = kind == OperationsNoticeKind.TaskProgress ? task?.Status : null,
                CreatedUtc = DateTime.UtcNow, WhatsApp = optedIn.Contains(id) ? WhatsAppDelivery.Queued : WhatsAppDelivery.InAppOnly
            });
    }
}
