using HrSystemDhg.Web.Data;
using HrSystemDhg.Web.Models;
using Microsoft.EntityFrameworkCore;

namespace HrSystemDhg.Web.Services;
public class AttendanceService(HrDbContext db, CalendarService calendar)
{
    public async Task RebuildAsync(int employeeId, DateTime date)
    {
        var e = await db.Employees.SingleAsync(x => x.Id == employeeId);
        var rule = await calendar.RuleAsync(employeeId, date);
        var window = CalendarService.Window(rule, date);
        var punches = await db.ZkPunchs.Where(x => x.Pin == e.ZkPin && x.PunchTime >= window.Start.AddHours(-3) && x.PunchTime <= window.End.AddHours(4)).OrderBy(x => x.PunchTime).ToListAsync();
        var day = await db.AttendanceDays.SingleOrDefaultAsync(x => x.EmployeeId == employeeId && x.Date == date.Date);
        if (day?.IsCorrected == true)
            return;
        if (day == null)
        {
            day = new()
            {
                EmployeeId = employeeId,
                Date = date.Date
            };
            db.AttendanceDays.Add(day);
        }

        day.ExpectedHours = rule.DailyHours;
        day.FirstIn = punches.FirstOrDefault()?.PunchTime;
        day.LastOut = punches.Count > 1 ? punches.Last().PunchTime : null;
        day.WorkedHours = day.LastOut.HasValue ? (decimal)Math.Max(0, (day.LastOut.Value - day.FirstIn!.Value).TotalHours - rule.BreakMinutes / 60d) : 0;
        day.LateMinutes = day.FirstIn.HasValue ? Math.Max(0, (int)(day.FirstIn.Value - window.Start).TotalMinutes - rule.GraceMinutes) : 0;
        day.EarlyMinutes = day.LastOut.HasValue ? Math.Max(0, (int)(window.End - day.LastOut.Value).TotalMinutes) : 0;
        var approved = await db.LeaveRequests.Where(x => x.EmployeeId == employeeId && x.Status == "Approved" && x.From <= date && x.To >= date).ToListAsync();
        foreach (var r in approved)
        {
            var range = CalendarService.LeaveWindow(r, rule, date);
            if (day.FirstIn.HasValue && day.LastOut.HasValue)
            {
                var left = day.FirstIn.Value > range.Start ? day.FirstIn.Value : range.Start;
                var right = day.LastOut.Value < range.End ? day.LastOut.Value : range.End;
                day.WorkedHours = Math.Max(0, day.WorkedHours - (decimal)Math.Max(0, (right - left).TotalHours));
            }
        }

        var fullLeave = approved.Any(x => x.StartTime == null);
        if (fullLeave)
            day.LateMinutes = day.EarlyMinutes = 0;
        foreach (var r in approved.Where(x => x.StartTime != null))
        {
            var leaveWindow = CalendarService.LeaveWindow(r, rule, date);
            var start = leaveWindow.Start;
            var end = leaveWindow.End;
            if (start <= window.Start && day.FirstIn.HasValue)
                day.LateMinutes = Math.Max(0, (int)(day.FirstIn.Value - (end > window.Start ? end : window.Start)).TotalMinutes - rule.GraceMinutes);
            if (end >= window.End && day.LastOut.HasValue)
                day.EarlyMinutes = Math.Max(0, (int)(start - day.LastOut.Value).TotalMinutes);
        }

        day.Status = await calendar.HolidayAsync(employeeId, date) ? "Holiday" : !CalendarService.Weekday(rule, date) ? "Weekend" : fullLeave ? "On leave" : approved.Count > 0 ? "On leave (partial)" : punches.Count == 0 ? "Absent" : punches.Count == 1 ? "Incomplete punch" : day.LateMinutes > 0 ? "Late" : "Present";
        await db.SaveChangesAsync();
    }

    public async Task ImportAsync(int deviceId, string pin, DateTime localTime, string actor, string reason)
    {
        if (string.IsNullOrWhiteSpace(reason))
            throw new InvalidOperationException("An import reason is required.");
        if (!await db.ZkDevices.AnyAsync(x => x.Id == deviceId))
            throw new InvalidOperationException("Configure this device first.");
        var e = await db.Employees.SingleOrDefaultAsync(x => x.ZkPin == pin);
        if (e == null && db.Scoped)
            throw new InvalidOperationException("Map the PIN to an employee in your branch before import.");
        if (!await db.ZkPunchs.AnyAsync(x => x.DeviceId == deviceId && x.Pin == pin && x.PunchTime == localTime))
        {
            db.ZkPunchs.Add(new() { DeviceId = deviceId, Pin = pin, PunchTime = localTime });
            db.AuditLogs.Add(new() { Actor = actor, Action = $"Manual punch import: device {deviceId}; PIN {pin}; {reason}" });
            await db.SaveChangesAsync();
        }

        if (e != null)
        {
            var date = localTime.Date;
            var rule = await calendar.RuleAsync(e.Id, date);
            if (CalendarService.Window(rule, date.AddDays(-1)).End > date && localTime < date.AddHours(rule.StartHour - 3))
                date = date.AddDays(-1);
            await RebuildAsync(e.Id, date);
        }
    }
}
