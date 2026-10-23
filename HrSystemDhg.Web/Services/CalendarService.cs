using HrSystemDhg.Web.Data;
using HrSystemDhg.Web.Models;
using Microsoft.EntityFrameworkCore;

namespace HrSystemDhg.Web.Services;
public class CalendarService(HrDbContext db)
{
    public async Task<EmployeeAssignment> AssignmentAsync(int employeeId, DateTime date) => await db.EmployeeAssignments.Where(x => x.EmployeeId == employeeId && x.EffectiveFrom <= date && (x.EffectiveTo == null || x.EffectiveTo >= date)).OrderByDescending(x => x.EffectiveFrom).FirstOrDefaultAsync() ?? throw new InvalidOperationException("No effective assignment for employee " + employeeId);
    public async Task<WorkRule> RuleAsync(int employeeId, DateTime date)
    {
        var a = await AssignmentAsync(employeeId, date);
        return await db.WorkRules.Where(x => x.EffectiveFrom <= date && (x.BranchId == null || x.BranchId == a.BranchId) && (x.DepartmentId == null || x.DepartmentId == a.DepartmentId) && (x.ShiftId == null || x.ShiftId == a.ShiftId)).OrderByDescending(x => x.ShiftId != null).ThenByDescending(x => x.DepartmentId != null).ThenByDescending(x => x.BranchId != null).ThenByDescending(x => x.EffectiveFrom).ThenByDescending(x => x.Id).FirstOrDefaultAsync() ?? throw new InvalidOperationException("Configure an effective work rule first.");
    }

    public static bool Weekday(WorkRule rule, DateTime date) => rule.WorkingWeekdays.Split(',').Contains(((int)date.DayOfWeek).ToString());
    public async Task<bool> HolidayAsync(int employeeId, DateTime date)
    {
        var a = await AssignmentAsync(employeeId, date);
        return await db.Holidays.AnyAsync(x => x.Date == date.Date && (x.BranchId == null || x.BranchId == a.BranchId));
    }

    public async Task<bool> WorkingAsync(int employeeId, DateTime date) => Weekday(await RuleAsync(employeeId, date), date) && !await HolidayAsync(employeeId, date);
    public static (DateTime Start, DateTime End) Window(WorkRule rule, DateTime date)
    {
        var start = date.Date.AddHours(rule.StartHour).AddMinutes(rule.StartMinute);
        return (start, start.AddHours((double)rule.DailyHours).AddMinutes(rule.BreakMinutes));
    }

    public static (DateTime Start, DateTime End) LeaveWindow(LeaveRequest request, WorkRule rule, DateTime date)
    {
        var shift = Window(rule, date);
        if (request.StartTime == null)
            return shift;
        var start = date.Date + request.StartTime.Value;
        if (shift.End.Date > date.Date && start < shift.Start)
            start = start.AddDays(1);
        var end = start.Date + request.EndTime!.Value;
        if (end <= start)
            end = end.AddDays(1);
        return (start, end);
    }

    public static decimal LeaveFraction(LeaveRequest request, WorkRule rule)
    {
        if (request.StartTime == null)
            return 1;
        var window = LeaveWindow(request, rule, request.From);
        return Math.Clamp((decimal)(window.End - window.Start).TotalHours / rule.DailyHours, 0, 1);
    }

    public async Task<decimal> LeaveDaysAsync(LeaveRequest request, int? year = null)
    {
        decimal days = 0;
        for (var date = request.From.Date; date <= request.To.Date; date = date.AddDays(1))
        {
            if (year != null && date.Year != year)
                continue;
            if (await WorkingAsync(request.EmployeeId, date))
                days += LeaveFraction(request, await RuleAsync(request.EmployeeId, date));
        }

        return days;
    }
}
