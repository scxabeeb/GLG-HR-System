using HrSystemDhg.Web.Data;
using HrSystemDhg.Web.Models;
using Microsoft.EntityFrameworkCore;

namespace HrSystemDhg.Web.Services;
public class AttendanceService(HrDbContext db, CalendarService calendar, TimeProvider? timeProvider = null)
{
    public const int CalculationVersion = 3;
    public static readonly DateTime ActualMinutesEffectiveFrom = new(2026, 11, 1);
    public DateTime LocalNow => TimeZoneInfo.ConvertTime((timeProvider ?? TimeProvider.System).GetUtcNow(), TimeZoneInfo.FindSystemTimeZoneById("Africa/Mogadishu")).DateTime;
    public bool DayClosed(WorkRule rule, DateTime date) => LocalNow >= AttendanceMinutes.CheckoutDeadline(rule, date);

    public async Task RebuildAsync(int employeeId, DateTime date)
    {
        var e = await db.Employees.SingleAsync(x => x.Id == employeeId);
        var attendanceStart = AttendanceEligibility.StartDate(e);
        if (date.Date < attendanceStart)
        {
            var exempt = await db.AttendanceDays.SingleOrDefaultAsync(x=>x.EmployeeId==employeeId && x.Date==date.Date);
            if (exempt==null) { exempt=new(){EmployeeId=employeeId,Date=date.Date};db.AttendanceDays.Add(exempt); }
            if (!exempt.IsCorrected)
            {
                var records=await db.ZkPunchs.Where(p=>p.Pin==e.ZkPin && p.PunchTime>=date.Date && p.PunchTime<date.Date.AddDays(1)).OrderBy(p=>p.PunchTime).ToListAsync();
                exempt.FirstIn=records.FirstOrDefault()?.PunchTime;exempt.LastOut=records.Count>1?records.Last().PunchTime:null;
            }
            exempt.WorkedHours=AttendanceMinutes.Worked(exempt.FirstIn,exempt.LastOut)/60m;
            exempt.ExpectedHours=0;exempt.MissingMinutes=exempt.LateMinutes=exempt.EarlyMinutes=0;
            exempt.Status=date.Date==AttendanceEligibility.RegistrationDate(e)?"Registration day":"Not yet due";
            exempt.StatusMessage=$"Attendance starts {attendanceStart:dd MMM yyyy}. No absence or attendance deduction before this date.";
            exempt.CalculationVersion=CalculationVersion;await db.SaveChangesAsync();return;
        }
        var rule = await calendar.RuleAsync(employeeId, date);
        var window = CalendarService.Window(rule, date);
        var from = rule.UsePunchWindows ? date.Date : window.Start.AddHours(-3);
        var to = rule.UsePunchWindows ? date.Date.AddDays(1).AddTicks(-1) : window.End.AddHours(4);
        var punches = await db.ZkPunchs.Where(x => x.Pin == e.ZkPin && x.PunchTime >= from && x.PunchTime <= to).OrderBy(x => x.PunchTime).ToListAsync();
        var day = await db.AttendanceDays.SingleOrDefaultAsync(x => x.EmployeeId == employeeId && x.Date == date.Date);
        if (day?.IsCorrected == true)
        {
            var leave = await db.LeaveRequests.Where(r => r.EmployeeId == employeeId && r.Status == "Approved" && r.From <= date && r.To >= date).ToListAsync();
            day.WorkedHours = AttendanceMinutes.Worked(day.FirstIn,day.LastOut,rule.BreakMinutes) / 60m;
            if(day.FirstIn.HasValue && day.LastOut.HasValue)
                foreach(var approvedLeave in leave)
                {
                    var range = CalendarService.LeaveWindow(approvedLeave,rule,date);
                    var left = day.FirstIn.Value > range.Start ? day.FirstIn.Value : range.Start;
                    var right = day.LastOut.Value < range.End ? day.LastOut.Value : range.End;
                    day.WorkedHours = Math.Max(0,day.WorkedHours - (decimal)Math.Max(0,(right-left).TotalHours));
                }
            var holiday = await calendar.HolidayAsync(employeeId, date);
            var weekend = !CalendarService.Weekday(rule, date);
            day.ExpectedHours = rule.DailyHours;
            if (rule.UsePunchWindows)
            {
                day.MissingMinutes = AttendanceMinutes.Missing(rule, date, day.FirstIn, day.LastOut, leave);
                day.LateMinutes = day.EarlyMinutes = 0;
                day.Status = holiday ? "Holiday" : weekend ? "Weekend" : leave.Any(r => r.StartTime == null) ? "On leave" :
                    day.FirstIn == null || day.LastOut == null || !AttendanceMinutes.CheckIn(rule, day.FirstIn.Value) || !AttendanceMinutes.CheckOut(rule, day.LastOut.Value) ? "Incomplete punch" : leave.Count > 0 ? "On leave (partial)" : day.MissingMinutes > 0 ? "Short hours" : "Present";
            }
            else if (!holiday && !weekend && leave.Any(r => r.StartTime == null)) { day.Status = "On leave"; day.LateMinutes = day.EarlyMinutes = 0; }
            if (AttendanceMinutes.CreditsFullDay(rule))
            {
                day.WorkedHours = AttendanceMinutes.CreditedHours(rule, date, day.FirstIn, day.LastOut, leave);
                day.StatusMessage = AttendanceMinutes.PairMessage(rule, day.FirstIn, day.LastOut);
            }
            else day.StatusMessage = null;
            if (day.Status is "Holiday" or "Weekend" or "On leave") day.MissingMinutes = 0;
            if (day.Status is "Holiday" or "Weekend")
            {
                day.ExpectedHours = 0;
                if (AttendanceMinutes.CreditsFullDay(rule)) day.WorkedHours = AttendanceMinutes.Worked(day.FirstIn, day.LastOut, rule.BreakMinutes) / 60m;
            }
            FinalizeSummary(rule, day);
            await db.SaveChangesAsync();
            return;
        }
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
        day.FirstIn = rule.UsePunchWindows ? punches.FirstOrDefault(p => AttendanceMinutes.CheckIn(rule, p.PunchTime))?.PunchTime : punches.FirstOrDefault()?.PunchTime;
        day.LastOut = rule.UsePunchWindows ? punches.LastOrDefault(p => AttendanceMinutes.CheckOut(rule, p.PunchTime))?.PunchTime : punches.Count > 1 ? punches.Last().PunchTime : null;
        day.WorkedHours = AttendanceMinutes.Worked(day.FirstIn, day.LastOut, rule.BreakMinutes) / 60m;
        day.LateMinutes = !rule.UsePunchWindows && day.FirstIn.HasValue ? Math.Max(0, (int)(day.FirstIn.Value - window.Start).TotalMinutes - rule.GraceMinutes) : 0;
        day.EarlyMinutes = !rule.UsePunchWindows && day.LastOut.HasValue ? Math.Max(0, (int)(window.End - day.LastOut.Value).TotalMinutes) : 0;
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

        if (AttendanceMinutes.CreditsFullDay(rule))
        {
            day.WorkedHours = AttendanceMinutes.CreditedHours(rule, date, day.FirstIn, day.LastOut, approved);
            day.StatusMessage = AttendanceMinutes.PairMessage(rule, day.FirstIn, day.LastOut);
        }
        else day.StatusMessage = null;
        var fullLeave = approved.Any(x => x.StartTime == null);
        if (fullLeave)
            day.LateMinutes = day.EarlyMinutes = 0;
        foreach (var r in approved.Where(x => x.StartTime != null && !rule.UsePunchWindows))
        {
            var leaveWindow = CalendarService.LeaveWindow(r, rule, date);
            var start = leaveWindow.Start;
            var end = leaveWindow.End;
            if (start <= window.Start && day.FirstIn.HasValue)
                day.LateMinutes = Math.Max(0, (int)(day.FirstIn.Value - (end > window.Start ? end : window.Start)).TotalMinutes - rule.GraceMinutes);
            if (end >= window.End && day.LastOut.HasValue)
                day.EarlyMinutes = Math.Max(0, (int)(start - day.LastOut.Value).TotalMinutes);
        }

        day.MissingMinutes = rule.UsePunchWindows ? AttendanceMinutes.Missing(rule, date, day.FirstIn, day.LastOut, approved) : Math.Max(0, day.LateMinutes + day.EarlyMinutes);
        day.Status = await calendar.HolidayAsync(employeeId, date) ? "Holiday" : !CalendarService.Weekday(rule, date) ? "Weekend" : fullLeave ? "On leave" : approved.Count > 0 ? "On leave (partial)" : punches.Count == 0 ? "Absent" : (day.FirstIn == null || day.LastOut == null) ? "Incomplete punch" : rule.UsePunchWindows && day.MissingMinutes > 0 ? "Short hours" : !rule.UsePunchWindows && day.LateMinutes > 0 ? "Late" : "Present";

        if (day.Status is "Holiday" or "Weekend" or "On leave") day.MissingMinutes = 0;
        if (day.Status is "Holiday" or "Weekend")
        {
            day.ExpectedHours = 0;
            if (AttendanceMinutes.CreditsFullDay(rule)) day.WorkedHours = AttendanceMinutes.Worked(day.FirstIn, day.LastOut, rule.BreakMinutes) / 60m;
        }
        FinalizeSummary(rule, day);
        await db.SaveChangesAsync();
    }

    private void FinalizeSummary(WorkRule rule, AttendanceDay day)
    {
        AttendanceMinutes.ApplyClosingPhase(rule, day, LocalNow);
        if (rule.UsePunchWindows && !AttendanceMinutes.CreditsFullDay(rule) && day.ExpectedHours > 0 && day.Status != "On leave" && AttendanceMinutes.ValidPair(rule, day.FirstIn, day.LastOut))
            day.StatusMessage = $"{Math.Round(day.WorkedHours * 60m):0} worked min / {AttendanceMinutes.Required(rule)} required min; {day.MissingMinutes} unexcused min short.";
        day.CalculationVersion = CalculationVersion;
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
            await RebuildAsync(e.Id, await WorkDateAsync(e.Id, localTime));
        }
    }
    public async Task<DateTime> WorkDateAsync(int employeeId, DateTime localTime)
    {
        var date = localTime.Date;
        var employee=await db.Employees.SingleAsync(e=>e.Id==employeeId);
        if (date<AttendanceEligibility.StartDate(employee)) return date;
        var rule = await calendar.RuleAsync(employeeId, date);
        if (CalendarService.Window(rule, date.AddDays(-1)).End > date && localTime < date.AddHours(rule.StartHour - 3))
            return date.AddDays(-1);
        return date;
    }
}
