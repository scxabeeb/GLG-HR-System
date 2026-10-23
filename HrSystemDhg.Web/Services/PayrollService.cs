using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using HrSystemDhg.Web.Data;
using HrSystemDhg.Web.Models;
using Microsoft.EntityFrameworkCore;

namespace HrSystemDhg.Web.Services;
public static class PayMath
{
    public static (decimal applied, decimal net) Calculate(decimal gross, decimal attendance, decimal unpaid, bool apply) => (apply ? attendance : 0, Math.Max(0, gross - (apply ? attendance : 0) - unpaid));
}

public class PayrollService(HrDbContext db, CalendarService calendar, AttendanceService attendance)
{
    public async Task<string> HashAsync(PayrollPeriod p)
    {
        var data = new object[]
        {
            await db.Employees.AsNoTracking().OrderBy(x => x.Id).ToListAsync(),
            await db.EmployeeAssignments.AsNoTracking().OrderBy(x => x.Id).ToListAsync(),
            await db.SalaryVersions.AsNoTracking().OrderBy(x => x.Id).ToListAsync(),
            await db.WorkRules.AsNoTracking().OrderBy(x => x.Id).ToListAsync(),
            await db.DeductionRules.AsNoTracking().OrderBy(x => x.Id).ToListAsync(),
            await db.ZkPunchs.AsNoTracking().Where(x => x.PunchTime >= p.From.AddDays(-1) && x.PunchTime < p.To.AddDays(2)).OrderBy(x => x.Id).ToListAsync(),
            await db.LeaveRequests.AsNoTracking().Where(x => x.From <= p.To && x.To >= p.From).OrderBy(x => x.Id).ToListAsync(),
            await db.LeaveTypes.AsNoTracking().OrderBy(x => x.Id).ToListAsync(),
            await db.Holidays.AsNoTracking().OrderBy(x => x.Id).ToListAsync(),
            await db.OvertimeApprovals.AsNoTracking().Where(x => x.Date >= p.From && x.Date <= p.To).OrderBy(x => x.Id).ToListAsync(),
            await db.AttendanceDays.AsNoTracking().Where(x => x.Date >= p.From && x.Date <= p.To).OrderBy(x => x.Id).ToListAsync(),
            p.From,
            p.To,
            p.WorkingDays,
            p.BranchId ?? 0
        };
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(data))));
    }

    public async Task GenerateAsync(int id, bool apply, string actor) => await CalculateAsync(id, apply, actor);
    public async Task CalculateAsync(int id, bool apply, string actor)
    {
        await using var tx = await db.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable);
        var p = await db.PayrollPeriods.SingleAsync(x => x.Id == id);
        if (p.Status is not ("Draft" or "Calculated"))
            throw new InvalidOperationException("Reopen the period before recalculation.");
        if (p.WorkingDays <= 0 || p.To < p.From || (p.To - p.From).TotalDays > 62)
            throw new InvalidOperationException("Use a valid payroll period of at most 63 days.");
        var employees = await db.Employees.Where(x => x.RegistrationComplete && x.HireDate <= p.To && (x.TerminationDate == null || x.TerminationDate >= p.From)).ToListAsync();
        if (p.BranchId != null)
        {
            var ids = await db.EmployeeAssignments.Where(x => x.BranchId == p.BranchId && x.EffectiveFrom <= p.To && (x.EffectiveTo == null || x.EffectiveTo >= p.To)).Select(x => x.EmployeeId).ToListAsync();
            employees = employees.Where(x => ids.Contains(x.Id)).ToList();
        }

        if (employees.Count == 0)
            throw new InvalidOperationException("No completed employee registrations in this period.");
        var employeeIds = employees.Select(x => x.Id).ToList();
        if (await db.LeaveRequests.AnyAsync(x => employeeIds.Contains(x.EmployeeId) && (x.Status == "Pending" || x.Status == "Manager approved") && x.From <= p.To && x.To >= p.From))
            throw new InvalidOperationException("Resolve pending leave before payroll calculation.");
        p.Revision++;
        foreach (var e in employees)
        {
            var salary = await db.SalaryVersions.Where(x => x.EmployeeId == e.Id && !x.IsDraft && x.EffectiveFrom <= p.To && (x.EffectiveTo == null || x.EffectiveTo >= p.To)).OrderByDescending(x => x.EffectiveFrom).FirstOrDefaultAsync() ?? throw new InvalidOperationException(e.EmployeeNumber + " has no published effective salary.");
            var assignmentDate = e.TerminationDate.HasValue && e.TerminationDate < p.To ? e.TerminationDate.Value : p.To;
            var org = await calendar.AssignmentAsync(e.Id, assignmentDate);
            var leaves = await db.LeaveRequests.Where(x => x.EmployeeId == e.Id && x.Status == "Approved" && x.From <= p.To && x.To >= p.From).ToListAsync();
            var types = await db.LeaveTypes.ToDictionaryAsync(x => x.Id);
            decimal unpaid = 0, overtime = 0;
            int fullDays = 0, eligible = 0;
            var deductions = new List<PayrollDeduction>();
            for (var date = p.From.Date; date <= p.To.Date; date = date.AddDays(1))
            {
                // Use the employee's effective calendar even outside the employment interval for proration.
                var calendarDate = date < e.HireDate ? e.HireDate : date;
                var rule = await calendar.RuleAsync(e.Id, calendarDate);
                var dayOrg = await calendar.AssignmentAsync(e.Id, calendarDate);
                var working = CalendarService.Weekday(rule, date) && !await db.Holidays.AnyAsync(x => x.Date == date && (x.BranchId == null || x.BranchId == dayOrg.BranchId));
                if (working)
                    fullDays++;
                if (date < e.HireDate.Date || (e.TerminationDate.HasValue && date > e.TerminationDate.Value.Date))
                    continue;
                if (working)
                    eligible++;
                await attendance.RebuildAsync(e.Id, date);
                var day = await db.AttendanceDays.SingleAsync(x => x.EmployeeId == e.Id && x.Date == date);
                var daily = salary.Basic / p.WorkingDays;
                var leaveForDay = leaves.Where(x => x.From.Date <= date && x.To.Date >= date).ToList();
                decimal leaveFraction = 0;
                foreach (var leave in leaveForDay)
                {
                    var fraction = CalendarService.LeaveFraction(leave, rule);
                    leaveFraction += fraction;
                    if (working && !types[leave.LeaveTypeId].IsPaid)
                        unpaid += daily * fraction;
                }

                leaveFraction = Math.Min(1, leaveFraction);
                if (working && leaveFraction < 1)
                {
                    var deductionRule = await db.DeductionRules.Where(x => x.EffectiveFrom <= date && (x.BranchId == null || x.BranchId == dayOrg.BranchId)).OrderByDescending(x => x.BranchId != null).ThenByDescending(x => x.EffectiveFrom).ThenByDescending(x => x.Id).FirstOrDefaultAsync() ?? throw new InvalidOperationException("Configure effective deduction rules.");
                    decimal amount = 0;
                    var kind = "Late";
                    if (day.Status == "Absent" || day.FirstIn == null)
                    {
                        amount = daily * (1 - leaveFraction) * deductionRule.AbsenceMultiplier;
                        kind = "Absence";
                    }
                    else if (day.Status == "Incomplete punch" || day.FirstIn.HasValue && day.LastOut == null)
                    {
                        kind = "Incomplete punch";
                        amount = rule.IncompletePolicy switch
                        {
                            "Absent" => daily * (1 - leaveFraction) * deductionRule.AbsenceMultiplier,
                            "Half day" => daily * Math.Min(.5m, 1 - leaveFraction) * deductionRule.AbsenceMultiplier,
                            _ => throw new InvalidOperationException("Resolve incomplete punch for " + e.EmployeeNumber + " on " + date.ToString("yyyy-MM-dd"))};
                    }
                    else
                    {
                        var minutes = day.LateMinutes + day.EarlyMinutes;
                        var window = CalendarService.Window(rule, date);
                        foreach (var leave in leaveForDay.Where(x => x.StartTime != null && day.IsCorrected))
                        {
                            var range = CalendarService.LeaveWindow(leave, rule, date);
                            var start = range.Start;
                            var end = range.End;
                            if (start <= window.Start && day.FirstIn.HasValue)
                                minutes -= Math.Min(day.LateMinutes, Math.Max(0, (int)(end - window.Start).TotalMinutes));
                            if (end >= window.End && day.LastOut.HasValue)
                                minutes -= Math.Min(day.EarlyMinutes, Math.Max(0, (int)(window.End - start).TotalMinutes));
                        }

                        amount = Math.Max(0, minutes) * deductionRule.LatePerMinute;
                    }

                    if (amount > 0)
                        deductions.Add(new() { Date = date, Type = kind, Calculated = decimal.Round(amount, 2), Applied = apply ? decimal.Round(amount, 2) : 0, Reason = apply ? "Automatic rule" : "Waived at calculation" });
                }

                if (salary.OvertimeEligible && (working || rule.WeekendOvertime))
                {
                    var approved = await db.OvertimeApprovals.Where(x => x.EmployeeId == e.Id && x.Date == date).SumAsync(x => (decimal? )x.Hours) ?? 0;
                    var extra = Math.Max(0, day.WorkedHours - (working ? rule.DailyHours : 0));
                    overtime += Math.Min(approved, extra) * (daily / rule.DailyHours) * rule.OvertimeMultiplier;
                }
            }

            if (fullDays != p.WorkingDays)
                throw new InvalidOperationException($"Working days for {e.EmployeeNumber} are {fullDays}; update the period working-day count or calendar.");
            var ratio = Math.Min(1m, (decimal)eligible / p.WorkingDays);
            var basic = decimal.Round(salary.Basic * ratio, 2);
            var allowance = decimal.Round((salary.Housing + salary.Transport + salary.Allowances) * ratio, 2);
            overtime = decimal.Round(overtime, 2);
            var gross = basic + allowance + overtime;
            var line = new PayrollLine
            {
                PayrollPeriodId = id,
                EmployeeId = e.Id,
                Revision = p.Revision,
                BranchId = org.BranchId,
                DepartmentId = org.DepartmentId,
                SubDepartmentId = org.SubDepartmentId,
                Basic = basic,
                AllowanceTotal = allowance,
                Overtime = overtime,
                Gross = gross,
                AttendanceCalculated = deductions.Sum(x => x.Calculated),
                AttendanceApplied = deductions.Sum(x => x.Applied),
                UnpaidLeave = decimal.Round(unpaid, 2),
                Currency = salary.Currency
            };
            line.Net = Math.Max(0, line.Gross - line.AttendanceApplied - line.UnpaidLeave);
            db.PayrollLines.Add(line);
            await db.SaveChangesAsync();
            foreach (var d in deductions)
            {
                d.PayrollLineId = line.Id;
                db.PayrollDeductions.Add(d);
            }
        }

        p.Status = "Calculated";
        await db.SaveChangesAsync();
        p.SourceHash = await HashAsync(p);
        db.AuditLogs.Add(new() { Actor = actor, Action = $"Calculated payroll {id} revision {p.Revision}; deductions {(apply ? "applied" : "waived")}" });
        await db.SaveChangesAsync();
        await tx.CommitAsync();
    }

    public async Task DecideAsync(int id, string action, string actor, string? reason = null)
    {
        await using var tx = await db.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable);
        var p = await db.PayrollPeriods.SingleAsync(x => x.Id == id);
        if (action == "Reopen")
        {
            if (p.Status == "Paid")
                throw new InvalidOperationException("Paid payroll cannot be reopened; make a separate adjustment period.");
            if (string.IsNullOrWhiteSpace(reason))
                throw new InvalidOperationException("A reason is required.");
            p.Status = "Draft";
            var ids = await db.PayrollLines.Where(x => x.PayrollPeriodId == id).Select(x => x.Id).ToListAsync();
            foreach (var s in await db.Payslips.Where(x => ids.Contains(x.PayrollLineId)).ToListAsync())
                s.IsPublished = false;
        }
        else if (action == "Paid")
        {
            if (p.Status != "Locked")
                throw new InvalidOperationException("Only locked payroll can be marked paid.");
            p.Status = "Paid";
        }
        else
        {
            if (action == "Review" && p.Status != "Calculated" || action == "Lock" && p.Status != "Reviewed" || action is not ("Review" or "Lock"))
                throw new InvalidOperationException("Invalid payroll transition.");
            if (p.SourceHash != await HashAsync(p))
                throw new InvalidOperationException("Source data changed. Reopen and recalculate payroll before review/lock.");
            p.Status = action == "Review" ? "Reviewed" : "Locked";
        }

        db.AuditLogs.Add(new() { Actor = actor, Action = $"Payroll {id}: {action}; {reason}" });
        await db.SaveChangesAsync();
        await tx.CommitAsync();
    }

    public async Task AdjustAsync(int deductionId, decimal amount, string reason, string actor)
    {
        if (string.IsNullOrWhiteSpace(reason) || amount < 0)
            throw new InvalidOperationException("Provide a reason and nonnegative amount.");
        await using var tx = await db.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable);
        var d = await db.PayrollDeductions.SingleAsync(x => x.Id == deductionId);
        var l = await db.PayrollLines.SingleAsync(x => x.Id == d.PayrollLineId);
        var p = await db.PayrollPeriods.SingleAsync(x => x.Id == l.PayrollPeriodId);
        if (p.Status != "Calculated" || l.Revision != p.Revision)
            throw new InvalidOperationException("Only current calculated payroll can be adjusted.");
        d.Applied = amount;
        d.Reason = reason;
        await db.SaveChangesAsync();
        l.AttendanceApplied = await db.PayrollDeductions.Where(x => x.PayrollLineId == l.Id).SumAsync(x => x.Applied);
        l.Net = l.NetOverride ?? Math.Max(0, l.Gross - l.AttendanceApplied - l.UnpaidLeave);
        db.AuditLogs.Add(new() { Actor = actor, Action = $"Deduction {d.Id} overridden to {amount}: {reason}" });
        await db.SaveChangesAsync();
        await tx.CommitAsync();
    }

    public async Task PublishAsync(int id, string actor)
    {
        await using var tx = await db.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable);
        var p = await db.PayrollPeriods.SingleAsync(x => x.Id == id);
        if (p.Status is not ("Locked" or "Paid"))
            throw new InvalidOperationException("Lock payroll before publishing.");
        foreach (var l in await db.PayrollLines.Where(x => x.PayrollPeriodId == id && x.Revision == p.Revision).ToListAsync())
        {
            var slip = await db.Payslips.SingleOrDefaultAsync(x => x.PayrollLineId == l.Id);
            if (slip == null)
                db.Payslips.Add(new() { PayrollLineId = l.Id, IsPublished = true });
            else
                slip.IsPublished = true;
        }

        db.AuditLogs.Add(new() { Actor = actor, Action = $"Published payroll {id} revision {p.Revision}" });
        await db.SaveChangesAsync();
        await tx.CommitAsync();
    }

    public async Task OverrideLineAsync(int lineId, decimal? amount, string reason, string actor)
    {
        if (string.IsNullOrWhiteSpace(reason) || amount < 0)
            throw new InvalidOperationException("A nonnegative net amount and reason are required.");
        await using var tx = await db.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable);
        var line = await db.PayrollLines.SingleAsync(x => x.Id == lineId);
        var period = await db.PayrollPeriods.SingleAsync(x => x.Id == line.PayrollPeriodId);
        if (period.Status != "Calculated" || period.Revision != line.Revision)
            throw new InvalidOperationException("Only current calculated payroll can be overridden.");
        line.NetOverride = amount;
        line.OverrideReason = reason;
        line.Net = amount ?? Math.Max(0, line.Gross - line.AttendanceApplied - line.UnpaidLeave);
        db.AuditLogs.Add(new() { Actor = actor, Action = $"Payroll line {lineId} net override {(amount.HasValue ? amount.Value.ToString() : "cleared")}: {reason}" });
        await db.SaveChangesAsync();
        await tx.CommitAsync();
    }
}
